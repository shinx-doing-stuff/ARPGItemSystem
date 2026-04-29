using System.Collections.Generic;
using System.IO;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Weapon = ARPGItemSystem.Common.GlobalItems.Weapon;
using Armor = ARPGItemSystem.Common.GlobalItems.Armor;
using Accessory = ARPGItemSystem.Common.GlobalItems.Accessory;

namespace ARPGItemSystem.Common.Network
{
    public enum ReforgePacketType : byte
    {
        RerollRequest = 0,
        RerollResult = 1,
        RerollRejected = 2
    }

    public enum ItemCategory : byte
    {
        Weapon = 0,
        Armor = 1,
        Accessory = 2
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
                case ReforgePacketType.RerollRequest:
                    HandleRerollRequest(reader, whoAmI);
                    break;
                case ReforgePacketType.RerollResult:
                    HandleRerollResult(reader);
                    break;
                case ReforgePacketType.RerollRejected:
                    HandleRerollRejected(reader);
                    break;
            }
        }

        public static void SendRerollRequest(int affixIndex, bool isPrefix, ItemCategory cat, WeaponDamageCategory damCat, int itemValue, List<int> excludeList)
        {
            var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            packet.Write((byte)ReforgePacketType.RerollRequest);
            packet.Write((byte)affixIndex);
            packet.Write(isPrefix);
            packet.Write((byte)cat);
            packet.Write((byte)damCat);
            packet.Write(itemValue);
            packet.Write((byte)excludeList.Count);
            foreach (var id in excludeList) packet.Write((byte)id);
            packet.Send();
        }

        private static void HandleRerollRequest(BinaryReader reader, int whoAmI)
        {
            byte affixIndex = reader.ReadByte();
            bool isPrefix = reader.ReadBoolean();
            var cat = (ItemCategory)reader.ReadByte();
            var damCat = (WeaponDamageCategory)reader.ReadByte();
            int itemValue = reader.ReadInt32();
            byte excludeCount = reader.ReadByte();
            var excludeList = new List<int>();
            for (int i = 0; i < excludeCount; i++) excludeList.Add(reader.ReadByte());

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

            RollNewModifier(cat, damCat, isPrefix, excludeList, tier,
                out int newTypeID, out int newMagnitude, out string newTooltip);

            var result = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            result.Write((byte)ReforgePacketType.RerollResult);
            result.Write(affixIndex);
            result.Write(isPrefix);
            result.Write(newTypeID);
            result.Write(newMagnitude);
            result.Write(tier);
            result.Write(newTooltip);
            result.Send(whoAmI);
        }

        private static void HandleRerollResult(BinaryReader reader)
        {
            byte affixIndex = reader.ReadByte();
            bool isPrefix = reader.ReadBoolean();
            int newTypeID = reader.ReadInt32();
            int newMagnitude = reader.ReadInt32();
            int newTier = reader.ReadInt32();
            string newTooltip = reader.ReadString();

            var item = Main.reforgeItem;
            if (item.IsAir) return;

            ApplyModifierToItem(item, affixIndex, isPrefix, newTypeID, newMagnitude, newTier, newTooltip);
            ModContent.GetInstance<UISystem>().Panel.RefreshAffix(affixIndex);
        }

        private static void HandleRerollRejected(BinaryReader reader)
        {
            reader.ReadByte();
            ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
        }

        public static void DoRerollDirectly(Item item, int affixIndex, bool isPrefix, ItemCategory cat, WeaponDamageCategory damCat, List<int> excludeList)
        {
            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(item.value, tier);

            if (!Main.LocalPlayer.BuyItem(cost))
            {
                ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
                return;
            }

            RollNewModifier(cat, damCat, isPrefix, excludeList, tier,
                out int newTypeID, out int newMagnitude, out string newTooltip);

            ApplyModifierToItem(item, affixIndex, isPrefix, newTypeID, newMagnitude, tier, newTooltip);
        }

        private static void RollNewModifier(ItemCategory cat, WeaponDamageCategory damCat, bool isPrefix, List<int> excludeList, int tier,
            out int newTypeID, out int newMagnitude, out string newTooltip)
        {
            newTypeID = 0; newMagnitude = 0; newTooltip = "";

            switch (cat)
            {
                case ItemCategory.Weapon:
                {
                    var damageClass = GetDamageClass(damCat);
                    var modType = isPrefix ? Weapon.ModifierType.Prefix : Weapon.ModifierType.Suffix;
                    var m = new Weapon.WeaponModifier(modType, excludeList, damageClass, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
                case ItemCategory.Armor:
                {
                    var modType = isPrefix ? Armor.ModifierType.Prefix : Armor.ModifierType.Suffix;
                    var m = new Armor.ArmorModifier(modType, excludeList, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
                case ItemCategory.Accessory:
                {
                    var modType = isPrefix ? Accessory.ModifierType.Prefix : Accessory.ModifierType.Suffix;
                    var m = new Accessory.AccessoryModifier(modType, excludeList, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
            }
        }

        private static void ApplyModifierToItem(Item item, int affixIndex, bool isPrefix, int newTypeID, int newMagnitude, int newTier, string newTooltip)
        {
            if (item.damage > 0 && item.maxStack == 1)
            {
                var manager = item.GetGlobalItem<Weapon.WeaponManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Weapon.PrefixType)newTypeID;
                else mod.suffixType = (Weapon.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
            else if (item.accessory)
            {
                var manager = item.GetGlobalItem<Accessory.AccessoryManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Accessory.PrefixType)newTypeID;
                else mod.suffixType = (Accessory.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
            else
            {
                var manager = item.GetGlobalItem<Armor.ArmorManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Armor.PrefixType)newTypeID;
                else mod.suffixType = (Armor.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
        }

        public static ItemCategory GetItemCategory(Item item)
        {
            if (item.damage > 0 && item.maxStack == 1) return ItemCategory.Weapon;
            if (item.accessory) return ItemCategory.Accessory;
            return ItemCategory.Armor;
        }

        public static WeaponDamageCategory GetDamageCategory(Item item)
        {
            if (item.DamageType == DamageClass.Melee || item.DamageType == DamageClass.MeleeNoSpeed || item.DamageType == DamageClass.SummonMeleeSpeed)
                return WeaponDamageCategory.Melee;
            if (item.DamageType == DamageClass.Ranged) return WeaponDamageCategory.Ranged;
            if (item.DamageType == DamageClass.Magic || item.DamageType == DamageClass.MagicSummonHybrid)
                return WeaponDamageCategory.Magic;
            if (item.DamageType == DamageClass.Summon) return WeaponDamageCategory.Summon;
            return WeaponDamageCategory.Other;
        }

        public static List<int> GetExcludeList(Item item, int affixIndex, bool isPrefix)
        {
            var result = new List<int>();
            if (item.damage > 0 && item.maxStack == 1)
            {
                var list = item.GetGlobalItem<Weapon.WeaponManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Weapon.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Weapon.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
            }
            else if (item.accessory)
            {
                var list = item.GetGlobalItem<Accessory.AccessoryManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Accessory.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Accessory.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
            }
            else
            {
                var list = item.GetGlobalItem<Armor.ArmorManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Armor.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Armor.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
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
