using ARPGItemSystem.Common.Affixes;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Armor
{
    public class ArmorManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Armor;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage < 1 && entity.maxStack == 1
               && !entity.accessory && !entity.vanity;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesArmor();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesArmor();

        public override void UpdateEquip(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatLifeIncrease:
                        player.statLifeMax2 += a.Magnitude;
                        break;
                    case AffixId.FlatDefenseIncrease:
                        item.defense = (int)(item.OriginalDefense * (1 + a.Magnitude / 100f));
                        break;
                    case AffixId.PercentageDefenseIncrease:
                        item.defense = (int)(item.OriginalDefense * (1 + a.Magnitude / 100f));
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
