# Affix Pool Expansion — Batch 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 8 new affixes (LifeRegeneration, ManaRegeneration, ThornDamage, DamageToManaBeforeLife, NearbyDamageBonus, DistantDamageBonus, LowHpDamageBonus, FullHpDamageBonus) and consolidate the player-hit handling pipeline into a single `PlayerHurtPipeline` ModPlayer.

**Architecture:** Six of the affixes are pure `case` additions to existing managers (registry + localization + switch entry). Two (ThornDamage, DamageToManaBeforeLife) require a new `PlayerSurvivalPlayer` aggregator and a new `PlayerHurtPipeline` ModPlayer that owns all incoming-damage modification. Adding `PlayerHurtPipeline` lets us delete `ElementalHitFromNPCGlobalNPC.cs` and the `ProjectileManager.ModifyHitPlayer` method, since `ModPlayer.ModifyHurt` sees both contact and projectile sources via `modifiers.DamageSource`.

**Tech Stack:** C# 12 / .NET 8.0, tModLoader 2026-02 (TML_2026_02), MonoGame/XNA. Cross-mod reference to ARPGEnemySystem (via `build.txt` modReferences + `.csproj` `<Reference>`).

**Spec references:**

- Batch-1 spec: `docs/superpowers/specs/2026-05-03-affix-pool-expansion-batch-1-design.md`
- Parent spec: `docs/superpowers/specs/2026-05-02-affix-pool-utility-expansion-design.md`
- CLAUDE.md (root of `ARPGItemSystem/`) — read this first; covers architecture, save format, MP rules, and the AffixId append-only constraint.

**Testing model:** tModLoader has no automated test harness. Each task ends with a `dotnet build` compile check, and behavior-changing tasks add a tModLoader Build & Reload + an in-game verification step. The verification steps in this plan come from §9 of the batch-1 spec.

**Build commands:**

- Compile-only (fast feedback): `dotnet build` from `ARPGItemSystem/` (requires `../ARPGEnemySystem/bin/Debug/net8.0/ARPGEnemySystem.dll` to exist — build ARPGEnemySystem once first if it doesn't).
- Full deploy: in tModLoader → Workshop → Mod Sources → `ARPGItemSystem` → **Build & Reload**. This is the only path that gets the changes into a running game session.
- All paths in this plan are relative to `ARPGItemSystem/`.

---

## File Map

| Path                                                | Action                                                               | Responsibility                                                                                        |
| --------------------------------------------------- | -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| `Common/Affixes/AffixId.cs`                         | Modify (append 8)                                                    | Enum of stable persisted affix IDs                                                                    |
| `Common/Affixes/AffixRegistry.cs`                   | Modify (add 8 defs)                                                  | Single source of truth for affix definitions                                                          |
| `Localization/en-US_Mods.ARPGItemSystem.hjson`      | Modify (add 8 keys)                                                  | Tooltip strings                                                                                       |
| `Common/GlobalItems/Weapon/WeaponManager.cs`        | Modify (4 cases in `ModifyHitNPC`)                                   | Weapon affix application during direct hits                                                           |
| `Common/GlobalItems/ProjectileManager.cs`           | Modify (4 new cases in `ModifyHitNPC`; **delete** `ModifyHitPlayer`) | Player→enemy projectile path; was also enemy→player resistance (now removed)                          |
| `Common/GlobalItems/Armor/ArmorManager.cs`          | Modify (2 cases in `UpdateEquip`)                                    | Per-frame armor effects                                                                               |
| `Common/GlobalItems/Accessory/AccessoryManager.cs`  | Modify (2 cases in `UpdateAccessory`)                                | Per-frame accessory effects                                                                           |
| `Common/Players/PlayerSurvivalPlayer.cs`            | **Create**                                                           | Aggregates `ThornsPercent` + `ManaAbsorbPercent` from gear                                            |
| `Common/Players/PlayerHurtPipeline.cs`              | **Create**                                                           | Owns `ModifyHurt` and `OnHurt` for all incoming-damage modification (resistance, mana-absorb, thorns) |
| `Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs` | **Delete**                                                           | Replaced by `PlayerHurtPipeline.ModifyHurt` (NPC contact branch)                                      |

---

## Task List

The 9 tasks below follow the implementation order from §11 of the batch-1 spec, decomposed into bite-sized, individually committable steps.

1. **Task 1** — Append 8 AffixId enum entries
2. **Task 2** — Add 8 AffixDef registry entries
3. **Task 3** — Add 8 localization keys
4. **Task 4** — Add 4 weapon-damage cases in WeaponManager + ProjectileManager.ModifyHitNPC
5. **Task 5** — Add LifeRegen + ManaRegen cases in ArmorManager + AccessoryManager
6. **Task 6** — Create PlayerSurvivalPlayer (aggregator only — no in-game effect yet)
7. **Task 7** — Create PlayerHurtPipeline (resistance dispatch) and delete the two old hooks atomically
8. **Task 8** — Extend PlayerHurtPipeline with mana-absorb logic
9. **Task 9** — Extend PlayerHurtPipeline with thorns OnHurt logic

---

## Task 1: Append 8 AffixId enum entries

**Files:**

- Modify: `Common/Affixes/AffixId.cs`

The integer value of each enum member is persisted in item save tags. Inserting or moving entries corrupts every saved item that holds an affix with a shifted ID. **Append-only — never reorder.**

- [ ] **Step 1.1: Add 8 entries at the end of the enum**

Open `Common/Affixes/AffixId.cs`. The current last entry is `ManaCostReduction` on line 52 with no trailing comma. Add a comma after it, then append 8 new entries.

Replace the end of the enum (lines 50–53):

```csharp
        // All categories
        FlatCritChance,
        ManaCostReduction
    }
```

with:

```csharp
        // All categories
        FlatCritChance,
        ManaCostReduction,

        // Batch-1 (2026-05-03): hurt-pipeline + conditional + distance affixes
        LifeRegeneration,
        ManaRegeneration,
        ThornDamage,
        DamageToManaBeforeLife,
        NearbyDamageBonus,
        DistantDamageBonus,
        LowHpDamageBonus,
        FullHpDamageBonus
    }
```

- [ ] **Step 1.2: Compile check**

Run: `dotnet build`
Expected: build succeeds with no errors. The 8 new IDs aren't referenced anywhere yet, but adding them to the enum must not break compilation.

- [ ] **Step 1.3: Commit**

```bash
git add Common/Affixes/AffixId.cs
git commit -m "feat: append 8 batch-1 AffixId entries"
```

---

## Task 2: Add 8 AffixDef registry entries

**Files:**

- Modify: `Common/Affixes/AffixRegistry.cs`

Each `AffixDef` requires:

- `Id` — matches the enum value
- `Kind` — `AffixKind.Prefix` or `AffixKind.Suffix`
- `Tiers` — Dictionary keyed by `ItemCategory`, each value a `List<Tier>` of **exactly 10** entries (T0 best → T9 worst). `BuildRegistry()` validates this on load and throws if violated.
- `AllowedDamageClasses` — `null` for all 8 affixes in this batch (no damage-class restrictions)

Tier values are taken from §3.2 (LowHp), §4.3 (DamageToManaBeforeLife), and the parent spec for the others. **DamageToManaBeforeLife** uses `Kind = AffixKind.Prefix` per §4.1, not Suffix as proposed in the parent spec.

- [ ] **Step 2.1: Locate the insertion point**

Open `Common/Affixes/AffixRegistry.cs`. The `defs` list is built in `BuildRegistry()`. Find the end of the list — it ends with the last existing entry (likely `ManaCostReduction` or similar) and a closing `};`. Insert all 8 new defs immediately before that closing `};`.

To find it precisely, search for `Id = AffixId.ManaCostReduction` and scroll down to the closing `},` of that entry — append the new defs after it.

- [ ] **Step 2.2: Add LifeRegeneration def**

```csharp
                // ============== BATCH-1 AFFIXES (2026-05-03) ==============

                // A.1 — LifeRegeneration: Armor + Accessory, Prefix.
                // Magnitude is in vanilla Player.lifeRegen units (2 = 1 HP/second).
                new AffixDef {
                    Id = AffixId.LifeRegeneration,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(5,6), new(4,5), new(4,4), new(3,4), new(3,3),
                            new(2,3), new(2,2), new(1,2), new(1,1), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(3,4), new(3,3), new(2,3), new(2,3), new(2,2),
                            new(1,2), new(1,2), new(1,1), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.3: Add ManaRegeneration def**

```csharp
                // A.2 — ManaRegeneration: Armor + Accessory, Prefix.
                // Magnitude is in vanilla Player.manaRegen units.
                new AffixDef {
                    Id = AffixId.ManaRegeneration,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(10,12), new(8,10), new(7,9), new(6,8), new(5,7),
                            new(4,6),   new(3,5),  new(2,4), new(1,2), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,8), new(5,7), new(4,6), new(3,5), new(3,4),
                            new(2,3), new(2,3), new(1,2), new(1,2), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.4: Add ThornDamage def**

```csharp
                // A.3 — ThornDamage: Armor + Accessory, Suffix. Aggregate cap 80%.
                // Magnitude is percent of incoming damage reflected to attacker.
                new AffixDef {
                    Id = AffixId.ThornDamage,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(30,35), new(25,30), new(20,25), new(16,20), new(13,16),
                            new(10,13), new(7,10),  new(5,7),   new(3,5),   new(1,3)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(30,35), new(25,30), new(20,25), new(16,20), new(13,16),
                            new(10,13), new(7,10),  new(5,7),   new(3,5),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.5: Add DamageToManaBeforeLife def (PREFIX, revised tiers)**

This affix uses `Kind = AffixKind.Prefix` per §4.1 (not Suffix from the parent). Tiers are from §4.3 (≈0.22× parent values). Aggregate cap 40%.

```csharp
                // A.4 — DamageToManaBeforeLife: Armor + Accessory, PREFIX (per §4.1).
                // Aggregate cap 40% (§4.2). Magnitudes ≈0.22× parent (§4.3).
                // Per-hit cap = 25% of statManaMax2 applied at hit time (see PlayerHurtPipeline).
                new AffixDef {
                    Id = AffixId.DamageToManaBeforeLife,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(8,9), new(7,8), new(6,7), new(5,6), new(4,5),
                            new(4,4), new(3,4), new(2,3), new(2,2), new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(5,6), new(4,5), new(3,4), new(2,3), new(2,2),
                            new(2,2), new(1,2), new(1,1), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.6: Add NearbyDamageBonus def**

```csharp
                // D.1 — NearbyDamageBonus: Weapon, Suffix. Bonus when target ≤ 256px (16 tiles).
                new AffixDef {
                    Id = AffixId.NearbyDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(50,60), new(42,50), new(35,42), new(28,35), new(22,28),
                            new(17,22), new(12,17), new(8,12),  new(4,8),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.7: Add DistantDamageBonus def**

```csharp
                // D.2 — DistantDamageBonus: Weapon, Suffix. Bonus when target ≥ 768px (48 tiles).
                // Same magnitudes as NearbyDamageBonus — coexistence creates a mid-range dead zone.
                new AffixDef {
                    Id = AffixId.DistantDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(50,60), new(42,50), new(35,42), new(28,35), new(22,28),
                            new(17,22), new(12,17), new(8,12),  new(4,8),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.8: Add LowHpDamageBonus def (revised tiers from §3.2)**

Tiers are 10% lower than parent spec (multiplicative, preserves curve shape). Boundaries align: T*n.max == T*(n-1).min.

```csharp
                // C.1 — LowHpDamageBonus: Weapon, Suffix. Graduated ramp — full magnitude at HP ≤25%,
                // zero at HP ≥70%, linear between (see §3.1 of spec). Tiers are §3.2 (×0.9 of parent).
                new AffixDef {
                    Id = AffixId.LowHpDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(54,63), new(45,54), new(38,45), new(32,38), new(25,32),
                            new(20,25), new(14,20), new(10,14), new(5,10),  new(1,5)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.9: Add FullHpDamageBonus def**

Uses parent spec C.2 verbatim (same magnitudes as parent C.1, before our §3 revision). Asymmetric with LowHp by design — LowHp's lower magnitudes compensate for its ramped-up math.

```csharp
                // C.2 — FullHpDamageBonus: Weapon, Suffix. Bonus when statLife >= statLifeMax2.
                // Magnitudes verbatim from parent spec (60-70 down to 1-6) — kept asymmetric with C.1
                // because C.1's ramped scaling reduces its effective average; binary FullHp keeps full value.
                new AffixDef {
                    Id = AffixId.FullHpDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(60,70), new(50,60), new(42,50), new(35,42), new(28,35),
                            new(22,28), new(16,22), new(11,16), new(6,11),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = null
                },
```

- [ ] **Step 2.10: Compile check**

Run: `dotnet build`
Expected: build succeeds. If `BuildRegistry()` validation runs at static init time, any mismatch in tier list count (must be exactly 10) would fail at game-load time, not compile time — so this step only catches syntax errors.

- [ ] **Step 2.11: Build & Reload + in-game smoke test**

In tModLoader → Workshop → Mod Sources → `ARPGItemSystem` → **Build & Reload**. Open a world with the mod loaded.

Expected: mod loads without throwing. Logs (visible in tModLoader log window) should NOT contain a tier-count validation error from `BuildRegistry()`. Pick up a fresh weapon/armor/accessory and inspect tooltips — the new affixes will appear with **no localized text yet** (the key will display as raw `Mods.ARPGItemSystem.Affixes.LifeRegeneration` or similar, depending on whether they roll). This is expected and gets fixed in Task 3.

- [ ] **Step 2.12: Commit**

```bash
git add Common/Affixes/AffixRegistry.cs
git commit -m "feat: register 8 batch-1 AffixDef entries"
```

---

## Task 3: Add 8 localization keys

**Files:**

- Modify: `Localization/en-US_Mods.ARPGItemSystem.hjson`

Keys must exactly match the `AffixId` enum names. The `{0}` placeholder is replaced with the rolled magnitude at draw time.

- [ ] **Step 3.1: Add 8 entries to the `Affixes:` block**

Open `Localization/en-US_Mods.ARPGItemSystem.hjson`. The `Affixes:` block currently ends with `AllElementalPenetration: "{0}% All Elemental Penetration"` followed by a closing `}` on the next line. Insert the 8 new keys before the closing `}`.

Insert these lines between `AllElementalPenetration: ...` and the closing brace:

```
	// Batch-1 (2026-05-03)
	LifeRegeneration: "+{0} Life Regen"
	ManaRegeneration: "+{0} Mana Regen"
	ThornDamage: "Reflects {0}% of melee damage taken"
	DamageToManaBeforeLife: "{0}% of damage absorbed by mana first (cap 40%)"
	NearbyDamageBonus: "+{0}% damage to nearby enemies"
	DistantDamageBonus: "+{0}% damage to distant enemies"
	LowHpDamageBonus: "Up to +{0}% damage as Life decreases (max at 25%)"
	FullHpDamageBonus: "+{0}% damage at full Life"
```

(Use a tab character for indentation — match the existing file's indentation style.)

- [ ] **Step 3.2: Compile check**

Run: `dotnet build`
Expected: build succeeds. Hjson is parsed at runtime by tModLoader; syntax errors won't show up at compile time.

- [ ] **Step 3.3: Build & Reload + in-game tooltip verification**

Build & Reload. In a world, find/spawn a weapon-armor-accessory set with the new affixes rolled and inspect tooltips. (Quick way: spawn many items via cheat sheet / debug, look for ones that rolled the new IDs.)

Expected: tooltips display the localized text instead of the raw key. For example: a weapon with NearbyDamageBonus should show `+15% damage to nearby enemies` (with the actual rolled magnitude). The text appears alongside any other rolled affixes.

If raw keys still show, the hjson didn't load — check tModLoader log for a parse error.

- [ ] **Step 3.4: Commit**

```bash
git add Localization/en-US_Mods.ARPGItemSystem.hjson
git commit -m "feat: add localization for 8 batch-1 affixes"
```

---

## Task 4: Weapon-damage cases in WeaponManager + ProjectileManager.ModifyHitNPC

**Files:**

- Modify: `Common/GlobalItems/Weapon/WeaponManager.cs` (in `ModifyHitNPC`, ~line 52)
- Modify: `Common/GlobalItems/ProjectileManager.cs` (in `ModifyHitNPC`, ~line 49)

The four conditional/distance damage affixes use the same hook in both files. They follow the existing pattern (`case AffixId.X:`) alongside the `CritMultiplier` case.

For projectiles, the active player is `Main.player[projectile.owner]` — the existing code already establishes this in the `ElementalDamageCalculator.ApplyToHit(...)` call.

- [ ] **Step 4.1: Add cases in `WeaponManager.ModifyHitNPC`**

Open `Common/GlobalItems/Weapon/WeaponManager.cs`. Find `ModifyHitNPC` (around line 52) — it currently contains a `foreach (var a in Affixes)` loop with a single `if (a.Id == AffixId.CritMultiplier)` check, then a call to `ElementalDamageCalculator.ApplyToHit(...)`.

Replace the body of that `foreach` loop. The current body:

```csharp
        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.CritMultiplier)
                    modifiers.CritDamage += a.Magnitude / 100f;
                // PercentageArmorPen removed — now handled inside ElementalDamageCalculator
                // as a reduction to enemy physical resistance before the cap
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }
```

becomes:

```csharp
        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.NearbyDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) <= 256f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.DistantDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) >= 608f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.LowHpDamageBonus:
                    {
                        float hpPct = player.statLifeMax2 > 0
                            ? player.statLife / (float)player.statLifeMax2
                            : 1f;
                        float factor = MathHelper.Clamp((0.70f - hpPct) / 0.45f, 0f, 1f);
                        modifiers.SourceDamage += a.Magnitude * factor / 100f;
                        break;
                    }
                    case AffixId.FullHpDamageBonus:
                        if (player.statLife >= player.statLifeMax2)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                }
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }
```

`Vector2`, `MathHelper`, and `NPC` are already imported in this file (`using Microsoft.Xna.Framework;` covers Vector2/MathHelper).

- [ ] **Step 4.2: Add the same cases in `ProjectileManager.ModifyHitNPC`**

Open `Common/GlobalItems/ProjectileManager.cs`. Find `ModifyHitNPC` around line 49 — it has the same `if (a.Id == AffixId.CritMultiplier)` shape. The active player here is obtained via `Main.player[projectile.owner]`, which is already used in the `ElementalDamageCalculator.ApplyToHit(...)` call below.

Replace the existing `ModifyHitNPC` body:

```csharp
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Affixes.Count == 0) return;

            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.CritMultiplier)
                    modifiers.CritDamage += a.Magnitude / 100f;
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, Main.player[projectile.owner], target, ref modifiers);
        }
```

with:

```csharp
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Affixes.Count == 0) return;

            var player = Main.player[projectile.owner];

            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.NearbyDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) <= 256f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.DistantDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) >= 608f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.LowHpDamageBonus:
                    {
                        float hpPct = player.statLifeMax2 > 0
                            ? player.statLife / (float)player.statLifeMax2
                            : 1f;
                        float factor = MathHelper.Clamp((0.70f - hpPct) / 0.45f, 0f, 1f);
                        modifiers.SourceDamage += a.Magnitude * factor / 100f;
                        break;
                    }
                    case AffixId.FullHpDamageBonus:
                        if (player.statLife >= player.statLifeMax2)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                }
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }
```

- [ ] **Step 4.3: Compile check**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 4.4: Build & Reload + in-game verification**

Build & Reload. Smoke-test each of the four affixes in-game:

| Affix              | Verification                                                                                                                                                           |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| NearbyDamageBonus  | Stand within 16 tiles of an enemy and hit it with a weapon rolling this affix → bonus applies (visible in damage numbers). Move beyond 16 tiles, hit again → no bonus. |
| DistantDamageBonus | Inverse: bonus at ≥48 tiles, none at <48.                                                                                                                              |
| LowHpDamageBonus   | At full HP → no bonus. Take damage to ~50% HP → partial bonus (~half magnitude). Drop below 25% HP → full magnitude.                                                   |
| FullHpDamageBonus  | At exact full HP → bonus. Lose any HP → no bonus.                                                                                                                      |

Hard to read damage numbers? Drop into a creative-mode world, get a weak weapon with one of these affixes, hit a target dummy, compare against the same weapon without the affix.

- [ ] **Step 4.5: Commit**

```bash
git add Common/GlobalItems/Weapon/WeaponManager.cs Common/GlobalItems/ProjectileManager.cs
git commit -m "feat: weapon damage cases for nearby, distant, lowhp, fullhp"
```

---

## Task 5: LifeRegen + ManaRegen cases in ArmorManager + AccessoryManager

**Files:**

- Modify: `Common/GlobalItems/Armor/ArmorManager.cs` (in `UpdateEquip`)
- Modify: `Common/GlobalItems/Accessory/AccessoryManager.cs` (in `UpdateAccessory`)

Both regen affixes are direct increments on per-tick `Player.lifeRegen` / `Player.manaRegen` fields.

- [ ] **Step 5.1: Add cases in `ArmorManager.UpdateEquip`**

Open `Common/GlobalItems/Armor/ArmorManager.cs`. Find the `switch (a.Id)` block in `UpdateEquip`. Add two new cases after the existing `case AffixId.ManaCostReduction:` (or anywhere in the switch — order is not significant):

```csharp
                    case AffixId.LifeRegeneration:
                        player.lifeRegen += a.Magnitude;
                        break;
                    case AffixId.ManaRegeneration:
                        player.manaRegen += a.Magnitude;
                        break;
```

- [ ] **Step 5.2: Add cases in `AccessoryManager.UpdateAccessory`**

Open `Common/GlobalItems/Accessory/AccessoryManager.cs`. Find the `switch (a.Id)` block in `UpdateAccessory`. Add the same two cases:

```csharp
                    case AffixId.LifeRegeneration:
                        player.lifeRegen += a.Magnitude;
                        break;
                    case AffixId.ManaRegeneration:
                        player.manaRegen += a.Magnitude;
                        break;
```

- [ ] **Step 5.3: Compile check**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 5.4: Build & Reload + in-game verification**

Build & Reload. Equip an armor or accessory rolling LifeRegeneration → stand still and observe HP regenerating faster than vanilla. Compare with the same gear minus the affix. Same procedure for ManaRegeneration with mana.

A simple sanity check: stand still with no buffs, no campfire/heart lantern, check HP tick rate. With LifeRegen +5 affix, you should regen perceptibly faster than baseline.

- [ ] **Step 5.5: Commit**

```bash
git add Common/GlobalItems/Armor/ArmorManager.cs Common/GlobalItems/Accessory/AccessoryManager.cs
git commit -m "feat: life and mana regen affixes in armor and accessory managers"
```

---

## Task 6: Create PlayerSurvivalPlayer (aggregator)

**Files:**

- Create: `Common/Players/PlayerSurvivalPlayer.cs`

Pure aggregator. Walks armor + accessory affixes in `PostUpdateEquips`, sums `ThornsPercent` and `ManaAbsorbPercent` into public fields, applies caps. No hit-pipeline hooks — those are added in Task 7+. Until Task 7 wires it up, this file has no externally visible effect (the values are aggregated but nobody reads them).

- [ ] **Step 6.1: Write the new file**

Create `Common/Players/PlayerSurvivalPlayer.cs` with:

```csharp
using System;
using System.Collections.Generic;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    // Pure aggregator for survival affixes. Walks armor + accessory each PostUpdateEquips
    // and sums Thorns / ManaAbsorb percentages, applying per-affix caps.
    // The values are read by PlayerHurtPipeline at hit time.
    public class PlayerSurvivalPlayer : ModPlayer
    {
        public float ThornsPercent;       // capped at 80%
        public float ManaAbsorbPercent;   // capped at 40% (per spec §4.2)

        public override void PostUpdateEquips()
        {
            ThornsPercent = 0f;
            ManaAbsorbPercent = 0f;

            for (int i = 0; i < Player.armor.Length; i++)
            {
                var item = Player.armor[i];
                if (item.IsAir) continue;

                if (item.TryGetGlobalItem<ArmorManager>(out var am))
                    Apply(am.Affixes);
                else if (item.TryGetGlobalItem<AccessoryManager>(out var acc))
                    Apply(acc.Affixes);
            }

            ThornsPercent     = Math.Min(ThornsPercent,     80f);
            ManaAbsorbPercent = Math.Min(ManaAbsorbPercent, 40f);
        }

        private void Apply(List<Affix> affixes)
        {
            foreach (var a in affixes)
            {
                switch (a.Id)
                {
                    case AffixId.ThornDamage:
                        ThornsPercent += a.Magnitude;
                        break;
                    case AffixId.DamageToManaBeforeLife:
                        ManaAbsorbPercent += a.Magnitude;
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 6.2: Compile check**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 6.3: Build & Reload sanity**

Build & Reload. Open a world. Equip armor/accessories with ThornDamage or DamageToManaBeforeLife rolled. There is no in-game effect yet — but the mod must load without errors, and `PostUpdateEquips` must run without exceptions. (Watch the tModLoader log window for stack traces.)

- [ ] **Step 6.4: Commit**

```bash
git add Common/Players/PlayerSurvivalPlayer.cs
git commit -m "feat: add PlayerSurvivalPlayer aggregator for thorns and mana-absorb"
```

---

## Task 7: Create PlayerHurtPipeline (resistance dispatch) + delete old hooks

**Files:**

- Create: `Common/Players/PlayerHurtPipeline.cs`
- Modify: `Common/GlobalItems/ProjectileManager.cs` (delete `ModifyHitPlayer` method)
- Delete: `Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs`

This is the riskiest task — it moves the elemental resistance application from two GlobalXXX hooks into one ModPlayer hook. **Do all three changes in one commit** so the resistance pipeline is never simultaneously active in two places (which would double-apply resistance).

The new file in this task implements the **resistance dispatch only** — branches for projectile and contact sources, builds the elemental breakdown, registers a `ModifyHurtInfo` callback that overrides `info.Damage` with the resistance-applied total. Mana-absorb (Task 8) and thorns (Task 9) are added on top in the next two tasks.

- [ ] **Step 7.1: Write `PlayerHurtPipeline.cs` with resistance dispatch + log**

Create `Common/Players/PlayerHurtPipeline.cs`:

```csharp
using System;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using EnemyProjectileManager = ARPGEnemySystem.Common.GlobalProjectiles.ProjectileManager;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    // Single owner of the player's incoming-damage modification pipeline.
    // ModifyHurt dispatches on modifiers.DamageSource:
    //   - SourceProjectileLocalIndex >= 0 → enemy projectile branch
    //   - SourceNPCIndex >= 0           → NPC contact branch
    //   - else                          → vanilla math runs unchanged (lava/fall/drown/PvP)
    //
    // Replaces the old per-hook implementations:
    //   - ProjectileManager.ModifyHitPlayer (deleted)
    //   - ElementalHitFromNPCGlobalNPC.cs (deleted)
    //
    // Mana-absorb (Task 8) and thorns (Task 9) extend this file in subsequent tasks.
    public class PlayerHurtPipeline : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            var src = modifiers.DamageSource;

            // Branch A: enemy projectile
            if (src.SourceProjectileLocalIndex >= 0)
            {
                var proj = Main.projectile[src.SourceProjectileLocalIndex];
                if (!proj.active) return;
                if (!proj.TryGetGlobalProjectile<EnemyProjectileManager>(out var pm)) return;

                float firePct, coldPct, lightPct;
                string sourceName;
                if (pm.modNPC != null)
                {
                    firePct  = pm.modNPC.FireDamagePct;
                    coldPct  = pm.modNPC.ColdDamagePct;
                    lightPct = pm.modNPC.LightningDamagePct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else if (pm.modBossNPC != null)
                {
                    firePct  = pm.modBossNPC.FireDamagePct;
                    coldPct  = pm.modBossNPC.ColdDamagePct;
                    lightPct = pm.modBossNPC.LightningDamagePct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else return;

                // Projectile damage is pre-scaled in ARPGEnemySystem; vanilla doesn't apply DamageVar to it.
                RegisterHandler(ref modifiers, proj.damage, firePct, coldPct, lightPct, sourceName, isProj: true);
                return;
            }

            // Branch B: NPC direct contact
            if (src.SourceNPCIndex >= 0)
            {
                var npc = Main.npc[src.SourceNPCIndex];
                if (!npc.active) return;

                float firePct, coldPct, lightPct;
                if (npc.TryGetGlobalNPC<NPCManager>(out var nd))
                {
                    firePct  = nd.FireDamagePct;
                    coldPct  = nd.ColdDamagePct;
                    lightPct = nd.LightningDamagePct;
                }
                else if (npc.TryGetGlobalNPC<BossManager>(out var bd))
                {
                    firePct  = bd.FireDamagePct;
                    coldPct  = bd.ColdDamagePct;
                    lightPct = bd.LightningDamagePct;
                }
                else return;

                // Contact hits use vanilla ±15% damage variance.
                RegisterHandler(ref modifiers, Main.DamageVar(npc.damage), firePct, coldPct, lightPct, npc.GivenOrTypeName, isProj: false);
                return;
            }

            // Branch C: lava / fall / drown / PvP / custom — vanilla math runs unchanged.
        }

        private void RegisterHandler(ref Player.HurtModifiers modifiers,
                                      float baseDamage,
                                      float firePct, float coldPct, float lightPct,
                                      string sourceName, bool isProj)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            var elem = Player.GetModPlayer<PlayerElementalPlayer>();

            float totalElemPct = (firePct + coldPct + lightPct) / 100f;
            float physPortion  = baseDamage * Math.Max(0f, 1f - totalElemPct);
            float firePortion  = baseDamage * firePct  / 100f;
            float coldPortion  = baseDamage * coldPct  / 100f;
            float lightPortion = baseDamage * lightPct / 100f;

            float physFinal  = ElementalMath.ApplyResistance(physPortion,  elem.PhysRes,      cap);
            float fireFinal  = ElementalMath.ApplyResistance(firePortion,  elem.FireRes,      cap);
            float coldFinal  = ElementalMath.ApplyResistance(coldPortion,  elem.ColdRes,      cap);
            float lightFinal = ElementalMath.ApplyResistance(lightPortion, elem.LightningRes, cap);

            int finalDamage = Math.Max(1, (int)Math.Round(physFinal + fireFinal + coldFinal + lightFinal));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

            // Capture log values for the closure.
            float pf = physFinal,  ff = fireFinal,  cf = coldFinal,  lf = lightFinal;
            float pp = physPortion, fp = firePortion, cp = coldPortion, lp = lightPortion;
            float pr = elem.PhysRes, fr = elem.FireRes, cr = elem.ColdRes, lr = elem.LightningRes;
            float fpct = firePct, cpct = coldPct, lpct = lightPct;
            string srcName = sourceName;
            bool wasProj = isProj;

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                // Mana-absorb hooks in here in Task 8.

                if (logEnabled)
                {
                    string tag = wasProj ? "[proj] " : "";
                    Main.NewText($"← {tag}{srcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {pf,6:F1}  (raw:{pp,5:F1}  res:{pr:F1}%)", Color.Silver);
                    if (fpct > 0) Main.NewText($"  Fire:  {ff,6:F1}  (raw:{fp,5:F1}  res:{fr:F1}%)",  new Color(255, 120, 50));
                    if (cpct > 0) Main.NewText($"  Cold:  {cf,6:F1}  (raw:{cp,5:F1}  res:{cr:F1}%)",  new Color(100, 200, 255));
                    if (lpct > 0) Main.NewText($"  Light: {lf,6:F1}  (raw:{lp,5:F1}  res:{lr:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {info.Damage}", Color.OrangeRed);
                }
            };
        }
    }
}
```

A note on the `ref` parameter and lambda: `RegisterHandler` takes `ref modifiers`. Inside it, we register a callback on `modifiers.ModifyHurtInfo` (an event/delegate field). The callback closes over local variables (`finalDamage`, `pf`, `srcName`, etc.), which is safe because `RegisterHandler` is called once per hurt event and the locals don't change after registration.

- [ ] **Step 7.2: Delete `ProjectileManager.ModifyHitPlayer` method**

Open `Common/GlobalItems/ProjectileManager.cs`. Locate the `ModifyHitPlayer(...)` method (currently around line 64–137 — the 70+ line block that does enemy-projectile→player resistance). Delete the entire method, including its leading `// Handles enemy-projectile → player hits...` comment block.

After deletion, the `ProjectileManager` class still has `OnSpawn` (player→enemy affix capture) and `ModifyHitNPC` (player→enemy elemental + new conditional bonuses from Task 4). The class compiles fine without `ModifyHitPlayer` — it was an override and removing it just means tModLoader doesn't call this class for `ModifyHitPlayer` events.

You can also remove the now-unused `using` directives at the top of the file if any are exclusively referenced inside the deleted method:

- `using EnemyProjectileManager = ARPGEnemySystem.Common.GlobalProjectiles.ProjectileManager;` — check if it's used elsewhere in the file. If yes, keep it. If only `ModifyHitPlayer` referenced it, remove the alias.
- `using ARPGItemSystem.Common.Players;` — check the same way (it was used to reference `PlayerElementalPlayer`).

Either way, an unused `using` is just a compile warning, not an error — leaving them is acceptable, removing them is cleaner.

- [ ] **Step 7.3: Delete `ElementalHitFromNPCGlobalNPC.cs`**

```bash
git rm Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs
```

If the `Common/GlobalNPCs/` directory becomes empty after this, leave the empty directory in place — tModLoader doesn't care, and we don't want to track directory deletion as a separate operation.

- [ ] **Step 7.4: Compile check**

Run: `dotnet build`
Expected: build succeeds. If it fails with "type or namespace `ElementalHitFromNPCGlobalNPC` not found", check that nothing else referenced the deleted class.

- [ ] **Step 7.5: Build & Reload + REGRESSION CHECK + new-feature check**

This is the riskiest step. Build & Reload, then verify both **the regression** (resistance was working before, must still work) **and the new dispatch** (PlayerHurtPipeline now handles both branches).

**Regression check (must continue working):**

| Test                                                                                              | Expected                                                        |
| ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| Take a hit from a managed NPC contact (e.g., a zombie with elemental damage from ARPGEnemySystem) | Resistance applies, damage matches what it was before this task |
| Take a hit from a managed NPC's projectile (e.g., a skeleton archer's arrow)                      | Resistance applies, damage matches what it was before this task |
| Enable `EnemyConfigClient.EnableElementalDamageLog` in the config and take any hit                | The breakdown log appears in chat with the correct numbers      |

**Source-branch check:**

| Test                                     | Expected                                                   |
| ---------------------------------------- | ---------------------------------------------------------- |
| Fall from height onto land (fall damage) | Vanilla damage applied — no resistance recalc, no log line |
| Walk into lava                           | Vanilla damage applied — no resistance recalc, no log line |
| Drown underwater                         | Vanilla damage applied — no resistance recalc, no log line |

If any regression test fails (resistance no longer applied, or applied twice), the most likely cause is that an old hook wasn't deleted. Check that `Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs` is gone and that `ProjectileManager.cs` no longer contains a `ModifyHitPlayer` override.

If logging is doubled (each hit shows the breakdown twice), the lambda is being registered twice — verify `PlayerHurtPipeline.ModifyHurt` only ends up calling `RegisterHandler` once per hit (one branch returns before the next runs).

- [ ] **Step 7.6: Commit**

```bash
git add Common/Players/PlayerHurtPipeline.cs Common/GlobalItems/ProjectileManager.cs
git commit -m "refactor: consolidate hurt pipeline into PlayerHurtPipeline"
```

(The `git rm` from Step 7.3 already staged the deletion of `ElementalHitFromNPCGlobalNPC.cs`, so it's included in this commit.)

---

## Task 8: Add mana-absorb to PlayerHurtPipeline

**Files:**

- Modify: `Common/Players/PlayerHurtPipeline.cs` (extend the lambda registered in `RegisterHandler`)

Apply the §4.6 math: `routed = info.Damage * ManaAbsorbPercent / 100`, then clamped by per-hit cap `statManaMax2 × 0.25`, then clamped by available mana. Drain mana, set regen delay, subtract from `info.Damage`. Log when the affix actually absorbs (not on every hit).

- [ ] **Step 8.1: Extend the `ModifyHurtInfo` lambda**

In `Common/Players/PlayerHurtPipeline.cs`, locate the lambda registered in `RegisterHandler`:

```csharp
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                // Mana-absorb hooks in here in Task 8.

                if (logEnabled)
                {
                    string tag = wasProj ? "[proj] " : "";
                    // ... existing log lines ...
                }
            };
```

Replace the placeholder comment with the mana-absorb logic. The lambda body becomes:

```csharp
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                // Mana-absorb (spec §4.6): % of damage routed to mana, capped by per-hit ceiling
                // (25% of statManaMax2) and by current mana available. Triggers regen delay.
                var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
                int absorbed = 0;
                if (sp.ManaAbsorbPercent > 0 && info.Damage > 0 && Player.statManaMax2 > 0)
                {
                    int routed      = (int)(info.Damage * sp.ManaAbsorbPercent / 100f);
                    int perHitCap   = (int)(Player.statManaMax2 * 0.25f);
                    int cappedRoute = Math.Min(routed, perHitCap);
                    absorbed        = Math.Min(cappedRoute, Player.statMana);

                    Player.statMana       -= absorbed;
                    Player.manaRegenDelay  = Math.Max(Player.manaRegenDelay, 40);
                    info.Damage           -= absorbed;
                }

                if (logEnabled)
                {
                    string tag = wasProj ? "[proj] " : "";
                    Main.NewText($"← {tag}{srcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {pf,6:F1}  (raw:{pp,5:F1}  res:{pr:F1}%)", Color.Silver);
                    if (fpct > 0) Main.NewText($"  Fire:  {ff,6:F1}  (raw:{fp,5:F1}  res:{fr:F1}%)",  new Color(255, 120, 50));
                    if (cpct > 0) Main.NewText($"  Cold:  {cf,6:F1}  (raw:{cp,5:F1}  res:{cr:F1}%)",  new Color(100, 200, 255));
                    if (lpct > 0) Main.NewText($"  Light: {lf,6:F1}  (raw:{lp,5:F1}  res:{lr:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {finalDamage}", Color.OrangeRed);
                    if (absorbed > 0)
                    {
                        Main.NewText($"  Absorb: {absorbed} (mana: {Player.statMana + absorbed} → {Player.statMana})", new Color(180, 100, 200));
                        Main.NewText($"  After absorb: {info.Damage}", Color.OrangeRed);
                    }
                }
            };
```

Note: the `Total:` line now reads `finalDamage` (the captured local) instead of `info.Damage`, because by the time we log, mana-absorb may already have decreased `info.Damage`. The `Total:` represents the post-resistance-pre-absorb value, which is more useful for debugging.

- [ ] **Step 8.2: Compile check**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 8.3: Build & Reload + in-game verification**

Build & Reload. Equip armor or accessories with `DamageToManaBeforeLife` rolled and test these specific cases (from spec §9):

| Case                      | Test                                                                                                            | Expected                                                                                                                                                                              |
| ------------------------- | --------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| (a) Full mana, hit lands  | Take a hit with full mana                                                                                       | Mana drops, HP drops less than the raw incoming damage. With log on, `Absorb:` and `After absorb:` lines appear.                                                                      |
| (b) Empty mana, hit lands | Drain mana to 0, take a hit                                                                                     | All damage falls through; mana stays 0; no `Absorb:` log line.                                                                                                                        |
| (c) Regen delay           | Right after an absorb, watch mana                                                                               | Mana doesn't regenerate for ≥2/3 sec (40 ticks at 60fps).                                                                                                                             |
| (d) Per-hit cap           | Boost mana to ~400 (potions + FlatManaIncrease), take a single big hit (e.g., a Wall of Flesh slam at ~200 dmg) | `Absorb:` line shows ≤ `statManaMax2 × 0.25`, NOT the full routed amount. (e.g., with 400 mana, max absorb per hit is 100.)                                                           |
| (e) Aggregate cap         | Stack T0 armor + T0 accessory rolls (~66 magnitude total), take a hit                                           | Effective absorb behaves as if `ManaAbsorbPercent = 40` (capped). Easiest check: equip rolls totaling 66 absorb%; hit a 100-damage source; absorb should be 40 (= 40% × 100), not 66. |
| (f) Low-investment chip   | With `statManaMax2 = 20` (no investment), take a 5-damage chip hit at 40% absorb                                | Absorbed = 5 × 0.4 = 2, capped by per-hit (20 × 0.25 = 5) → not the binding constraint here; absorbed = 2.                                                                            |

If absorb numbers seem off, enable `EnableElementalDamageLog` and read the breakdown line-by-line.

- [ ] **Step 8.4: Commit**

```bash
git add Common/Players/PlayerHurtPipeline.cs
git commit -m "feat: mana-absorb (DamageToManaBeforeLife) with per-hit cap"
```

---

## Task 9: Add thorns OnHurt to PlayerHurtPipeline

**Files:**

- Modify: `Common/Players/PlayerHurtPipeline.cs` (add `OnHurt` override)

Thorns reflects damage to the attacker — only on direct NPC contact (not projectile hits). It runs in `OnHurt` because `info.Damage` at that point is fully settled (post-resistance, post-mana-absorb), which is exactly the value the spec wants reflected (post-absorb thorns, per the design decisions in §5).

- [ ] **Step 9.1: Add `OnHurt` override**

In `Common/Players/PlayerHurtPipeline.cs`, add this override to the class. Place it after `RegisterHandler` (anywhere in the class works — placing it near the other override is conventional):

```csharp
        public override void OnHurt(Player.HurtInfo info)
        {
            var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
            if (sp.ThornsPercent <= 0) return;

            // Direct NPC contact only — skip projectile hits per spec A.3
            if (info.DamageSource.SourceProjectileLocalIndex >= 0) return;
            int npcIdx = info.DamageSource.SourceNPCIndex;
            if (npcIdx < 0) return;
            var npc = Main.npc[npcIdx];
            if (!npc.active || npc.whoAmI != npcIdx) return;

            int reflected = (int)(info.Damage * sp.ThornsPercent / 100f);
            if (reflected <= 0) return;

            // StrikeNPC auto-broadcasts in MP; no custom packet needed.
            npc.StrikeNPC(npc.CalculateHitInfo(reflected, 0, false, 0f, DamageClass.Default, true));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;
            if (logEnabled)
                Main.NewText($"  Thorns: {reflected} → {npc.GivenOrTypeName}", Color.LightGreen);
        }
```

`DamageClass` requires `using Terraria.ModLoader;` (already present in the file).

- [ ] **Step 9.2: Compile check**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 9.3: Build & Reload + in-game verification**

Build & Reload. Equip armor/accessories with `ThornDamage` rolled and verify:

| Case                   | Test                                                                   | Expected                                                                                                                             |
| ---------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Direct contact reflect | Stand still, let a Zombie walk into you                                | Zombie loses HP equal to ~`ThornsPercent%` of the damage you took. With log on, `Thorns:` line appears.                              |
| Projectile filter      | Get hit by a Skeleton Archer's arrow                                   | NO reflect — projectile hits don't trigger thorns. No `Thorns:` log line.                                                            |
| Aggregate cap          | Stack thorns rolls totaling >80% across gear                           | Effective reflect behaves as if `ThornsPercent = 80` (capped in `PlayerSurvivalPlayer`).                                             |
| Reflect after absorb   | With both ThornDamage AND DamageToManaBeforeLife rolled — take a hit   | Thorns reflects from `info.Damage` AFTER mana absorb (i.e., reduced if mana absorbed some). Log order: resistance → absorb → thorns. |
| Multiplayer            | Host + join, stand near host with thorns equipped, let zombie hit host | Reflected damage visible to both clients (vanilla `StrikeNPC` net-syncs).                                                            |

- [ ] **Step 9.4: Commit**

```bash
git add Common/Players/PlayerHurtPipeline.cs
git commit -m "feat: ThornDamage reflect on direct NPC contact"
```

---

## Final Verification

After Task 9, the full §9 verification checklist from the spec should pass. Re-run the spec's full table once more end-to-end:

- [ ] LifeRegeneration — HP tick rate increases proportionally to magnitude
- [ ] ManaRegeneration — mana tick rate increases
- [ ] ThornDamage — contact reflects, projectiles don't, debug log shows reflected amount
- [ ] DamageToManaBeforeLife — full/empty mana cases, regen delay, per-hit cap, aggregate cap, low-investment chip absorb, gear-wide investment cap-approach
- [ ] NearbyDamageBonus — bonus inside 16 tiles, none outside
- [ ] DistantDamageBonus — bonus past 48 tiles, none inside
- [ ] LowHpDamageBonus — graduated ramp at 100% / 47% / 20% HP
- [ ] FullHpDamageBonus — bonus at exact full, none after any HP loss
- [ ] **Refactor regression** — NPC contact resistance, NPC projectile resistance, fall/lava/drown all behave the same as before this batch

Multiplayer:

- [ ] Host + join, observe each new affix in MP at least once
- [ ] Thorns reflect visible across clients
- [ ] Mana-absorb consistent on hit-receiver's client
- [ ] LifeRegen / ManaRegen visible on each client's own bar

If any check fails, read the spec's §9 case description and the related task in this plan to diagnose.

---

## Squash policy

Per project memory: **squash iteration commits before merging**. The 9 commits in this plan can stand on their own as a logical history (one commit per task) — they don't need squashing if you find them clean. If iteration during execution adds fix commits (e.g., "fix: typo", "fix: regression"), squash them into the parent task's commit before merging the branch.
