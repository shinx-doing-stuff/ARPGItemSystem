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

        // Only the two defense affixes remain at the per-item path: they write to item.defense
        // so the vanilla armor tooltip displays the correct number. All other player-stat affixes
        // are applied by ARPGCharacterSystem.Common.Stats.Sources.EquipmentStatSource.Dispatch.
        public override void UpdateEquip(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatDefenseIncrease:
                        item.defense = item.OriginalDefense + a.Magnitude;
                        break;
                    case AffixId.PercentageDefenseIncrease:
                        item.defense = (int)(item.OriginalDefense * (1 + a.Magnitude / 100f));
                        break;
                }
            }
        }
    }
}
