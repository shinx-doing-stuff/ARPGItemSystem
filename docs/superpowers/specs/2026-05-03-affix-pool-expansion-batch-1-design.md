# Affix Pool Expansion — Batch 1

**Date:** 2026-05-03
**Scope:** ARPGItemSystem only
**Parent spec:** [2026-05-02-affix-pool-utility-expansion-design.md](2026-05-02-affix-pool-utility-expansion-design.md)

## Overview

First implementation batch from the 2026-05-02 catalog. Implements 8 affixes that share a common property: **none require new projectile-sync infrastructure (`SendExtraAI`/`ReceiveExtraAI`)**. They split across three small architectural changes:

- 5 affixes are pure `case` additions to existing managers
- 2 affixes (ThornDamage, DamageToManaBeforeLife) require a new hurt-pipeline ModPlayer
- 1 affix (LowHpDamageBonus) deviates from the parent spec's binary threshold to a graduated scaling formula
- 1 affix (DamageToManaBeforeLife) deviates from the parent spec on Kind, cap, magnitudes, and per-hit math — see §4

The parent spec's §I rebalance (Suffix→Prefix promotions) is already applied in `AffixRegistry.cs` as of 2026-05-03. This batch can leverage the new prefix pool for opportunity-cost design (§4).

Out of scope for this batch: ProjectileBounce, ProjectileHoming, ProjectilePiercing, ExtraProjectileChance, ManaFueledDamage. Each will be its own follow-up.

## Affixes In This Batch

| # | Affix | Slot | Kind | Source in parent spec |
|---|---|---|---|---|
| 1 | LifeRegeneration | Armor + Accessory | Prefix | A.1 |
| 2 | ManaRegeneration | Armor + Accessory | Prefix | A.2 |
| 3 | ThornDamage | Armor + Accessory | Suffix | A.3 |
| 4 | DamageToManaBeforeLife | Armor + Accessory | **Prefix** | A.4 *(modified — see §4)* |
| 5 | NearbyDamageBonus | Weapon | Suffix | D.1 |
| 6 | DistantDamageBonus | Weapon | Suffix | D.2 |
| 7 | LowHpDamageBonus | Weapon | Suffix | C.1 *(modified — see §3)* |
| 8 | FullHpDamageBonus | Weapon | Suffix | C.2 |

Tier tables for affixes 1, 2, 3, 5, 6, 8 are taken **verbatim** from the parent spec. Affix 7 (LowHpDamageBonus) has a revised tier table — see §3. Affix 4 (DamageToManaBeforeLife) has revised Kind, magnitudes, cap, and per-hit math — see §4.

---

## §1 AffixId Append Order

New entries appended to `Common/Affixes/AffixId.cs` in this order, after the current last entry (`ManaCostReduction`):

```
LifeRegeneration,
ManaRegeneration,
ThornDamage,
DamageToManaBeforeLife,
NearbyDamageBonus,
DistantDamageBonus,
LowHpDamageBonus,
FullHpDamageBonus
```

**CRITICAL:** Append-only. The integer value of each enum member is persisted in item save tags. Inserting or moving entries corrupts every saved item that holds an affix whose ID shifts. This rule applies to all future batches as well.

---

## §2 Architecture — Three-File Hurt Pipeline

The two affixes that hook into the player's hurt pipeline (ThornDamage and DamageToManaBeforeLife), together with the existing elemental resistance logic, are restructured into three files with single, clear responsibilities.

### 2.1 The three files

| File | Responsibility | Hooks |
|---|---|---|
| `Common/Players/PlayerElementalPlayer.cs` *(existing — unchanged)* | Aggregate elemental resistance and penetration values from gear | `PostUpdateEquips` only |
| `Common/Players/PlayerSurvivalPlayer.cs` *(new)* | Aggregate survival affix values (`ThornsPercent`, `ManaAbsorbPercent`) | `PostUpdateEquips` only |
| `Common/Players/PlayerHurtPipeline.cs` *(new)* | Apply all incoming-damage modifiers and reactions | `ModifyHurt` + `OnHurt` |

`PlayerSurvivalPlayer` and `PlayerElementalPlayer` are **pure aggregators**: walk armor + accessory affixes, sum values, write fields. They do not touch the hurt pipeline. `PlayerHurtPipeline` is the **single owner** of all "what happens when this player is hit" logic: it reads from both aggregators and applies the result.

### 2.2 Why this split

The existing elemental resistance code lives across two GlobalXXX files: `ProjectileManager.ModifyHitPlayer` (for enemy projectile hits) and `ElementalHitFromNPCGlobalNPC.ModifyHitPlayer` (for NPC contact hits). Adding mana-absorb naively to those files would duplicate the logic across both, and any future hurt-pipeline affix (block %, damage reduction, etc.) would face the same duplication.

`ModPlayer.ModifyHurt` fires for **every** hurt event regardless of source, with `modifiers.DamageSource` exposing the attacker's index. Consolidating both branches into one `ModifyHurt` removes the duplication and gives every future hurt affix a single, obvious home.

### 2.3 Refactor — files removed / changed

| File | Change |
|---|---|
| `Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs` | **Deleted.** Logic absorbed into `PlayerHurtPipeline.ModifyHurt` (NPC contact branch). |
| `Common/GlobalItems/ProjectileManager.cs` | **`ModifyHitPlayer` method removed.** Logic absorbed into `PlayerHurtPipeline.ModifyHurt` (projectile branch). The rest of the class — `OnSpawn` (player→enemy affix capture) and `ModifyHitNPC` (player→enemy elemental + new conditional bonuses) — stays. |

The cross-mod constraint from `CLAUDE.md` ("enemy projectile resistance lives in ARPGItemSystem because it requires `PlayerElementalPlayer`") is preserved: `PlayerHurtPipeline` is in ARPGItemSystem and reads `PlayerElementalPlayer` directly.

### 2.4 PlayerSurvivalPlayer — new file

```csharp
namespace ARPGItemSystem.Common.Players
{
    public class PlayerSurvivalPlayer : ModPlayer
    {
        public float ThornsPercent;       // capped at 80%
        public float ManaAbsorbPercent;   // capped at 40% (see §4)

        public override void PostUpdateEquips()
        {
            ThornsPercent = 0f;
            ManaAbsorbPercent = 0f;

            for (int i = 0; i < Player.armor.Length; i++)
            {
                var item = Player.armor[i];
                if (item.IsAir) continue;

                if (item.TryGetGlobalItem<ArmorManager>(out var am)) Apply(am.Affixes);
                else if (item.TryGetGlobalItem<AccessoryManager>(out var acc)) Apply(acc.Affixes);
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
                    case AffixId.ThornDamage:            ThornsPercent     += a.Magnitude; break;
                    case AffixId.DamageToManaBeforeLife: ManaAbsorbPercent += a.Magnitude; break;
                }
            }
        }
    }
}
```

### 2.5 PlayerHurtPipeline — new file

`ModifyHurt` dispatches on `modifiers.DamageSource`:

```csharp
public override void ModifyHurt(ref Player.HurtModifiers modifiers)
{
    var src = modifiers.DamageSource;

    // Branch A: enemy projectile (was: ProjectileManager.ModifyHitPlayer)
    if (src.SourceProjectileLocalIndex >= 0)
    {
        var proj = Main.projectile[src.SourceProjectileLocalIndex];
        if (!proj.active) return;
        if (!proj.TryGetGlobalProjectile<EnemyProjectileManager>(out var pm)) return;

        float firePct, coldPct, lightPct;
        string sourceName;
        if (pm.modNPC != null)
        {
            firePct = pm.modNPC.FireDamagePct;
            coldPct = pm.modNPC.ColdDamagePct;
            lightPct = pm.modNPC.LightningDamagePct;
            sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
        }
        else if (pm.modBossNPC != null)
        {
            firePct = pm.modBossNPC.FireDamagePct;
            coldPct = pm.modBossNPC.ColdDamagePct;
            lightPct = pm.modBossNPC.LightningDamagePct;
            sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
        }
        else return;

        // baseDamage uses proj.damage directly — projectile damage is pre-scaled in ARPGEnemySystem;
        // vanilla doesn't apply DamageVar to projectile-source hits.
        RegisterHandler(ref modifiers, proj.damage, firePct, coldPct, lightPct, sourceName, isProj: true);
        return;
    }

    // Branch B: NPC direct contact (was: ElementalHitFromNPCGlobalNPC.ModifyHitPlayer)
    if (src.SourceNPCIndex >= 0)
    {
        var npc = Main.npc[src.SourceNPCIndex];
        if (!npc.active) return;

        float firePct, coldPct, lightPct;
        if (npc.TryGetGlobalNPC<NPCManager>(out var nd))
        {
            firePct = nd.FireDamagePct; coldPct = nd.ColdDamagePct; lightPct = nd.LightningDamagePct;
        }
        else if (npc.TryGetGlobalNPC<BossManager>(out var bd))
        {
            firePct = bd.FireDamagePct; coldPct = bd.ColdDamagePct; lightPct = bd.LightningDamagePct;
        }
        else return;

        // Contact hits use Main.DamageVar(npc.damage) — same as old ElementalHitFromNPCGlobalNPC.
        RegisterHandler(ref modifiers, Main.DamageVar(npc.damage), firePct, coldPct, lightPct, npc.GivenOrTypeName, isProj: false);
        return;
    }

    // Branch C: lava / fall / drown / PvP — vanilla math runs unchanged.
}
```

`RegisterHandler` builds the elemental breakdown and registers a single `ModifyHurtInfo` callback that does resistance → mana-absorb in sequence (mana-absorb math per §4.6). The callback also writes the debug log lines (see §6).

### 2.6 OnHurt — thorns

```csharp
public override void OnHurt(Player.HurtInfo info)
{
    var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
    if (sp.ThornsPercent <= 0) return;

    // Direct NPC contact only — skip projectile hits (spec A.3 filter)
    if (info.DamageSource.SourceProjectileLocalIndex >= 0) return;
    int npcIdx = info.DamageSource.SourceNPCIndex;
    if (npcIdx < 0) return;
    var npc = Main.npc[npcIdx];
    if (!npc.active || npc.whoAmI != npcIdx) return;

    int reflected = (int)(info.Damage * sp.ThornsPercent / 100f);
    if (reflected <= 0) return;

    npc.StrikeNPC(npc.CalculateHitInfo(reflected, 0, false, 0f, DamageClass.Default, true));

    if (LogEnabled())
        Main.NewText($"  Thorns: {reflected} → {npc.GivenOrTypeName}", Color.LightGreen);
}
```

`info.Damage` at this point is post-resistance, post-mana-absorb (the final HP-damage value), so thorns reflects from the actual HP damage taken — the resolution requested for question 1.

### 2.7 Hook lives where, and why

| Affix | Hook | Reason |
|---|---|---|
| LifeRegeneration | `ArmorManager.UpdateEquip` + `AccessoryManager.UpdateAccessory` | Direct write to `Player.lifeRegen`; per-tick aggregation |
| ManaRegeneration | Same | Direct write to `Player.manaRegen` |
| ThornDamage | `PlayerSurvivalPlayer.PostUpdateEquips` (aggregate) + `PlayerHurtPipeline.OnHurt` (apply) | `OnHurt` is "react to a hit"; thorns doesn't modify `info.Damage`, so no need for `ModifyHurt` |
| DamageToManaBeforeLife | `PlayerSurvivalPlayer.PostUpdateEquips` (aggregate) + `PlayerHurtPipeline.ModifyHurt` callback (apply) | Modifies `info.Damage`; must register a `ModifyHurtInfo` callback |
| NearbyDamageBonus | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC` | Same pattern as existing weapon damage cases |
| DistantDamageBonus | Same | Same |
| LowHpDamageBonus | Same | Same |
| FullHpDamageBonus | Same | Same |

---

## §3 LowHpDamageBonus — Graduated Scaling

The parent spec specifies a binary threshold (`hpPct ≤ 0.35` → full magnitude). This batch replaces it with a linear ramp.

### 3.1 Formula

```csharp
float hpPct = player.statLifeMax2 > 0
    ? player.statLife / (float)player.statLifeMax2
    : 1f;
float factor = MathHelper.Clamp((0.70f - hpPct) / 0.45f, 0f, 1f);
float bonus  = a.Magnitude * factor;
modifiers.SourceDamage += bonus / 100f;
```

| Player HP | Damage bonus |
|---|---|
| ≥ 70% | 0 |
| 47.5% (midpoint) | 50% of magnitude |
| ≤ 25% | full magnitude |

The rolled magnitude is the **cap** the player reaches at low HP, not a flat applied bonus.

### 3.2 Tier Table (revised)

Parent spec values multiplied by 0.9 (multiplicative — preserves curve shape; flat −10pp would push T8/T9 negative). Boundaries align (`T_n.min == T_(n-1).max`) — same pattern as the original.

| Tier | Min | Max |
|------|-----|-----|
| T0   | 54  | 63  |
| T1   | 45  | 54  |
| T2   | 38  | 45  |
| T3   | 32  | 38  |
| T4   | 25  | 32  |
| T5   | 20  | 25  |
| T6   | 14  | 20  |
| T7   | 10  | 14  |
| T8   | 5   | 10  |
| T9   | 1   | 5   |

### 3.3 Tooltip

`LowHpDamageBonus: "Up to +{0}% damage as Life decreases (max at 25%)"`

The other 7 affixes use the parent spec's wording verbatim (with the exception of DamageToManaBeforeLife — see §4.5).

---

## §4 DamageToManaBeforeLife — Rebalanced

Four deviations from the parent spec, all aimed at preventing the melee/ranged abuse pattern: those classes have effectively-free mana sustain (vanilla mana potions only carry the Mana Sickness debuff, which only hurts magic damage), so without constraints this affix becomes near-immortal "tank with mana potions" gameplay.

### 4.1 Kind: Suffix → Prefix

The parent spec's §I rebalance is now applied; the prefix slot competes with damage-class % prefixes (`PercentageMeleeDamageIncrease`, etc.). Putting `DamageToManaBeforeLife` in that pool means melee/ranged players choose between offensive scaling and mana-fueled defense — a real opportunity cost on the slot they care most about.

Existing prefix competitors after this change: `FlatLifeIncrease`, `FlatDefenseIncrease`, `FlatManaIncrease`, `PercentageDefenseIncrease`, `FlatCritChance`, `ManaCostReduction`, all 5 damage-class % entries.

### 4.2 Aggregate cap: 80% → 40%

Parent spec proposed 80%. Lowered to 40% because:

- 80% with mana-potion sustain creates near-damage-immunity for non-magic classes
- 40% keeps the "mana acts as a partial second HP bar" feel without making it dominant
- Even at 40%, a player chugging mana potions still benefits — but burst damage and chip damage remain meaningful

### 4.3 Magnitudes: revised tier table

Parent spec values multiplied by ~0.22 (slight bump from the 0.2 draft). The combined effect of low magnitudes + 40% aggregate cap + per-hit cap is to make this affix a *gear-wide investment* rather than a one-roll powerhouse — even maxed rolls require broad coverage to approach the cap.

| Tier | Armor | Accessory |
|------|-------|-----------|
| T0   |  8– 9 |  5– 6     |
| T1   |  7– 8 |  4– 5     |
| T2   |  6– 7 |  3– 4     |
| T3   |  5– 6 |  2– 3     |
| T4   |  4– 5 |  2– 2     |
| T5   |  4– 4 |  2– 2     |
| T6   |  3– 4 |  1– 2     |
| T7   |  2– 3 |  1– 1     |
| T8   |  2– 2 |  1– 1     |
| T9   |  1– 2 |  1– 1     |

With cap 40%, full investment yields:
- 4× T0 armor (max 9 each) = 36 → 4% under cap; armor alone still cannot reach cap.
- 4× T0 armor + 5× T0 accessory (max 6 each) = 36 + 30 = 66 → caps at 40, with 26 magnitude as overflow buffer (losing one or two pieces doesn't immediately drop below cap).
- 4× T9 armor (max 2 each) = 8 → 8% effective; the affix is barely felt at trash tiers.

The "armor alone cannot cap" property is preserved (max armor stack 36 < 40), so accessory investment remains meaningful. Note the tight ranges (often 1–2 wide) — single-roll variance is small at this scale; the player feels gear-aggregate value, not lottery-roll value.

### 4.4 Per-hit absorb cap: `≤ 25% × statManaMax2`

Each individual hit can drain at most **25% of the player's max mana**, regardless of how much mana is currently in the pool. This is the key anti-abuse lever: even with 1000 mana freshly-potion'd, one boss slam can drain at most 250 mana (= 250 absorbed damage). Without this cap, a huge mana pool turns one hit into a near-total negate.

Combined with `manaRegenDelay = max(current, 40)` after each absorb, this also means the player cannot chain-absorb multiple hits at full strength without potion downtime.

The cap intentionally does NOT scale with current mana — it scales with `statManaMax2` (max mana). This means even melee/ranged players with no mana investment still benefit from chip-damage reduction (their small mana pool absorbs a small fraction per hit), but they don't get the burst-tank ceiling that high mana investment provides.

### 4.5 Tooltip

`DamageToManaBeforeLife: "{0}% of damage absorbed by mana first (cap 40%)"`

The "(cap 40%)" suffix communicates the aggregate ceiling so players don't expect linear stacking past the cap. The per-hit cap is intentionally not in the tooltip — too much detail for the line; documented in spec instead.

### 4.6 Apply math (in `PlayerHurtPipeline`'s `ModifyHurtInfo` callback)

```csharp
// After resistance has set info.Damage
var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
if (sp.ManaAbsorbPercent > 0 && info.Damage > 0 && Player.statManaMax2 > 0)
{
    int routed     = (int)(info.Damage * sp.ManaAbsorbPercent / 100f);
    int perHitCap  = (int)(Player.statManaMax2 * 0.25f);
    int cappedRoute = Math.Min(routed, perHitCap);                    // §4.4
    int absorbed    = Math.Min(cappedRoute, Player.statMana);         // can't drain past current mana
    Player.statMana -= absorbed;
    Player.manaRegenDelay = Math.Max(Player.manaRegenDelay, 40);
    info.Damage -= absorbed;
}
```

Three nested mins enforce: routed = % of damage; cappedRoute = clamped to per-hit ceiling; absorbed = clamped to actual mana available.

---

## §5 Resolved Design Decisions

| Question | Decision |
|---|---|
| Thorns reflects pre- or post-mana-absorb damage? | **Post-absorb.** `info.Damage` in `OnHurt` is already the final HP-damage value. Naturally falls out of putting thorns in `OnHurt`. |
| Mana-absorb sets `manaRegenDelay`? | **Yes.** `Player.manaRegenDelay = Math.Max(Player.manaRegenDelay, 40)` after each absorb. Prevents passive sustain. |
| C.1 tooltip wording? | `"Up to +{0}% damage as Life decreases (max at 25%)"` |
| DamageToManaBeforeLife class-balance fix? | Three-lever approach (§4): Prefix kind, 40% cap, per-hit cap = 25% × statManaMax2. |

---

## §6 Debug Logging

The existing elemental log (gated by `EnemyConfigClient.EnableElementalDamageLog`) is preserved and extended to cover mana-absorb and thorns. Same client-side flag — debugging hurt-pipeline behavior should be one switch, not three.

Format inside `PlayerHurtPipeline`'s `ModifyHurtInfo` callback:

```
← [proj] Skeleton hit you           or    ← Skeleton hit you
  Phys:    18.0  (raw: 24.0  res: 25.0%)
  Fire:    12.5  (raw: 20.0  res: 37.5%)
  ...
  Total: 30
  Absorb: 9 (mana: 50 → 41)         (only printed if absorbed > 0)
  After absorb: 21                  (only printed if absorbed > 0)
```

Format inside `OnHurt` (thorns), same gate:

```
  Thorns: 7 → Skeleton              (only printed if reflected > 0)
```

The `[proj]` tag distinguishes projectile from contact hits — same convention the old projectile log used.

Logging is single-client (`Main.netMode != Server && target.whoAmI == Main.myPlayer`), no MP impact.

---

## §7 Cross-Cutting Concerns Verified

| Concern | Resolution |
|---|---|
| Save format | Unchanged — `AffixIds`/`Magnitudes`/`Tiers`/`Kinds` lists handle new IDs by integer |
| MP affix sync | `AffixItemManager.NetSend/NetReceive` covers the new affixes transparently |
| Thorns MP | `npc.StrikeNPC` net-syncs vanilla-style, no custom packet |
| Mana-absorb MP | `Player.statMana` is per-player; computed on hit-owner's client |
| LifeRegen / ManaRegen MP | `Player.lifeRegen` / `manaRegen` are per-player, recomputed each tick |
| `LowHpDamageBonus` divide-by-zero | Guard: `statLifeMax2 > 0 ? ... : 1f` (treats invalid state as full HP, no bonus) |
| Distance affixes on minion sub-projectiles | `Main.player[projectile.owner]` — owner is valid because affixes are only present on player-spawned projectiles |
| `ModifyHurt` for unmanaged sources (lava/fall/drown) | Early return; vanilla math unchanged |
| NPC despawn between hit and callback | Guard: `npc.active && npc.whoAmI == src.SourceNPCIndex` |
| Reforge UI / near-max ding | Reads tier max from registry — works once new defs are registered, no UI changes needed |
| Aggregate caps | Thorns capped at 80%; mana-absorb capped at **40%** (§4.2) — both clamped in `PlayerSurvivalPlayer.PostUpdateEquips` |
| Per-hit absorb cap | `min(routed, statManaMax2 × 0.25)` clamps the worst-case burst absorb (§4.4) |
| Mana-absorb low-investment benefit | Per-hit cap scales with `statManaMax2` so even 0-investment classes still absorb chip damage proportional to their pool (§4.4) |

---

## §8 Complete Change Inventory

### New files (2)

- `Common/Players/PlayerSurvivalPlayer.cs`
- `Common/Players/PlayerHurtPipeline.cs`

### Edited files (7)

- `Common/Affixes/AffixId.cs` — append 8 enum entries
- `Common/Affixes/AffixRegistry.cs` — add 8 `AffixDef` entries. C.1 uses revised table from §3. **A.4 uses `Kind = AffixKind.Prefix` and the revised table from §4.3.**
- `Localization/en-US_Mods.ARPGItemSystem.hjson` — add 8 keys under `Affixes:`. C.1 uses revised wording from §3.3. **A.4 uses revised wording from §4.5.**
- `Common/GlobalItems/Armor/ArmorManager.cs` — add `LifeRegeneration` and `ManaRegeneration` cases in `UpdateEquip`
- `Common/GlobalItems/Accessory/AccessoryManager.cs` — same two cases in `UpdateAccessory`
- `Common/GlobalItems/Weapon/WeaponManager.cs` — add 4 cases (Nearby, Distant, LowHp with §3 formula, FullHp) in `ModifyHitNPC`
- `Common/GlobalItems/ProjectileManager.cs` — (a) mirror the same 4 cases in `ModifyHitNPC`; (b) DELETE the `ModifyHitPlayer` method

### Deleted files (1)

- `Common/GlobalNPCs/ElementalHitFromNPCGlobalNPC.cs`

---

## §9 In-Game Verification Checklist

No automated tests — verification is manual. Per affix:

1. Roll/equip the affix; inspect tooltip renders correctly
2. Confirm effect triggers under expected conditions
3. Confirm effect does NOT trigger under off-conditions

Specific cases:

| Affix | Verify |
|---|---|
| LifeRegeneration | Health bar tick rate increases proportionally to magnitude |
| ManaRegeneration | Mana bar tick rate increases |
| ThornDamage | Touching a hostile NPC deals reflect damage; NPC projectile hits do **not** trigger reflect; debug log shows reflected amount |
| DamageToManaBeforeLife | (a) Hit with full mana → mana drops, HP drops less. (b) Hit with empty mana → all damage falls through. (c) `manaRegenDelay` triggers (mana doesn't insta-regenerate). (d) **Per-hit cap:** with high mana pool (e.g. 400 mana) and a single big hit (200 dmg) at 40% absorb — verify drained mana ≤ `statManaMax2 × 0.25` (i.e., ≤ 100 in this example), not the full routed 80. (e) **Aggregate cap:** stacking 4× T0 armor + 5× T0 accessory rolls (≈66 total magnitude) — verify effective absorb does not exceed 40%. (f) **Low-investment benefit:** with 20 mana max (no investment), small chip hits (e.g. 5 dmg) still partially absorbed proportional to mana pool (per-hit cap = 5). (g) **Gear-wide investment:** 4× T0 armor only (max 36 total) → effective absorb is up to 36%, **not** capped — confirm cap is approached only with broad accessory investment (per §4.3). |
| NearbyDamageBonus | Hit enemy at <16 tiles → bonus applied; >16 tiles → no bonus |
| DistantDamageBonus | Inverse of above at 48 tiles |
| LowHpDamageBonus | Hit at 100% HP → no bonus; 47% HP → ~half magnitude; 20% HP → full magnitude (use damage tooltip / log to verify) |
| FullHpDamageBonus | Bonus at exact full HP; lost any HP → no bonus |

Refactor regression check:

- Take a hit from a managed NPC contact → resistance applied (was working before refactor)
- Take a hit from a managed NPC projectile → resistance applied (was working before refactor)
- Take fall damage → vanilla math unaffected
- Take lava damage → vanilla math unaffected

Multiplayer:

- Host + join: thorns reflect visible to both clients
- Mana-absorb consistent on hit-receiver's client
- LifeRegen / ManaRegen visible on each client's own bar

---

## §10 Out of Scope

Reserved for future batches per the parent spec:

- ProjectileBounce, ProjectileHoming, ProjectilePiercing, ExtraProjectileChance — require `SendExtraAI`/`ReceiveExtraAI` infrastructure on `ProjectileManager`
- ManaFueledDamage — requires interaction design with the new mana-absorb mechanic (now §4) and per-hit cap math
- ~~Suffix → Prefix rebalance (parent §I)~~ — **Already applied in `AffixRegistry.cs` as of 2026-05-03**, marked complete in parent spec. Leveraged in §4.1 of this batch.

---

## §11 Implementation Order (suggested)

The 8 affixes can be implemented in any order, but a low-risk path:

1. Append the 8 AffixId enum entries — single file, no behavior change
2. Add the 8 AffixDef registry entries — affixes start rolling but have no effect yet
3. Add the 8 localization keys — tooltips render
4. Add weapon damage cases (Nearby, Distant, LowHp, FullHp) in WeaponManager + ProjectileManager — verify each in-game
5. Add LifeRegen / ManaRegen cases in ArmorManager + AccessoryManager — verify
6. Create PlayerSurvivalPlayer (aggregator) — no externally visible effect yet
7. Create PlayerHurtPipeline (does both branches and the new mana-absorb / thorns logic) — verify resistance regression first, then mana-absorb, then thorns
8. Delete ElementalHitFromNPCGlobalNPC.cs and ProjectileManager.ModifyHitPlayer — final cleanup

Step 7 is the riskiest; the regression check from §9 is most important after that step.
