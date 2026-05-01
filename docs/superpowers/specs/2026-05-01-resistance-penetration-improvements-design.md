# Resistance Penetration Improvements

**Date:** 2026-05-01
**Scope:** ARPGItemSystem only

## Overview

Two improvements to the penetration affix system:

1. `FlatArmorPen` and `PercentageArmorPen` become Suffix (was Prefix) and gain an Accessory category.
2. New `AllElementalPenetration` affix — Suffix, Weapon + Accessory, penetrates all three elemental resistances at reduced magnitude (~55% of specific pen).

## Change 1 — Armor Pen: Suffix + Accessory Category

### Kind Change

Both `FlatArmorPen` and `PercentageArmorPen` change from `AffixKind.Prefix` to `AffixKind.Suffix` in `AffixRegistry`. This makes them consistent with all other penetration affixes which are Suffix.

Existing saved items are unaffected: their `Kinds` tag stores the value at roll time. Only newly rolled items land in the Suffix slot.

### New Accessory Tiers

`FlatArmorPen` (Accessory):

| Tier | Min | Max |
|------|-----|-----|
| 0    | 10  | 12  |
| 1    | 9   | 10  |
| 2    | 7   | 9   |
| 3    | 6   | 7   |
| 4    | 5   | 6   |
| 5    | 4   | 5   |
| 6    | 3   | 4   |
| 7    | 2   | 3   |
| 8    | 1   | 2   |
| 9    | 1   | 1   |

`PercentageArmorPen` (Accessory):

| Tier | Min | Max |
|------|-----|-----|
| 0    | 5   | 6   |
| 1    | 5   | 5   |
| 2    | 4   | 5   |
| 3    | 4   | 4   |
| 4    | 3   | 4   |
| 5    | 3   | 3   |
| 6    | 2   | 3   |
| 7    | 2   | 2   |
| 8    | 1   | 1   |
| 9    | 1   | 1   |

### Aggregation in PlayerElementalPlayer

Two new fields: `FlatArmorPen` (float) and `PercentArmorPen` (float), reset to 0 in `PostUpdateEquips`. Populated by `ApplyPenetrationAffixes` (accessories only):

```csharp
case AffixId.FlatArmorPen:       FlatArmorPen  += a.Magnitude; break;
case AffixId.PercentageArmorPen: PercentArmorPen += a.Magnitude; break;
```

### Calculator Change

```csharp
float flatArmorPen = GetMagnitude(affixes, AffixId.FlatArmorPen) + playerElem.FlatArmorPen;
float percArmorPen = GetMagnitude(affixes, AffixId.PercentageArmorPen) + playerElem.PercentArmorPen;
```

Stacking is additive — same as elemental pen. On the calculator's `effectiveDefense` line, both weapon and accessory contributions are already combined before the resistance derivation.

---

## Change 2 — New `AllElementalPenetration` Affix

### AffixId

Append `AllElementalPenetration` to the `AffixId` enum after `LightningPenetration`. Never insert or reorder — integer values are persisted.

### AffixDef

- **Kind:** `AffixKind.Suffix`
- **Categories:** Weapon, Accessory

Weapon tiers:

| Tier | Min | Max |
|------|-----|-----|
| 0    | 16  | 18  |
| 1    | 14  | 15  |
| 2    | 12  | 13  |
| 3    | 10  | 12  |
| 4    | 9   | 10  |
| 5    | 7   | 8   |
| 6    | 5   | 6   |
| 7    | 4   | 5   |
| 8    | 2   | 3   |
| 9    | 1   | 2   |

Accessory tiers:

| Tier | Min | Max |
|------|-----|-----|
| 0    | 3   | 3   |
| 1    | 2   | 3   |
| 2    | 2   | 3   |
| 3    | 2   | 2   |
| 4    | 2   | 2   |
| 5    | 1   | 2   |
| 6    | 1   | 2   |
| 7    | 1   | 1   |
| 8    | 1   | 1   |
| 9    | 1   | 1   |

### Aggregation in PlayerElementalPlayer

In `ApplyPenetrationAffixes` (accessories), `AllElementalPenetration` adds its magnitude to all three existing pen fields:

```csharp
case AffixId.AllElementalPenetration:
    FirePen      += a.Magnitude;
    ColdPen      += a.Magnitude;
    LightningPen += a.Magnitude;
    break;
```

### Calculator Change

Universal pen from the weapon affix list is read once and added to all three specific pens. `playerElem.FirePen/ColdPen/LightningPen` already include the accessory's `AllElementalPenetration` contribution via `ApplyPenetrationAffixes`.

```csharp
float universalPen = GetMagnitude(affixes, AffixId.AllElementalPenetration);
float firePen  = GetMagnitude(affixes, AffixId.FirePenetration)      + universalPen + playerElem.FirePen;
float coldPen  = GetMagnitude(affixes, AffixId.ColdPenetration)      + universalPen + playerElem.ColdPen;
float lightPen = GetMagnitude(affixes, AffixId.LightningPenetration) + universalPen + playerElem.LightningPen;
```

### Localization

Add to `Localization/en-US_Mods.ARPGItemSystem.hjson` under `Affixes`:

```
AllElementalPenetration: "{0}% All Elemental Penetration"
```

### Debug Log

The existing pen log line in `ElementalDamageCalculator` gains a `universalPen` entry when non-zero:

```
if (universalPen != 0) penParts.Append($"  all:{universalPen:F0}%");
```

---

## Files Changed

| File | Change |
|------|--------|
| `Common/Affixes/AffixId.cs` | Append `AllElementalPenetration` |
| `Common/Affixes/AffixRegistry.cs` | Kind change + Accessory tiers for armor pen; new `AllElementalPenetration` def |
| `Common/Players/PlayerElementalPlayer.cs` | Two new armor pen fields; `AllElementalPenetration` case in `ApplyPenetrationAffixes` |
| `Common/Elements/ElementalDamageCalculator.cs` | Armor pen reads `playerElem`; universal pen local var added to all three pens; debug log update |
| `Localization/en-US_Mods.ARPGItemSystem.hjson` | New `AllElementalPenetration` key |

## Out of Scope

- No changes to `ARPGEnemySystem`
- No new network sync fields (armor pen and universal pen affect the attacker's side only — no per-NPC state)
- No migration for saved items (kind change only affects future rolls; existing items keep their stored kind)
