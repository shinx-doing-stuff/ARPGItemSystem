# Resistance Penetration Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend armor pen affixes to accessories (as Suffix), and add a new `AllElementalPenetration` affix that penetrates all three elemental resistances at ~55% of specific pen magnitude.

**Architecture:** All changes are within `ARPGItemSystem`. Affix definitions live in `AffixRegistry`; aggregation of equipment pen totals lives in `PlayerElementalPlayer`; hit math lives in `ElementalDamageCalculator`. Armor pen from accessories is aggregated the same way elemental pen already is — fields on `PlayerElementalPlayer`, reset each frame in `PostUpdateEquips`, summed by `ApplyPenetrationAffixes`, then read in the calculator. Universal elemental pen from a weapon is read directly from the weapon's affix list in the calculator.

**Tech Stack:** C# / .NET 8 / tModLoader. No automated test harness — verification is `dotnet build` (compile check) then in-game with the `EnableElementalDamageLog` config toggle.

---

## File Map

| File | Change |
|------|--------|
| `Common/Affixes/AffixId.cs` | Append `AllElementalPenetration` enum value |
| `Common/Affixes/AffixRegistry.cs` | `FlatArmorPen` + `PercentageArmorPen`: Prefix → Suffix, add Accessory tiers; new `AllElementalPenetration` def |
| `Common/Players/PlayerElementalPlayer.cs` | Add `FlatArmorPen`/`PercentArmorPen` fields; add 3 new cases in `ApplyPenetrationAffixes` |
| `Common/Elements/ElementalDamageCalculator.cs` | Armor pen reads `playerElem`; add `universalPen` var; debug log update |
| `Localization/en-US_Mods.ARPGItemSystem.hjson` | Add `AllElementalPenetration` tooltip key |

---

## Task 1: Extend AffixId enum

**Files:**
- Modify: `Common/Affixes/AffixId.cs`

`AllElementalPenetration` must be appended — never inserted mid-enum. Integer values are persisted in item saves; reordering corrupts all saved items.

- [ ] **Step 1: Add the enum value**

In `Common/Affixes/AffixId.cs`, find the elemental penetration block and append `AllElementalPenetration`:

```csharp
// Weapon + Accessory — elemental penetration (subtracts from enemy resistance before cap)
FirePenetration,
ColdPenetration,
LightningPenetration,
AllElementalPenetration,
```

- [ ] **Step 2: Compile check**

Run from the `ARPGItemSystem` directory:
```
dotnet build
```
Expected: build succeeds. `AllElementalPenetration` is referenced nowhere yet so no errors expected.

- [ ] **Step 3: Commit**

```
git add Common/Affixes/AffixId.cs
git commit -m "feat: append AllElementalPenetration to AffixId enum"
```

---

## Task 2: Update AffixRegistry — armor pen kind and accessory tiers

**Files:**
- Modify: `Common/Affixes/AffixRegistry.cs`

Change `FlatArmorPen` and `PercentageArmorPen` from `AffixKind.Prefix` to `AffixKind.Suffix` and add `Accessory` tier tables. Existing saved items keep their stored kind; only newly rolled items land in the Suffix slot.

- [ ] **Step 1: Replace the FlatArmorPen AffixDef**

Find the existing `FlatArmorPen` entry (~line 81) and replace the entire `new AffixDef { ... }` block with:

```csharp
new AffixDef {
    Id = AffixId.FlatArmorPen,
    Kind = AffixKind.Suffix,
    Tiers = new Dictionary<ItemCategory, List<Tier>>
    {
        [ItemCategory.Weapon] = new List<Tier> {
            new(46,50), new(41,45), new(36,40), new(31,35), new(26,30),
            new(21,25), new(16,20), new(11,15), new(6,10),  new(1,5)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(10,12), new(9,10), new(7,9), new(6,7), new(5,6),
            new(4,5),   new(3,4),  new(2,3), new(1,2), new(1,1)
        }
    },
    AllowedDamageClasses = null
},
```

- [ ] **Step 2: Replace the PercentageArmorPen AffixDef**

Find the existing `PercentageArmorPen` entry (~line 94) and replace the entire block with:

```csharp
new AffixDef {
    Id = AffixId.PercentageArmorPen,
    Kind = AffixKind.Suffix,
    Tiers = new Dictionary<ItemCategory, List<Tier>>
    {
        [ItemCategory.Weapon] = new List<Tier> {
            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(5,6), new(5,5), new(4,5), new(4,4), new(3,4),
            new(3,3), new(2,3), new(2,2), new(1,1), new(1,1)
        }
    },
    AllowedDamageClasses = null
},
```

- [ ] **Step 3: Compile check**

```
dotnet build
```
Expected: build succeeds.

- [ ] **Step 4: Commit**

```
git add Common/Affixes/AffixRegistry.cs
git commit -m "feat: armor pen affixes become Suffix and gain Accessory tiers"
```

---

## Task 3: Add AllElementalPenetration to AffixRegistry and localization

**Files:**
- Modify: `Common/Affixes/AffixRegistry.cs`
- Modify: `Localization/en-US_Mods.ARPGItemSystem.hjson`

- [ ] **Step 1: Add the AllElementalPenetration AffixDef**

In `Common/Affixes/AffixRegistry.cs`, find the `// ============== WEAPON + ACCESSORY — ELEMENTAL PENETRATION ==============` section. After the `LightningPenetration` block and before the `// ============== ALL CATEGORIES ==============` comment, insert:

```csharp
new AffixDef {
    Id = AffixId.AllElementalPenetration,
    Kind = AffixKind.Suffix,
    Tiers = new Dictionary<ItemCategory, List<Tier>>
    {
        [ItemCategory.Weapon] = new List<Tier> {
            new(16,18), new(14,15), new(12,13), new(10,12), new(9,10),
            new(7,8),   new(5,6),   new(4,5),   new(2,3),   new(1,2)
        },
        [ItemCategory.Accessory] = new List<Tier> {
            new(3,3), new(2,3), new(2,3), new(2,2), new(2,2),
            new(1,2), new(1,2), new(1,1), new(1,1), new(1,1)
        }
    },
    AllowedDamageClasses = null
},
```

- [ ] **Step 2: Add the localization key**

In `Localization/en-US_Mods.ARPGItemSystem.hjson`, find the elemental penetration comment block and append the new key:

```
// Armor/Accessory elemental penetration
FirePenetration: "{0}% Fire Penetration"
ColdPenetration: "{0}% Cold Penetration"
LightningPenetration: "{0}% Lightning Penetration"
AllElementalPenetration: "{0}% All Elemental Penetration"
```

- [ ] **Step 3: Compile check**

```
dotnet build
```
Expected: build succeeds.

- [ ] **Step 4: Commit**

```
git add Common/Affixes/AffixRegistry.cs Localization/en-US_Mods.ARPGItemSystem.hjson
git commit -m "feat: add AllElementalPenetration affix definition and tooltip"
```

---

## Task 4: Aggregate armor pen and universal elemental pen in PlayerElementalPlayer

**Files:**
- Modify: `Common/Players/PlayerElementalPlayer.cs`

`PlayerElementalPlayer` already aggregates `FirePen`/`ColdPen`/`LightningPen` from accessories. This task adds two armor pen fields on the same pattern, and handles `AllElementalPenetration` by fanning it out to all three existing pen fields.

- [ ] **Step 1: Add the two new public fields**

After the existing pen fields (`FirePen`, `ColdPen`, `LightningPen`), add:

```csharp
public float FlatArmorPen;
public float PercentArmorPen;
```

The class field section should look like:

```csharp
public float FirePen;
public float ColdPen;
public float LightningPen;

public float FlatArmorPen;
public float PercentArmorPen;
```

- [ ] **Step 2: Reset the new fields in PostUpdateEquips**

In `PostUpdateEquips`, after the existing pen resets, add:

```csharp
FlatArmorPen    = 0f;
PercentArmorPen = 0f;
```

The reset block should look like:

```csharp
FirePen      = 0f;
ColdPen      = 0f;
LightningPen = 0f;
FlatArmorPen    = 0f;
PercentArmorPen = 0f;
```

- [ ] **Step 3: Handle the three new affix cases in ApplyPenetrationAffixes**

In `ApplyPenetrationAffixes`, add three new cases after the existing `LightningPenetration` case:

```csharp
private void ApplyPenetrationAffixes(List<Affix> affixes)
{
    foreach (var a in affixes)
    {
        switch (a.Id)
        {
            case AffixId.FirePenetration:      FirePen      += a.Magnitude; break;
            case AffixId.ColdPenetration:      ColdPen      += a.Magnitude; break;
            case AffixId.LightningPenetration: LightningPen += a.Magnitude; break;
            case AffixId.FlatArmorPen:         FlatArmorPen    += a.Magnitude; break;
            case AffixId.PercentageArmorPen:   PercentArmorPen += a.Magnitude; break;
            case AffixId.AllElementalPenetration:
                FirePen      += a.Magnitude;
                ColdPen      += a.Magnitude;
                LightningPen += a.Magnitude;
                break;
        }
    }
}
```

- [ ] **Step 4: Compile check**

```
dotnet build
```
Expected: build succeeds.

- [ ] **Step 5: Commit**

```
git add Common/Players/PlayerElementalPlayer.cs
git commit -m "feat: aggregate accessory armor pen and universal elemental pen in PlayerElementalPlayer"
```

---

## Task 5: Consume new fields in ElementalDamageCalculator

**Files:**
- Modify: `Common/Elements/ElementalDamageCalculator.cs`

Three changes: (1) armor pen reads from `playerElem` in addition to the weapon's affixes, (2) universal pen from the weapon is read once and added to all three elemental pens, (3) the debug log shows `all:` pen when non-zero.

- [ ] **Step 1: Combine accessory armor pen into the existing armor pen locals**

Find the armor pen section (~line 81):

```csharp
float flatArmorPen = GetMagnitude(affixes, AffixId.FlatArmorPen);
float percArmorPen = GetMagnitude(affixes, AffixId.PercentageArmorPen);
```

Replace with:

```csharp
float flatArmorPen = GetMagnitude(affixes, AffixId.FlatArmorPen) + playerElem.FlatArmorPen;
float percArmorPen = GetMagnitude(affixes, AffixId.PercentageArmorPen) + playerElem.PercentArmorPen;
```

- [ ] **Step 2: Add universalPen and incorporate into all three elemental pens**

Find the elemental pen section (~line 67):

```csharp
float firePen  = GetMagnitude(affixes, AffixId.FirePenetration)  + playerElem.FirePen;
float coldPen  = GetMagnitude(affixes, AffixId.ColdPenetration)  + playerElem.ColdPen;
float lightPen = GetMagnitude(affixes, AffixId.LightningPenetration) + playerElem.LightningPen;
```

Replace with:

```csharp
float universalPen = GetMagnitude(affixes, AffixId.AllElementalPenetration);
float firePen  = GetMagnitude(affixes, AffixId.FirePenetration)      + universalPen + playerElem.FirePen;
float coldPen  = GetMagnitude(affixes, AffixId.ColdPenetration)      + universalPen + playerElem.ColdPen;
float lightPen = GetMagnitude(affixes, AffixId.LightningPenetration) + universalPen + playerElem.LightningPen;
```

Note: `playerElem.FirePen` etc. already include the `AllElementalPenetration` magnitude from accessories (added in `ApplyPenetrationAffixes`). `universalPen` here covers only the weapon's own `AllElementalPenetration` roll.

- [ ] **Step 3: Update the debug log pen line**

Find the `penParts` StringBuilder block inside the `if (logEnabled)` section. It currently appends entries for `anyArmPen`, `firePen`, `coldPen`, `lightPen`. Add the `universalPen` entry:

```csharp
if (anyArmPen)     penParts.Append($"  arm:{flatArmorPen:F0}flat/{percArmorPen:F0}%");
if (universalPen != 0) penParts.Append($"  all:{universalPen:F0}%");
if (firePen  != 0) penParts.Append($"  fire:{firePen:F0}%");
if (coldPen  != 0) penParts.Append($"  cold:{coldPen:F0}%");
if (lightPen != 0) penParts.Append($"  light:{lightPen:F0}%");
```

Also update the condition that controls whether the `[pen]` line is shown at all to include `universalPen`:

```csharp
if (anyArmPen || universalPen != 0 || firePen != 0 || coldPen != 0 || lightPen != 0)
```

- [ ] **Step 4: Compile check**

```
dotnet build
```
Expected: build succeeds with no errors or warnings.

- [ ] **Step 5: Commit**

```
git add Common/Elements/ElementalDamageCalculator.cs
git commit -m "feat: armor pen and AllElementalPenetration applied in hit calculator"
```

---

## Task 6: In-game verification

No automated test harness. Load the mod in tModLoader and verify manually.

- [ ] **Step 1: Build and load**

In tModLoader: Workshop → Mod Sources → ARPGItemSystem → Build & Reload.

- [ ] **Step 2: Enable the damage log**

Open Mod Configs → ARPGEnemySystem → set `EnableElementalDamageLog = true`.

- [ ] **Step 3: Verify armor pen on accessories**

Use Cheat Sheet or an item editor to spawn an accessory and reroll it until `FlatArmorPen` or `PercentageArmorPen` appears. Confirm:
- The tooltip shows the armor pen value.
- When hitting an enemy while wearing it, the `[pen]` log line shows `arm:Xflat/Y%` with the combined weapon + accessory value.

- [ ] **Step 4: Verify armor pen is now Suffix on weapons**

Reroll a weapon. Confirm armor pen rolls appear in the Suffix slot (shown as "Suffix" in the tooltip, competing with crit/elemental pen for the suffix slot rather than damage/speed for the prefix slot).

- [ ] **Step 5: Verify AllElementalPenetration on a weapon**

Reroll weapons until `All Elemental Penetration` appears. Hit an enemy that has elemental resistances. Confirm:
- The `[pen]` log line shows `all:X%`.
- The effective `fire:`, `cold:`, `light:` resistance values shown in the log are all reduced by the universal pen value compared to without it.

- [ ] **Step 6: Verify AllElementalPenetration on an accessory**

Reroll accessories until `All Elemental Penetration` appears. Equip it and hit an enemy. Confirm:
- The pen log shows the three elemental resistance values are reduced (the `playerElem.FirePen/ColdPen/LightningPen` path).
- The `all:` label does NOT appear (that label is for the weapon's own `universalPen` local; accessory pen feeds through `playerElem.FirePen` etc. and shows as part of the individual `fire:`/`cold:`/`light:` pen display).

- [ ] **Step 7: Final commit (if any last fixes were made)**

```
git add -p
git commit -m "fix: <describe any fixes made during verification>"
```
