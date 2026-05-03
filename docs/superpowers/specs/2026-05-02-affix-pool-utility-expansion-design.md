# Affix Pool Utility Expansion

**Date:** 2026-05-02
**Scope:** ARPGItemSystem only

## Overview

Catalog of new affix candidates that expand the pool beyond pure stat scaling. Where the existing 33 affixes are mostly numeric multipliers (damage, defense, resistance), these add **functional / behavioral** modifiers — things that change _how_ combat plays out, not just by how much.

This document is a **catalog**, not a tight implementation bundle. Each affix is documented with enough detail (slot, kind, magnitude semantics, tier table proposal, hook, multiplayer notes) to be implemented independently. Pick what to ship in any order.

## Design Philosophy

Affixes added here intentionally **avoid the trivialization patterns** that turn combat into a passive activity:

- ❌ **No lifesteal / leech** — incentivizes standing still and tanking; vanilla accessories already cover this niche
- ❌ **No movement speed** — turns kiting into a degenerate strategy; belongs on accessories like Hermes Boots
- ❌ **No heal-on-kill** — same family as lifesteal
- ❌ **No mana steal** — paired with mana-fueled damage would create a self-sustaining loop

If a stat creates a "stand and farm" or "outrun the boss" play pattern, it does not belong in this affix pool. Such effects are reserved for vanilla accessories where they are already balanced.

## Affix Catalog

Affixes are grouped by theme. Each entry is implementable independently of the others.

---

### A. Defensive / Sustain

#### A.1 LifeRegeneration

- **Slot:** Armor + Accessory
- **Kind:** Prefix
- **Magnitude unit:** Vanilla `Player.lifeRegen` units (2 = 1 HP / second)
- **Behavior:** `Player.lifeRegen += magnitude` in `UpdateEquip` (Armor) / `UpdateAccessory` (Accessory). Plain vanilla regen — accumulates with food buffs, Heart Lantern, Campfire, etc.
- **Tier table proposal:**

  | Tier | Armor | Accessory |
  | ---- | ----- | --------- |
  | T0   | 5–6   | 3–4       |
  | T1   | 4–5   | 3–3       |
  | T2   | 4–4   | 2–3       |
  | T3   | 3–4   | 2–3       |
  | T4   | 3–3   | 2–2       |
  | T5   | 2–3   | 1–2       |
  | T6   | 2–2   | 1–2       |
  | T7   | 1–2   | 1–1       |
  | T8   | 1–1   | 1–1       |
  | T9   | 1–1   | 1–1       |

- **Hook:** `ArmorManager.UpdateEquip`, `AccessoryManager.UpdateAccessory`
- **MP:** No new sync. `Player.lifeRegen` is per-player and recomputed each tick.

#### A.2 ManaRegeneration

- **Slot:** Armor + Accessory
- **Kind:** Prefix
- **Magnitude unit:** Vanilla `Player.manaRegen` units (1 = 1 mana / 60 ticks roughly)
- **Behavior:** `Player.manaRegen += magnitude` in `UpdateEquip` / `UpdateAccessory`. Plain vanilla regen.
- **Tier table proposal:**

  | Tier | Armor | Accessory |
  | ---- | ----- | --------- |
  | T0   | 10–12 | 6–8       |
  | T1   | 8–10  | 5–7       |
  | T2   | 7–9   | 4–6       |
  | T3   | 6–8   | 3–5       |
  | T4   | 5–7   | 3–4       |
  | T5   | 4–6   | 2–3       |
  | T6   | 3–5   | 2–3       |
  | T7   | 2–4   | 1–2       |
  | T8   | 1–2   | 1–2       |
  | T9   | 1–1   | 1–1       |

- **Hook:** `ArmorManager.UpdateEquip`, `AccessoryManager.UpdateAccessory`
- **MP:** No new sync.

#### A.3 ThornDamage

- **Slot:** Armor + Accessory
- **Kind:** Suffix
- **Magnitude unit:** Percentage of damage taken reflected
- **Behavior:** When the player is hit by an NPC contact attack, deal `magnitude%` of the incoming damage back to the attacker. Implemented in `ModPlayer.OnHurt` or `Player.HurtModifiers` callback. Filter to direct contact only — do not reflect projectile hits (would chain-react with our own elemental projectile resistance) and do not reflect a reflected hit.
- **Vanilla precedent:** Thorns potion / Thorns enchantment behavior — flat 33% reflect.
- **Tier table proposal (same for Armor and Accessory):**

  | Tier | Min | Max |
  | ---- | --- | --- |
  | T0   | 30  | 35  |
  | T1   | 25  | 30  |
  | T2   | 20  | 25  |
  | T3   | 16  | 20  |
  | T4   | 13  | 16  |
  | T5   | 10  | 13  |
  | T6   | 7   | 10  |
  | T7   | 5   | 7   |
  | T8   | 3   | 5   |
  | T9   | 1   | 3   |

- **Hook:** `PlayerElementalPlayer.OnHurt` (or new dedicated `ModPlayer`). Use `Player.HurtInfo.DamageSource.SourceNPCIndex` to identify the attacker.
- **MP:** Damage application to NPC must go through `NPC.StrikeNPC` so vanilla net-syncs the result. No custom packet needed.

#### A.4 DamageToManaBeforeLife

- **Slot:** Armor + Accessory
- **Kind:** Suffix
- **Magnitude unit:** Percentage of incoming damage absorbed by mana first
- **Behavior:** PoE "Mind over Matter" pattern. When the player is hit, route `magnitude%` of damage to mana first. Each mana point absorbs 1 damage. If mana cannot absorb the full routed amount, the remainder falls through to HP.
  - Example: 100 incoming damage at 30% magnitude with 50 mana → 30 routed to mana (mana → 20, all absorbed), 70 hits HP.
  - Example: 100 incoming damage at 50% magnitude with 20 mana → 50 routed to mana (mana → 0, 30 absorbed, 20 falls through), 70 hits HP.
- **Tier table proposal:**

  | Tier | Armor | Accessory |
  | ---- | ----- | --------- |
  | T0   | 35–40 | 20–25     |
  | T1   | 30–35 | 18–22     |
  | T2   | 25–30 | 15–18     |
  | T3   | 22–25 | 12–15     |
  | T4   | 18–22 | 10–12     |
  | T5   | 15–18 | 8–10      |
  | T6   | 12–15 | 6–8       |
  | T7   | 8–12  | 4–6       |
  | T8   | 5–8   | 2–4       |
  | T9   | 1–5   | 1–2       |

  Aggregate cap: 80% total across all equipped items, to prevent immortal mana-pool builds.

- **Hook:** `PlayerElementalPlayer.ModifyHurt` — apply via `ModifyHurtInfo` callback **after** elemental resistance. Applying before resistance would absorb damage that resistance would have negated anyway, wasting mana.
- **MP:** No new sync. Hurt is processed per-client.

---

### B. Offensive Utility — Weapon (Projectile Behaviors)

These four affixes change projectile behavior and share the **Unified Percentage-Trigger System** described in section D. Note that **ProjectileHoming is the only affix in this group where triggers are capped at 1** — see B.2 and section D for details.

#### B.1 ProjectileBounce

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent (see section D — triggers uncapped)
- **Behavior:** On spawn, roll a bounce-trigger count. Store `BouncesRemaining` on `ProjectileManager`. In `OnTileCollide`, if remaining > 0: reflect velocity on the collided axis (ExampleBullet pattern), play bounce sound, decrement, return false. When 0, return true (default kill behavior).
- **Restrictions:** Any weapon that spawns projectiles. No DamageClass restriction — visuals preserved (projectile keeps its original sprite).
- **Tier table proposal:**

  | Tier | Min % | Max % |
  | ---- | ----- | ----- |
  | T0   | 80    | 100   |
  | T1   | 65    | 80    |
  | T2   | 50    | 65    |
  | T3   | 40    | 50    |
  | T4   | 30    | 40    |
  | T5   | 22    | 30    |
  | T6   | 15    | 22    |
  | T7   | 10    | 15    |
  | T8   | 5     | 10    |
  | T9   | 2     | 5     |

- **Hook:** `ProjectileManager.OnSpawn` (roll triggers), `ProjectileManager.OnTileCollide` (reflect/decrement)
- **MP:** `BouncesRemaining` must be synced via `SendExtraAI`/`ReceiveExtraAI` so non-owner clients see consistent reflection. `ProjectileManager` currently has no extra-AI sync — this infrastructure must be added when any projectile affix is first implemented.
- **Reference:** `ExampleMod/Content/Projectiles/ExampleBullet.cs` — `OnTileCollide` pattern

#### B.2 ProjectileHoming

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent (see section D — **triggers capped at 1**)
- **Behavior:** On spawn, roll triggers per section D formula, then clamp to 1 (`triggers = Math.Min(triggers, 1)`). If triggers == 1, set `HomingArmed = true` on `ProjectileManager`. In `PostAI`, run the **ExampleHomingProjectile algorithm verbatim**:
  - 10-frame initial delay
  - 400px detect radius, find closest NPC via `CanBeChasedBy()` + `Collision.CanHit`
  - Smooth-steer: `velocity.ToRotation().AngleTowards(targetAngle, MathHelper.ToRadians(3)).ToRotationVector2() * speed`
  - Homing remains armed for the projectile's full lifetime (no chain — one target, one flight)
- **Why capped at 1:** Higher trigger counts would create chain-projectile behavior (redirect between enemies on hit) — complex to implement correctly and potentially trivializing. The percentage system still meaningfully controls whether the projectile homes at all.
- **Tier table proposal (max% never exceeds 100 — triggers cap anyway):**

  | Tier | Min % | Max % |
  | ---- | ----- | ----- |
  | T0   | 80    | 100   |
  | T1   | 65    | 80    |
  | T2   | 50    | 65    |
  | T3   | 40    | 50    |
  | T4   | 30    | 40    |
  | T5   | 22    | 30    |
  | T6   | 15    | 22    |
  | T7   | 10    | 15    |
  | T8   | 5     | 10    |
  | T9   | 2     | 5     |

- **Hook:** `ProjectileManager.OnSpawn` (set HomingArmed), `ProjectileManager.PostAI` (steer). No `OnHitNPC` hook needed — no chain mechanics.
- **MP:** `HomingArmed`, `HomingTargetIndex`, `HomingDelayTimer` must be synced via `SendExtraAI`/`ReceiveExtraAI`. Steering math is deterministic — each client can render homing locally once state is synced.
- **Reference:** `ExampleMod/Content/Projectiles/ExampleHomingProjectile.cs` — copy `AI()` body, `FindClosestNPC`, `IsValidTarget` verbatim

#### B.3 ProjectilePiercing

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent (see section D — triggers uncapped)
- **Behavior:** On spawn, roll pierce-trigger count. If > 0 AND `projectile.penetrate != -1` (skip if already infinite-piercing): `projectile.penetrate += triggers; projectile.maxPenetrate = projectile.penetrate;`. Vanilla decrements penetrate on each hit — no further state tracking needed.
- **Restrictions:** Any weapon that spawns projectiles.
- **Tier table proposal (conservative — pierce trivializes crowd content):**

  | Tier | Min % | Max % |
  | ---- | ----- | ----- |
  | T0   | 75    | 100   |
  | T1   | 60    | 75    |
  | T2   | 45    | 60    |
  | T3   | 35    | 45    |
  | T4   | 25    | 35    |
  | T5   | 18    | 25    |
  | T6   | 12    | 18    |
  | T7   | 8     | 12    |
  | T8   | 4     | 8     |
  | T9   | 2     | 4     |

- **Hook:** `ProjectileManager.OnSpawn` only.
- **MP:** `projectile.penetrate` is a vanilla field, automatically net-synced. No extra sync needed.
- **Reference:** `ExampleMod/Content/Projectiles/ExamplePiercingProjectile.cs` — explanatory comments on penetrate/immunity modes; the mechanic itself is one line.

#### B.4 ExtraProjectileChance

- **Slot:** Weapon
- **Kind:** Prefix
- **Magnitude unit:** Percent (see section D — triggers uncapped)
- **Behavior:** In the weapon's `Shoot` hook, roll an extra-shot count. For each extra trigger, spawn one additional `Projectile.NewProjectile` with the same type/damage/knockback/owner and a small random angular spread (~10°). Each spawned extra runs its own `OnSpawn`, rolling its own Bounce/Homing/Piercing independently.
- **Restrictions:** **DamageClass = Melee, Ranged, or Magic.** Excluded only from Summon (each summon use's spawn is gated by the minion cap inside its own logic — bypassing it via duplicate spawn would produce uncapped minions). Includes Melee because some melee weapons spawn projectiles (sword energy waves, certain swung weapons). For melee weapons that don't spawn anything via `Shoot`, the affix is simply a dead roll on that item — accepted; player can reroll.
- **Tier table proposal (tight — extras compound damage output):**

  | Tier | Min % | Max % |
  | ---- | ----- | ----- |
  | T0   | 45    | 60    |
  | T1   | 35    | 45    |
  | T2   | 28    | 35    |
  | T3   | 22    | 28    |
  | T4   | 16    | 22    |
  | T5   | 12    | 16    |
  | T6   | 8     | 12    |
  | T7   | 5     | 8     |
  | T8   | 3     | 5     |
  | T9   | 1     | 3     |

- **Hook:** `WeaponManager.Shoot`
- **MP:** Each `NewProjectile` call broadcasts its own spawn. No extra sync.

---

### C. Conditional / Resource-Based Damage — Weapon

#### C.1 LowHpDamageBonus (Berserker)

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent damage bonus
- **Behavior:** In `ModifyHitNPC`, check `player.statLife / (float)player.statLifeMax2 <= 0.35f`. If true, apply `+magnitude%` damage.
- **Restrictions:** Any weapon. Apply in both `WeaponManager.ModifyHitNPC` and `ProjectileManager.ModifyHitNPC`.
- **Tier table proposal:**

  | Tier | Min | Max |
  | ---- | --- | --- |
  | T0   | 60  | 70  |
  | T1   | 50  | 60  |
  | T2   | 42  | 50  |
  | T3   | 35  | 42  |
  | T4   | 28  | 35  |
  | T5   | 22  | 28  |
  | T6   | 16  | 22  |
  | T7   | 11  | 16  |
  | T8   | 6   | 11  |
  | T9   | 1   | 6   |

- **Hook:** `WeaponManager.ModifyHitNPC`, `ProjectileManager.ModifyHitNPC`
- **MP:** No extra sync.

#### C.2 FullHpDamageBonus (Iron Will)

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent damage bonus
- **Behavior:** In `ModifyHitNPC`, check `player.statLife >= player.statLifeMax2`. If true, apply `+magnitude%` damage.
- **Restrictions:** Any weapon.
- **Tier table proposal:** Same magnitudes as LowHpDamageBonus. Iron Will is easier to maintain on opening shots / kiting but harder in sustained fights — parity is intentional.
- **Hook:** `WeaponManager.ModifyHitNPC`, `ProjectileManager.ModifyHitNPC`
- **MP:** No extra sync.

#### C.3 ManaFueledDamage

- **Slot:** Weapon
- **Kind:** Prefix
- **Magnitude unit:** Percent damage bonus
- **Behavior:** On each hit, check if `player.statMana >= manaCost` where `manaCost = Math.Max(1, magnitude / 5)`. If yes: drain mana (`player.statMana -= manaCost; player.manaRegenDelay = Math.Max(Player.manaRegenDelay, 60)`), apply `+magnitude%` damage bonus. If no mana: no bonus, no penalty.
  - **Design intent:** Melee and Ranged players have a mana pool that vanilla never draws on. This affix gives them a reason to invest in `FlatManaIncrease` affixes on armor/accessories — more mana pool = more hits before the bonus runs out. The finite mana creates a natural burst window rather than passive sustain. Fast weapons drain faster, creating build tension around attack speed vs. mana pool size.
  - **Interaction with ManaCostReduction:** `ManaCostReduction` applies to weapon _use_ cost (via `ModifyManaCost`) — that is a separate mechanism and does not interact with the per-hit drain from this affix.
  - **Interaction with DamageToManaBeforeLife:** Stacking both means incoming hits drain the same mana pool that ManaFueledDamage uses. Intentional — high-risk, high-reward mana management.
- **Restrictions:** Any damage class. Melee/Ranged players benefit most (their mana is otherwise unused). Magic players already have incentive for mana — this gives them a secondary use.
- **Tier table proposal:**

  | Tier | Damage bonus | Mana cost per hit (approx) |
  | ---- | ------------ | -------------------------- |
  | T0   | 80–100%      | 16–20                      |
  | T1   | 65–80%       | 13–16                      |
  | T2   | 52–65%       | 11–13                      |
  | T3   | 42–52%       | 9–11                       |
  | T4   | 33–42%       | 7–9                        |
  | T5   | 25–33%       | 5–7                        |
  | T6   | 18–25%       | 4–5                        |
  | T7   | 12–18%       | 3–4                        |
  | T8   | 7–12%        | 2–3                        |
  | T9   | 3–7%         | 1–2                        |

- **Hook:** `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`
- **MP:** Mana drain (`player.statMana`) is per-player and computed on the owner client. No extra sync — vanilla syncs player stats.

---

### D. Offensive Utility — Weapon (Distance-Based)

#### D.1 NearbyDamageBonus

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent damage bonus
- **Behavior:** In `ModifyHitNPC`, compute `Vector2.Distance(player.Center, target.Center)`. If distance ≤ 256px (16 tiles), apply `+magnitude%` damage. Binary threshold — full bonus inside, none outside.
- **Restrictions:** Any weapon. Apply in `WeaponManager.ModifyHitNPC` (melee/direct) and `ProjectileManager.ModifyHitNPC` (projectile hits). For projectiles, player is `Main.player[projectile.owner]`.
- **Coexistence:** Can roll alongside `DistantDamageBonus` on the same weapon — creates a positional tradeoff with dead-zones at mid-range.
- **Tier table proposal:**

  | Tier | Min | Max |
  | ---- | --- | --- |
  | T0   | 50  | 60  |
  | T1   | 42  | 50  |
  | T2   | 35  | 42  |
  | T3   | 28  | 35  |
  | T4   | 22  | 28  |
  | T5   | 17  | 22  |
  | T6   | 12  | 17  |
  | T7   | 8   | 12  |
  | T8   | 4   | 8   |
  | T9   | 1   | 4   |

- **Hook:** `WeaponManager.ModifyHitNPC`, `ProjectileManager.ModifyHitNPC`
- **MP:** Damage computed on hit-owner client; vanilla syncs the resulting damage value.

#### D.2 DistantDamageBonus

- **Slot:** Weapon
- **Kind:** Suffix
- **Magnitude unit:** Percent damage bonus
- **Behavior:** Mirror of NearbyDamageBonus. If distance ≥ 768px (48 tiles), apply `+magnitude%` damage. Same binary threshold.
- **Restrictions:** Same as NearbyDamageBonus.
- **Coexistence:** Can roll alongside NearbyDamageBonus — see above.
- **Tier table proposal:** Same magnitudes as NearbyDamageBonus.
- **Hook:** Same as NearbyDamageBonus.

---

## E. Unified Percentage-Trigger System

Shared formula for **ProjectileBounce**, **ProjectileHoming**, **ProjectilePiercing**, **ExtraProjectileChance**.

### E.1 Magnitude Semantics

Affix magnitude is in **percent units** representing the _expected number of activations per projectile / per shot_.

- < 100%: probability of one activation
- = 100%: guaranteed one activation
- > 100%: each full 100% guarantees one activation, the remainder is rolled once for an additional activation

Formula:

```csharp
int triggers = magnitude / 100;
if (Main.rand.NextFloat() < (magnitude % 100) / 100f)
    triggers++;
```

**Exception — ProjectileHoming:** After this formula, clamp: `triggers = Math.Min(triggers, 1)`. Homing is binary (armed or not) — no chain behavior. The percentage only controls _whether_ the projectile homes, not how many enemies it chains through.

### E.2 Examples

| Magnitude | Bounce                  | Homing              | ExtraProj              | Piercing                |
| --------- | ----------------------- | ------------------- | ---------------------- | ----------------------- |
| 50%       | 50% chance × 1 bounce   | 50% chance to home  | 50% chance × 1 extra   | 50% chance × 1 pierce   |
| 100%      | 1 bounce guaranteed     | Homes guaranteed    | 1 extra guaranteed     | 1 pierce guaranteed     |
| 150%      | 1 bounce + 50% for 2nd  | Homes (capped at 1) | 1 extra + 50% for 2nd  | 1 pierce + 50% for 2nd  |
| 350%      | 3 bounces + 50% for 4th | Homes (capped at 1) | 3 extras + 50% for 4th | 3 pierces + 50% for 4th |

### E.3 When Triggers Are Rolled

- **Bounce, Homing, Piercing:** rolled in `ProjectileManager.OnSpawn` per projectile (each projectile rolls independently)
- **ExtraProjectile:** rolled in `WeaponManager.Shoot` per shot; each spawned extra runs its own `OnSpawn` and rolls Bounce/Homing/Piercing fresh

### E.4 Independence Between Affixes

A single projectile can simultaneously be bouncing, homing, and piercing. Homing and Piercing both modify `projectile.penetrate` — Piercing bumps it at spawn; Homing does **not** (no chain = no extra hits = no penetrate bump needed). Bounce does not interact with penetrate.

---

## F. Implementation Notes

### F.1 AffixId Ordering

**CRITICAL:** New entries to the `AffixId` enum must be **appended at the end**, never inserted in the middle. Integer values are persisted in item saves; reordering corrupts every saved item that had affixes whose IDs shifted.

Suggested append order (group by theme for readability — order within the append block doesn't matter, only that they go after the existing 33 entries):

```
LifeRegeneration, ManaRegeneration, ThornDamage, DamageToManaBeforeLife,
ProjectileBounce, ProjectileHoming, ProjectilePiercing, ExtraProjectileChance,
NearbyDamageBonus, DistantDamageBonus,
LowHpDamageBonus, FullHpDamageBonus, ManaFueledDamage
```

When implementing piecemeal, append in the order chosen — never go back and reorder.

### F.2 Multiplayer Sync for Projectile Affixes

`ProjectileManager` currently has no `SendExtraAI` / `ReceiveExtraAI`. This infrastructure must be added before any projectile behavior affix can work correctly on non-owner clients.

Required synced fields:

| Field               | Used by | Type                          |
| ------------------- | ------- | ----------------------------- |
| `BouncesRemaining`  | Bounce  | byte                          |
| `HomingArmed`       | Homing  | bool                          |
| `HomingTargetIndex` | Homing  | short (NPC.whoAmI, −1 = none) |
| `HomingDelayTimer`  | Homing  | byte (0–10)                   |

Total: ~5 bytes per projectile. Only write non-default values to avoid bloating every projectile's sync packet.

`projectile.penetrate` (Piercing) and `projectile.velocity` (Bounce reflection) are vanilla fields — net-synced automatically. Do not re-sync them.

### F.3 Localization Keys

Each new affix needs an entry under `Affixes:` in `Localization/en-US_Mods.ARPGItemSystem.hjson`:

```
LifeRegeneration: "+{0} Life Regen"
ManaRegeneration: "+{0} Mana Regen"
ThornDamage: "Reflects {0}% of melee damage taken"
DamageToManaBeforeLife: "{0}% of damage taken absorbed by mana first"
ProjectileBounce: "{0}% projectile bounce chance"
ProjectileHoming: "{0}% projectile homing chance"
ProjectilePiercing: "{0}% projectile piercing chance"
ExtraProjectileChance: "{0}% chance to fire an extra projectile"
NearbyDamageBonus: "+{0}% damage to nearby enemies"
DistantDamageBonus: "+{0}% damage to distant enemies"
LowHpDamageBonus: "+{0}% damage when below 35% Life"
FullHpDamageBonus: "+{0}% damage at full Life"
ManaFueledDamage: "+{0}% damage, costs {0}/5 mana per hit"
```

Note: `ManaFueledDamage` references `{0}` twice — once for the damage bonus, once to show the derived cost. The tooltip system may need a custom format or computed string rather than raw `{0}` substitution — verify at implementation time.

Keys must exactly match `AffixId` enum names.

### F.4 Stat-Apply Hook Routing

| Affix                  | Hook(s)                                                                                |
| ---------------------- | -------------------------------------------------------------------------------------- |
| LifeRegeneration       | `ArmorManager.UpdateEquip` + `AccessoryManager.UpdateAccessory`                        |
| ManaRegeneration       | `ArmorManager.UpdateEquip` + `AccessoryManager.UpdateAccessory`                        |
| ThornDamage            | `PlayerElementalPlayer.OnHurt` (or new `ModPlayer`) — aggregate via `PostUpdateEquips` |
| DamageToManaBeforeLife | `PlayerElementalPlayer.ModifyHurt` — `ModifyHurtInfo` callback, after resistance       |
| ProjectileBounce       | `ProjectileManager.OnSpawn` + `ProjectileManager.OnTileCollide`                        |
| ProjectileHoming       | `ProjectileManager.OnSpawn` + `ProjectileManager.PostAI`                               |
| ProjectilePiercing     | `ProjectileManager.OnSpawn` only                                                       |
| ExtraProjectileChance  | `WeaponManager.Shoot`                                                                  |
| NearbyDamageBonus      | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`                        |
| DistantDamageBonus     | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`                        |
| LowHpDamageBonus       | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`                        |
| FullHpDamageBonus      | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`                        |
| ManaFueledDamage       | `WeaponManager.ModifyHitNPC` + `ProjectileManager.ModifyHitNPC`                        |

For all weapon damage-modifier affixes, add new `case AffixId.X:` blocks alongside the existing `CritMultiplier` case in `WeaponManager`/`ProjectileManager.ModifyHitNPC`.

### F.5 Aggregate Caps

| Affix                  | Suggested cap                                                          |
| ---------------------- | ---------------------------------------------------------------------- |
| ThornDamage            | 80% (prevents deadlock if two enemies both have thorns-like abilities) |
| DamageToManaBeforeLife | 80% (preserve a HP-damage path)                                        |
| All others             | None                                                                   |

Caps applied in `PlayerElementalPlayer.PostUpdateEquips` alongside existing resistance aggregation.

### F.6 No Automated Tests

Verification is manual in-game. Smoke-test checklist per affix:

1. Roll the affix (or hand-set via debug) and inspect tooltip
2. Confirm effect triggers under expected conditions
3. Confirm effect does NOT trigger under off-conditions
4. In multiplayer: host + join and verify consistent visuals for projectile affixes

---

## G. Deferred / Out of Scope

| Idea                                                | Reason                                                                                           |
| --------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Lifesteal, Heal-on-kill, Mana steal                 | Trivialization — sustain encourages standing still                                               |
| Movement speed                                      | Trivialization — kiting bosses                                                                   |
| Energy shield (custom HP-before-life resource pool) | Significant scope — needs its own design session (custom resource UI, regen system, net packets) |
| Dodge chance                                        | Invincibility-frame handling is complex; vanilla Black Belt already in this space                |
| Block chance / damage reduction                     | Redundant with PercentageDefenseIncrease                                                         |
| Damage vs specific enemy types                      | No clean enemy category API in tModLoader — would need custom tagging                            |
| Bonus damage by biome / day-night                   | Thematic clarity; deferred                                                                       |
| Recovery on kill                                    | Same family as lifesteal                                                                         |
| Reflect-as-projectile (visible bounce shot)         | Visual polish — ThornDamage covers the mechanic                                                  |
| Pickup magnet range                                 | Vanilla accessory niche                                                                          |
| Jump speed / extra jumps                            | Vanilla accessory niche                                                                          |

If any are revisited, they get their own design doc.

---

## H. Pickable Implementation Order Suggestion

1. **Cheap stats — no new infrastructure:**
   LifeRegeneration, ManaRegeneration, NearbyDamageBonus, DistantDamageBonus, LowHpDamageBonus, FullHpDamageBonus, ManaFueledDamage
   — All are `case` additions in existing managers plus enum + localization entries.

2. **Hurt-pipeline affixes:**
   ThornDamage, DamageToManaBeforeLife
   — Requires `OnHurt` / `ModifyHurt` hooks and aggregation in `PlayerElementalPlayer`.

3. **Projectile affixes — add sync infrastructure first, then each affix independently:**
   First: add `SendExtraAI`/`ReceiveExtraAI` to `ProjectileManager` + unified-trigger helper method.
   Then in any order: ProjectilePiercing → ProjectileBounce → ExtraProjectileChance → ProjectileHoming.
   Piercing first because it needs no new `ProjectileManager` fields (pure `OnSpawn`, no runtime state to sync).

---

## I. Existing Pool Rebalance — Suffix → Prefix

**STATUS: APPLIED (2026-05-03).** All 10 promotions in §I.1 are landed in `AffixRegistry.cs`. Verified: every entry in the table below now has `Kind = AffixKind.Prefix`. The "after 10 promotions" column in §I.3 is the current live state.

The current pool has 11 prefixes vs 22 suffixes (1 : 2). With the 13 new affixes added (4 prefix + 9 suffix), the imbalance worsens to 15 : 31.

The new affixes mostly _should_ be suffixes — they describe situational/conditional/reactive properties ("of Berserker", "of Burning Reflection", "of Bouncing Shots"). The fix is to rebalance the **existing** pool by promoting intrinsic-property suffixes to prefixes, preserving the new affixes as designed.

### I.1 Suffixes Promoted to Prefixes

10 existing affixes change from `AffixKind.Suffix` to `AffixKind.Prefix`:

| AffixId                           | Reasoning                                                                                                                                       |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `VelocityIncrease`                | "Swift Bow" — describes how the weapon shoots, intrinsic                                                                                        |
| `ManaCostReduction`               | "Efficient Wand" — efficiency is a quality of the item                                                                                          |
| `PercentageGenericDamageIncrease` | "Devastating Helmet" — armor enchanted for raw power                                                                                            |
| `PercentageMeleeDamageIncrease`   | "Brutal Armor" — class-aligned intrinsic                                                                                                        |
| `PercentageRangedDamageIncrease`  | "Steady Armor" — class-aligned intrinsic                                                                                                        |
| `PercentageMagicDamageIncrease`   | "Mystic Armor" — class-aligned intrinsic                                                                                                        |
| `PercentageSummonDamageIncrease`  | "Commanding Armor" — class-aligned intrinsic                                                                                                    |
| `IncreasedFireDamage`             | "Burning Sword" — describes the weapon's elemental nature                                                                                       |
| `IncreasedColdDamage`             | "Freezing Sword" — same                                                                                                                         |
| `IncreasedLightningDamage`        | "Shocking Sword" — same; pairs naturally with the existing `GainPercentAsX` prefixes (the prefix slot becomes "elemental nature of the weapon") |

### I.2 Suffixes That Stay as Suffix

| AffixId                                                                                 | Why kept                                                                                      |
| --------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `FlatArmorPen`, `PercentageArmorPen`                                                    | Recently moved to Suffix in 2026-05-01 — preserve consistency with rest of penetration family |
| `FirePenetration`, `ColdPenetration`, `LightningPenetration`, `AllElementalPenetration` | Penetration cluster is suffix                                                                 |
| `FireResistance`, `ColdResistance`, `LightningResistance`                               | Canonical "of Fire Resistance" pattern                                                        |
| `PercentageCritChance`, `CritMultiplier`, `FlatCritChance`                              | Could go either way — leave alone to minimize churn                                           |

### I.3 Resulting Balance

|                                           | Prefixes | Suffixes |
| ----------------------------------------- | -------- | -------- |
| Existing (current)                        | 11       | 22       |
| After 10 promotions                       | 21       | 12       |
| Plus 13 new affixes (4 prefix + 9 suffix) | **25**   | **21**   |

Net result: **25 : 21** — slightly prefix-leaning but essentially balanced (compared to 1 : 2 today).

### I.4 Migration & Save Compatibility

Same pattern as the 2026-05-01 ArmorPen Suffix move:

- **Existing saved items unaffected:** `Kinds` is stored per-affix in the save tag at roll time. Items rolled before this change keep `Kinds[i] = Suffix` for these 10 affixes.
- **Newly rolled items:** Land in the Prefix slot. Display color changes from blue (suffix) to green (prefix).
- **Transitional cosmetic:** During the migration period, the same affix name may appear with different colors on different items in the same player's inventory (legacy = blue, new rolls = green). Not a bug; will fade as items are rerolled or replaced.
- **Reroll behavior:** Reforging an old item naturally migrates it — the next roll lands in the new (Prefix) slot.

### I.5 Implementation

Single-line change per affix in `AffixRegistry.cs` — flip `Kind = AffixKind.Suffix` to `Kind = AffixKind.Prefix` on the 10 entries listed in I.1. No tier table changes, no behavior changes, no localization changes.

This rebalance can be applied independently of any new-affix work — they're orthogonal changes. Order doesn't matter; do whichever first.
