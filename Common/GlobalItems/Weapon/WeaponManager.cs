using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Weapon
{
    public class WeaponManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Weapon;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage > 0 && entity.maxStack <= 1;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesWeapon();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesWeapon();

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatDamageIncrease:
                        damage.Base += a.Magnitude / 100f * item.OriginalDamage;
                        break;
                    case AffixId.PercentageDamageIncrease:
                        damage *= 1 + a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatCritChance:
                        crit += a.Magnitude;
                        break;
                    case AffixId.PercentageCritChance:
                        crit *= 1 + a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.CritMultiplier)
                    modifiers.CritDamage += a.Magnitude / 100f;
                // PercentageArmorPen removed — now handled inside ElementalDamageCalculator
                // as a reduction to enemy physical resistance before the cap
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.KnockbackIncrease)
                    knockback += a.Magnitude / 100f;
            }
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.VelocityIncrease)
                    velocity *= 1 + a.Magnitude / 100f;
            }
        }

        public override float UseSpeedMultiplier(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.AttackSpeedIncrease)
                    return base.UseSpeedMultiplier(item, player) + a.Magnitude / 100f;
            }
            return base.UseSpeedMultiplier(item, player);
        }

        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.ManaCostReduction)
                    reduce -= a.Magnitude / 100f;
            }
        }
    }
}
