# Reforge UI Design

**Date:** 2026-04-29
**Mod:** ARPGItemSystem

## Overview

Replace the vanilla Goblin Tinkerer reforge panel with a custom ARPG modifier UI. Players interact with the Goblin Tinkerer exactly as before, but instead of the vanilla reforge panel opening, our panel appears. Each affix on the held item is displayed as an independent row that can be rerolled individually via a hammer button. No new keybinds are introduced. The existing keybind-based reroll system is removed entirely.

## Architecture

### Files Removed
- `Common/Players/Keybind.cs`
- `Common/Systems/KeybindSystem.cs`
- `Common/UI/CraftingPanel.cs` — empty stub, superseded by `ReforgePanel.cs`

### Files Added
- `Common/Systems/UISystem.cs` — `ModSystem` that owns a `UserInterface` + `ReforgePanel` instance and implements `ModifyInterfaceLayers` to swap out the `"Vanilla: Reforge Menu"` layer with our own `GameInterfaceLayer`
- `Common/UI/ReforgePanel.cs` — `UIState` containing the item slot, affix lines, title, and placeholder text
- `Common/UI/AffixLine.cs` — `UIElement` for a single affix row (hammer button + affix text + cost text)

### Files Modified
- `Common/GlobalItems/Weapon/WeaponModifier.cs` — add `tier` field
- `Common/GlobalItems/Armor/ArmorModifier.cs` — add `tier` field
- `Common/GlobalItems/Accessory/AccessoryModifier.cs` — add `tier` field
- All three Manager files (`WeaponManager`, `ArmorManager`, `AccessoryManager`) — plumb `tier` through `SaveData`/`LoadData` and `NetSend`/`NetReceive`
- `Localization/en-US_Mods.ARPGItemSystem.hjson` — add UI localization keys

## UI Layout

The panel is a centered `UIPanel` (vanilla component, texture-pack-compatible).

```
┌──────────────────────────────────┐
│         Modifier Reforge         │  UITextPanel (title)
│                                  │
│            [item slot]           │  UIItemSlot wrapping Main.reforgeItem
│          Iron Broadsword         │  UIText (item name, updates on item drop)
│                                  │
│  [🔨]  +47% Damage       3g 20s  │  AffixLine (prefix, green)
│  [🔨]  +12% Attack Speed  1g 50s │  AffixLine (prefix, green)
│                                  │
│  [🔨]  +8% Crit Chance      80s  │  AffixLine (suffix, blue)
│                                  │
│   Place an item to begin         │  UIText placeholder (hidden when item present)
└──────────────────────────────────┘
```

### AffixLine Components
- `UIImageButton` using vanilla hammer sprite (`TextureAssets.ReforgeButton[0]`) — no custom art
- `UIText` for affix description — green for prefixes, blue for suffixes (matches existing tooltip colors)
- `UIText` for cost — right-aligned, formatted as vanilla coin display
- Hammer button is grayed out and non-interactive while awaiting a server response packet (prevents double-clicks mid-packet)

## Interception Approach

`UISystem` implements `ModifyInterfaceLayers` and removes the layer named `"Vanilla: Reforge Menu"`, inserting our `GameInterfaceLayer` in its place. Our layer activates when `Main.InReforgeMenu` is true.

Vanilla manages `Main.reforgeItem` (drag-in, drag-out, ESC to close, item return to inventory) for free. We do not re-implement item slot state management.

If the layer name `"Vanilla: Reforge Menu"` is not found at load time, a warning is logged so the failure is visible rather than silent.

## Reroll Flow (Per Affix)

1. Player clicks hammer button on an affix line
2. Hammer buttons across all lines are disabled immediately (pending state)
3. Client sends a **reroll request** `ModPacket` to server: `playerWhoAmI`, affix index, affix slot type (`Prefix`/`Suffix`)
4. Server validates the player can afford the cost via `player.BuyItem(cost)`. If not, sends back a rejection packet — client re-enables buttons
5. Server deducts cost, runs `GenerateModifier` for that slot (exclude list is built from all affixes on the item *except* the one being replaced — the replaced slot is being overwritten so its type must remain eligible for the new roll)
6. Server sends back a **reroll result** `ModPacket`: new type ID, new magnitude, new tier, new tooltip string
7. Client applies the result to `Main.reforgeItem`'s GlobalItem modifier list, refreshes all `AffixLine` displays, re-enables buttons

The roll is **server-authoritative**: the client never computes the new affix, only the server does. This prevents desync and cheating in multiplayer.

## Cost Formula

```
cost = item.value * scale * base^(9 - tier)
```

- `tier` — stored on the modifier struct (0 = best, 9 = worst)
- `9 - tier` — converts tier to an exponent so tier 0 is most expensive
- `scale` and `base` — tunable constants on a `ReforgeConfig` static class

### Example (scale = 1.0, base = 2.0, item.value = 10g)

| Tier | Multiplier | Cost     |
|------|-----------|----------|
| 0    | 512×      | 51g 20s  |
| 1    | 256×      | 25g 60s  |
| 2    | 128×      | 12g 80s  |
| 3    | 64×       | 6g 40s   |
| 4    | 32×       | 3g 20s   |
| 5    | 16×       | 1g 60s   |
| 6    | 8×        | 80s      |
| 7    | 4×        | 40s      |
| 8    | 2×        | 20s      |
| 9    | 1×        | 10s      |

`scale` and `base` are independent knobs: `base` controls steepness, `scale` controls the overall price floor.

## Data Layer Changes

### Modifier Structs
All three modifier structs (`WeaponModifier`, `ArmorModifier`, `AccessoryModifier`) gain:
```csharp
public int tier;
```
Set inside `GenerateModifier` at roll time. Carried through all serialization paths:

- `SaveData`/`LoadData` — added to parallel int lists as `PrefixTierList` / `SuffixTierList`
- `NetSend`/`NetReceive` — written/read alongside ID and magnitude (write order must match read order exactly)
- Reroll result packet — carries `tier` so client can immediately update cost display

### Backward Compatibility
Existing saves will have no `tier` data for their modifiers. On `LoadData`, missing tier lists default to tier 9 (weakest/cheapest) so old items don't get absurdly expensive reroll costs.

## Localization

New keys added to `Localization/en-US_Mods.ARPGItemSystem.hjson`:

```hjson
UI: {
    ReforgePanel: {
        Title: Modifier Reforge
        Placeholder: Place an item to begin
    }
}
```

All hard-coded combat text strings from `Keybind.cs` are deleted along with that file.

## Multiplayer Compatibility

- All rolls are server-authoritative (see Reroll Flow above)
- Client disables UI during the request/response round-trip to prevent double-spend
- When the item returns to the player's inventory on menu close, tModLoader's normal item sync fires `NetSend`/`NetReceive` — no additional sync needed at that point
- If a client disconnects mid-reforge, the item returns to their inventory on reconnect; the server already applied and deducted any completed rerolls

## Out of Scope (Future Features)

The following were discussed but are not part of this spec:
- Boss-gated affix locking (lock icon + condition check per hammer button)
- Item-based reroll cost (replacing gold with a consumable item)

Both can be added to `AffixLine` and the reroll request packet without architectural changes.
