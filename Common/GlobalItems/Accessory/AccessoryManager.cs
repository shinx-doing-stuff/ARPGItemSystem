using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.IO;
using System.IO;
using Terraria.Utilities;
using System;
using System.Linq.Expressions;
using log4net.Core;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Terraria.WorldBuilding;
using Terraria.Localization;
using ARPGItemSystem.Common.GlobalItems.Weapon;

namespace ARPGItemSystem.Common.GlobalItems.Accessory
{
    public class AccessoryManager : GlobalItem
    {
        public List<AccessoryModifier> modifierList = new List<AccessoryModifier>();
        public override bool InstancePerEntity => true;

        // This is needed to make sure reference types are cloned properly to new instances
        public override GlobalItem Clone(Item from, Item to)
        {
            var clone = base.Clone(from, to);
            ((AccessoryManager)clone).modifierList = modifierList.ToList();
            return clone;
        }

        // Clear vanilla modifier system
        public override bool? PrefixChance(Item item, int pre, UnifiedRandom rand)
        {
            return pre == -3;
        }

        // Only applies to accessories
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return lateInstantiation && entity.accessory;
        }

        // Roll modifiers on item creation
        public override void OnCreated(Item item, ItemCreationContext context)
        {
            Reroll(item);
        }

        public override bool OnPickup(Item item, Player player)
        {
            if (modifierList.Count == 0)
                Reroll(item);
            return true;
        }

        public void Reroll(Item item)
        {
            modifierList.Clear();
            // Add prefixes
            for (int i = 0; i < utils.GetAmountOfPrefixesAccessory(); i++)
            {
                List<int> excludeList = utils.CreateExcludeList(modifierList, ModifierType.Prefix);
                int tier = utils.GetTier();
                modifierList.Add(new AccessoryModifier(ModifierType.Prefix, excludeList, tier));
            }
            // Add suffixes
            for (int i = 0; i < utils.GetAmountOfSuffixesAccessory(); i++)
            {
                List<int> excludeList = utils.CreateExcludeList(modifierList, ModifierType.Suffix);
                int tier = utils.GetTier();
                modifierList.Add(new AccessoryModifier(ModifierType.Suffix, excludeList, tier));
            }
        }

        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            foreach (var modifier in modifierList)
            {
                switch (modifier.prefixType)
                {
                    case PrefixType.FlatLifeIncrease:
                        player.statLifeMax2 += modifier.magnitude;
                        break;
                    case PrefixType.FlatDefenseIncrease:
                        player.statDefense += modifier.magnitude;
                        break;
                    case PrefixType.FlatManaIncrease:
                        player.statManaMax2 += modifier.magnitude;
                        break;
                }
                switch (modifier.suffixType)
                {
                    case SuffixType.PercentageGenericDamageIncrease:
                        player.GetDamage<GenericDamageClass>() += modifier.magnitude / 100f;
                        break;
                    // Apply first to create pseudo "increased" multiplier
                    case SuffixType.PercentageMeleeDamageIncrease:
                        player.GetDamage<MeleeDamageClass>() += modifier.magnitude / 100f;
                        break;
                    // Apply after flat defense to create pseudo "more" multiplier
                    case SuffixType.PercentageRangedDamageIncrease:
                        player.GetDamage<RangedDamageClass>() += modifier.magnitude / 100f;
                        break;
                    case SuffixType.PercentageMagicDamageIncrease:
                        player.GetDamage<MagicDamageClass>() += modifier.magnitude / 100f;
                        break;
                    case SuffixType.PercentageSummonDamageIncrease:
                        player.GetDamage<SummonDamageClass>() += modifier.magnitude / 100f;
                        break;
                    case SuffixType.FlatCritChance:
                        player.GetCritChance(DamageClass.Generic) += modifier.magnitude;
                        break;
                    case SuffixType.ManaCostReduction:
                        player.manaCost -= modifier.magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            var useManaTip = tooltips.FirstOrDefault(tip => tip.Name == "UseMana" && tip.Mod == "Terraria");
            if (useManaTip is not null)
            {
                useManaTip.Text = Language.GetTextValue("CommonItemTooltip.UsesMana", Main.LocalPlayer.GetManaCost(item));
            }

            foreach (var modifier in modifierList)
            {
                if (modifier.modifierType == ModifierType.Prefix)
                    tooltips.Add(new TooltipLine(Mod, "CustomPrefix", string.Format(modifier.tooltip, modifier.magnitude)) { OverrideColor = Color.LightGreen });
                else
                    tooltips.Add(new TooltipLine(Mod, "CustomPrefix", string.Format(modifier.tooltip, modifier.magnitude)) { OverrideColor = Color.DeepSkyBlue });
            }
        }
        public override void SaveData(Item item, TagCompound tag)
        {
            List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
            List<string> prefixTooltipList;
            List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
            List<string> suffixTooltipList;
            SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                          out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

            tag["PrefixIDList"] = prefixIDList; tag["PrefixMagnitudeList"] = prefixMagnitudeList;
            tag["PrefixTooltipList"] = prefixTooltipList; tag["PrefixTierList"] = prefixTierList;
            tag["SuffixIDList"] = suffixIDList; tag["SuffixMagnitudeList"] = suffixMagnitudeList;
            tag["SuffixTooltipList"] = suffixTooltipList; tag["SuffixTierList"] = suffixTierList;
        }

        public override void LoadData(Item item, TagCompound tag)
        {
            if (!tag.ContainsKey("PrefixIDList"))
            {
                Reroll(item);
                return;
            }

            var prefixIDList = tag.GetList<int>("PrefixIDList").ToList();
            var prefixMagnitudeList = tag.GetList<int>("PrefixMagnitudeList").ToList();
            var prefixTooltipList = tag.GetList<string>("PrefixTooltipList").ToList();
            var prefixTierList = tag.ContainsKey("PrefixTierList")
                ? tag.GetList<int>("PrefixTierList").ToList()
                : Enumerable.Repeat(9, prefixIDList.Count).ToList();

            var suffixIDList = tag.GetList<int>("SuffixIDList").ToList();
            var suffixMagnitudeList = tag.GetList<int>("SuffixMagnitudeList").ToList();
            var suffixTooltipList = tag.GetList<string>("SuffixTooltipList").ToList();
            var suffixTierList = tag.ContainsKey("SuffixTierList")
                ? tag.GetList<int>("SuffixTierList").ToList()
                : Enumerable.Repeat(9, suffixIDList.Count).ToList();

            modifierList.Clear();
            for (int i = 0; i < prefixIDList.Count; i++)
                modifierList.Add(new AccessoryModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
            for (int i = 0; i < suffixIDList.Count; i++)
                modifierList.Add(new AccessoryModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
        }

        public override void NetSend(Item item, BinaryWriter writer)
        {
            List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
            List<string> prefixTooltipList;
            List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
            List<string> suffixTooltipList;
            SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                          out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

            writer.Write(prefixIDList.Count);
            foreach (var v in prefixIDList) writer.Write(v);
            writer.Write(prefixMagnitudeList.Count);
            foreach (var v in prefixMagnitudeList) writer.Write(v);
            writer.Write(prefixTooltipList.Count);
            foreach (var v in prefixTooltipList) writer.Write(v);
            writer.Write(prefixTierList.Count);
            foreach (var v in prefixTierList) writer.Write(v);

            writer.Write(suffixIDList.Count);
            foreach (var v in suffixIDList) writer.Write(v);
            writer.Write(suffixMagnitudeList.Count);
            foreach (var v in suffixMagnitudeList) writer.Write(v);
            writer.Write(suffixTooltipList.Count);
            foreach (var v in suffixTooltipList) writer.Write(v);
            writer.Write(suffixTierList.Count);
            foreach (var v in suffixTierList) writer.Write(v);
        }
        public override void NetReceive(Item item, BinaryReader reader)
        {
            var prefixIDList = new List<int>(); var prefixMagnitudeList = new List<int>();
            var prefixTooltipList = new List<string>(); var prefixTierList = new List<int>();
            var suffixIDList = new List<int>(); var suffixMagnitudeList = new List<int>();
            var suffixTooltipList = new List<string>(); var suffixTierList = new List<int>();

            int c;
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixIDList.Add(reader.ReadInt32());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixMagnitudeList.Add(reader.ReadInt32());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTooltipList.Add(reader.ReadString());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTierList.Add(reader.ReadInt32());

            c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixIDList.Add(reader.ReadInt32());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixMagnitudeList.Add(reader.ReadInt32());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTooltipList.Add(reader.ReadString());
            c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTierList.Add(reader.ReadInt32());

            modifierList.Clear();
            for (int i = 0; i < prefixIDList.Count; i++)
                modifierList.Add(new AccessoryModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
            for (int i = 0; i < suffixIDList.Count; i++)
                modifierList.Add(new AccessoryModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
        }

        private void SerializeData(
            out List<int> prefixIDList, out List<int> prefixMagnitudeList,
            out List<string> prefixTooltipList, out List<int> prefixTierList,
            out List<int> suffixIDList, out List<int> suffixMagnitudeList,
            out List<string> suffixTooltipList, out List<int> suffixTierList)
        {
            prefixIDList = new List<int>(); prefixMagnitudeList = new List<int>();
            prefixTooltipList = new List<string>(); prefixTierList = new List<int>();
            suffixIDList = new List<int>(); suffixMagnitudeList = new List<int>();
            suffixTooltipList = new List<string>(); suffixTierList = new List<int>();

            foreach (var modifier in modifierList)
            {
                if (modifier.modifierType == ModifierType.Prefix)
                {
                    prefixIDList.Add((int)modifier.prefixType);
                    prefixMagnitudeList.Add(modifier.magnitude);
                    prefixTooltipList.Add(modifier.tooltip);
                    prefixTierList.Add(modifier.tier);
                }
                else
                {
                    suffixIDList.Add((int)modifier.suffixType);
                    suffixMagnitudeList.Add(modifier.magnitude);
                    suffixTooltipList.Add(modifier.tooltip);
                    suffixTierList.Add(modifier.tier);
                }
            }
        }


    }
}
