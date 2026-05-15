using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Terraria;

namespace ARPGItemSystem.Common.GlobalItems.Weapon
{
    public class WeaponManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Weapon;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage > 0 && entity.maxStack <= 1;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesWeapon();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesWeapon();

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.VelocityIncrease)
                    velocity *= 1 + a.Magnitude / 100f;
            }
        }
    }
}
