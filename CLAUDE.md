# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Non-Negotiable Requirements

- **Multiplayer-compatible:** All features must work correctly in multiplayer. Use `NetSend`/`NetReceive` for syncing per-entity state, `ModPacket` for mod-initiated network messages, and `Main.netMode` guards where needed. Never assume single-player.
- **Clear localization:** All player-visible strings (tooltips, UI text, chat messages) must use `.hjson` localization keys — never hard-coded string literals.

## Build & Development

This is a tModLoader mod for Terraria targeting .NET 6. To build:
- **In-game (recommended):** tModLoader → Workshop → Mod Sources → select `ARPGItemSystem` → Build & Reload
- **CLI compile check:** `dotnet build` (verifies compilation but does not deploy)

There are no automated tests. Testing requires running tModLoader with the mod loaded.

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
OnCreated(item) → Manager.Reroll(item)
  → utils.GetTier()              // boss-progression-based, tier 0 (best) to 9 (worst)
  → utils.GetAmountOf*()         // count depends on specific boss milestones
  → XxxModifier.GenerateModifier()
      → TierDatabase[enum][tier] // random magnitude within min/max range
      → TooltipDatabase[enum]    // format string stored on the modifier
  → modifierList stored on GlobalItem instance (InstancePerEntity = true)
       ↓
Applied each frame via:
  ModifyWeaponDamage / ModifyWeaponCrit / ModifyHitNPC / UseSpeedMultiplier / ModifyManaCost
  ModifyShootStats                                                    (WeaponManager)
  UpdateEquip / UpdateInventory                                       (ArmorManager)
  UpdateAccessory                                                     (AccessoryManager)
  ModifyHitNPC (PercentageArmorPen, CritMultiplier on projectiles)    (ProjectileManager)
```

### Key Files

- **`Common/GlobalItems/Database/TierDatabase.cs`** — Single `Dictionary<Enum, List<Tier>>` holding all weapon, armor, and accessory prefix/suffix types. Each entry has 10 `Tier` structs (tier 0 = strongest, tier 9 = weakest). This is the only place to change modifier value ranges.
- **`Common/GlobalItems/Database/TooltipDatabase.cs`** — Maps every modifier enum variant to a `{0}%`/`+{0}` format string. Stored on the modifier struct at roll time so it survives without a database lookup on load.
- **`Common/GlobalItems/utils.cs`** — Shared `GetTier()` (same boss-milestone shrinking logic as ARPGEnemySystem), `GetAmountOf*()` per item type and slot, and three overloads of `CreateExcludeList()` for deduplication across the three modifier namespaces.
- **`Common/GlobalItems/Weapon/WeaponModifier.cs`** — Defines `PrefixType`, `SuffixType`, `ModifierType` enums and `WeaponModifier` struct. Weapon modifiers are filtered by `DamageClass`: summon weapons cannot roll `ManaCostReduction` or `VelocityIncrease`; adjust the `*WeaponPrefixType`/`*WeaponSuffixType` lists per damage class when adding modifiers.
- **`Common/GlobalItems/Armor/ArmorModifier.cs`** and **`Common/GlobalItems/Accessory/AccessoryModifier.cs`** — Parallel structs for armor/accessory modifier types. These do not filter by damage class.
- **`Common/GlobalItems/ProjectileManager.cs`** — Copies the spawning weapon's `modifierList` onto projectiles fired via `EntitySource_ItemUse_WithAmmo` (excludes consumables and fishing poles). Applies `PercentageArmorPen` and `CritMultiplier` to projectile hits against NPCs.

### Critical: Namespace Collision

`ModifierType`, `PrefixType`, and `SuffixType` are defined **separately** in each of the three namespaces (`ARPGItemSystem.Common.GlobalItems.Weapon`, `.Armor`, `.Accessory`). `TierDatabase` and `TooltipDatabase` use fully qualified names like `Weapon.PrefixType.FlatDamageIncrease` to disambiguate. Follow this pattern in any shared code that spans item categories.

### Persistence Pattern

All three managers use identical patterns for `SaveData`/`LoadData` and `NetSend`/`NetReceive`: parallel lists of `int` type-IDs, `int` magnitudes, and `string` tooltip format strings — prefixes and suffixes serialized in separate groups. **Read order must exactly match write order.** The tooltip string is stored redundantly to avoid needing a database lookup on deserialization.

### Adding a New Modifier

1. Add the enum value to `PrefixType` or `SuffixType` in the appropriate namespace file (`Weapon/`, `Armor/`, or `Accessory/`)
2. Add a 10-entry `List<Tier>` to `TierDatabase.modifierTierDatabase` in `Common/GlobalItems/Database/TierDatabase.cs`
3. Add a format string to `TooltipDatabase.modifierTooltipDatabase`
4. Add the stat effect in the manager's appropriate hook (`ModifyWeaponDamage`, `UpdateEquip`, `UpdateAccessory`, etc.)
5. If it affects projectiles, add a case in `ProjectileManager.ModifyHitNPC`
6. For weapons: update the per-damage-class allowed-list fields in `WeaponModifier` (`meleeWeaponPrefixType`, `rangedWeaponSuffixType`, etc.)

## UI Architecture (Reforge Panel)

The reforge panel (`Common/UI/`) replaces the Goblin Tinkerer's vanilla reforge UI.

### Key Files
- **`Common/Systems/UISystem.cs`** — `ModSystem` that inserts the panel layer after `"Vanilla: Inventory"`. Suppresses `Main.InReforgeMenu` before inventory draws so vanilla's slot doesn't render. Intercepts ESC to close in one press.
- **`Common/UI/ReforgePanel.cs`** — `UIState` containing the item slot, affix lines, title, placeholder. Syncs `Main.reforgeItem = _slot.SlotItem` each frame so packet/hammer code still reads the correct item.
- **`Common/UI/UIReforgeSlot.cs`** — Custom `UIElement` item slot. Interaction handled in `DrawSelf` using `Main.mouseLeft && Main.mouseLeftRelease` (first frame of press). Mouse coords suppressed to -9999 before `ItemSlot.Draw` call to prevent double-interaction.
- **`Common/UI/AffixLine.cs`** — One row per modifier: hammer button, affix text, coin-icon cost display.
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
