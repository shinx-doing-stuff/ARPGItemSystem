# Hybrid Affixes Design

**Date:** 2026-05-13  
**Mod:** ARPGItemSystem (data model + registry) + ARPGCharacterSystem (dispatch)  
**Status:** Approved

## Problem

All current affixes modify exactly one stat. We want **hybrid affixes** that modify two stats simultaneously — either a double-positive trade (e.g. +HP and +Mana) or a trade-off (e.g. +HP and −Mana). The secondary magnitude should vary per tier the same way the primary does, not be a fixed constant.

## Design

### Data Model

Add one field to `Affix`:

```csharp
public readonly struct Affix
{
    public readonly AffixId Id;
    public readonly int Magnitude;   // primary stat rolled value
    public readonly int Magnitude2;  // secondary stat rolled value; 0 for all non-hybrid affixes
    public readonly int Tier;
}
```

`Magnitude2 = 0` is the natural default — all non-hybrid affixes are unaffected. Old saves that lack `"Magnitudes2"` in their `TagCompound` load correctly with all zeros.

### AffixDef Extension

Add `SecondaryTiers` to `AffixDef`:

```csharp
public Dictionary<ItemCategory, List<Tier>>? SecondaryTiers { get; init; }
public bool IsHybrid => SecondaryTiers != null;
```

`SecondaryTiers` is null on all non-hybrid affixes. When present it follows the same convention as `Tiers`: exactly 10 entries per category, `Min`/`Max` inclusive range. The existing 10-count validation loop in `BuildRegistry()` is extended to cover `SecondaryTiers`.

Negative `Min`/`Max` values in `SecondaryTiers` express a penalty. There is no special-casing for sign — a negative `Magnitude2` naturally renders as e.g. `−18` via the `{1}` format arg.

### Rolling

`AffixRoller.Roll` rolls `Magnitude2` from `SecondaryTiers[category][tier]` at the same tier index used for the primary:

```csharp
int magnitude2 = 0;
if (def.SecondaryTiers != null && def.SecondaryTiers.TryGetValue(category, out var secTiers))
    magnitude2 = Main.rand.Next(secTiers[tier].Min, secTiers[tier].Max + 1);

return new Affix(def.Id, magnitude, magnitude2, tier);
```

Both stats scale together (same tier roll) but each has independent variance via its own range. A narrow range on the secondary (e.g. `new(-22,-19)`) keeps the trade-off predictable while still not being a fixed constant.

### Persistence

`AffixItemManager` adds one parallel list to its existing four:

| Tag key | Content |
|---|---|
| `"AffixIds"` | existing |
| `"Magnitudes"` | existing |
| `"Magnitudes2"` | **new** — absent on old saves, defaults to all-zeros |
| `"Tiers"` | existing |
| `"Kinds"` | existing |

`LoadData` treats a missing `"Magnitudes2"` key as all zeros — no migration or reroll triggered.

`NetSend`/`NetReceive` each add one `int` per affix (the `Magnitude2` field). Both sides of the connection always run the same mod build, so no version negotiation is needed.

`Clone` requires no change — `Affix` is a struct, so `Affixes.ToList()` already deep-copies `Magnitude2`.

### Tooltip

`AffixItemManager.ModifyTooltips` passes `Magnitude2` as `{1}` when the affix is hybrid:

```csharp
text = def.IsHybrid
    ? Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{affix.Id}", affix.Magnitude, affix.Magnitude2)
    : Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{affix.Id}", affix.Magnitude);
```

Localization keys for hybrid affixes use two positional args. Examples:

```
FortifiedBody:  "+{0} max life, {1} max mana"
BalancedGrowth: "+{0} max life, +{1} max mana"
```

`{1}` on a negative `Magnitude2` renders as e.g. `-18`, giving a natural read: `+52 max life, -18 max mana`.

### Dispatch (EquipmentStatSource)

Cases for hybrid affixes use `a.Magnitude` for the primary and `a.Magnitude2` for the secondary directly — no registry lookup needed at dispatch time:

```csharp
case AffixId.FortifiedBody:
    def.BonusMaxHp  += a.Magnitude;
    res.BonusMaxMana += a.Magnitude2;  // negative value at runtime
    break;

case AffixId.BalancedGrowth:
    def.BonusMaxHp  += a.Magnitude;
    res.BonusMaxMana += a.Magnitude2;
    break;
```

### Registry Entries (initial two affixes)

**FortifiedBody** — Armor + Accessory, Prefix. Large HP gain, mana penalty. HP is ~125% of standalone `FlatLifeIncrease` to compensate for the cost.

Primary (HP) tier table — Armor:
`(70,76) (63,69) (56,62) (49,55) (42,48) (35,41) (28,34) (21,27) (14,20) (6,13)`

Secondary (−Mana) tier table — Armor:
`(-32,-28) (-28,-25) (-25,-22) (-22,-19) (-18,-16) (-15,-13) (-12,-10) (-9,-7) (-6,-4) (-3,-2)`

Accessory values scale proportionally (same ratio as existing FlatLifeIncrease/FlatManaIncrease accessory-to-armor ratio).

**BalancedGrowth** — Armor + Accessory, Prefix. Modest HP and mana, both positive. Each stat is ~65% of its standalone counterpart since one affix slot buys two stats.

Primary (HP) and Secondary (Mana) tier tables — Armor:
`(37,40) (33,36) (29,32) (25,28) (21,24) (17,20) (13,16) (9,12) (5,8) (1,4)`

Accessory mana secondary skews slightly higher than HP to match the existing accessory FlatManaIncrease scale.

## Files Changed

| File | Change |
|---|---|
| `ARPGItemSystem/Common/Affixes/Affix.cs` | Add `Magnitude2` field + update constructor |
| `ARPGItemSystem/Common/Affixes/AffixDef.cs` | Add `SecondaryTiers`, `IsHybrid` |
| `ARPGItemSystem/Common/Affixes/AffixRoller.cs` | Roll `magnitude2` when `SecondaryTiers` present |
| `ARPGItemSystem/Common/Affixes/AffixItemManager.cs` | `SaveData`/`LoadData`/`NetSend`/`NetReceive`/`ModifyTooltips` |
| `ARPGItemSystem/Common/Affixes/AffixId.cs` | Append `FortifiedBody`, `BalancedGrowth` |
| `ARPGItemSystem/Common/Affixes/AffixRegistry.cs` | Add two `AffixDef` entries + extend validation |
| `ARPGCharacterSystem/Common/Stats/Sources/EquipmentStatSource.cs` | Add dispatch cases |
| `ARPGItemSystem/Localization/en-US_Mods.ARPGItemSystem.hjson` | Add two tooltip keys |

## Acceptance Criteria

1. `Affix` struct compiles with `Magnitude2`; existing construction sites updated.
2. `AffixRoller` rolls `Magnitude2` from `SecondaryTiers`; non-hybrid affixes always get `Magnitude2 = 0`.
3. Old saves (no `"Magnitudes2"` key) load without error; all existing affixes get `Magnitude2 = 0`.
4. New saves round-trip `Magnitude2` correctly through `SaveData`/`LoadData`.
5. `NetSend`/`NetReceive` transmit `Magnitude2`; both sides reconstruct identical `Affix` instances.
6. `FortifiedBody` tooltip reads e.g. `+52 max life, -18 max mana` (primary positive, secondary negative).
7. `BalancedGrowth` tooltip reads e.g. `+28 max life, +25 max mana` (both positive).
8. `EquipmentStatSource` applies both `Magnitude` and `Magnitude2` to the correct containers.
9. `AffixRegistry` validation throws on `SecondaryTiers` entries with ≠ 10 tiers.
10. No changes to `AffixLine.cs` (reforge panel) — the "max N" ceiling label continues to reflect the primary stat only; the secondary is a design-time constant, not a player-facing ceiling.
