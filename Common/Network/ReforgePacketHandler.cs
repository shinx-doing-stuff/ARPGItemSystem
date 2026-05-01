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
        RerollRequest = 0,
        RerollResult = 1,
        RerollRejected = 2
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
                case ReforgePacketType.RerollRequest:  HandleRerollRequest(reader, whoAmI); break;
                case ReforgePacketType.RerollResult:   HandleRerollResult(reader);          break;
                case ReforgePacketType.RerollRejected: HandleRerollRejected(reader);        break;
            }
        }

        public static void SendRerollRequest(int affixIndex, AffixKind kind, ItemCategory cat,
            WeaponDamageCategory damCat, int itemValue, List<AffixId> excludeIds)
        {
            var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            packet.Write((byte)ReforgePacketType.RerollRequest);
            packet.Write((byte)affixIndex);
            packet.Write((byte)kind);
            packet.Write((byte)cat);
            packet.Write((byte)damCat);
            packet.Write(itemValue);
            packet.Write((byte)excludeIds.Count);
            foreach (var id in excludeIds) packet.Write((int)id);
            packet.Send();
        }

        private static void HandleRerollRequest(BinaryReader reader, int whoAmI)
        {
            byte affixIndex = reader.ReadByte();
            var kind = (AffixKind)reader.ReadByte();
            var cat = (ItemCategory)reader.ReadByte();
            var damCat = (WeaponDamageCategory)reader.ReadByte();
            int itemValue = reader.ReadInt32();
            byte excludeCount = reader.ReadByte();
            var excludeIds = new List<AffixId>(excludeCount);
            for (int i = 0; i < excludeCount; i++) excludeIds.Add((AffixId)reader.ReadInt32());

            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(itemValue, tier);
            var player = Main.player[whoAmI];

            if (!player.BuyItem(cost))
            {
                var rejection = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
                rejection.Write((byte)ReforgePacketType.RerollRejected);
                rejection.Write(affixIndex);
                rejection.Send(whoAmI);
                return;
            }

            RollReplacement(cat, kind, damCat, excludeIds, tier,
                out AffixId newId, out int newMagnitude);

            var result = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            result.Write((byte)ReforgePacketType.RerollResult);
            result.Write(affixIndex);
            result.Write((int)newId);
            result.Write(newMagnitude);
            result.Write(tier);
            result.Send(whoAmI);
        }

        private static void HandleRerollResult(BinaryReader reader)
        {
            if (Main.netMode == NetmodeID.Server) return;

            byte affixIndex = reader.ReadByte();
            var newId = (AffixId)reader.ReadInt32();
            int newMagnitude = reader.ReadInt32();
            int newTier = reader.ReadInt32();

            var item = Main.reforgeItem;
            if (item.IsAir) return;

            ApplyAffixReplacement(item, affixIndex, newId, newMagnitude, newTier);
            ModContent.GetInstance<ReforgeUISystem>().Panel.RefreshAffix(affixIndex);
        }

        private static void HandleRerollRejected(BinaryReader reader)
        {
            if (Main.netMode == NetmodeID.Server) return;
            reader.ReadByte();
            ModContent.GetInstance<ReforgeUISystem>().Panel.SetAllPending(false);
        }

        public static void DoRerollDirectly(Item item, int affixIndex, AffixKind kind,
            ItemCategory cat, WeaponDamageCategory damCat, List<AffixId> excludeIds)
        {
            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(item.value, tier);

            if (!Main.LocalPlayer.BuyItem(cost))
            {
                ModContent.GetInstance<ReforgeUISystem>().Panel.SetAllPending(false);
                return;
            }

            RollReplacement(cat, kind, damCat, excludeIds, tier,
                out AffixId newId, out int newMagnitude);

            ApplyAffixReplacement(item, affixIndex, newId, newMagnitude, tier);
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

        public static List<AffixId> GetExcludeIds(Item item, int affixIndex)
        {
            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            var result = new List<AffixId>();
            if (mgr == null || affixIndex < 0 || affixIndex >= mgr.Affixes.Count) return result;

            var targetKind = AffixRegistry.Get(mgr.Affixes[affixIndex].Id).Kind;
            for (int i = 0; i < mgr.Affixes.Count; i++)
            {
                if (i == affixIndex) continue;
                var a = mgr.Affixes[i];
                if (AffixRegistry.Get(a.Id).Kind == targetKind)
                    result.Add(a.Id);
            }
            return result;
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
