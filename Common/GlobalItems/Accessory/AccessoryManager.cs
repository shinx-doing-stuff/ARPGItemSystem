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

        // UpdateAccessory removed — every player-stat affix that was here is now applied
        // by ARPGCharacterSystem.Common.Stats.Sources.EquipmentStatSource.Dispatch.
    }
}
