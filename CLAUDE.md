# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Non-Negotiable Requirements

- **Multiplayer-compatible:** All features must work correctly in multiplayer. Use `NetSend`/`NetReceive` for syncing per-entity state, `ModPacket` for mod-initiated network messages, and `Main.netMode` guards where needed. Never assume single-player.
- **Clear localization:** All player-visible strings (tooltips, UI text, chat messages) must use `.hjson` localization keys — never hard-coded string literals.

## Build & Development

This is a tModLoader mod for Terraria targeting .NET 6. To build:
- **In-game (recommended):** tModLoader → Workshop → Mod Sources → select `ARPGItemSystem` → Build & Reload
- **CLI compile check:** `dotnet build` (verifies compilation but does not deploy — requires ARPGEnemySystem built first so its dll exists at `../ARPGEnemySystem/bin/Debug/net8.0/ARPGEnemySystem.dll`)

There are no automated tests. Testing requires running tModLoader with the mod loaded.

## Cross-Mod Dependency

`build.txt` declares `modReferences = ARPGEnemySystem` (runtime load order). `ARPGItemSystem.csproj` has a `<Reference>` pointing to ARPGEnemySystem's compiled dll (compile-time type resolution). Both are required. When referencing ARPGEnemySystem's `Config` class, use a type alias to avoid ambiguity with any future `Config` in this mod's namespaces:
```csharp
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
```

**ARPGCharacterSystem is also a hard runtime requirement.** `ARPGItemSystem.cs` does a `HasMod("ARPGCharacterSystem")` check in `PostSetupContent` and throws if absent — symmetric with the check ARPGCharacterSystem already does. Mutual `modReferences` (load-order) and mutual `<Reference>` (compile-time) are both impossible (cycles), so this runtime check is the strongest enforcement available.

## In-game Reroll

Press **C** (default `CraftKeyBind`) while holding a weapon, armor piece, or accessory to reroll its modifiers. Cost is 2× the item's buy value. Implemented in `Common/Players/Keybind.cs`; keybind registered in `Common/Systems/KeyBindSystem.cs`.

## Architecture

### High-Level Concept

Replaces Terraria's vanilla prefix/reforge system with an ARPG-style modifier system. Every weapon, armor piece, and accessory rolls random **prefixes** and **suffixes** on creation. The vanilla prefix system is suppressed on all three manager classes by returning `pre == -3` from `PrefixChance`.

### Item Routing

Three `GlobalItem` subclasses, each filtering via `AppliesToEntity(lateInstantiation: true)`:

| Manager | Filter condition |
|---|---|
| `WeaponManager` | `damage > 0 && maxStack == 1` |
| `ArmorManager` | `damage < 1 && maxStack == 1 && !accessory && !vanity` |
| `AccessoryManager` | `accessory == true` |

### Core Data Flow

```
OnCreated(item) → AffixItemManager.Reroll(item)
  → utils.GetTier()              // boss-progression-based, tier 0 (best) to 9 (worst)
  → utils.GetAmountOf*()         // count depends on specific boss milestones
  → AffixRoller.Roll()
      → AffixRegistry.RollPool() // filters by category, kind, DamageClass
      → def.Tiers[category][tier] // random magnitude within min/max range
  → Affixes stored on GlobalItem instance (InstancePerEntity = true)
       ↓
Applied each frame via:
  ModifyWeaponDamage / ModifyWeaponCrit / ModifyHitNPC / UseSpeedMultiplier / ModifyManaCost
  ModifyShootStats                                                    (WeaponManager)
  UpdateEquip                                                         (ArmorManager)
  UpdateAccessory                                                     (AccessoryManager)
  ModifyHitNPC → conditional damage cases only (Nearby/Distant/LowHp/FullHp); elemental application now in CharacterSystem.OutgoingHitPlayer
  (elemental + survival aggregation now in ARPGCharacterSystem)
  (incoming-damage pipeline now in ARPGCharacterSystem)
```

### Key Files

- **`Common/Affixes/AffixRegistry.cs`** — Single source of truth. One `AffixDef` entry per affix containing: `AffixId`, `AffixKind` (Prefix/Suffix), tooltip format string, per-`ItemCategory` tier tables (10 entries each), and optional `HashSet<DamageClass>` restriction for weapons. **This is the only place to define, change, or remove affixes.**
- **`Common/Affixes/AffixItemManager.cs`** — Abstract `GlobalItem` base class shared by all three managers. Owns `List<Affix> Affixes`, handles `SaveData`/`LoadData`, `NetSend`/`NetReceive`, `Clone`, `ModifyTooltips`, and the `Reroll` entry point. Save format uses tag keys `"AffixIds"`, `"Magnitudes"`, `"Tiers"`, `"Kinds"` — absence of `"AffixIds"` means pre-refactor save and triggers a fresh reroll. The list is order-significant: all prefixes precede all suffixes. `ReforgePacketHandler.AddAffix` enforces this by inserting new prefixes before the first existing suffix (tooltip line order reflects list order).
- **`Common/Affixes/AffixRoller.cs`** — Picks a random eligible `AffixDef` from the pool (filtered by category, kind, DamageClass, and existing-affix deduplication) and rolls a magnitude. Uses `Main.rand`.
- **`Common/GlobalItems/utils.cs`** — `GetTier()` and six `GetAmountOf*()` methods. Boss-progression logic lives here.
- **`Common/GlobalItems/ProjectileManager.cs`** — **Player→enemy only.** Snapshots weapon affixes in `OnSpawn`, applies conditional damage bonuses (Nearby/Distant/LowHp/FullHp) in `ModifyHitNPC`. `OnSpawn` handles three sources: `EntitySource_ItemUse` (player weapons), `EntitySource_Parent` where parent is a player projectile (sentry/minion sub-shots inherit parent affixes), and everything else is left empty. Elemental math is handled by `ARPGCharacterSystem.Common.Players.OutgoingHitPlayer.ModifyHitNPCWithProj`, which reads the snapshot affixes from this manager.
- Player-stat aggregators, the incoming-damage pipeline, and the outgoing-hit elemental calculator now live in `ARPGCharacterSystem` — see that mod's CLAUDE.md.

### Weapon Tooltip — Elemental Damage Breakdown

`WeaponManager.ModifyTooltips` overrides the base to insert one colored line per active `GainPercentAsFire/Cold/Lightning` affix directly under the vanilla `"Damage"` line. The displayed gained number is derived by **regex-parsing the integer already shown on the Damage line** (`Mod=="Terraria", Name=="Damage"`) so the math is verifiable to the player — `displayed × gain% × (1 + increased%)`, rounded. The increased% factor mirrors `ElementalDamageCalculator`'s pre-resistance formula. Colors match the chat-debug palette: Fire `(255,120,50)`, Cold `(100,200,255)`, Lightning `(255,240,80)`. Localization keys live under `WeaponTooltip.GainedFire/Cold/Lightning` in `en-US_Mods.ARPGItemSystem.hjson`.

Lines are skipped when `gain <= 0` or rounded `gained == 0`, so weapons without elemental affixes get no extra clutter. Tooltip rendering is per-client; multiplayer sync is already covered by `AffixItemManager.NetSend/NetReceive`. The base `ModifyTooltips` (which appends affix lines at the end) still runs first, so nothing about the affix-list rendering changes — only weapons get the inline elemental breakdown.

### Persistence Pattern

`AffixItemManager` writes four parallel `List<int/byte>` entries to `TagCompound`: `AffixIds`, `Magnitudes`, `Tiers`, `Kinds`. Read order matches write order. Tooltips are **not** stored — they are looked up at draw time from the registry. Migration: if `"AffixIds"` key is absent (old save), `LoadData` calls `Reroll()` immediately.

### Adding a New Affix

**Step 1 — `Common/Affixes/AffixId.cs`:** Add the new value to the `AffixId` enum. **CRITICAL: only append new entries — never insert in the middle or reorder.** The integer value of each enum member is persisted to disk in item saves. Inserting or moving entries shifts all subsequent IDs, corrupting every saved item that had those affixes.

**Step 2 — `Common/Affixes/AffixRegistry.cs`:** Add one `new AffixDef { ... }` entry to the `defs` list in `BuildRegistry()`:

```csharp
new AffixDef {
    Id = AffixId.YourNewAffix,
    Kind = AffixKind.Prefix,           // or Suffix
    Tiers = new Dictionary<ItemCategory, List<Tier>>
    {
        [ItemCategory.Weapon] = new List<Tier> {   // add only the categories that can roll it
            new(51,55), new(46,50), new(41,45), new(36,40), new(31,35),
            new(26,30), new(21,25), new(16,20), new(11,15), new(5,10)  // exactly 10 entries
        }
    },
    AllowedDamageClasses = null   // null = all damage classes; set a HashSet<DamageClass> to restrict
},
```

The `BuildRegistry()` validation loop will throw a clear error on load if you supply a tier list that isn't exactly 10 entries.

**Step 3 — `Localization/en-US_Mods.ARPGItemSystem.hjson`:** Add the tooltip text under `Affixes`:

```
Affixes: {
    YourNewAffix: "{0}% Your Tooltip Text"
}
```

The key must exactly match the `AffixId` enum name. `{0}` is replaced with the rolled magnitude at draw time.

**Step 4 — stat-apply hook:** Add a `case AffixId.YourNewAffix:` in the appropriate manager:
- Weapons → `WeaponManager.cs` (pick the right hook: `ModifyWeaponDamage`, `ModifyWeaponCrit`, `ModifyHitNPC`, etc.)
- Armor → `ArmorManager.UpdateEquip`
- Accessories → `AccessoryManager.UpdateAccessory`

**Step 5 (projectiles only):** Add a case in `ProjectileManager.ModifyHitNPC`.

**Exception — elemental affixes:** `GainPercentAsX`, `IncreasedXDamage`, `FlatArmorPen`, and `PercentageArmorPen` are handled entirely inside `ElementalDamageCalculator.ApplyToHit` via `GetMagnitude`. Do NOT add switch cases for these in `WeaponManager.ModifyHitNPC` or `ProjectileManager.ModifyHitNPC`. Resistance affixes (`PhysicalResistance`, `FireResistance`, `ColdResistance`, `LightningResistance`) are read in `PlayerElementalPlayer.PostUpdateEquips` — no switch cases needed in `ArmorManager` or `AccessoryManager`.

**Exception — hurt-pipeline affixes** (`ThornDamage`, `DamageToManaBeforeLife`): Aggregated in `PlayerSurvivalPlayer.PostUpdateEquips` (Step 4 → `PlayerSurvivalPlayer.Apply`), applied in `PlayerHurtPipeline` (`ModifyHurt` for mana-absorb, `OnHurt` for thorns). Do NOT add cases in `ArmorManager`/`AccessoryManager`.

That's it. The new affix automatically enters the roll pool, saves/loads by ID, and syncs over the network — no other files to touch.

## UI Architecture (Reforge Panel)

The reforge panel (`Common/UI/`) replaces the Goblin Tinkerer's vanilla reforge UI.

### Key Files
- **`Common/Systems/ReforgeUISystem.cs`** — `ModSystem` that inserts the reforge panel layer after `"Vanilla: Inventory"`. Suppresses `Main.InReforgeMenu` before inventory draws so vanilla's slot doesn't render. Intercepts ESC to close in one press.
- Resistance shield rendering moved to `ARPGCharacterSystem` (`Common/Systems/ResistanceShieldUISystem.cs`).
- **`Common/UI/ReforgePanel.cs`** — `UIState` containing the item slot, affix lines, title, placeholder. Syncs `Main.reforgeItem = _slot.SlotItem` each frame so packet/hammer code still reads the correct item.
- **`Common/UI/UIReforgeSlot.cs`** — Custom `UIElement` item slot. Interaction handled in `DrawSelf` using `Main.mouseLeft && Main.mouseLeftRelease` (first frame of press). Mouse coords suppressed to -9999 before `ItemSlot.Draw` call to prevent double-interaction.
- **`Common/UI/AffixLine.cs`** — One row per existing modifier: lock toggle on the left, colored affix text in the middle (green for prefix, blue for suffix), right-aligned dim "max N" label showing the magnitude ceiling at current boss progression (computed in `Refresh()` from `def.Tiers[mgr.Category][utils.GetBestTier()].Max`). `Refresh()` is called from `ReforgePanel.RefreshAffix` after a reroll completes; that path also plays `Best_reforge` (custom mod sound at `Assets/Sounds/Best_reforge.wav`) when the new magnitude is ≥ `NearMaxThreshold` (0.85) of the best-tier max. The threshold lives as a `const` on `ReforgePanel`. The ding is per-affix, so a multi-affix reroll where several land near-max will stack.
- **`Common/Config/ReforgeConfig.cs`** — Cost formula constants (`Scale=1.0`, `Base=2.0`, exponential scaling by tier).
- **`Common/Network/ReforgePacketHandler.cs`** — Server-authoritative reroll: client sends request, server deducts cost via `player.BuyItem`, rolls new modifier, sends result back.
- **`Common/Players/ItemInitializerPlayer.cs`** — `OnEnterWorld` gives affixes to all inventory items that don't have them yet (covers starter items).

### Interface Layer Pattern
`"Vanilla: Reforge Menu"` does **not exist** in tML 2026-02. The reforge slot is drawn inside `"Vanilla: Inventory"`. Pattern used:
1. Layer before inventory: saves `Main.InReforgeMenu`, sets it false (suppresses vanilla slot)
2. `"Vanilla: Inventory"` draws without reforge slot
3. Layer after inventory: restores `Main.InReforgeMenu`, draws our panel

### mouseLeft / mouseLeftRelease
- `mouseLeft` = button currently pressed
- `mouseLeftRelease` = was **not** pressed last update (first-frame detection when both true)
- Do NOT set `mouseLeft = false` to consume — it causes the next frame to compute `mouseLeftRelease = true` again, creating a repeated-swap loop
