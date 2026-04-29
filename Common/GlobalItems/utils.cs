using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;

namespace ARPGItemSystem.Common.GlobalItems
{
    internal static class utils
    {
        internal static int GetAmountOfSuffixesWeapon()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedBoss2) maxCount += 1;
            if (Main.hardMode) minCount += 1;
            if (NPC.downedMechBossAny) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount,maxCount+1);
        }
        internal static int GetAmountOfPrefixesWeapon()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedBoss3) maxCount += 1;
            if (Main.hardMode) minCount += 1;
            if (NPC.downedGolemBoss) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }
        internal static int GetAmountOfSuffixesArmor()
        {
            int maxCount = 1;
            int minCount = 0;
            if (NPC.downedBoss2) minCount += 1;
            if (Main.hardMode) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }
        internal static int GetAmountOfPrefixesArmor()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedGolemBoss) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }
        internal static int GetAmountOfSuffixesAccessory()
        {
            int maxCount = 1;
            int minCount = 0;
            if (Main.hardMode) minCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }
        internal static int GetAmountOfPrefixesAccessory()
        {
            int maxCount = 1;
            int minCount = 0;
            if (NPC.downedGolemBoss) minCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }
        internal static int GetTier()
        {
            Random random = new Random();
            int bestTier = 8;   // lowest tier number = strongest roll
            int worstTier = 10; // highest tier number = weakest roll

            if (NPC.downedSlimeKing) bestTier -= 1;
            if (NPC.downedBoss2) worstTier -= 1;
            if (NPC.downedBoss3) bestTier -= 1;
            if (Main.hardMode) bestTier -= 1;
            if (NPC.downedQueenSlime) worstTier -= 1;
            if (NPC.downedMechBossAny) bestTier -= 1;
            if (NPC.downedGolemBoss) worstTier -= 1;
            if (NPC.downedPlantBoss) bestTier -= 1;
            if (NPC.downedFishron) worstTier -= 1;
            if (NPC.downedEmpressOfLight) { bestTier -= 1; worstTier -= 1; }
            if (NPC.downedAncientCultist) bestTier -= 1;
            if (NPC.downedMoonlord) { bestTier -= 1; worstTier -= 1; }

            bestTier = Math.Max(0, bestTier);
            worstTier = Math.Max(bestTier + 1, worstTier);
            return random.Next(bestTier, worstTier);
        }

        internal static List<int> CreateExcludeList(List<WeaponModifier> modifierList, Weapon.ModifierType type)
        {
            // Prepare both exclude list for prefix and suffix
            var excludePrefixListEnum = modifierList.Select(o => o.prefixType).ToList();
            var excludeSuffixListEnum = modifierList.Select(o => o.suffixType).ToList();
            List<int> excludeListInt = new List<int>();

            switch (type)
            {
                case Weapon.ModifierType.Prefix:
                    foreach (var item in excludePrefixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
                case Weapon.ModifierType.Suffix:
                    foreach (var item in excludeSuffixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
            }

            return excludeListInt;
        }
        internal static List<int> CreateExcludeList(List<ArmorModifier> modifierList, Armor.ModifierType type)
        {
            // Prepare both exclude list for prefix and suffix
            var excludePrefixListEnum = modifierList.Select(o => o.prefixType).ToList();
            var excludeSuffixListEnum = modifierList.Select(o => o.suffixType).ToList();
            List<int> excludeListInt = new List<int>();

            switch (type)
            {
                case Armor.ModifierType.Prefix:
                    foreach (var item in excludePrefixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
                case Armor.ModifierType.Suffix:
                    foreach (var item in excludeSuffixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
            }

            return excludeListInt;
        }
        internal static List<int> CreateExcludeList(List<AccessoryModifier> modifierList, Accessory.ModifierType type)
        {
            // Prepare both exclude list for prefix and suffix
            var excludePrefixListEnum = modifierList.Select(o => o.prefixType).ToList();
            var excludeSuffixListEnum = modifierList.Select(o => o.suffixType).ToList();
            List<int> excludeListInt = new List<int>();

            switch (type)
            {
                case Accessory.ModifierType.Prefix:
                    foreach (var item in excludePrefixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
                case Accessory.ModifierType.Suffix:
                    foreach (var item in excludeSuffixListEnum)
                    {
                        excludeListInt.Add((int)item);
                    }
                    break;
            }

            return excludeListInt;
        }
    }
}
