using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Network
{
    public enum ReforgePacketType : byte
    {
        RerollAllUnlockedRequest = 0,
        RerollAllUnlockedResult = 1,
        RerollAllUnlockedRejected = 2,
        FillEmptySlotRequest = 3,
        FillEmptySlotResult = 4,
        FillEmptySlotRejected = 5
    }

    public enum WeaponDamageCategory : byte
    {
        Melee = 0,
        Ranged = 1,
        Magic = 2,
        Summon = 3,
        Other = 4
    }

    public static class ReforgePacketHandler
    {
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var type = (ReforgePacketType)reader.ReadByte();
            switch (type)
            {
                case ReforgePacketType.RerollAllUnlockedRequest:    HandleRerollAllUnlockedRequest(reader, whoAmI);  break;
                case ReforgePacketType.RerollAllUnlockedResult:     HandleRerollAllUnlockedResult(reader);           break;
                case ReforgePacketType.RerollAllUnlockedRejected:   HandleRerollAllUnlockedRejected();               break;
                case ReforgePacketType.FillEmptySlotRequest:        HandleFillEmptySlotRequest(reader, whoAmI);      break;
                case ReforgePacketType.FillEmptySlotResult:         HandleFillEmptySlotResult(reader);               break;
                case ReforgePacketType.FillEmptySlotRejected:       HandleFillEmptySlotRejected();                   break;
            }
        }

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
        //       int  storedTier  (needed so server can include locked costs in total)
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
            List<(AffixKind kind, AffixId id, int storedTier)> locked)
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
                packet.Write(l.storedTier);
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
            var locked = new List<(AffixKind kind, AffixId id, int storedTier)>(numLocked);
            for (int i = 0; i < numLocked; i++)
            {
                var kind = (AffixKind)reader.ReadByte();
                var id = (AffixId)reader.ReadInt32();
                int storedTier = reader.ReadInt32();
                locked.Add((kind, id, storedTier));
            }

            int totalCost = ComputeRerollAllUnlockedCost(itemValue, unlocked, locked);
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
            List<(AffixKind kind, AffixId id, int storedTier)> locked)
        {
            int totalCost = ComputeRerollAllUnlockedCost(itemValue, unlocked, locked);

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
            List<(AffixKind kind, AffixId id, int storedTier)> locked)
        {
            // Charge based on ALL affix tiers (unlocked + locked) so that locking
            // more lines always increases cost rather than reducing the base sum.
            long sum = 0;
            foreach (var u in unlocked)
                sum += ReforgeConfig.CalculateCost(itemValue, u.storedTier);
            foreach (var l in locked)
                sum += ReforgeConfig.CalculateCost(itemValue, l.storedTier);
            return (int)(sum * ReforgeConfig.LockMultiplier(locked.Count));
        }

        private static List<(byte index, AffixId id, int magnitude, int tier)> RollMultipleReplacements(
            ItemCategory cat, WeaponDamageCategory damCat,
            List<(byte index, AffixKind kind, int storedTier)> unlocked,
            List<(AffixKind kind, AffixId id, int storedTier)> locked)
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

        private static void RollReplacement(ItemCategory cat, AffixKind kind,
            WeaponDamageCategory damCat, List<AffixId> excludeIds, int tier,
            out AffixId newId, out int newMagnitude)
        {
            newId = AffixId.None;
            newMagnitude = 0;

            DamageClass weaponClass = cat == ItemCategory.Weapon ? GetDamageClass(damCat) : null;

            var pool = AffixRegistry
                .RollPool(cat, kind, weaponClass)
                .Where(def => !excludeIds.Contains(def.Id))
                .ToList();

            if (pool.Count == 0) return;

            var def = pool[Main.rand.Next(pool.Count)];
            var range = def.Tiers[cat][tier];
            newId = def.Id;
            newMagnitude = Main.rand.Next(range.Min, range.Max + 1);
        }

        private static void ApplyAffixReplacement(Item item, int affixIndex,
            AffixId newId, int newMagnitude, int newTier)
        {
            if (newId == AffixId.None) return;

            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            if (mgr == null || affixIndex < 0 || affixIndex >= mgr.Affixes.Count) return;
            var list = mgr.Affixes;
            list[affixIndex] = new Affix(newId, newMagnitude, newTier);
        }

        public static ItemCategory GetItemCategory(Item item)
        {
            if (item.damage > 0 && item.maxStack <= 1) return ItemCategory.Weapon;
            if (item.accessory) return ItemCategory.Accessory;
            return ItemCategory.Armor;
        }

        public static WeaponDamageCategory GetDamageCategory(Item item)
        {
            if (item.DamageType == DamageClass.Melee
                || item.DamageType == DamageClass.MeleeNoSpeed
                || item.DamageType == DamageClass.SummonMeleeSpeed)
                return WeaponDamageCategory.Melee;
            if (item.DamageType == DamageClass.Ranged) return WeaponDamageCategory.Ranged;
            if (item.DamageType == DamageClass.Magic
                || item.DamageType == DamageClass.MagicSummonHybrid)
                return WeaponDamageCategory.Magic;
            if (item.DamageType == DamageClass.Summon) return WeaponDamageCategory.Summon;
            return WeaponDamageCategory.Other;
        }

        private static DamageClass GetDamageClass(WeaponDamageCategory cat) => cat switch
        {
            WeaponDamageCategory.Melee => DamageClass.Melee,
            WeaponDamageCategory.Ranged => DamageClass.Ranged,
            WeaponDamageCategory.Magic => DamageClass.Magic,
            WeaponDamageCategory.Summon => DamageClass.Summon,
            _ => DamageClass.Generic
        };
    }
}
