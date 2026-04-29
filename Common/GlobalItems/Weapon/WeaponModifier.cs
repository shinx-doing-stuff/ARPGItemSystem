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

namespace ARPGItemSystem.Common.GlobalItems.Weapon
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
        FlatDamageIncrease, // 1
        PercentageDamageIncrease, // 2
        FlatArmorPen, // 3
        PercentageArmorPen, // 4
        AttackSpeedIncrease, // 5
        KnockbackIncrease // 6
    }
    public enum SuffixType
    {
        None, // 0
        FlatCritChance, // 1
        PercentageCritChance, // 2
        CritMultiplier, // 3
        ManaCostReduction, // 4
        VelocityIncrease // 5
    }

    public struct WeaponModifier
    {
        public ModifierType modifierType;
        public PrefixType prefixType = PrefixType.None;
        public SuffixType suffixType = SuffixType.None;
        public int magnitude = 0;
        public int tier = 9;
        public string tooltip = "";

        public List<int> meleeWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
        public List<int> rangedWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
        public List<int> magicWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
        public List<int> summonWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 6 };
        public List<int> meleeWeaponSuffixType = new List<int>() { 0, 1, 2, 3 };
        public List<int> rangedWeaponSuffixType = new List<int>() { 0, 1, 2, 3, 5 };
        public List<int> magicWeaponSuffixType = new List<int>() { 0, 1, 2, 3, 4, 5 };
        public List<int> summonWeaponSuffixType = new List<int>() { 0, 1, 2, 3 };

        // Used when deserializing (SaveData/LoadData/NetReceive)
        public WeaponModifier(ModifierType type, int magnitude, string tooltip, PrefixType prefixType = PrefixType.None, SuffixType suffixType = SuffixType.None, int tier = 9)
        {
            modifierType = type;
            this.magnitude = magnitude;
            this.tooltip = tooltip;
            this.prefixType = prefixType;
            this.suffixType = suffixType;
            this.tier = tier;
        }

        // Used when generating a new modifier
        public WeaponModifier(ModifierType type, List<int> excludeList, DamageClass damageType, int tier = 0)
        {
            modifierType = type;
            GenerateModifier(modifierType, excludeList, damageType, tier);
        }

        public void GenerateModifier(ModifierType type, List<int> excludeList, DamageClass damageType, int tier = 0)
        {
            List<int> IDs = new List<int>();
            Random random = new Random();

            if (type == ModifierType.Prefix)
            {
                if (damageType == DamageClass.Melee || damageType == DamageClass.MeleeNoSpeed || damageType == DamageClass.SummonMeleeSpeed) { IDs = new List<int>(meleeWeaponPrefixType); }
                else if (damageType == DamageClass.Ranged) { IDs = new List<int>(rangedWeaponPrefixType); }
                else if (damageType == DamageClass.Magic || damageType == DamageClass.MagicSummonHybrid) { IDs = new List<int>(magicWeaponPrefixType); }
                else if (damageType == DamageClass.Summon) { IDs = new List<int>(summonWeaponPrefixType); }
                else { IDs = new List<int>(summonWeaponPrefixType); }

                IDs = IDs.Where(val => !excludeList.Contains(val) && val != 0).ToList();
                prefixType = (PrefixType)IDs[random.Next(0, IDs.Count)];
                magnitude = random.Next(TierDatabase.modifierTierDatabase[prefixType][tier].minValue, TierDatabase.modifierTierDatabase[prefixType][tier].maxValue + 1);
                tooltip = TooltipDatabase.modifierTooltipDatabase[prefixType];
                this.tier = tier;
            }
            if (type == ModifierType.Suffix)
            {
                if (damageType == DamageClass.Melee || damageType == DamageClass.MeleeNoSpeed || damageType == DamageClass.SummonMeleeSpeed) { IDs = new List<int>(meleeWeaponSuffixType); }
                else if (damageType == DamageClass.Ranged) { IDs = new List<int>(rangedWeaponSuffixType); }
                else if (damageType == DamageClass.Magic || damageType == DamageClass.MagicSummonHybrid) { IDs = new List<int>(magicWeaponSuffixType); }
                else if (damageType == DamageClass.Summon) { IDs = new List<int>(summonWeaponSuffixType); }
                else { IDs = new List<int>(summonWeaponSuffixType); }

                IDs = IDs.Where(val => !excludeList.Contains(val) && val != 0).ToList();
                suffixType = (SuffixType)IDs[random.Next(0, IDs.Count)];
                magnitude = random.Next(TierDatabase.modifierTierDatabase[suffixType][tier].minValue, TierDatabase.modifierTierDatabase[suffixType][tier].maxValue + 1);
                tooltip = TooltipDatabase.modifierTooltipDatabase[suffixType];
                this.tier = tier;
            }
        }
    }
}
