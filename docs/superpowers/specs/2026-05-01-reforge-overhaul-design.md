# Reforge Overhaul — Design

**Status:** Draft
**Date:** 2026-05-01
**Mod:** ARPGItemSystem

## Goal

Overhaul the reforge panel into a single-button "Reforge All Unlocked" operation with per-line locks, plus a way to fill empty affix slots that appear when the global affix cap grows during progression.

The redesign is **lightweight by intent** — this is a small augmentation to Terraria, not an attempt to clone PoE. No new currencies, no orbs, no item leveling. Coin only.

## Problems Being Solved

1. **Cap-growth stranding** — Items roll their affix count once at creation. When the global cap grows (e.g., a weapon had 2 prefixes pre-mechs, but `GetAmountOfPrefixesWeapon`'s max is now 3 post-Golem), the item is permanently stuck below the current cap. There is currently no mechanism to add a third affix.
2. **No tension in line-by-line reroll** — Players safely reroll only bad lines while keeping good ones. Every reroll is incremental; there are no transformative or risky moments.

## Player-Facing Behavior

### Reforge panel layout (after the change)

For each existing affix on the item, the panel shows one row:
- **Lock button** (replaces the current hammer button) — toggles the line between locked and unlocked. Locked lines are visually distinct (e.g., gold border / different background tint). Lock state is **session-only** and not saved on the item.
- **Affix text** — unchanged (prefix in green, suffix in blue).
- (No per-line cost display anymore.)

For each "missing" affix slot (item has fewer prefixes/suffixes than the **current possible cap** at this progression), the panel shows an empty row:
- **`+` button** — clicking pays a one-time large coin cost to roll a single new affix into that slot. The slot is then filled and converts into a regular (unlocked) affix row.
- **Placeholder text** — e.g., "Empty Prefix Slot" / "Empty Suffix Slot" in muted color.
- (No cost text on the row itself; cost is communicated via tooltip or a small label.)

At the bottom of the panel:
- **Reforge button** (single hammer icon) — rerolls all unlocked existing affixes simultaneously. Empty slots are not touched by this button.
- **Total cost display** — coin cost for the reforge, recomputed live as the player toggles locks.
- **Disabled state** — if all existing affixes are locked, the button is disabled with a tooltip hint ("Unlock at least one affix to reforge").

### What the player sees through a progression cycle

1. Pre-Hardmode, player crafts a sword. It rolls 1 prefix + 1 suffix (cap is 1/1 here).
2. Post-WoF, the cap rises to 2/2. Player opens reforge panel: now sees their original 1 prefix + 1 suffix as locked-by-choice candidates, plus an "Empty Prefix" row and an "Empty Suffix" row each with a `+` button.
3. Player clicks `+` on Empty Prefix → pays large coin → empty row becomes a real affix row. Same for the suffix.
4. Player likes their first prefix but not the suffixes — locks the first prefix, hits the bottom Reforge button, pays a coin cost (slightly elevated by the single lock), gets fresh rolls on the other 3 lines.
5. Repeats until happy or until coin runs low.

## Mechanics & Formulas

### Capacity sync — current possible cap

The current `utils.cs` has `GetAmountOfPrefixesWeapon`, `GetAmountOfSuffixesWeapon`, etc. — each returns `random.Next(minCount, maxCount + 1)`. These are **non-deterministic** and rolled once at item creation, so they can't be used directly to ask "what's the cap right now?".

Add three new deterministic helpers per category in `utils.cs`:
- `GetMaxPrefixesWeapon()` returns the current `maxCount` value (deterministic, just the progression checks).
- `GetMaxSuffixesWeapon()` likewise.
- Same for armor and accessory (6 new functions total).

These are the values used to detect empty slots in the reforge UI.

**Existing `GetAmountOfX` functions are unchanged** — they continue to govern roll-on-create. The new deterministic Max functions are read-only by the reforge UI/network layer.

**Why "max", not the rolled `random.Next(min, max)`?** In Option A's philosophy, items should reflect current progression. A player post-Plantera should be able to fill all slots up to the current max regardless of what their item originally rolled. The `+` button is the player's choice; it costs them coin.

### Cost formulas

The current cost formula (`ReforgeConfig.CalculateCost`) stays as the per-line baseline and is reused.

```csharp
// Existing — unchanged
public static int CalculateCost(int itemValue, int tier)
    => (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));
```

**Reforge All Unlocked cost** (paid once when the bottom hammer is clicked):

```
unlockedCost = sum over each unlocked affix's per-line CalculateCost(itemValue, affix.Tier)
totalCost    = unlockedCost * LockMultiplier(numLocked)

LockMultiplier table (tunable in ReforgeConfig):
  numLocked = 0   →  1.0×
  numLocked = 1   →  1.5×
  numLocked = 2   →  2.25×
  numLocked = 3   →  3.5×
  numLocked = 4   →  5.5×
  numLocked = 5+  →  9.0×
```

The multiplier escalates so that locking many lines and rerolling few is significantly more expensive than rerolling fully. This creates the design intent: locks are for protecting genuinely good rolls, not for surgical cherry-picking. **Surgical play is intentionally taxed** — locking 5 of 6 lines costs roughly 9× a single-line reroll under the proposed table. This pushes players toward broader rerolls and replaces the old per-line reroll without a dedicated single-line button.

**Empty slot fill cost** (paid when `+` is clicked):

```
emptySlotCost = CalculateCost(itemValue, currentTier) * EmptySlotMultiplier
```

where `EmptySlotMultiplier` is a new `ReforgeConfig` constant defaulting to `5.0` and `currentTier` is the tier the server rolls during the fill operation (see "Tier-source rule" below). Server-authoritative; client displays an estimated cost using a locally-rolled `utils.GetTier()` snapshot, matching the existing per-line behavior pattern where displayed cost is an approximation.

Rationale: filling an empty slot is a one-shot, no-risk operation that strictly adds value to the item. It should be a meaningful coin sink — "great cost" per the design — but not so prohibitive that players never bother.

### Tier-source rule (cost calculation)

`utils.GetTier()` is non-deterministic per call (returns a random tier inside the current progression band), so client and server can roll different values. To keep cost calculations sane:

- **Reforge All Unlocked cost** uses each unlocked affix's **stored tier** (`affix.Tier` from the existing roll). Client and server agree because both read the same stored tier from the item / packet payload. The server still rolls fresh tiers for the *replacement* affixes — but billing is on the old tiers, not the new ones. This matches the existing per-line reroll's behavior pattern.
- **Empty slot fill cost** has no stored tier (slot is empty). The **server rolls `GetTier()` once** when handling the request and bills using that. The client's pre-click display is an estimate, may differ slightly from the actual charge, and that's acceptable — same gap exists today on per-line reroll.

### Lock state

Locks live entirely in UI state on `AffixLine` (a new `bool _locked` field with a click handler). They are not part of `Affix`, are not saved to disk, and do not cross multiplayer. They reset whenever the panel rebuilds its rows (when the slotted item changes or the panel closes).

### Reforge All Unlocked operation

Server-authoritative, same pattern as the existing per-line reroll:

1. Client gathers indices of unlocked affixes from its UI, plus their kinds and excluded IDs (existing affixes the new rolls must avoid duplicating, both within the kept set and within the new rolls themselves).
2. Client sends a `RerollAllUnlocked` packet with the list of unlocked indices, their kinds, the item value, the lock count, and an exclude set.
3. Server validates affordability against `totalCost`, deducts coin via `player.BuyItem`.
4. Server rolls N replacement affixes one at a time, building up its own dedup set so the new rolls don't duplicate each other or duplicate any locked affix. Order: prefixes first, then suffixes, in original index order.
5. Server sends a `RerollAllUnlockedResult` packet listing the new `(index, AffixId, magnitude, tier)` tuples.
6. Client applies them to its `Main.reforgeItem`, calls `RefreshAffix` on each affected row.

In single-player, the same flow runs without packets via a `DoRerollAllUnlockedDirectly` helper.

### Empty slot fill operation

Same pattern, simpler payload:

1. Client sends a `FillEmptySlot` packet with `(kind, itemValue, excludeIds)`.
2. Server validates affordability against `emptySlotCost` (using its own `GetTier()` roll), deducts coin, rolls one affix.
3. Server replies with `FillEmptySlotResult` carrying `(AffixId, magnitude, tier)`.
4. Client appends the new affix to the end of `mgr.Affixes` and rebuilds rows.

### Empty slot indexing

Affixes in the storage list are not currently kind-segregated — they're a flat `List<Affix>`. The reforge panel renders them as separate prefix and suffix groups for display, but the underlying list ordering is roll-order from creation.

For the empty-slot design we need to know "this is empty prefix slot #2". Approach:

- For display, the panel splits affixes by kind: prefixes first, then suffixes. Empty rows are appended to whichever kind has fewer affixes than its `GetMaxX()` cap.
- When a `+` button is clicked, the new rolled affix is appended to the end of `mgr.Affixes`. The panel rebuild then re-groups for display.

Storage order doesn't affect gameplay — affixes are independent, the tooltip iterator (`ModifyTooltips`) walks the list and uses `AffixRegistry.Get(a.Id).Kind` for color/grouping. The on-disk save format (parallel `int`/`byte` lists) preserves whatever order is in `Affixes`.

## Code Impact

### Files to modify

- **`Common/Config/ReforgeConfig.cs`** — Add `LockMultiplier(int locks)` helper and `EmptySlotMultiplier` constant. Existing `CalculateCost` unchanged.
- **`Common/GlobalItems/utils.cs`** — Add 6 deterministic `GetMaxX` helpers next to the existing rolling helpers.
- **`Common/UI/AffixLine.cs`** — Replace hammer button with lock toggle button. Remove per-line cost display. Remove the `OnHammerClicked` reroll logic. Add `bool Locked` property + getter for the panel to read.
- **`Common/UI/ReforgePanel.cs`** — Rebuild row construction to include empty-slot rows. Add the bottom Reforge button with live cost display. Drive a "reforge all unlocked" handler. Add an "empty slot row" UIElement (new small class, can live in this file or alongside `AffixLine`).
- **`Common/Network/ReforgePacketHandler.cs`** — Add six new packet types: `RerollAllUnlocked` / `RerollAllUnlockedResult` / `RerollAllUnlockedRejected` and `FillEmptySlot` / `FillEmptySlotResult` / `FillEmptySlotRejected`. Reuse existing `RollReplacement` for individual rolls. Add a `RollMultipleReplacements` helper that maintains a running exclude-set so new rolls don't duplicate each other or any locked affix. **Remove the old per-line packet types** (`RerollRequest`, `RerollResult`, `RerollRejected`) and the `DoRerollDirectly` / `SendRerollRequest` helpers — they are unused after the cutover.
- **`Localization/en-US_Mods.ARPGItemSystem.hjson`** — Add new keys for lock tooltip, empty slot placeholder text, reforge-all button label, "all locked" hint, etc.

### Files NOT changed

- `Affix.cs`, `AffixDef.cs`, `AffixId.cs`, `AffixRegistry.cs`, `AffixRoller.cs`, `AffixKind.cs`, `Tier.cs`, `ItemCategory.cs` — affix data model is untouched.
- `AffixItemManager.cs` — save format unchanged. Existing `Reroll` method stays for `OnCreated` use; we don't call it from the new flow.
- All gameplay-side files (managers for weapon/armor/accessory, projectile manager, elemental calculator, player elemental, NPC global) — untouched.

## Edge Cases

- **All existing affixes locked, no empty slots** — Reforge button is disabled with a tooltip hint. `+` buttons obviously absent.
- **All existing affixes locked, empty slots present** — Reforge button is disabled (no unlocked affixes to reroll). `+` buttons remain functional.
- **Item with zero affixes** (shouldn't normally happen post-creation, but defensively) — Panel shows only empty rows. Player fills them via `+` buttons.
- **Cap shrunk (theoretical only — current design never shrinks)** — Item has more affixes than `GetMaxX()`. The panel renders all of them as normal rows; no empty rows; reforge functions normally. We never auto-delete.
- **Item value is zero** (some boss-drop weapons have `value = 0`) — `CalculateCost` returns 0; reforge is free. This matches existing behavior. Not addressed here; design preserves status quo.
- **Coin shortfall** — Rejected via the existing `BuyItem` failure path. Server sends rejection packet; client clears pending state. Same as today.
- **Item leaves slot mid-operation** (network round-trip in progress) — Existing behavior: result packet checks `Main.reforgeItem.IsAir` and bails. New flow follows same guard.
- **Multiplayer — two clients reforging simultaneously** — Each client's request is processed independently against their own player's coin and inventory. No cross-client state. Same as today.

## Non-Goals (explicitly out of scope)

- Materials / souls / per-tier reforge currencies (rejected during brainstorming — too heavy for this mod's tone).
- Persistent locks (rejected — session-only is simpler).
- Per-line surgical reroll button (replaced by lock-everything-else + Reforge All).
- Affix biasing / imprinting / category targeting.
- Item leveling, tier upgrade, item XP.
- Touching the existing `random.Next` non-determinism quirk in `utils.cs` (cosmetic bug, separate concern).
- Tooltip preview of "what would I roll into" (metagaming).

## Open Tuning Items (decide during implementation)

- Exact `LockMultiplier` table values — the proposed `[1.0, 1.5, 2.25, 3.5, 5.5, 9.0]` is a starting point; needs in-game playtest.
- `EmptySlotMultiplier = 5.0` default — needs feel-check.
- Visual treatment of the lock button (texture choice, locked-row tint).
