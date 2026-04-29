using ARPGItemSystem.Common.Affixes;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Accessory
{
    public class AccessoryManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Accessory;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.accessory;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesAccessory();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesAccessory();

        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatLifeIncrease:
                        player.statLifeMax2 += a.Magnitude;
                        break;
                    case AffixId.FlatDefenseIncrease:
                        player.statDefense += a.Magnitude;
                        break;
                    case AffixId.FlatManaIncrease:
                        player.statManaMax2 += a.Magnitude;
                        break;
                    case AffixId.PercentageGenericDamageIncrease:
                        player.GetDamage<GenericDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMeleeDamageIncrease:
                        player.GetDamage<MeleeDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageRangedDamageIncrease:
                        player.GetDamage<RangedDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMagicDamageIncrease:
                        player.GetDamage<MagicDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageSummonDamageIncrease:
                        player.GetDamage<SummonDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.FlatCritChance:
                        player.GetCritChance(DamageClass.Generic) += a.Magnitude;
                        break;
                    case AffixId.ManaCostReduction:
                        player.manaCost -= a.Magnitude / 100f;
                        break;
                }
            }
        }
    }
}
