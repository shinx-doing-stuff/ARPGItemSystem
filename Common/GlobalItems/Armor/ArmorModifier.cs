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
using ARPGItemSystem.Common.GlobalItems.Database;

namespace ARPGItemSystem.Common.GlobalItems.Armor
{
    public enum ModifierType
    {
        None,
        Prefix,
        Suffix
    }
    public enum PrefixType
    {
        None, // 0
        FlatLifeIncrease, // 1
        FlatDefenseIncrease, // 2
        PercentageDefenseIncrease, // 3
        FlatManaIncrease, // 4
    }
    public enum SuffixType
    {
        None, // 0
        PercentageGenericDamageIncrease, // 1
        PercentageMeleeDamageIncrease, // 2
        PercentageRangedDamageIncrease, // 3
        PercentageMagicDamageIncrease, // 4
        PercentageSummonDamageIncrease, // 5
        FlatCritChance, // 6
        ManaCostReduction, // 7
    }

    public struct ArmorModifier
    {
        public ModifierType modifierType;
        public PrefixType prefixType = PrefixType.None;
        public SuffixType suffixType = SuffixType.None;
        public int magnitude = 0;
        public int tier = 9;
        public string tooltip = "";

        // Used when deserializing (SaveData/LoadData/NetReceive)
        public ArmorModifier(ModifierType type, int magnitude, string tooltip, PrefixType prefixType = PrefixType.None, SuffixType suffixType = SuffixType.None, int tier = 9)
        {
            modifierType = type;
            this.magnitude = magnitude;
            this.tooltip = tooltip;
            this.prefixType = prefixType;
            this.suffixType = suffixType;
            this.tier = tier;
        }

        // Used when generating a new modifier
        public ArmorModifier(ModifierType type, List<int> excludeList, int tier = 0)
        {
            modifierType = type;
            GenerateModifier(modifierType, excludeList, tier);
        }

        public void GenerateModifier(ModifierType type, List<int> excludeList, int tier = 0)
        {
            List<int> IDs = new List<int>();
            Random random = new Random();

            if (type == ModifierType.Prefix)
            {
                IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(PrefixType)).Length - 1));
                IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
                prefixType = (PrefixType)IDs[random.Next(0, IDs.Count)];
                magnitude = random.Next(TierDatabase.modifierTierDatabase[prefixType][tier].minValue, TierDatabase.modifierTierDatabase[prefixType][tier].maxValue + 1);
                tooltip = TooltipDatabase.modifierTooltipDatabase[prefixType];
                this.tier = tier;
            }
            if (type == ModifierType.Suffix)
            {
                IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(SuffixType)).Length - 1));
                IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
                suffixType = (SuffixType)IDs[random.Next(0, IDs.Count)];
                magnitude = random.Next(TierDatabase.modifierTierDatabase[suffixType][tier].minValue, TierDatabase.modifierTierDatabase[suffixType][tier].maxValue + 1);
                tooltip = TooltipDatabase.modifierTooltipDatabase[suffixType];
                this.tier = tier;
            }
        }
    }
}
