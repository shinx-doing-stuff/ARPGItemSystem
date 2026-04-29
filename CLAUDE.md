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
  ModifyHitNPC (PercentageArmorPen, CritMultiplier on projectiles)    (ProjectileManager)
```

### Key Files

- **`Common/Affixes/AffixRegistry.cs`** — Single source of truth. One `AffixDef` entry per affix containing: `AffixId`, `AffixKind` (Prefix/Suffix), tooltip format string, per-`ItemCategory` tier tables (10 entries each), and optional `HashSet<DamageClass>` restriction for weapons. **This is the only place to define, change, or remove affixes.**
- **`Common/Affixes/AffixItemManager.cs`** — Abstract `GlobalItem` base class shared by all three managers. Owns `List<Affix> Affixes`, handles `SaveData`/`LoadData`, `NetSend`/`NetReceive`, `Clone`, `ModifyTooltips`, and the `Reroll` entry point. Save format uses tag keys `"AffixIds"`, `"Magnitudes"`, `"Tiers"`, `"Kinds"` — absence of `"AffixIds"` means pre-refactor save and triggers a fresh reroll.
- **`Common/Affixes/AffixRoller.cs`** — Picks a random eligible `AffixDef` from the pool (filtered by category, kind, DamageClass, and existing-affix deduplication) and rolls a magnitude. Uses `Main.rand`.
- **`Common/GlobalItems/utils.cs`** — `GetTier()` and six `GetAmountOf*()` methods. Boss-progression logic lives here.
- **`Common/GlobalItems/ProjectileManager.cs`** — Snapshots the spawning weapon's `Affixes` list. Applies `PercentageArmorPen` and `CritMultiplier` to projectile hits.

### Persistence Pattern

`AffixItemManager` writes four parallel `List<int/byte>` entries to `TagCompound`: `AffixIds`, `Magnitudes`, `Tiers`, `Kinds`. Read order matches write order. Tooltips are **not** stored — they are looked up at draw time from the registry. Migration: if `"AffixIds"` key is absent (old save), `LoadData` calls `Reroll()` immediately.

### Adding a New Affix

**Step 1 — `Common/Affixes/AffixId.cs`:** Add the new value to the `AffixId` enum.

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

That's it. The new affix automatically enters the roll pool, saves/loads by ID, and syncs over the network — no other files to touch.

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
