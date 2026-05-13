# Hybrid Affixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add hybrid affixes that modify two stats simultaneously, with independent per-tier variance on both the primary and secondary stat, and an optional negative secondary for trade-off builds.

**Architecture:** Add `Magnitude2` to the `Affix` struct (zero for all non-hybrid affixes — fully backward compatible). `AffixDef` gains a nullable `SecondaryTiers` dictionary following the same 10-tier convention as `Tiers`. `AffixRoller` rolls `Magnitude2` when `SecondaryTiers` is present. Persistence, net-sync, and tooltip each gain one small extension. New hybrid affix IDs are appended to `AffixId` (never inserted), registered in `AffixRegistry`, and dispatched by `EquipmentStatSource`.

**Tech Stack:** C# / .NET 8, tModLoader 1.4.4, `TagCompound` for persistence, `BinaryWriter`/`BinaryReader` for net-sync, hjson for localization.

**Spec:** `ARPGItemSystem/docs/superpowers/specs/2026-05-13-hybrid-affixes-design.md`

---

## File Map

| File | Change |
|---|---|
| `ARPGItemSystem/Common/Affixes/Affix.cs` | Add `Magnitude2` field; update constructor to 4-arg |
| `ARPGItemSystem/Common/Affixes/AffixDef.cs` | Add `SecondaryTiers`, `IsHybrid` |
| `ARPGItemSystem/Common/Affixes/AffixRoller.cs` | Roll `magnitude2` from `SecondaryTiers` when present |
| `ARPGItemSystem/Common/Affixes/AffixItemManager.cs` | `SaveData`/`LoadData`/`NetSend`/`NetReceive`/`ModifyTooltips` |
| `ARPGItemSystem/Common/Affixes/AffixId.cs` | Append `FortifiedBody`, `BalancedGrowth` |
| `ARPGItemSystem/Common/Affixes/AffixRegistry.cs` | Two new `AffixDef` entries; extend validation to cover `SecondaryTiers` |
| `ARPGCharacterSystem/Common/Stats/Sources/EquipmentStatSource.cs` | Two new dispatch cases |
| `ARPGItemSystem/Localization/en-US_Mods.ARPGItemSystem.hjson` | Two new tooltip keys |

---

## Task 1 — Extend `Affix` struct + `AffixDef` + all Affix construction sites

**Why one task:** Changing the `Affix` constructor signature breaks every `new Affix(...)` call site at compile time. All three call sites live in files touched by this task (`AffixRoller.cs` and `AffixItemManager.cs`), so they must all be updated together to restore compilation.

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/Affix.cs`
- Modify: `ARPGItemSystem/Common/Affixes/AffixDef.cs`
- Modify: `ARPGItemSystem/Common/Affixes/AffixRoller.cs`
- Modify: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs` (construction sites in `LoadData` and `NetReceive` only — persistence wiring comes in Task 2)

---

- [ ] **Step 1: Extend `Affix.cs`**

Replace the entire file content:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    public readonly struct Affix
    {
        public readonly AffixId Id;
        public readonly int Magnitude;
        public readonly int Magnitude2;  // 0 for all non-hybrid affixes
        public readonly int Tier;

        public Affix(AffixId id, int magnitude, int magnitude2, int tier)
        {
            Id = id;
            Magnitude = magnitude;
            Magnitude2 = magnitude2;
            Tier = tier;
        }
    }
}
```

- [ ] **Step 2: Extend `AffixDef.cs`**

Replace the entire file content:

```csharp
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public class AffixDef
    {
        public AffixId Id { get; init; }
        public AffixKind Kind { get; init; }

        // Per-category tier tables. Each list MUST contain exactly 10 Tier entries.
        public Dictionary<ItemCategory, List<Tier>> Tiers { get; init; }

        // Restricts which weapon DamageClasses this affix can roll on.
        // null = unrestricted. Only consulted when category == ItemCategory.Weapon.
        public HashSet<DamageClass> AllowedDamageClasses { get; init; }

        // Secondary stat tier tables for hybrid affixes. null = single-stat affix.
        // Each list MUST contain exactly 10 Tier entries. Negative Min/Max = penalty.
        public Dictionary<ItemCategory, List<Tier>>? SecondaryTiers { get; init; }

        public bool IsHybrid => SecondaryTiers != null;
    }
}
```

- [ ] **Step 3: Update `AffixRoller.cs` — roll `Magnitude2`**

Replace the return statement at line 29 (currently `return new Affix(def.Id, magnitude, tier);`) with the following block. The full file after the change:

```csharp
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public static class AffixRoller
    {
        public static Affix? Roll(
            ItemCategory category,
            AffixKind kind,
            Item item,
            IReadOnlyCollection<Affix> existing,
            int tier)
        {
            var weaponClass = category == ItemCategory.Weapon ? item.DamageType : null;
            var existingIds = new HashSet<AffixId>(existing.Select(a => a.Id));

            var pool = AffixRegistry
                .RollPool(category, kind, weaponClass)
                .Where(def => !existingIds.Contains(def.Id))
                .ToList();

            if (pool.Count == 0) return null;

            var def = pool[Main.rand.Next(pool.Count)];
            var range = def.Tiers[category][tier];
            int magnitude = Main.rand.Next(range.Min, range.Max + 1);

            int magnitude2 = 0;
            if (def.SecondaryTiers != null && def.SecondaryTiers.TryGetValue(category, out var secTiers))
                magnitude2 = Main.rand.Next(secTiers[tier].Min, secTiers[tier].Max + 1);

            return new Affix(def.Id, magnitude, magnitude2, tier);
        }
    }
}
```

- [ ] **Step 4: Fix the two `Affix` construction sites in `AffixItemManager.cs`**

`LoadData` at line 132 — change:
```csharp
// Before
Affixes.Add(new Affix((AffixId)ids[i], magnitudes[i], tiers[i]));
```
```csharp
// After (magnitude2 = 0 placeholder; Task 2 will read it from the tag)
Affixes.Add(new Affix((AffixId)ids[i], magnitudes[i], 0, tiers[i]));
```

`NetReceive` at line 164 — change:
```csharp
// Before
Affixes.Add(new Affix(id, magnitude, tier));
```
```csharp
// After (magnitude2 = 0 placeholder; Task 2 will read it from the stream)
Affixes.Add(new Affix(id, magnitude, 0, tier));
```

- [ ] **Step 5: Verify compilation**

Build via tModLoader: **Workshop → Mod Sources → ARPGItemSystem → Build**.

Expected: build succeeds with no errors. If any `new Affix(` with 3 args is reported, find and fix it (grep for `new Affix(` to catch stragglers).

- [ ] **Step 6: Commit**

```
git add ARPGItemSystem/Common/Affixes/Affix.cs
git add ARPGItemSystem/Common/Affixes/AffixDef.cs
git add ARPGItemSystem/Common/Affixes/AffixRoller.cs
git add ARPGItemSystem/Common/Affixes/AffixItemManager.cs
git commit -m "feat: add Magnitude2 to Affix struct and SecondaryTiers to AffixDef"
```

---

## Task 2 — Wire `Magnitude2` through persistence and tooltip

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`

---

- [ ] **Step 1: Update `SaveData` to write `"Magnitudes2"`**

In `SaveData`, after the existing `var kinds = new List<byte>(n);` declaration, add a parallel list and populate it in the existing loop. The full updated `SaveData`:

```csharp
public override void SaveData(Item item, TagCompound tag)
{
    int n = Affixes.Count;
    var ids       = new List<int>(n);
    var magnitudes  = new List<int>(n);
    var magnitudes2 = new List<int>(n);
    var tiers     = new List<int>(n);
    var kinds     = new List<byte>(n);

    foreach (var a in Affixes)
    {
        ids.Add((int)a.Id);
        magnitudes.Add(a.Magnitude);
        magnitudes2.Add(a.Magnitude2);
        tiers.Add(a.Tier);
        kinds.Add((byte)AffixRegistry.Get(a.Id).Kind);
    }

    tag["AffixIds"]     = ids;
    tag["Magnitudes"]   = magnitudes;
    tag["Magnitudes2"]  = magnitudes2;
    tag["Tiers"]        = tiers;
    tag["Kinds"]        = kinds;
}
```

- [ ] **Step 2: Update `LoadData` to read `"Magnitudes2"` (absent = all zeros)**

Replace the current `LoadData` with:

```csharp
public override void LoadData(Item item, TagCompound tag)
{
    if (!tag.ContainsKey("AffixIds"))
    {
        Reroll(item);
        Initialized = true;
        return;
    }

    var ids        = tag.GetList<int>("AffixIds").ToList();
    var magnitudes  = tag.GetList<int>("Magnitudes").ToList();
    var magnitudes2 = tag.ContainsKey("Magnitudes2")
                        ? tag.GetList<int>("Magnitudes2").ToList()
                        : System.Linq.Enumerable.Repeat(0, ids.Count).ToList();
    var tiers      = tag.GetList<int>("Tiers").ToList();
    // Kinds written by SaveData for future-proofing; registry is authoritative for kind on load.
    _ = tag.GetList<byte>("Kinds");

    Affixes.Clear();
    for (int i = 0; i < ids.Count; i++)
        Affixes.Add(new Affix((AffixId)ids[i], magnitudes[i], magnitudes2[i], tiers[i]));

    Affixes.RemoveAll(a => a.Id == AffixId.None || !AffixRegistry.All.ContainsKey(a.Id));

    if (Affixes.Count == 0 && (RollPrefixCount() > 0 || RollSuffixCount() > 0))
        Initialized = false;
    else
        Initialized = true;
}
```

- [ ] **Step 3: Update `NetSend` to write `Magnitude2`**

After the existing `writer.Write(a.Tier);` line, add:
```csharp
writer.Write(a.Magnitude2);
```

The full updated `NetSend`:

```csharp
public override void NetSend(Item item, BinaryWriter writer)
{
    writer.Write(Affixes.Count);
    foreach (var a in Affixes)
    {
        writer.Write((int)a.Id);
        writer.Write(a.Magnitude);
        writer.Write(a.Tier);
        writer.Write(a.Magnitude2);
        writer.Write((byte)AffixRegistry.Get(a.Id).Kind);
    }
}
```

- [ ] **Step 4: Update `NetReceive` to read `Magnitude2`**

After the existing `int tier = reader.ReadInt32();` line, add a read before constructing the `Affix`. The full updated `NetReceive`:

```csharp
public override void NetReceive(Item item, BinaryReader reader)
{
    int count = reader.ReadInt32();
    Affixes.Clear();
    for (int i = 0; i < count; i++)
    {
        var id       = (AffixId)reader.ReadInt32();
        int magnitude  = reader.ReadInt32();
        int tier       = reader.ReadInt32();
        int magnitude2 = reader.ReadInt32();
        _ = reader.ReadByte();
        Affixes.Add(new Affix(id, magnitude, magnitude2, tier));
    }
    Initialized = true;
}
```

- [ ] **Step 5: Update `ModifyTooltips` to display `Magnitude2` for hybrid affixes**

Replace the tooltip text construction inside the `for` loop. The full updated loop body:

```csharp
for (int i = 0; i < Affixes.Count; i++)
{
    var affix = Affixes[i];
    var def   = AffixRegistry.Get(affix.Id);

    string text = def.IsHybrid
        ? Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{affix.Id}", affix.Magnitude, affix.Magnitude2)
        : Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{affix.Id}", affix.Magnitude);

    var color = def.Kind == AffixKind.Prefix
        ? Microsoft.Xna.Framework.Color.LightGreen
        : Microsoft.Xna.Framework.Color.DeepSkyBlue;
    tooltips.Add(new TooltipLine(Mod, $"Affix_{affix.Id}", text) { OverrideColor = color });
}
```

- [ ] **Step 6: Verify compilation**

Build via tModLoader: **Workshop → Mod Sources → ARPGItemSystem → Build**.

Expected: build succeeds. The `System.Linq` using in `LoadData` is already present at the top of `AffixItemManager.cs` (`using System.Linq;` on line 3) — no new using needed.

- [ ] **Step 7: Commit**

```
git add ARPGItemSystem/Common/Affixes/AffixItemManager.cs
git commit -m "feat: wire Magnitude2 through persistence, net-sync, and tooltip"
```

---

## Task 3 — Register `FortifiedBody` and `BalancedGrowth`

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixId.cs`
- Modify: `ARPGItemSystem/Common/Affixes/AffixRegistry.cs`
- Modify: `ARPGItemSystem/Localization/en-US_Mods.ARPGItemSystem.hjson`

---

- [ ] **Step 1: Append new IDs to `AffixId.cs`**

At the end of the enum, after `ChaosPenetration`, add:

```csharp
// Hybrid affixes (2026-05-13)
FortifiedBody,    // +HP (boosted), −Mana
BalancedGrowth,   // +HP, +Mana (both at ~65% of standalone)
```

**CRITICAL:** Only append — never insert between existing entries. Integer values are persisted to disk.

- [ ] **Step 2: Extend validation in `AffixRegistry.BuildRegistry()` to cover `SecondaryTiers`**

The existing validation loop ends at line 752. Replace it with:

```csharp
foreach (var def in defs)
{
    foreach (var (cat, list) in def.Tiers)
        if (list.Count != 10)
            throw new Exception($"AffixDef {def.Id} category {cat} has {list.Count} tier entries, expected 10");

    if (def.SecondaryTiers != null)
        foreach (var (cat, list) in def.SecondaryTiers)
            if (list.Count != 10)
                throw new Exception($"AffixDef {def.Id} SecondaryTiers category {cat} has {list.Count} tier entries, expected 10");
}
```

- [ ] **Step 3: Add `FortifiedBody` and `BalancedGrowth` entries to `defs` in `BuildRegistry()`**

Add the following block to the `defs` list, after the last existing entry (`ChaosPenetration`):

```csharp
// ============== HYBRID AFFIXES (2026-05-13) ==============

// FortifiedBody: Armor + Accessory, Prefix.
// Large HP gain at the cost of max mana. HP is ~125% of FlatLifeIncrease
// to compensate for the mana penalty. Secondary is a small range (not fixed)
// so both stats feel like they were rolled, not calculated.
new AffixDef {
    Id = AffixId.FortifiedBody,
    Kind = AffixKind.Prefix,
    Tiers = new Dictionary<ItemCategory, List<Tier>>           // primary: +HP
    {
        [ItemCategory.Armor] = new List<Tier> {
            new(70,76), new(63,69), new(56,62), new(49,55), new(42,48),
            new(35,41), new(28,34), new(21,27), new(14,20), new(6,13)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(30,33), new(27,29), new(23,26), new(20,22), new(17,19),
            new(14,16), new(11,13), new(8,10),  new(5,7),   new(2,4)
        }
    },
    SecondaryTiers = new Dictionary<ItemCategory, List<Tier>>  // secondary: −Mana (penalty)
    {
        [ItemCategory.Armor] = new List<Tier> {
            new(-32,-28), new(-28,-25), new(-25,-22), new(-22,-19), new(-18,-16),
            new(-15,-13), new(-12,-10), new(-9,-7),   new(-6,-4),   new(-3,-2)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(-15,-13), new(-13,-11), new(-11,-9), new(-10,-8), new(-8,-7),
            new(-7,-6),   new(-5,-4),   new(-4,-3),  new(-3,-2),  new(-2,-1)
        }
    },
    AllowedDamageClasses = null
},

// BalancedGrowth: Armor + Accessory, Prefix.
// Grants both HP and Mana simultaneously. Each is ~65% of its standalone
// counterpart since one affix slot is buying two stats. Accessory mana
// skews slightly higher to match the existing accessory FlatManaIncrease scale.
new AffixDef {
    Id = AffixId.BalancedGrowth,
    Kind = AffixKind.Prefix,
    Tiers = new Dictionary<ItemCategory, List<Tier>>           // primary: +HP
    {
        [ItemCategory.Armor] = new List<Tier> {
            new(37,40), new(33,36), new(29,32), new(25,28), new(21,24),
            new(17,20), new(13,16), new(9,12),  new(5,8),   new(1,4)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(16,18), new(14,16), new(12,14), new(10,12), new(8,10),
            new(6,8),   new(5,6),   new(3,4),   new(2,3),   new(1,2)
        }
    },
    SecondaryTiers = new Dictionary<ItemCategory, List<Tier>>  // secondary: +Mana
    {
        [ItemCategory.Armor] = new List<Tier> {
            new(37,40), new(33,36), new(29,32), new(25,28), new(21,24),
            new(17,20), new(13,16), new(9,12),  new(5,8),   new(1,4)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(32,35), new(28,31), new(25,27), new(21,24), new(18,20),
            new(15,17), new(11,14), new(8,10),  new(5,7),   new(1,4)
        }
    },
    AllowedDamageClasses = null
},
```

- [ ] **Step 4: Add localization keys**

In `Localization/en-US_Mods.ARPGItemSystem.hjson`, find the hybrid-appropriate section (after the Chaos Resistance/Penetration block, before the closing `}`). Add:

```
// Hybrid affixes
FortifiedBody: "+{0} max life, {1} max mana"
BalancedGrowth: "+{0} max life, +{1} max mana"
```

Note: `{1}` on `FortifiedBody` will render as e.g. `-18` since `Magnitude2` is negative — the minus sign is automatic, no special handling needed.

- [ ] **Step 5: Build and confirm validation works**

Build via tModLoader: **Workshop → Mod Sources → ARPGItemSystem → Build**.

Expected: build succeeds. The two new affixes now exist in the roll pool for armor and accessories, but `Magnitude2` is applied to nothing yet (dispatch comes in Task 4). Items with `FortifiedBody` or `BalancedGrowth` will show in the tooltip and persist correctly, but will only apply `Magnitude` to player stats until Task 4.

- [ ] **Step 6: Commit**

```
git add ARPGItemSystem/Common/Affixes/AffixId.cs
git add ARPGItemSystem/Common/Affixes/AffixRegistry.cs
git add ARPGItemSystem/Localization/en-US_Mods.ARPGItemSystem.hjson
git commit -m "feat: register FortifiedBody and BalancedGrowth hybrid affixes"
```

---

## Task 4 — Dispatch `Magnitude2` in `EquipmentStatSource` and verify in-game

**Files:**
- Modify: `ARPGCharacterSystem/Common/Stats/Sources/EquipmentStatSource.cs`

---

- [ ] **Step 1: Add dispatch cases**

In `EquipmentStatSource.Dispatch`, add two cases after the `FlatDefenseIncrease when fromAccessory` case (around line 82):

```csharp
case AffixId.FortifiedBody:
    def.BonusMaxHp   += a.Magnitude;   // positive HP
    res.BonusMaxMana += a.Magnitude2;  // negative mana (e.g. -18 at tier 3)
    break;

case AffixId.BalancedGrowth:
    def.BonusMaxHp   += a.Magnitude;   // positive HP
    res.BonusMaxMana += a.Magnitude2;  // positive mana
    break;
```

Both cases need `using ARPGItemSystem.Common.Affixes;` — already present at the top of the file.

- [ ] **Step 2: Build both mods**

Build `ARPGItemSystem` first, then `ARPGCharacterSystem` (dependency order).

In tModLoader: **Workshop → Mod Sources → ARPGItemSystem → Build**, then **Workshop → Mod Sources → ARPGCharacterSystem → Build & Reload**.

Expected: both build without errors.

- [ ] **Step 3: In-game verification — tooltip**

1. Open a world in singleplayer. Reroll a piece of armor or an accessory (press **C** while holding it) until `FortifiedBody` appears.
2. Hover over the item. Confirm the tooltip shows e.g. **+52 max life, -18 max mana** (two values on one line, primary positive, secondary negative).
3. Reroll again and confirm `BalancedGrowth` can also appear showing **+28 max life, +25 max mana** (both positive).

- [ ] **Step 4: In-game verification — stat application**

1. Equip a piece of armor with `FortifiedBody`. Open the character panel (`K`). Note max HP increases by `Magnitude` and max mana decreases by `|Magnitude2|`.
2. Equip a piece of armor with `BalancedGrowth`. Confirm both max HP and max mana increase by their respective rolled values.
3. Unequip — both stats should revert to base.

- [ ] **Step 5: In-game verification — persistence**

1. With a `FortifiedBody` item equipped, save and exit to menu.
2. Reload the world. Confirm the item's tooltip still shows the same two magnitude values (not rerolled, not zeroed out).

- [ ] **Step 6: In-game verification — old saves**

1. If you have a pre-Task-1 world save (any item with existing affixes), load it.
2. Confirm existing items load without errors and their affixes display normally (single-stat affixes unchanged).

- [ ] **Step 7: Commit**

```
git add ARPGCharacterSystem/Common/Stats/Sources/EquipmentStatSource.cs
git commit -m "feat: dispatch Magnitude2 for FortifiedBody and BalancedGrowth in EquipmentStatSource"
```

---

## Self-Review Notes

- **Spec coverage:** All 10 acceptance criteria are covered. Criterion 10 (reforge panel `AffixLine` unchanged) is satisfied implicitly — `AffixLine.cs` is not in the file map and is not touched.
- **Old-save backward compatibility:** `LoadData` defaults `"Magnitudes2"` to all-zeros when key is absent. No reroll triggered. Covered in Task 4 Step 6.
- **NetSend/NetReceive ordering:** `Magnitude2` is written after `Tier` in `NetSend` and read in the same order in `NetReceive`. Consistent.
- **Localization escaping:** Hybrid keys use quoted strings (`"..."`) because they contain a comma, which is valid hjson special syntax. Single-value keys that don't need escaping can stay unquoted (existing pattern).
