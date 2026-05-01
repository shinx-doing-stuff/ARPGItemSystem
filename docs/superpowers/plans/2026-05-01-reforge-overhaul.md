# Reforge Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the per-line reroll system with a single bottom "Reforge" button that rerolls all unlocked affixes simultaneously, plus `+` buttons on empty rows that fill in missing affix slots when the global cap has grown past the item's count.

**Architecture:** Affix data model and persistence are untouched. UI rows switch from hammer-per-line to lock-toggle-per-line; cost moves to a single bottom button with live recomputation. Packet handler gains two new server-authoritative operations (`RerollAllUnlocked`, `FillEmptySlot`) and loses the old per-line `Reroll` types after the cutover. Empty-slot detection uses new deterministic `GetMaxX` helpers; empty-slot fill cost uses a new deterministic `GetBestTier` helper for predictable client/server pricing.

**Tech Stack:** C# / .NET 8, tModLoader 2026.x, MonoGame UI (`UIState`/`UIElement`/`UIPanel`/`UIImageButton`/`UIText`), tModLoader `ModPacket` networking, hjson localization.

**Spec:** [`docs/superpowers/specs/2026-05-01-reforge-overhaul-design.md`](../specs/2026-05-01-reforge-overhaul-design.md)

**Notes for the engineer:**
- This codebase has **no automated tests**. Verification is `dotnet build` (compile check) plus a manual in-game checklist at the end.
- The mod must be **multiplayer-compatible**. The existing per-line reroll already uses server-authoritative packets; the new flow follows the same pattern — server validates cost, deducts coin, rolls, replies.
- Player-visible strings go in **`Localization/en-US_Mods.ARPGItemSystem.hjson`** under existing `UI.ReforgePanel.*` namespace. Do not hardcode strings in C#.
- `dotnet build` requires ARPGEnemySystem to have been built once so its DLL exists at `../ARPGEnemySystem/bin/Debug/net8.0/ARPGEnemySystem.dll`. If a build fails on missing reference, build ARPGEnemySystem first.
- Each task ends with a commit. Tasks are sequenced so every commit leaves the codebase in a compiling state.

---

## File Map

**Modified:**
- `Common/Config/ReforgeConfig.cs` — adds `LockMultiplier` + `EmptySlotMultiplier`
- `Common/GlobalItems/utils.cs` — adds 6 `GetMaxX` helpers + `GetBestTier`
- `Common/Network/ReforgePacketHandler.cs` — adds new packet flow, removes old per-line flow
- `Common/UI/AffixLine.cs` — replaces hammer button with lock toggle; drops cost display
- `Common/UI/ReforgePanel.cs` — adds bottom Reforge button, empty-slot rows, live cost
- `Localization/en-US_Mods.ARPGItemSystem.hjson` — adds new UI strings

**Created:**
- `Common/UI/UICostDisplay.cs` — extracted from `AffixLine` (was a private nested class) so the bottom button can reuse it
- `Common/UI/EmptySlotRow.cs` — new UI element for empty prefix/suffix slots with the `+` button

**Untouched:**
- `Common/Affixes/AffixId.cs`, `AffixRegistry.cs`, `AffixRoller.cs`, `AffixDef.cs`, `Affix.cs`, `AffixKind.cs`, `Tier.cs`, `ItemCategory.cs`
- `Common/Affixes/AffixItemManager.cs` (save format unchanged)
- All gameplay-side files (managers for weapon/armor/accessory, projectile manager, elemental calculator, player elemental, NPC global)
- `Common/Systems/ReforgeUISystem.cs`

---

### Task 1: Add deterministic helpers to `utils.cs`

**Files:**
- Modify: `Common/GlobalItems/utils.cs`

**Why:** The existing `GetAmountOfX` helpers each call `random.Next(min, max+1)`, so they return non-deterministic values per call and can't be used to ask "what's the cap right now?". We need 6 deterministic max helpers (one per category × kind) for empty-slot detection, plus a deterministic `GetBestTier` for empty-slot cost calculation.

- [ ] **Step 1: Open `Common/GlobalItems/utils.cs` and add the 6 new max helpers**

Add these methods inside the `utils` class, immediately after each existing `GetAmountOfX` method (so each `GetMax` lives next to its randomized sibling for readability):

```csharp
internal static int GetMaxSuffixesWeapon()
{
    int maxCount = 1;
    if (NPC.downedBoss2) maxCount += 1;
    if (NPC.downedMechBossAny) maxCount += 1;
    return maxCount;
}

internal static int GetMaxPrefixesWeapon()
{
    int maxCount = 1;
    if (NPC.downedBoss3) maxCount += 1;
    if (NPC.downedGolemBoss) maxCount += 1;
    return maxCount;
}

internal static int GetMaxSuffixesArmor()
{
    int maxCount = 1;
    if (Main.hardMode) maxCount += 1;
    return maxCount;
}

internal static int GetMaxPrefixesArmor()
{
    int maxCount = 1;
    if (NPC.downedGolemBoss) maxCount += 1;
    return maxCount;
}

internal static int GetMaxSuffixesAccessory()
{
    int maxCount = 1;
    return maxCount;
}

internal static int GetMaxPrefixesAccessory()
{
    int maxCount = 1;
    return maxCount;
}
```

These mirror the `maxCount` accumulation in each existing `GetAmountOfX` exactly — same boss-flag conditions, same increments — but skip `minCount` and `random.Next`.

- [ ] **Step 2: Add `GetBestTier()` directly under the existing `GetTier()` method**

```csharp
internal static int GetBestTier()
{
    int bestTier = 8;

    if (NPC.downedSlimeKing) bestTier -= 1;
    if (NPC.downedBoss3) bestTier -= 1;
    if (Main.hardMode) bestTier -= 1;
    if (NPC.downedMechBossAny) bestTier -= 1;
    if (NPC.downedPlantBoss) bestTier -= 1;
    if (NPC.downedEmpressOfLight) bestTier -= 1;
    if (NPC.downedAncientCultist) bestTier -= 1;
    if (NPC.downedMoonlord) bestTier -= 1;

    return Math.Max(0, bestTier);
}
```

Note: this includes only the boss flags that decrement `bestTier` in `GetTier()`. Flags that decrement `worstTier` are intentionally omitted — they don't affect the floor.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build` in the `ARPGItemSystem/` directory.
Expected: `Build succeeded` with no errors.

- [ ] **Step 4: Commit**

```bash
git add Common/GlobalItems/utils.cs
git commit -m "feat(utils): add deterministic GetMaxX and GetBestTier helpers

For the reforge overhaul: empty-slot detection needs deterministic
cap-at-progression values (existing GetAmountOfX is randomized per
call), and empty-slot cost calculation needs a deterministic best-tier
anchor that client and server can both compute identically."
```

---

### Task 2: Extend `ReforgeConfig` with `LockMultiplier` and `EmptySlotMultiplier`

**Files:**
- Modify: `Common/Config/ReforgeConfig.cs`

- [ ] **Step 1: Replace the file contents with the extended config**

```csharp
using System;

namespace ARPGItemSystem.Common.Config
{
    public static class ReforgeConfig
    {
        public const float Scale = 1.0f;
        public const float Base = 2.0f;

        // Multiplier applied to the empty-slot fill cost on top of CalculateCost.
        public const float EmptySlotMultiplier = 5.0f;

        public static int CalculateCost(int itemValue, int tier)
        {
            return (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));
        }

        // Cost-multiplier table for "Reforge All Unlocked" based on how many
        // affixes the player has locked. Locking many lines and rerolling few
        // is intentionally taxed so surgical play is expensive.
        public static float LockMultiplier(int locks) => locks switch
        {
            0 => 1.0f,
            1 => 1.5f,
            2 => 2.25f,
            3 => 3.5f,
            4 => 5.5f,
            _ => 9.0f
        };
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Common/Config/ReforgeConfig.cs
git commit -m "feat(config): add LockMultiplier and EmptySlotMultiplier"
```

---

### Task 3: Extract `UICostDisplay` to its own file

**Files:**
- Create: `Common/UI/UICostDisplay.cs`
- Modify: `Common/UI/AffixLine.cs`

**Why:** Currently `UICostDisplay` is a private nested class inside `AffixLine`. The bottom Reforge button (Task 8) needs the same coin-icon-rendering element, so we extract it. After the AffixLine rewrite (Task 6) the class would otherwise be orphaned anyway.

- [ ] **Step 1: Create `Common/UI/UICostDisplay.cs` with the extracted class**

```csharp
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // Draws a coin cost as platinum/gold/silver/copper item icons followed by their counts.
    // The text tint goes red when the local player can't afford the cost.
    public sealed class UICostDisplay : UIElement
    {
        public int Cost;

        public UICostDisplay(int cost)
        {
            Cost = cost;
            Width.Set(130, 0f);
            Height.Set(20, 0f);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (Cost <= 0) return;

            int platinum = Cost / 1000000;
            int gold     = (Cost / 10000) % 100;
            int silver   = (Cost / 100) % 100;
            int copper   = Cost % 100;

            var dim = GetDimensions();
            float x = dim.X + dim.Width;
            float y = dim.Y + dim.Height / 2f - 8f;
            var textTint = Main.LocalPlayer.CanAfford(Cost) ? Color.White : Color.Red;

            // Draw right-to-left so the least significant coin is on the far right
            if (copper > 0 || (platinum == 0 && gold == 0 && silver == 0))
                x = DrawCoin(sb, x, y, copper, ItemID.CopperCoin, textTint);
            if (silver > 0)
                x = DrawCoin(sb, x, y, silver, ItemID.SilverCoin, textTint);
            if (gold > 0)
                x = DrawCoin(sb, x, y, gold, ItemID.GoldCoin, textTint);
            if (platinum > 0)
                x = DrawCoin(sb, x, y, platinum, ItemID.PlatinumCoin, textTint);
        }

        private static float DrawCoin(SpriteBatch sb, float rightX, float y, int amount, int coinItemId, Color textTint)
        {
            Main.instance.LoadItem(coinItemId);
            Texture2D tex = TextureAssets.Item[coinItemId].Value;

            string text = amount.ToString();
            float textWidth = FontAssets.MouseText.Value.MeasureString(text).X * 0.75f;
            const float IconSize = 16f;
            float totalWidth = IconSize + 2f + textWidth + 4f;
            float startX = rightX - totalWidth;

            float scale = IconSize / Math.Max(tex.Width, tex.Height);
            sb.Draw(tex, new Vector2(startX, y + (IconSize - tex.Height * scale) / 2f),
                null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            Utils.DrawBorderString(sb, text,
                new Vector2(startX + IconSize + 2f, y), textTint, 0.75f);

            return startX - 2f;
        }
    }
}
```

This is byte-for-byte the existing private `UICostDisplay` from `AffixLine.cs`, just promoted to public with a top-level namespace.

- [ ] **Step 2: Remove the private nested `UICostDisplay` class from `AffixLine.cs`**

In `Common/UI/AffixLine.cs`, delete the nested class definition starting at:

```csharp
        // Draws the cost as coin icons (platinum/gold/silver/copper) instead of text abbreviations.
        private sealed class UICostDisplay : UIElement
        {
```

through its closing `}`. The outer `AffixLine` class continues to reference `UICostDisplay` via the field `_costDisplay` — that field now resolves to the new top-level type because both files share the `ARPGItemSystem.Common.UI` namespace.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add Common/UI/UICostDisplay.cs Common/UI/AffixLine.cs
git commit -m "refactor(ui): extract UICostDisplay to its own file

Pulls UICostDisplay out of AffixLine's private nested scope so the
bottom Reforge button (next commits) can use the same coin renderer."
```

---

### Task 4: Add new packet types and handlers (alongside the old ones)

**Files:**
- Modify: `Common/Network/ReforgePacketHandler.cs`

**Why:** The new flow needs `RerollAllUnlocked` + result + rejected packets and `FillEmptySlot` + result + rejected packets. We add them alongside the old ones so the build stays green; the old ones are removed in Task 9 once nothing references them.

- [ ] **Step 1: Extend `ReforgePacketType` enum**

In `Common/Network/ReforgePacketHandler.cs`, replace the existing enum block:

```csharp
public enum ReforgePacketType : byte
{
    RerollRequest = 0,
    RerollResult = 1,
    RerollRejected = 2
}
```

with:

```csharp
public enum ReforgePacketType : byte
{
    // Old per-line flow — removed once UI cutover lands.
    RerollRequest = 0,
    RerollResult = 1,
    RerollRejected = 2,

    // New flow — multi-line reroll with locks, plus empty-slot fill.
    RerollAllUnlockedRequest = 3,
    RerollAllUnlockedResult = 4,
    RerollAllUnlockedRejected = 5,
    FillEmptySlotRequest = 6,
    FillEmptySlotResult = 7,
    FillEmptySlotRejected = 8
}
```

- [ ] **Step 2: Extend the `HandlePacket` switch**

Replace the existing `HandlePacket` method:

```csharp
public static void HandlePacket(BinaryReader reader, int whoAmI)
{
    var type = (ReforgePacketType)reader.ReadByte();
    switch (type)
    {
        case ReforgePacketType.RerollRequest:  HandleRerollRequest(reader, whoAmI); break;
        case ReforgePacketType.RerollResult:   HandleRerollResult(reader);          break;
        case ReforgePacketType.RerollRejected: HandleRerollRejected(reader);        break;
    }
}
```

with:

```csharp
public static void HandlePacket(BinaryReader reader, int whoAmI)
{
    var type = (ReforgePacketType)reader.ReadByte();
    switch (type)
    {
        case ReforgePacketType.RerollRequest:               HandleRerollRequest(reader, whoAmI);             break;
        case ReforgePacketType.RerollResult:                HandleRerollResult(reader);                      break;
        case ReforgePacketType.RerollRejected:              HandleRerollRejected(reader);                    break;
        case ReforgePacketType.RerollAllUnlockedRequest:    HandleRerollAllUnlockedRequest(reader, whoAmI);  break;
        case ReforgePacketType.RerollAllUnlockedResult:     HandleRerollAllUnlockedResult(reader);           break;
        case ReforgePacketType.RerollAllUnlockedRejected:   HandleRerollAllUnlockedRejected();               break;
        case ReforgePacketType.FillEmptySlotRequest:        HandleFillEmptySlotRequest(reader, whoAmI);      break;
        case ReforgePacketType.FillEmptySlotResult:         HandleFillEmptySlotResult(reader);               break;
        case ReforgePacketType.FillEmptySlotRejected:       HandleFillEmptySlotRejected();                   break;
    }
}
```

- [ ] **Step 3: Add the `RerollAllUnlocked` send + handlers**

Add the following members to the `ReforgePacketHandler` class. Place them after the existing `HandleRerollRejected` method (just before `DoRerollDirectly`).

Wire format documentation (single comment, kept inline so it's discoverable next to the implementation):

```csharp
// ============================================================
// New flow: Reroll All Unlocked
// ============================================================
// Wire format (Request, after the type byte):
//   byte    ItemCategory
//   byte    WeaponDamageCategory
//   int     itemValue
//   byte    numUnlocked
//     repeat numUnlocked times:
//       byte affixIndex
//       byte AffixKind
//       int  storedTier  (used for cost calc only — server rolls fresh tier per replacement)
//   byte    numLocked
//     repeat numLocked times:
//       byte AffixKind
//       int  AffixId
//
// Wire format (Result, after the type byte):
//   byte    numResults
//     repeat numResults times:
//       byte affixIndex
//       int  AffixId
//       int  magnitude
//       int  tier
//
// Wire format (Rejected, after the type byte): no payload.

public static void SendRerollAllUnlockedRequest(
    ItemCategory cat, WeaponDamageCategory damCat, int itemValue,
    List<(byte index, AffixKind kind, int storedTier)> unlocked,
    List<(AffixKind kind, AffixId id)> locked)
{
    var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
    packet.Write((byte)ReforgePacketType.RerollAllUnlockedRequest);
    packet.Write((byte)cat);
    packet.Write((byte)damCat);
    packet.Write(itemValue);
    packet.Write((byte)unlocked.Count);
    foreach (var u in unlocked)
    {
        packet.Write(u.index);
        packet.Write((byte)u.kind);
        packet.Write(u.storedTier);
    }
    packet.Write((byte)locked.Count);
    foreach (var l in locked)
    {
        packet.Write((byte)l.kind);
        packet.Write((int)l.id);
    }
    packet.Send();
}

private static void HandleRerollAllUnlockedRequest(BinaryReader reader, int whoAmI)
{
    var cat = (ItemCategory)reader.ReadByte();
    var damCat = (WeaponDamageCategory)reader.ReadByte();
    int itemValue = reader.ReadInt32();

    byte numUnlocked = reader.ReadByte();
    var unlocked = new List<(byte index, AffixKind kind, int storedTier)>(numUnlocked);
    for (int i = 0; i < numUnlocked; i++)
    {
        byte index = reader.ReadByte();
        var kind = (AffixKind)reader.ReadByte();
        int storedTier = reader.ReadInt32();
        unlocked.Add((index, kind, storedTier));
    }

    byte numLocked = reader.ReadByte();
    var locked = new List<(AffixKind kind, AffixId id)>(numLocked);
    for (int i = 0; i < numLocked; i++)
    {
        var kind = (AffixKind)reader.ReadByte();
        var id = (AffixId)reader.ReadInt32();
        locked.Add((kind, id));
    }

    int totalCost = ComputeRerollAllUnlockedCost(itemValue, unlocked, numLocked);
    var player = Main.player[whoAmI];

    if (!player.BuyItem(totalCost))
    {
        var rejection = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
        rejection.Write((byte)ReforgePacketType.RerollAllUnlockedRejected);
        rejection.Send(whoAmI);
        return;
    }

    var results = RollMultipleReplacements(cat, damCat, unlocked, locked);

    var resultPacket = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
    resultPacket.Write((byte)ReforgePacketType.RerollAllUnlockedResult);
    resultPacket.Write((byte)results.Count);
    foreach (var r in results)
    {
        resultPacket.Write(r.index);
        resultPacket.Write((int)r.id);
        resultPacket.Write(r.magnitude);
        resultPacket.Write(r.tier);
    }
    resultPacket.Send(whoAmI);
}

private static void HandleRerollAllUnlockedResult(BinaryReader reader)
{
    if (Main.netMode == NetmodeID.Server) return;

    byte count = reader.ReadByte();
    var item = Main.reforgeItem;

    var indices = new List<int>(count);
    for (int i = 0; i < count; i++)
    {
        byte index = reader.ReadByte();
        var id = (AffixId)reader.ReadInt32();
        int magnitude = reader.ReadInt32();
        int tier = reader.ReadInt32();
        if (!item.IsAir)
            ApplyAffixReplacement(item, index, id, magnitude, tier);
        indices.Add(index);
    }

    var panel = ModContent.GetInstance<ReforgeUISystem>().Panel;
    panel?.SetAllPending(false);
    foreach (int i in indices)
        panel?.RefreshAffix(i);
}

private static void HandleRerollAllUnlockedRejected()
{
    if (Main.netMode == NetmodeID.Server) return;
    ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
}

public static void DoRerollAllUnlockedDirectly(
    Item item, ItemCategory cat, WeaponDamageCategory damCat, int itemValue,
    List<(byte index, AffixKind kind, int storedTier)> unlocked,
    List<(AffixKind kind, AffixId id)> locked)
{
    int totalCost = ComputeRerollAllUnlockedCost(itemValue, unlocked, locked.Count);

    if (!Main.LocalPlayer.BuyItem(totalCost))
    {
        ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
        return;
    }

    var results = RollMultipleReplacements(cat, damCat, unlocked, locked);
    var panel = ModContent.GetInstance<ReforgeUISystem>().Panel;

    foreach (var r in results)
        ApplyAffixReplacement(item, r.index, r.id, r.magnitude, r.tier);

    panel?.SetAllPending(false);
    foreach (var r in results)
        panel?.RefreshAffix(r.index);
}

private static int ComputeRerollAllUnlockedCost(
    int itemValue,
    List<(byte index, AffixKind kind, int storedTier)> unlocked,
    int numLocked)
{
    long sum = 0;
    foreach (var u in unlocked)
        sum += ReforgeConfig.CalculateCost(itemValue, u.storedTier);
    return (int)(sum * ReforgeConfig.LockMultiplier(numLocked));
}

private static List<(byte index, AffixId id, int magnitude, int tier)> RollMultipleReplacements(
    ItemCategory cat, WeaponDamageCategory damCat,
    List<(byte index, AffixKind kind, int storedTier)> unlocked,
    List<(AffixKind kind, AffixId id)> locked)
{
    // Per-kind running exclude lists. Start with locked affixes of each kind.
    // As we roll each unlocked replacement, add its ID to its kind's exclude list
    // so subsequent rolls don't duplicate it.
    var excludeByKind = new Dictionary<AffixKind, List<AffixId>>
    {
        [AffixKind.Prefix] = new List<AffixId>(),
        [AffixKind.Suffix] = new List<AffixId>()
    };
    foreach (var l in locked)
        excludeByKind[l.kind].Add(l.id);

    var results = new List<(byte index, AffixId id, int magnitude, int tier)>(unlocked.Count);

    foreach (var u in unlocked)
    {
        int newTier = utils.GetTier();
        RollReplacement(cat, u.kind, damCat, excludeByKind[u.kind], newTier,
            out AffixId newId, out int newMagnitude);
        if (newId == AffixId.None) continue;
        results.Add((u.index, newId, newMagnitude, newTier));
        excludeByKind[u.kind].Add(newId);
    }

    return results;
}
```

- [ ] **Step 4: Add the `FillEmptySlot` send + handlers**

Append directly after the previous block:

```csharp
// ============================================================
// New flow: Fill Empty Slot
// ============================================================
// Wire format (Request, after the type byte):
//   byte    ItemCategory
//   byte    WeaponDamageCategory
//   byte    AffixKind
//   int     itemValue
//   byte    numExclude
//     repeat numExclude times:
//       int  AffixId
//
// Wire format (Result, after the type byte):
//   int  AffixId
//   int  magnitude
//   int  tier
//
// Wire format (Rejected, after the type byte): no payload.

public static void SendFillEmptySlotRequest(
    ItemCategory cat, WeaponDamageCategory damCat, AffixKind kind,
    int itemValue, List<AffixId> excludeIds)
{
    var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
    packet.Write((byte)ReforgePacketType.FillEmptySlotRequest);
    packet.Write((byte)cat);
    packet.Write((byte)damCat);
    packet.Write((byte)kind);
    packet.Write(itemValue);
    packet.Write((byte)excludeIds.Count);
    foreach (var id in excludeIds) packet.Write((int)id);
    packet.Send();
}

private static void HandleFillEmptySlotRequest(BinaryReader reader, int whoAmI)
{
    var cat = (ItemCategory)reader.ReadByte();
    var damCat = (WeaponDamageCategory)reader.ReadByte();
    var kind = (AffixKind)reader.ReadByte();
    int itemValue = reader.ReadInt32();

    byte numExclude = reader.ReadByte();
    var excludeIds = new List<AffixId>(numExclude);
    for (int i = 0; i < numExclude; i++) excludeIds.Add((AffixId)reader.ReadInt32());

    int cost = ComputeEmptySlotCost(itemValue);
    var player = Main.player[whoAmI];

    if (!player.BuyItem(cost))
    {
        var rejection = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
        rejection.Write((byte)ReforgePacketType.FillEmptySlotRejected);
        rejection.Send(whoAmI);
        return;
    }

    int newTier = utils.GetTier();
    RollReplacement(cat, kind, damCat, excludeIds, newTier,
        out AffixId newId, out int newMagnitude);

    var resultPacket = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
    resultPacket.Write((byte)ReforgePacketType.FillEmptySlotResult);
    resultPacket.Write((int)newId);
    resultPacket.Write(newMagnitude);
    resultPacket.Write(newTier);
    resultPacket.Send(whoAmI);
}

private static void HandleFillEmptySlotResult(BinaryReader reader)
{
    if (Main.netMode == NetmodeID.Server) return;

    var newId = (AffixId)reader.ReadInt32();
    int newMagnitude = reader.ReadInt32();
    int newTier = reader.ReadInt32();

    var item = Main.reforgeItem;
    if (item.IsAir || newId == AffixId.None)
    {
        ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
        return;
    }

    AppendAffix(item, newId, newMagnitude, newTier);
    ModContent.GetInstance<ReforgeUISystem>().Panel?.RebuildRowsAfterFill();
}

private static void HandleFillEmptySlotRejected()
{
    if (Main.netMode == NetmodeID.Server) return;
    ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
}

public static void DoFillEmptySlotDirectly(
    Item item, ItemCategory cat, WeaponDamageCategory damCat, AffixKind kind,
    int itemValue, List<AffixId> excludeIds)
{
    int cost = ComputeEmptySlotCost(itemValue);

    if (!Main.LocalPlayer.BuyItem(cost))
    {
        ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
        return;
    }

    int newTier = utils.GetTier();
    RollReplacement(cat, kind, damCat, excludeIds, newTier,
        out AffixId newId, out int newMagnitude);

    if (newId == AffixId.None)
    {
        ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
        return;
    }

    AppendAffix(item, newId, newMagnitude, newTier);
    ModContent.GetInstance<ReforgeUISystem>().Panel?.RebuildRowsAfterFill();
}

public static int ComputeEmptySlotCost(int itemValue)
{
    return (int)(ReforgeConfig.CalculateCost(itemValue, utils.GetBestTier())
                 * ReforgeConfig.EmptySlotMultiplier);
}

private static void AppendAffix(Item item, AffixId id, int magnitude, int tier)
{
    AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
        ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
        : item.accessory
            ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
            : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

    if (mgr == null || id == AffixId.None) return;
    mgr.Affixes.Add(new Affix(id, magnitude, tier));
}
```

Note: `Panel.RebuildRowsAfterFill()` is referenced but not yet defined; we add it in Task 8 when the panel rewrite happens. The build will currently fail — that's expected; we keep this task scoped to the packet handler and stub the call here so the panel signature is documented in advance. **However, we cannot leave the build broken.** Comment out the `Panel.RebuildRowsAfterFill()` calls for now and put `// TODO(plan-task-8): replace with Panel.RebuildRowsAfterFill();` so a grep will surface them later:

In `HandleFillEmptySlotResult`:
```csharp
// TODO(plan-task-8): replace with Panel.RebuildRowsAfterFill();
ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
```

In `DoFillEmptySlotDirectly`, the success path:
```csharp
AppendAffix(item, newId, newMagnitude, newTier);
// TODO(plan-task-8): replace with Panel.RebuildRowsAfterFill();
ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
```

Both branches now end by clearing pending state, which is benign in the interim. Task 8 replaces both `TODO` lines with the real `Panel.RebuildRowsAfterFill()` call.

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`. (The TODO comments are inert.)

- [ ] **Step 6: Commit**

```bash
git add Common/Network/ReforgePacketHandler.cs
git commit -m "feat(net): add new packet types for reroll-all-unlocked + fill-empty-slot

Server-authoritative: client sends operation request with payload,
server validates cost/coin, rolls, replies with results or rejection.
Old per-line packets remain in place during the UI cutover and will
be removed once the panel no longer references them."
```

---

### Task 5: Add localization keys

**Files:**
- Modify: `Localization/en-US_Mods.ARPGItemSystem.hjson`

- [ ] **Step 1: Extend the `UI.ReforgePanel` block**

Replace the existing `ReforgePanel` block:

```hjson
	ReforgePanel: {
		Title: Modifier Reforge
		Placeholder: Place an item to begin

		Currency: {
			Platinum: p
			Gold: g
			Silver: s
			Copper: c
		}
	}
```

with:

```hjson
	ReforgePanel: {
		Title: Modifier Reforge
		Placeholder: Place an item to begin

		Currency: {
			Platinum: p
			Gold: g
			Silver: s
			Copper: c
		}

		EmptyPrefixSlot: Empty Prefix Slot
		EmptySuffixSlot: Empty Suffix Slot
		ReforgeButton: Reforge
		LockTooltip: Lock affix (will not be rerolled)
		UnlockTooltip: Unlock affix
		AddAffixTooltip: Add a random affix
		AllLockedHint: Unlock at least one affix to reforge
	}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded`. (hjson edits don't affect compilation, but a build run confirms nothing else regressed.)

- [ ] **Step 3: Commit**

```bash
git add Localization/en-US_Mods.ARPGItemSystem.hjson
git commit -m "i18n: add reforge overhaul UI strings"
```

---

### Task 6: Rewrite `AffixLine` as a lock-toggle row

**Files:**
- Modify: `Common/UI/AffixLine.cs`

**Why:** Each existing affix row becomes lock-toggle + affix text. No per-line cost. No per-line reroll button. Public `Locked` bool exposed so the panel can read it when sending `Reroll All Unlocked`. `SetPending` and `Refresh` continue to work — we just retarget what they affect.

- [ ] **Step 1: Replace the entire contents of `Common/UI/AffixLine.cs`**

```csharp
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // One row in the reforge panel for an existing affix on the slotted item.
    // Shows a lock toggle on the left and the affix text on the right.
    // Locks are session-only — reset whenever the row is rebuilt.
    public class AffixLine : UIElement
    {
        private readonly LockToggleButton _lockButton;
        private readonly UIText _affixText;
        private bool _isPending;
        private readonly int _affixIndex;
        private readonly bool _isPrefix;

        public bool Locked => _lockButton.Locked;
        public int AffixIndex => _affixIndex;
        public bool IsPrefix => _isPrefix;

        public AffixLine(string displayText, int affixIndex, bool isPrefix)
        {
            _affixIndex = affixIndex;
            _isPrefix = isPrefix;
            Height.Set(28, 0f);

            _lockButton = new LockToggleButton();
            _lockButton.Width.Set(22, 0f);
            _lockButton.Height.Set(22, 0f);
            _lockButton.Left.Set(0, 0f);
            _lockButton.VAlign = 0.5f;
            Append(_lockButton);

            _affixText = new UIText(displayText, 0.85f);
            _affixText.TextColor = isPrefix ? Color.LightGreen : Color.DeepSkyBlue;
            _affixText.Left.Set(28, 0f);
            _affixText.VAlign = 0.5f;
            Append(_affixText);
        }

        public void SetPending(bool pending)
        {
            _isPending = pending;
            _lockButton.SetEnabled(!pending);
        }

        public void Refresh()
        {
            var item = Main.reforgeItem;
            if (item.IsAir) return;

            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            if (mgr == null || _affixIndex < 0 || _affixIndex >= mgr.Affixes.Count) return;

            var a = mgr.Affixes[_affixIndex];
            string displayText = Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{a.Id}", a.Magnitude);
            _affixText.SetText(displayText);
        }

        // A small clickable square that toggles between locked (gold) and unlocked (gray).
        // Hover shows a tooltip; while disabled the click is suppressed and the visual is dimmed.
        private sealed class LockToggleButton : UIElement
        {
            public bool Locked { get; private set; }
            private bool _enabled = true;

            public LockToggleButton()
            {
                OnLeftClick += (_, _) =>
                {
                    if (!_enabled) return;
                    Locked = !Locked;
                };
            }

            public void SetEnabled(bool enabled) => _enabled = enabled;

            protected override void DrawSelf(SpriteBatch sb)
            {
                var dim = GetDimensions();
                var rect = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);

                Color fill = Locked ? new Color(180, 140, 40) : new Color(70, 70, 70);
                if (!_enabled) fill *= 0.5f;
                Color border = Locked ? new Color(255, 220, 100) : new Color(120, 120, 120);
                if (!_enabled) border *= 0.5f;

                DrawRect(sb, rect, fill);
                DrawBorder(sb, rect, border);

                string glyph = Locked ? "L" : "U";
                Color textColor = Locked ? Color.White : Color.LightGray;
                if (!_enabled) textColor *= 0.5f;
                Utils.DrawBorderString(sb,
                    glyph,
                    new Vector2(dim.X + dim.Width / 2f - 4f, dim.Y + dim.Height / 2f - 8f),
                    textColor,
                    0.85f);

                if (IsMouseHovering && _enabled)
                {
                    string key = Locked
                        ? "Mods.ARPGItemSystem.UI.ReforgePanel.UnlockTooltip"
                        : "Mods.ARPGItemSystem.UI.ReforgePanel.LockTooltip";
                    Main.instance.MouseText(Language.GetTextValue(key));
                }
            }

            private static void DrawRect(SpriteBatch sb, Rectangle rect, Color color)
            {
                sb.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, rect, color);
            }

            private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color color)
            {
                var tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
                sb.Draw(tex, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
            }
        }
    }
}
```

Notes on the changes:
- Removed `_hammerButton`, `_costDisplay`, `OnHammerClicked`, `MarkInsufficient`, the constructor's `tier` parameter — none are needed anymore.
- Removed the `ReforgeConfig` and `ReforgePacketHandler` and `ReforgeUISystem` and `ItemID` and `SoundEngine` and `SoundID` and `TextureAssets` (top-level reforge hammer) imports — they were only used by the removed code.
- `LockToggleButton` is a self-contained nested helper that draws a 22×22 square with a glyph using `TextureAssets.MagicPixel` (a 1×1 white pixel built into vanilla — no asset files needed). Uses tooltip via `Main.instance.MouseText`.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

If you get errors about missing types — check that you removed the unused `using` directives matching the removed code. The required usings for the new file are listed at the top of the code block above; do not add others.

- [ ] **Step 3: Commit**

```bash
git add Common/UI/AffixLine.cs
git commit -m "feat(ui): replace AffixLine hammer with a lock toggle

Each existing-affix row now shows a lock toggle plus the affix text;
the per-line cost display and per-line reroll action are gone. Lock
state is session-only (lives only on the row instance) and exposed
via the public Locked property so the panel can collect it for the
new bottom Reforge button."
```

---

### Task 7: Create `EmptySlotRow`

**Files:**
- Create: `Common/UI/EmptySlotRow.cs`

**Why:** When the slotted item has fewer affixes than the current `GetMaxX()` cap, the panel renders an empty row per missing slot. The row has a `+` button on the left and a placeholder text on the right; clicking `+` triggers a fill operation through the panel.

- [ ] **Step 1: Create the file**

```csharp
using System;
using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // One row representing a missing prefix or suffix slot the player may pay to fill.
    // Shows a "+" button on the left and a muted placeholder text on the right.
    public class EmptySlotRow : UIElement
    {
        private readonly PlusButton _plusButton;
        private readonly UIText _placeholderText;
        private readonly AffixKind _kind;
        private bool _enabled = true;

        public AffixKind Kind => _kind;

        public EmptySlotRow(AffixKind kind, Action onPlusClicked)
        {
            _kind = kind;
            Height.Set(28, 0f);

            _plusButton = new PlusButton(() =>
            {
                if (!_enabled) return;
                onPlusClicked?.Invoke();
            });
            _plusButton.Width.Set(22, 0f);
            _plusButton.Height.Set(22, 0f);
            _plusButton.Left.Set(0, 0f);
            _plusButton.VAlign = 0.5f;
            Append(_plusButton);

            string placeholderKey = kind == AffixKind.Prefix
                ? "Mods.ARPGItemSystem.UI.ReforgePanel.EmptyPrefixSlot"
                : "Mods.ARPGItemSystem.UI.ReforgePanel.EmptySuffixSlot";

            _placeholderText = new UIText(Language.GetTextValue(placeholderKey), 0.85f);
            _placeholderText.TextColor = new Color(140, 140, 140);
            _placeholderText.Left.Set(28, 0f);
            _placeholderText.VAlign = 0.5f;
            Append(_placeholderText);
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _plusButton.SetEnabled(enabled);
        }

        // A small clickable square with a "+" glyph. Click invokes the supplied action.
        private sealed class PlusButton : UIElement
        {
            private readonly Action _onClick;
            private bool _enabled = true;

            public PlusButton(Action onClick)
            {
                _onClick = onClick;
                OnLeftClick += (_, _) =>
                {
                    if (!_enabled) return;
                    _onClick?.Invoke();
                };
            }

            public void SetEnabled(bool enabled) => _enabled = enabled;

            protected override void DrawSelf(SpriteBatch sb)
            {
                var dim = GetDimensions();
                var rect = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);

                Color fill = new Color(40, 90, 50);
                Color border = new Color(120, 200, 130);
                if (!_enabled) { fill *= 0.5f; border *= 0.5f; }

                DrawRect(sb, rect, fill);
                DrawBorder(sb, rect, border);

                Color textColor = _enabled ? Color.White : Color.Gray;
                Utils.DrawBorderString(sb,
                    "+",
                    new Vector2(dim.X + dim.Width / 2f - 4f, dim.Y + dim.Height / 2f - 8f),
                    textColor,
                    0.95f);

                if (IsMouseHovering && _enabled)
                {
                    Main.instance.MouseText(
                        Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.AddAffixTooltip"));
                }
            }

            private static void DrawRect(SpriteBatch sb, Rectangle rect, Color color)
            {
                sb.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, rect, color);
            }

            private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color color)
            {
                var tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
                sb.Draw(tex, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Common/UI/EmptySlotRow.cs
git commit -m "feat(ui): add EmptySlotRow with plus button

Used by the panel rewrite to render missing-slot rows when an item's
affix count is below the current progression cap. Click invokes the
caller-supplied action (the panel wires it to FillEmptySlot)."
```

---

### Task 8: Rewrite `ReforgePanel` with bottom button + empty rows + live cost

**Files:**
- Modify: `Common/UI/ReforgePanel.cs`
- Modify: `Common/Network/ReforgePacketHandler.cs` (replace the two TODO comments from Task 4)

**Why:** The panel composes the new row types, drives the `Reroll All Unlocked` flow when the bottom hammer is clicked, drives the `Fill Empty Slot` flow when an empty row's `+` is clicked, and recomputes the displayed cost every tick (to reflect lock toggles and item changes).

- [ ] **Step 1: Replace `Common/UI/ReforgePanel.cs` entirely**

```csharp
using System.Collections.Generic;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class ReforgePanel : UIState
    {
        private UIPanel _panel;
        private UIReforgeSlot _slot;
        private UIText _itemName;
        private UIText _placeholder;

        private readonly List<AffixLine> _affixLines = new();
        private readonly List<EmptySlotRow> _emptyRows = new();

        private UIImageButton _reforgeButton;
        private UICostDisplay _reforgeCost;
        private UIText _reforgeHint;

        private int _lastItemType = -1;
        private int _lastItemNetID = -1;
        private int _lastAffixCount = -1;

        public override void OnInitialize()
        {
            _panel = new UIPanel();
            _panel.Width.Set(420, 0f);
            _panel.Height.Set(460, 0f);
            _panel.HAlign = 0.5f;
            _panel.VAlign = 0.5f;
            Append(_panel);

            var titleText = new UIText(Language.GetText("Mods.ARPGItemSystem.UI.ReforgePanel.Title"));
            var title = new UIPanel();
            title.Width.Set(0, 1f);
            title.Height.Set(30, 0f);
            title.Top.Set(-12, 0f);
            titleText.HAlign = 0.5f;
            titleText.VAlign = 0.5f;
            title.Append(titleText);
            _panel.Append(title);

            _slot = new UIReforgeSlot();
            _slot.HAlign = 0.5f;
            _slot.Top.Set(24, 0f);
            _panel.Append(_slot);

            _itemName = new UIText("", 0.9f);
            _itemName.HAlign = 0.5f;
            _itemName.Top.Set(84, 0f);
            _panel.Append(_itemName);

            _placeholder = new UIText(Language.GetText("Mods.ARPGItemSystem.UI.ReforgePanel.Placeholder"), 0.85f)
            {
                TextColor = Color.Gray,
                HAlign = 0.5f
            };
            _placeholder.Top.Set(110, 0f);
            _panel.Append(_placeholder);

            _reforgeButton = new UIImageButton(TextureAssets.Reforge[0]);
            _reforgeButton.Width.Set(28, 0f);
            _reforgeButton.Height.Set(28, 0f);
            _reforgeButton.Left.Set(20, 0f);
            _reforgeButton.Top.Set(-44, 1f);    // 44px above the panel's bottom edge
            _reforgeButton.OnLeftClick += OnReforgeClicked;
            _panel.Append(_reforgeButton);

            _reforgeCost = new UICostDisplay(0);
            _reforgeCost.Left.Set(0, 0f);
            _reforgeCost.HAlign = 1f;
            _reforgeCost.Top.Set(-40, 1f);
            _panel.Append(_reforgeCost);

            _reforgeHint = new UIText("", 0.8f);
            _reforgeHint.HAlign = 0.5f;
            _reforgeHint.Top.Set(-12, 1f);
            _reforgeHint.TextColor = new Color(180, 180, 180);
            _panel.Append(_reforgeHint);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Sync our slot's item to Main.reforgeItem so AffixLine / packet
            // handler code that reads Main.reforgeItem still works correctly.
            Main.reforgeItem = _slot.SlotItem;

            bool hasItem = !_slot.SlotItem.IsAir;
            int currentType = hasItem ? _slot.SlotItem.type : -1;
            int currentNetID = hasItem ? _slot.SlotItem.netID : -1;
            int currentAffixCount = hasItem ? GetAffixCount(_slot.SlotItem) : -1;

            // Rebuild rows when the slotted item changes, or when the affix count
            // shifts under us (e.g., after a fill operation appends one).
            if (hasItem && (currentType != _lastItemType
                            || currentNetID != _lastItemNetID
                            || currentAffixCount != _lastAffixCount))
            {
                RefreshRows();
                _lastItemType = currentType;
                _lastItemNetID = currentNetID;
                _lastAffixCount = currentAffixCount;
            }
            else if (!hasItem && (_affixLines.Count > 0 || _emptyRows.Count > 0))
            {
                ClearRows();
            }

            _placeholder.TextColor = hasItem ? Color.Transparent : Color.Gray;
            _itemName.SetText(hasItem ? _slot.SlotItem.Name : "");

            UpdateReforgeButtonState();
        }

        public bool SlotIsEmpty => _slot.SlotItem.IsAir;

        public void ClearSlot()
        {
            if (!_slot.SlotItem.IsAir)
            {
                if (!Main.reforgeItem.IsAir)
                {
                    if (Main.mouseItem.IsAir)
                        Main.mouseItem = _slot.SlotItem;
                    else
                    {
                        for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++)
                        {
                            if (Main.LocalPlayer.inventory[i].IsAir)
                            {
                                Main.LocalPlayer.inventory[i] = _slot.SlotItem;
                                break;
                            }
                        }
                    }
                }
            }
            _slot.SlotItem = new Item();
            Main.reforgeItem = new Item();
            ClearRows();
        }

        public void ReturnItemToPlayer()
        {
            if (_slot.SlotItem.IsAir) return;
            if (Main.mouseItem.IsAir)
                Main.mouseItem = _slot.SlotItem;
            else
            {
                for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++)
                {
                    if (Main.LocalPlayer.inventory[i].IsAir)
                    {
                        Main.LocalPlayer.inventory[i] = _slot.SlotItem;
                        break;
                    }
                }
            }
            _slot.SlotItem = new Item();
            Main.reforgeItem = new Item();
            ClearRows();
        }

        public void RefreshAffix(int index)
        {
            if (index >= 0 && index < _affixLines.Count)
                _affixLines[index].Refresh();
            SetAllPending(false);
        }

        public void SetAllPending(bool pending)
        {
            foreach (var line in _affixLines) line.SetPending(pending);
            foreach (var row in _emptyRows) row.SetEnabled(!pending);
            _reforgeButton?.SetVisibility(pending ? 0.4f : 1.0f, pending ? 0.4f : 1.0f);
        }

        // Called by the network handler after FillEmptySlot succeeds; the affix list
        // grew so we need to drop the old empty row and add a new affix row.
        public void RebuildRowsAfterFill()
        {
            SetAllPending(false);
            // The Update loop's _lastAffixCount drift detection will trigger
            // RefreshRows() naturally on the next tick. Nothing else to do here.
        }

        private void RefreshRows()
        {
            ClearRows();
            var item = _slot.SlotItem;
            if (item.IsAir) return;

            var mgr = GetManager(item);
            if (mgr == null) return;

            float yOffset = 110f;

            // Existing-affix rows, in storage order.
            for (int i = 0; i < mgr.Affixes.Count; i++)
            {
                var a = mgr.Affixes[i];
                var def = AffixRegistry.Get(a.Id);
                string text = Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{a.Id}", a.Magnitude);
                bool isPrefix = def.Kind == AffixKind.Prefix;

                var line = new AffixLine(text, i, isPrefix);
                line.Top.Set(yOffset, 0f);
                line.Width.Set(-20, 1f);
                line.Left.Set(10, 0f);
                _panel.Append(line);
                _affixLines.Add(line);
                yOffset += 32f;
            }

            // Empty-slot rows, separated by kind. Add as many as the cap exceeds the count.
            int existingPrefixes = CountByKind(mgr.Affixes, AffixKind.Prefix);
            int existingSuffixes = CountByKind(mgr.Affixes, AffixKind.Suffix);
            int maxPrefixes = GetMaxPrefixes(mgr.Category);
            int maxSuffixes = GetMaxSuffixes(mgr.Category);

            for (int i = existingPrefixes; i < maxPrefixes; i++)
            {
                var row = new EmptySlotRow(AffixKind.Prefix, () => OnFillEmptyClicked(AffixKind.Prefix));
                row.Top.Set(yOffset, 0f);
                row.Width.Set(-20, 1f);
                row.Left.Set(10, 0f);
                _panel.Append(row);
                _emptyRows.Add(row);
                yOffset += 32f;
            }

            for (int i = existingSuffixes; i < maxSuffixes; i++)
            {
                var row = new EmptySlotRow(AffixKind.Suffix, () => OnFillEmptyClicked(AffixKind.Suffix));
                row.Top.Set(yOffset, 0f);
                row.Width.Set(-20, 1f);
                row.Left.Set(10, 0f);
                _panel.Append(row);
                _emptyRows.Add(row);
                yOffset += 32f;
            }
        }

        private void ClearRows()
        {
            foreach (var line in _affixLines) _panel.RemoveChild(line);
            foreach (var row in _emptyRows) _panel.RemoveChild(row);
            _affixLines.Clear();
            _emptyRows.Clear();
            _lastItemType = -1;
            _lastItemNetID = -1;
            _lastAffixCount = -1;
        }

        private void UpdateReforgeButtonState()
        {
            int unlockedCount = 0;
            int lockedCount = 0;
            foreach (var line in _affixLines)
            {
                if (line.Locked) lockedCount++;
                else unlockedCount++;
            }

            if (_slot.SlotItem.IsAir || _affixLines.Count == 0)
            {
                _reforgeCost.Cost = 0;
                _reforgeButton.SetVisibility(0.4f, 0.4f);
                _reforgeHint.SetText("");
                return;
            }

            if (unlockedCount == 0)
            {
                _reforgeCost.Cost = 0;
                _reforgeButton.SetVisibility(0.4f, 0.4f);
                _reforgeHint.SetText(Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.AllLockedHint"));
                return;
            }

            int itemValue = _slot.SlotItem.value;
            long sum = 0;
            var mgr = GetManager(_slot.SlotItem);
            if (mgr != null)
            {
                for (int i = 0; i < _affixLines.Count && i < mgr.Affixes.Count; i++)
                {
                    if (_affixLines[i].Locked) continue;
                    sum += ReforgeConfig.CalculateCost(itemValue, mgr.Affixes[i].Tier);
                }
            }
            _reforgeCost.Cost = (int)(sum * ReforgeConfig.LockMultiplier(lockedCount));
            _reforgeButton.SetVisibility(1.0f, 1.0f);
            _reforgeHint.SetText("");
        }

        private void OnReforgeClicked(UIMouseEvent evt, UIElement listening)
        {
            if (_slot.SlotItem.IsAir || _affixLines.Count == 0) return;

            var mgr = GetManager(_slot.SlotItem);
            if (mgr == null) return;

            var unlocked = new List<(byte index, AffixKind kind, int storedTier)>();
            var locked = new List<(AffixKind kind, AffixId id)>();

            for (int i = 0; i < _affixLines.Count && i < mgr.Affixes.Count; i++)
            {
                var a = mgr.Affixes[i];
                var kind = AffixRegistry.Get(a.Id).Kind;
                if (_affixLines[i].Locked)
                    locked.Add((kind, a.Id));
                else
                    unlocked.Add(((byte)i, kind, a.Tier));
            }

            if (unlocked.Count == 0) return;

            // Affordability pre-check (server still validates authoritatively).
            if (!Main.LocalPlayer.CanAfford(_reforgeCost.Cost))
            {
                SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/Item_194"));
                return;
            }

            SoundEngine.PlaySound(SoundID.Item37);
            SetAllPending(true);

            var cat = ReforgePacketHandler.GetItemCategory(_slot.SlotItem);
            var damCat = ReforgePacketHandler.GetDamageCategory(_slot.SlotItem);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoRerollAllUnlockedDirectly(
                    _slot.SlotItem, cat, damCat, _slot.SlotItem.value, unlocked, locked);
            }
            else
            {
                ReforgePacketHandler.SendRerollAllUnlockedRequest(
                    cat, damCat, _slot.SlotItem.value, unlocked, locked);
            }
        }

        private void OnFillEmptyClicked(AffixKind kind)
        {
            if (_slot.SlotItem.IsAir) return;
            var mgr = GetManager(_slot.SlotItem);
            if (mgr == null) return;

            int cost = ReforgePacketHandler.ComputeEmptySlotCost(_slot.SlotItem.value);
            if (!Main.LocalPlayer.CanAfford(cost))
            {
                SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/Item_194"));
                return;
            }

            // Exclude all current same-kind affixes so the new roll doesn't duplicate them.
            var excludeIds = new List<AffixId>();
            foreach (var a in mgr.Affixes)
            {
                if (AffixRegistry.Get(a.Id).Kind == kind)
                    excludeIds.Add(a.Id);
            }

            SoundEngine.PlaySound(SoundID.Item37);
            SetAllPending(true);

            var cat = ReforgePacketHandler.GetItemCategory(_slot.SlotItem);
            var damCat = ReforgePacketHandler.GetDamageCategory(_slot.SlotItem);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoFillEmptySlotDirectly(
                    _slot.SlotItem, cat, damCat, kind, _slot.SlotItem.value, excludeIds);
            }
            else
            {
                ReforgePacketHandler.SendFillEmptySlotRequest(
                    cat, damCat, kind, _slot.SlotItem.value, excludeIds);
            }
        }

        private static AffixItemManager GetManager(Item item)
        {
            if (item.IsAir) return null;
            return item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();
        }

        private static int GetAffixCount(Item item)
        {
            var mgr = GetManager(item);
            return mgr?.Affixes.Count ?? 0;
        }

        private static int CountByKind(List<Affix> list, AffixKind kind)
        {
            int n = 0;
            foreach (var a in list)
                if (AffixRegistry.Get(a.Id).Kind == kind) n++;
            return n;
        }

        private static int GetMaxPrefixes(ItemCategory cat) => cat switch
        {
            ItemCategory.Weapon => utils.GetMaxPrefixesWeapon(),
            ItemCategory.Armor => utils.GetMaxPrefixesArmor(),
            ItemCategory.Accessory => utils.GetMaxPrefixesAccessory(),
            _ => 0
        };

        private static int GetMaxSuffixes(ItemCategory cat) => cat switch
        {
            ItemCategory.Weapon => utils.GetMaxSuffixesWeapon(),
            ItemCategory.Armor => utils.GetMaxSuffixesArmor(),
            ItemCategory.Accessory => utils.GetMaxSuffixesAccessory(),
            _ => 0
        };
    }
}
```

- [ ] **Step 2: Wire up `RebuildRowsAfterFill` in the packet handler**

In `Common/Network/ReforgePacketHandler.cs`, replace each TODO block left from Task 4:

In `HandleFillEmptySlotResult`, replace:
```csharp
AppendAffix(item, newId, newMagnitude, newTier);
// TODO(plan-task-8): replace with Panel.RebuildRowsAfterFill();
ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
```
with:
```csharp
AppendAffix(item, newId, newMagnitude, newTier);
ModContent.GetInstance<ReforgeUISystem>().Panel?.RebuildRowsAfterFill();
```

In `DoFillEmptySlotDirectly`, replace:
```csharp
AppendAffix(item, newId, newMagnitude, newTier);
// TODO(plan-task-8): replace with Panel.RebuildRowsAfterFill();
ModContent.GetInstance<ReforgeUISystem>().Panel?.SetAllPending(false);
```
with:
```csharp
AppendAffix(item, newId, newMagnitude, newTier);
ModContent.GetInstance<ReforgeUISystem>().Panel?.RebuildRowsAfterFill();
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add Common/UI/ReforgePanel.cs Common/Network/ReforgePacketHandler.cs
git commit -m "feat(ui): rewrite ReforgePanel with bottom button + empty rows

- Single Reforge button at the bottom; cost recomputed live from lock state
- Empty prefix/suffix rows appear when GetMaxX exceeds current count
- ClickPlus dispatches FillEmptySlot (single-player or networked)
- Click Reforge dispatches RerollAllUnlocked (single-player or networked)
- All-locked state disables the button with a hint string
- Wires up Panel.RebuildRowsAfterFill so the FillEmptySlot result
  triggers a row rebuild on the next Update tick"
```

---

### Task 9: Remove the obsolete per-line packet flow

**Files:**
- Modify: `Common/Network/ReforgePacketHandler.cs`

**Why:** Nothing references `RerollRequest`/`RerollResult`/`RerollRejected` anymore. Time to delete them. We also drop the now-unused helpers `SendRerollRequest`, `DoRerollDirectly`, `GetExcludeIds`.

- [ ] **Step 1: Remove the obsolete enum members**

In `Common/Network/ReforgePacketHandler.cs`, replace:

```csharp
public enum ReforgePacketType : byte
{
    // Old per-line flow — removed once UI cutover lands.
    RerollRequest = 0,
    RerollResult = 1,
    RerollRejected = 2,

    // New flow — multi-line reroll with locks, plus empty-slot fill.
    RerollAllUnlockedRequest = 3,
    RerollAllUnlockedResult = 4,
    RerollAllUnlockedRejected = 5,
    FillEmptySlotRequest = 6,
    FillEmptySlotResult = 7,
    FillEmptySlotRejected = 8
}
```

with:

```csharp
public enum ReforgePacketType : byte
{
    RerollAllUnlockedRequest = 0,
    RerollAllUnlockedResult = 1,
    RerollAllUnlockedRejected = 2,
    FillEmptySlotRequest = 3,
    FillEmptySlotResult = 4,
    FillEmptySlotRejected = 5
}
```

(Renumbering is safe — packets aren't persisted across sessions; both client and server build off the same code.)

- [ ] **Step 2: Remove obsolete switch cases**

In `HandlePacket`, delete these three lines:

```csharp
        case ReforgePacketType.RerollRequest:               HandleRerollRequest(reader, whoAmI);             break;
        case ReforgePacketType.RerollResult:                HandleRerollResult(reader);                      break;
        case ReforgePacketType.RerollRejected:              HandleRerollRejected(reader);                    break;
```

- [ ] **Step 3: Remove obsolete handler methods**

Delete these methods in their entirety:
- `SendRerollRequest`
- `HandleRerollRequest`
- `HandleRerollResult`
- `HandleRerollRejected`
- `DoRerollDirectly`
- `GetExcludeIds`

Keep these — they're used by the new flow:
- `RollReplacement`
- `GetItemCategory`
- `GetDamageCategory`
- `GetDamageClass`

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded`. Any errors here mean a leftover caller — search the codebase for `SendRerollRequest`, `DoRerollDirectly`, or `GetExcludeIds` and either remove the call site or migrate it to the new flow.

- [ ] **Step 5: Commit**

```bash
git add Common/Network/ReforgePacketHandler.cs
git commit -m "chore(net): remove obsolete per-line reroll packet flow

The new RerollAllUnlocked + FillEmptySlot flow replaces the per-line
reroll completely. Drops Send/Handle/Do/GetExclude helpers and
renumbers the packet enum to 0..5 (no compatibility concern — packets
are session-scoped and both endpoints share this code)."
```

---

### Task 10: In-game verification checklist

**Files:** none — manual testing only.

**Why:** This codebase has no automated tests. Real verification requires running the mod inside tModLoader and exercising every code path. The commit at the end records that the manual pass succeeded.

- [ ] **Step 1: Build and load the mod in tModLoader**

In tModLoader: Workshop → Mod Sources → select `ARPGItemSystem` → Build & Reload. The build should succeed; if a localization warning appears, ensure all new keys you added in Task 5 are present.

- [ ] **Step 2: Single-player smoke pass**

Start a fresh single-player world, give yourself coin and a few items, talk to the Goblin Tinkerer to open the reforge panel.

For each of the following, place the appropriate item in the slot and verify the listed behavior:

- **Weapon (e.g., Wooden Sword)** — exactly the rolled affixes show as rows with lock toggles. Bottom Reforge button shows a coin cost. Click Reforge → coin deducted, affix text updates on each unlocked row, locked lines (if any) preserved exactly. Click any lock to toggle gold ↔ gray; cost recomputes immediately.
- **Armor piece** — same as weapon. Some armor pieces start with no affixes (early-game accessory cap is 0); panel should still show empty rows and let you `+`-fill.
- **Accessory** — same. Confirm accessory-specific affix pool by rerolling several times.

Observable failures to watch for:
- Cost stays 0 → `_reforgeCost` not updating; check `UpdateReforgeButtonState`.
- Lock toggle doesn't change visually → `LockToggleButton.DrawSelf` not invalidating; should be fine but worth eyeballing.
- Reforge does nothing → check chat/log for unhandled packet exceptions.
- Locked line gets rerolled anyway → bug in the unlocked/locked split in `OnReforgeClicked`.

- [ ] **Step 3: Empty slot fill — controlled progression test**

Start a fresh world, kill bosses in order, and after each boss visit the reforge panel with the same low-tier weapon you crafted at world start:

1. Pre-Eye of Cthulhu — weapon should show 1 prefix + 1 suffix, no empty rows.
2. After Eater of Worlds / Brain of Cthulhu (`NPC.downedBoss2 = true`) — weapon should now show an "Empty Suffix Slot" row (since `GetMaxSuffixesWeapon` rose to 2). Click `+`, watch coin be deducted, watch a new affix row appear in place of the empty row. The new affix's tier should be in the current band.
3. After Skeletron (`NPC.downedBoss3 = true`) — empty prefix slot appears. Fill it.
4. Continue to hardmode → mech bosses → Plantera → Golem and verify each cap milestone produces a new empty row that can be filled.

Observable failures to watch for:
- Empty rows don't appear → `GetMaxX` helpers in `utils.cs` don't match the boss flags used in `GetAmountOfX` (Task 1); cross-check.
- Filling an empty row creates a duplicate affix that's already on the item → exclude list in `OnFillEmptyClicked` is wrong; should pull from `mgr.Affixes` filtered by kind.
- Filling appends to the wrong end (suffix becomes prefix on the panel) → the panel display logic uses `AffixRegistry.Get(a.Id).Kind` for grouping; storage order doesn't matter.

- [ ] **Step 4: Multiplayer smoke**

Host a multiplayer server (in-game `Multiplayer → Host & Play`), connect a second client (or use a multiplayer mod test setup). On client A:

- Reforge an item → confirm coin is deducted on the server-authoritative side, affixes change.
- Click `+` to fill an empty slot → confirm the new affix appears, coin deducted.
- Lock all lines and try to Reforge → button should be disabled, click does nothing.
- Try to Reforge with insufficient coin → server sends rejection, panel returns to ready state, no coin deducted.

The other client (B) shouldn't be affected by A's reforge — verify no item state crosses between players.

- [ ] **Step 5: Edge cases**

- ESC closes panel cleanly with the item returned (existing behavior preserved).
- Walking away from the Goblin Tinkerer closes the panel and returns the item.
- Hovering the lock button shows the localized tooltip.
- Hovering the `+` button shows the localized tooltip.
- All-locked state shows the localized "unlock at least one affix" hint.
- Cost text turns red when player can't afford; sound effect plays on rejected click.

- [ ] **Step 6: Commit the verification record**

If everything above passed:

```bash
git commit --allow-empty -m "test: reforge overhaul verified in-game

Single-player and multiplayer smoke passed:
- Reforge button rerolls all unlocked, locks preserved
- Lock multiplier correctly inflates cost as locks accumulate
- Empty rows appear at every cap-growth milestone and fill correctly
- All-locked state disables the button with the localized hint
- Affordability rejection (client-side and server-side) works
- Tooltips render correctly on lock and plus buttons
- Item-return on ESC and Goblin-Tinkerer-walks-away preserved"
```

If something failed: do not make the commit above. Open a new task to fix the failure, fix, re-verify, then commit.

---

## Self-Review

**Spec coverage check (against [the design doc](../specs/2026-05-01-reforge-overhaul-design.md)):**
- "Empty slot rows ... `+` button" — Task 7 (`EmptySlotRow`) + Task 8 (panel renders empty rows).
- "Lock button replaces hammer ... session-only" — Task 6 rewrites `AffixLine` to lock toggle; locks live on the row instance only.
- "Bottom hammer button rerolls all unlocked" — Task 8 (`OnReforgeClicked`).
- "Cost shown next to bottom hammer; recomputed live" — Task 8 (`UpdateReforgeButtonState` runs every Update tick).
- "Disabled state when all locked" — Task 8 (`UpdateReforgeButtonState` sets visibility + hint).
- Cost formula `unlockedCost × LockMultiplier(numLocked)` — Task 2 + Task 4 (`ComputeRerollAllUnlockedCost`) + Task 8 (live preview matches).
- Empty slot cost `CalculateCost(itemValue, GetBestTier()) × EmptySlotMultiplier` — Task 1 (`GetBestTier`) + Task 2 (`EmptySlotMultiplier`) + Task 4 (`ComputeEmptySlotCost`).
- 6 deterministic `GetMaxX` helpers — Task 1.
- Server-authoritative packet flow with rejection on insufficient coin — Task 4.
- Per-kind running exclude set during `RollMultipleReplacements` — Task 4.
- Old per-line packet types removed — Task 9.
- New localization keys — Task 5.
- `AffixItemManager` save format unchanged — confirmed; no task touches it.

**Placeholder scan:** Searched the plan for "TODO", "TBD", "implement later", "similar to", "etc". The `TODO(plan-task-8)` markers in Task 4 are intentional and explicitly resolved in Task 8 Step 2. No other placeholders.

**Type consistency check:**
- `AffixLine` constructor: `(string displayText, int affixIndex, bool isPrefix)` in Task 6; called by panel in Task 8 with the same signature.
- `EmptySlotRow` constructor: `(AffixKind kind, Action onPlusClicked)` in Task 7; called by panel in Task 8 with the same signature.
- `Panel.RebuildRowsAfterFill()` declared in Task 8; called from `HandleFillEmptySlotResult` and `DoFillEmptySlotDirectly` in Task 8 Step 2 (after the rename from the Task 4 TODO).
- `ReforgePacketHandler.ComputeEmptySlotCost(int)` declared in Task 4; called in Task 8 (`OnFillEmptyClicked`).
- `ReforgePacketHandler.SendRerollAllUnlockedRequest(...)` and `DoRerollAllUnlockedDirectly(...)` parameters: `(ItemCategory, WeaponDamageCategory, int, List<(byte,AffixKind,int)>, List<(AffixKind,AffixId)>)` — Task 4 declarations match Task 8 call sites.
- `ReforgePacketHandler.SendFillEmptySlotRequest(...)` and `DoFillEmptySlotDirectly(...)` parameters: `(ItemCategory, WeaponDamageCategory, AffixKind, int, List<AffixId>)` — Task 4 declarations match Task 8 call sites.
- Existing public helpers `GetItemCategory`, `GetDamageCategory` are reused in Task 8 — they survive Task 9 deletion list.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-01-reforge-overhaul.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
