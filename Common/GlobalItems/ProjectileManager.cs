using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems
{
    public class ProjectileManager : GlobalProjectile
    {
        public List<Affix> Affixes = new();

        // Raw base damage (item.OriginalDamage) of the weapon that spawned this projectile.
        // Read by ARPGCharacterSystem's ElementalDamageCalculator to compute the weapon-base
        // DoT scaling base. 0 if the projectile has no weapon source.
        public int WeaponBaseDamage;

        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Direct weapon use — captures melee, ranged, magic, minions, and sentries.
            if (source is EntitySource_ItemUse itemSource
                && !itemSource.Item.consumable
                && itemSource.Item.fishingPole <= 0)
            {
                if (itemSource.Item.TryGetGlobalItem<WeaponManager>(out var wm))
                {
                    Affixes = wm.Affixes.ToList();
                    WeaponBaseDamage = itemSource.Item.OriginalDamage;
                }
                return;
            }

            // Projectiles fired BY a sentry or minion — inherit parent's affix data.
            if (source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parentProj
                && parentProj.TryGetGlobalProjectile<ProjectileManager>(out var parentPm)
                && parentPm.Affixes.Count > 0)
            {
                Affixes = parentPm.Affixes.ToList();
                WeaponBaseDamage = parentPm.WeaponBaseDamage;
            }
        }
    }
}
