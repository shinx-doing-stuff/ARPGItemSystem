# Affix Pool Refactor — Design

**Date:** 2026-04-29
**Status:** Approved (pending user review of this written spec)

## Context

The current modifier system in `ARPGItemSystem` defines `PrefixType`, `SuffixType`, and `ModifierType` enums **separately** in three namespaces (`Weapon`, `Armor`, `Accessory`). The same conceptual modifier — e.g. `FlatCritChance`, `ManaCostReduction`, `FlatLifeIncrease` — is duplicated across categories. Tier value tables and tooltip formats live in two parallel dictionaries (`TierDatabase`, `TooltipDatabase`) with one entry per category-specific enum value. Adding a new modifier requires editing 4–6 files; concept changes ripple through multiple namespaces.

The hardcoded per-damage-class allow-lists in `WeaponModifier.cs` (`MeleeWeaponPrefixTypes`, etc.) are mostly identical to one another and contain a `0` placeholder that gets filtered out at use time. Save/Load/NetSend/NetReceive code is duplicated almost line-for-line across `WeaponManager`, `ArmorManager`, `AccessoryManager`. Tooltip strings are stored on every rolled modifier instance and serialized into save files and network packets, freezing the wording at roll time.

## Goal

Refactor the affix pool to a **single source of truth**, with each item category declaring an allowed list picked from that pool, and consolidate duplicated infrastructure (modifier struct, serialization, exclude-list helpers).

## Scope

This spec covers **Scope B** as agreed during brainstorming:

- Unify the three `PrefixType`/`SuffixType` enums into a single `AffixId` enum.
- Single canonical registry holding all affix metadata (kind, tooltip format, per-category tier tables, weapon damage-class restrictions).
- Single `Affix` struct shared across all item categories.
- Lift `SaveData`, `LoadData`, `NetSend`, `NetReceive` into a shared `AffixItemManager` base class.
- Look up tooltip text at draw time instead of storing it on the rolled affix.
- Migrate save data by **renaming the save tag keys**; old saves load with empty affix lists and are immediately rerolled by the existing `OnPickup` / `UpdateInventory` paths.

### Explicitly out of scope

- **Localization of tooltip strings** — tooltip text remains hardcoded English in the registry. The existing CLAUDE.md violation persists and is tracked as a separate follow-up.
- **Centralizing stat-apply logic** — stat-apply switches stay in their respective managers. Each manager still owns its hooks (`ModifyWeaponDamage`, `UpdateEquip`, `UpdateAccessory`, etc.).
- **Random-instance hygiene** — existing `new Random()` per call sites are not refactored; new code in this spec uses `Main.rand` instead.
- **`GetAmountOf*` consolidation** — remains as six separate utility methods.

## Non-Goals (player-facing)

- **Save backward compatibility is not a goal.** Players who load a world after this update will see all existing affixes wiped and rerolled. Stated explicitly during brainstorming.

## Design

### Namespace and file layout

New namespace: `ARPGItemSystem.Common.Affixes`

```
Common/Affixes/
├── AffixId.cs              // unified enum
├── AffixKind.cs            // Prefix | Suffix
├── ItemCategory.cs         // Weapon | Armor | Accessory
├── Tier.cs                 // value range struct
├── Affix.cs                // rolled instance struct
├── AffixDef.cs             // registry entry
├── AffixRegistry.cs        // static registry + queries
├── AffixRoller.cs          // roll-one-affix helper
└── AffixItemManager.cs     // shared GlobalItem base class
```

Files removed:
- `Common/GlobalItems/Weapon/WeaponModifier.cs`
- `Common/GlobalItems/Armor/ArmorModifier.cs`
- `Common/GlobalItems/Accessory/AccessoryModifier.cs`
- `Common/GlobalItems/Database/TierDatabase.cs`
- `Common/GlobalItems/Database/TooltipDatabase.cs`

### Core types

```csharp
public enum ItemCategory { Weapon, Armor, Accessory }

public enum AffixKind { Prefix, Suffix }

public enum AffixId {
    None = 0,
    // Weapon-only
    FlatDamageIncrease, PercentageDamageIncrease,
    FlatArmorPen, PercentageArmorPen,
    AttackSpeedIncrease, KnockbackIncrease,
    PercentageCritChance, CritMultiplier, VelocityIncrease,
    // Armor-only
    PercentageDefenseIncrease,
    // Armor + Accessory
    FlatLifeIncrease, FlatDefenseIncrease, FlatManaIncrease,
    PercentageGenericDamageIncrease,
    PercentageMeleeDamageIncrease, PercentageRangedDamageIncrease,
    PercentageMagicDamageIncrease, PercentageSummonDamageIncrease,
    // All categories
    FlatCritChance, ManaCostReduction
}

public struct Tier { public int Min; public int Max; }

public struct Affix {
    public AffixId Id;
    public int Magnitude;
    public int Tier;
}

public class AffixDef {
    public AffixId Id;
    public AffixKind Kind;
    public string TooltipFormat;                          // e.g. "{0}% Increased Base Damage"
    public Dictionary<ItemCategory, List<Tier>> Tiers;    // per-category tier table (10 entries each)
    public HashSet<DamageClass> AllowedDamageClasses;     // null = all classes; only consulted for weapons
}
```

The `Affix` struct intentionally drops:
- `prefixType`/`suffixType` pair → kind is derived via the registry (also stored on disk as a small byte for self-contained loads, see Save format).
- Cached tooltip string → looked up at draw time via the registry.

### Registry shape

```csharp
public static class AffixRegistry {
    public static IReadOnlyDictionary<AffixId, AffixDef> All { get; }
    public static AffixDef Get(AffixId id);
    public static IEnumerable<AffixDef> RollPool(
        ItemCategory category,
        AffixKind kind,
        DamageClass weaponClass = null);
}
```

`RollPool` filters `All` by:
1. `def.Kind == kind`
2. `def.Tiers.ContainsKey(category)` (i.e. defined for this category)
3. If `category == Weapon` and `def.AllowedDamageClasses != null`, then `weaponClass` must be in the set

Built once in a static constructor from a single declaration list. **Adding a new affix is one entry; removing one is one entry.**

#### Tier table — Shape 1 (always per-category)

Single-category affixes still declare a one-key dict. Example:

```csharp
new AffixDef {
    Id = AffixId.FlatDamageIncrease,
    Kind = AffixKind.Prefix,
    TooltipFormat = "{0}% Increased Base Damage",
    Tiers = new() {
        [ItemCategory.Weapon] = new() {
            new(51,55), new(46,50), new(41,45), new(36,40), new(31,35),
            new(26,30), new(21,25), new(16,20), new(11,15), new(5,10)
        }
    },
    AllowedDamageClasses = null
}
```

Multi-category affixes with different scales (e.g. `FlatCritChance`) declare both:

```csharp
new AffixDef {
    Id = AffixId.FlatCritChance,
    Kind = AffixKind.Suffix,
    TooltipFormat = "{0}% Additional Critical Strike Chance",
    Tiers = new() {
        [ItemCategory.Weapon]    = [/* 19,20 ... 1,2  */],
        [ItemCategory.Armor]     = [/*  5,10 ... 1,2  */],
        [ItemCategory.Accessory] = [/*  3, 4 ... 1,2  */]
    },
    AllowedDamageClasses = null
}
```

#### Weapon damage-class restrictions

Replaces the four `XxxWeaponPrefixTypes`/`XxxWeaponSuffixTypes` lists in `WeaponModifier.cs`. Today's effective restrictions, derived from the four lists:

| Affix                     | Allowed damage classes                                                         |
|---------------------------|--------------------------------------------------------------------------------|
| FlatDamageIncrease        | all                                                                            |
| PercentageDamageIncrease  | all                                                                            |
| FlatArmorPen              | all                                                                            |
| PercentageArmorPen        | all                                                                            |
| AttackSpeedIncrease       | Melee, MeleeNoSpeed, SummonMeleeSpeed, Ranged, Magic, MagicSummonHybrid (**not Summon**) |
| KnockbackIncrease         | all                                                                            |
| FlatCritChance            | all                                                                            |
| PercentageCritChance      | all                                                                            |
| CritMultiplier            | all                                                                            |
| VelocityIncrease          | Ranged, Magic, MagicSummonHybrid (**not Melee\*, not Summon**)                 |
| ManaCostReduction         | Magic, MagicSummonHybrid (**Magic-only**)                                      |

Encoded on the affix as `AllowedDamageClasses`. `null` means "all damage classes". For restricted affixes, declare the explicit set; e.g.:

```csharp
// AttackSpeedIncrease
AllowedDamageClasses = new() {
    DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.SummonMeleeSpeed,
    DamageClass.Ranged, DamageClass.Magic, DamageClass.MagicSummonHybrid
}

// VelocityIncrease
AllowedDamageClasses = new() {
    DamageClass.Ranged, DamageClass.Magic, DamageClass.MagicSummonHybrid
}

// ManaCostReduction
AllowedDamageClasses = new() {
    DamageClass.Magic, DamageClass.MagicSummonHybrid
}
```

Implementation note: the registry stores `DamageClass` references. Equality checks use `==` against the singleton instances on `DamageClass` (which is how the existing code already compares them).

The `else { IDs = new List<int>(SummonWeaponPrefixTypes); }` fallback for unknown damage classes is dropped. Every vanilla Terraria item has a defined `DamageType`, so unknown classes never appear in practice; if one ever does, the affected `RollPool` query will return only affixes whose `AllowedDamageClasses` is `null` (i.e. unrestricted ones), which is a safer default than "treat as Summon".

### Roller

```csharp
public static class AffixRoller {
    public static Affix? Roll(
        ItemCategory category,
        AffixKind kind,
        Item item,
        IReadOnlyCollection<Affix> existing,
        int tier);
}
```

Algorithm:
1. `pool = AffixRegistry.RollPool(category, kind, item.DamageType)`
2. Drop any `def` whose `Id` is in `existing.Select(a => a.Id)`
3. If pool is empty, return `null` (caller skips adding)
4. Pick one uniformly via `Main.rand`
5. `range = def.Tiers[category][tier]`; magnitude = `Main.rand.Next(range.Min, range.Max + 1)`
6. Return `new Affix { Id = def.Id, Magnitude = magnitude, Tier = tier }`

Uses `Main.rand` to avoid the correlated-seed pitfall of repeatedly constructing `new Random()` within one frame.

### Manager hierarchy

```csharp
public abstract class AffixItemManager : GlobalItem {
    public List<Affix> Affixes = new();
    internal bool _initialized;

    public override bool InstancePerEntity => true;
    public abstract ItemCategory Category { get; }

    // Number of prefixes / suffixes to roll on creation
    protected abstract int RollPrefixCount();
    protected abstract int RollSuffixCount();

    public override GlobalItem Clone(Item from, Item to) { /* deep-copy Affixes */ }
    public override bool? PrefixChance(...) => pre == -3;
    public override void OnCreated(...)         { Reroll(item); _initialized = true; }
    public override bool OnPickup(...)          { if (Affixes.Count == 0) Reroll(item); _initialized = true; return true; }
    public override void UpdateInventory(...)   { if (_initialized) return; Reroll(item); _initialized = true; }

    public void Reroll(Item item) {
        Affixes.Clear();
        for (int i = 0; i < RollPrefixCount(); i++) AddRoll(item, AffixKind.Prefix);
        for (int i = 0; i < RollSuffixCount(); i++) AddRoll(item, AffixKind.Suffix);
    }

    private void AddRoll(Item item, AffixKind kind) {
        // utils lives in ARPGItemSystem.Common.GlobalItems and stays there;
        // the new Affixes namespace takes a using-directive on it.
        var rolled = AffixRoller.Roll(Category, kind, item, Affixes, utils.GetTier());
        if (rolled.HasValue) Affixes.Add(rolled.Value);
    }

    public override void ModifyTooltips(...) {
        // Lookup TooltipFormat from registry, format with magnitude,
        // green for prefix / blue for suffix
    }

    public override void SaveData(Item, TagCompound)   { /* unified format */ }
    public override void LoadData(Item, TagCompound)   { /* migration handling here */ }
    public override void NetSend(Item, BinaryWriter)   { /* unified format */ }
    public override void NetReceive(Item, BinaryReader){ /* unified format */ }
}
```

Concrete subclasses:

```csharp
public class WeaponManager : AffixItemManager {
    public override ItemCategory Category => ItemCategory.Weapon;
    public override bool AppliesToEntity(Item e, bool late) => late && e.damage > 0 && e.maxStack == 1;
    protected override int RollPrefixCount() => utils.GetAmountOfPrefixesWeapon();
    protected override int RollSuffixCount() => utils.GetAmountOfSuffixesWeapon();

    // Stat-apply hooks: ModifyWeaponDamage, ModifyWeaponCrit, ModifyHitNPC,
    // ModifyWeaponKnockback, ModifyShootStats, UseSpeedMultiplier, ModifyManaCost
}

public class ArmorManager : AffixItemManager {
    public override ItemCategory Category => ItemCategory.Armor;
    public override bool AppliesToEntity(Item e, bool late) =>
        late && e.damage < 1 && e.maxStack == 1 && !e.accessory && !e.vanity;
    protected override int RollPrefixCount() => utils.GetAmountOfPrefixesArmor();
    protected override int RollSuffixCount() => utils.GetAmountOfSuffixesArmor();

    // Stat-apply hook: UpdateEquip
}

public class AccessoryManager : AffixItemManager {
    public override ItemCategory Category => ItemCategory.Accessory;
    public override bool AppliesToEntity(Item e, bool late) => late && e.accessory;
    protected override int RollPrefixCount() => utils.GetAmountOfPrefixesAccessory();
    protected override int RollSuffixCount() => utils.GetAmountOfSuffixesAccessory();

    // Stat-apply hook: UpdateAccessory
}
```

Each concrete manager's stat-apply switch becomes a single `switch (affix.Id)` instead of two switches over `prefixType`+`suffixType`. The dead `None` branches go away.

### Save format

Single grouped list (no separate prefix/suffix lists; the kind is part of each entry):

| Tag key       | Type      | Meaning                                |
|---------------|-----------|----------------------------------------|
| `"AffixIds"`  | `int[]`   | `(int)Affix.Id` per entry              |
| `"Magnitudes"`| `int[]`   | rolled magnitude per entry             |
| `"Tiers"`     | `int[]`   | rolled tier index per entry            |
| `"Kinds"`     | `byte[]`  | `0` = Prefix, `1` = Suffix per entry   |

Read order matches write order. All four lists have the same length (the entry count).

`"Kinds"` is technically derivable from the registry (`AffixDef.Kind`), but storing it on disk:
- Keeps `LoadData` self-contained (no registry lookup during deserialization)
- Future-proofs against changing an affix's kind (the saved item retains its original tag)

NetSend / NetReceive use the same shape, written as `[count, ids..., magnitudes..., tiers..., kinds...]` with explicit length prefixes.

### Migration

**No version field, no translation table.** Migration is implicit via tag rename.

In `LoadData`:

```csharp
if (!tag.ContainsKey("AffixIds")) {
    // Pre-refactor save (or fresh item with no data).
    // Existing OnPickup/UpdateInventory will reroll if needed.
    Reroll(item);
    _initialized = true;
    return;
}
// Normal load: read AffixIds / Magnitudes / Tiers / Kinds, populate Affixes.
_initialized = true;
```

This is safe because:
1. tModLoader's `TagCompound` returns empty / default values for missing keys (no exceptions).
2. The discriminator (`tag.ContainsKey("AffixIds")`) cleanly separates "old save with old keys" from "new save that legitimately rolled zero affixes".
3. Old keys (`"PrefixIDList"`, `"SuffixIDList"`, `"PrefixTooltipList"`, etc.) are never read, so no risk of casting old ints to the new `AffixId` enum.

### Tooltip rendering

In `AffixItemManager.ModifyTooltips`:

```csharp
foreach (var affix in Affixes) {
    var def = AffixRegistry.Get(affix.Id);
    var text = string.Format(def.TooltipFormat, affix.Magnitude);
    var color = def.Kind == AffixKind.Prefix ? Color.LightGreen : Color.DeepSkyBlue;
    tooltips.Add(new TooltipLine(Mod, "CustomAffix", text) { OverrideColor = color });
}
```

Tooltip text is no longer carried on each rolled affix or serialized. Editing wording in the registry is reflected immediately on all existing items.

### Adjustments to dependent code

- **`ProjectileManager.cs`** (`Common/GlobalItems/ProjectileManager.cs`): switch from copying `WeaponManager.modifierList` and switching on `prefixType`/`suffixType` to copying `WeaponManager.Affixes` and switching on `affix.Id`. Same set of cases (`PercentageArmorPen`, `CritMultiplier`).
- **`Network/ReforgePacketHandler.cs`**: server-authoritative single-affix reroll. Update to operate on `Affix` instead of the old per-category modifier struct. Algorithm unchanged: client sends a slot index, server rerolls that one slot via `AffixRoller.Roll(category, kind, item, otherAffixes, tier)`.
- **`UI/AffixLine.cs`, `UI/ReforgePanel.cs`**: adapt to the new `Affix` type. Tooltip lookup goes through the registry. Cost formula in `ReforgeConfig` is unchanged (still scales by `affix.Tier`).
- **`utils.cs`**: collapse the three `CreateExcludeList` overloads to one: `IEnumerable<AffixId> ExistingIds(IEnumerable<Affix> list, AffixKind kind) => list.Where(a => RegistryKindOf(a.Id) == kind).Select(a => a.Id)`. Or remove it entirely — the roller already filters by `existing` directly. `GetTier` and the six `GetAmountOf*` methods stay as they are.

### Behaviour preservation

The refactor is intended to preserve current gameplay behaviour exactly:
- Same affixes available on each item category and damage class
- Same tier value ranges per (affix, category) pair
- Same per-tier roll count logic via `GetAmountOf*`
- Same boss-progression-based tier selection via `GetTier`
- Same suppression of vanilla prefixes via `PrefixChance(... pre == -3)`

The only intentional behavioural changes:
- All existing items get rerolled once after the update (acknowledged non-goal)
- Tooltip wording becomes live-editable (changes in the registry apply to existing items)
- The "unknown damage class falls back to summon list" oddity in `WeaponModifier.cs` goes away (no observable effect — every vanilla item has a real `DamageType`)

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| New code crashes on items with no affixes after migration | `OnPickup` / `UpdateInventory` already handle this path; `Reroll` is called explicitly on old-save load |
| Multiplayer desync between client/server with mismatched mod versions | tModLoader already enforces matching mod versions on connect — out of scope |
| Reroll hot path performance degrades from registry lookups | Registry is a `Dictionary<AffixId, AffixDef>` (O(1)); pool filtering is over ~20 entries, called only on creation/reroll |
| Future affix additions break saved kind values | `"Kinds"` byte stored alongside id keeps each save self-describing |

## Acceptance criteria

1. Building the mod with the refactor produces no compiler warnings or errors.
2. A new world: items roll affixes consistent with the registry's per-category tier scales.
3. An existing world from before the refactor: all items load with no crashes; affixes are wiped and refreshed on first interaction (pickup or inventory update).
4. The reforge panel rerolls a single affix correctly in singleplayer and multiplayer.
5. Adding a new affix to the registry (one entry) makes it eligible to roll without touching any other file (apart from the per-manager stat-apply switch — out of scope to remove that step).
6. Tooltip wording changes in the registry are reflected immediately on existing items in inventory.
