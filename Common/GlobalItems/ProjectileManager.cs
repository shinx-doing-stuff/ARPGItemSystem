using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems
{
    public class ProjectileManager : GlobalProjectile
    {
        public List<Affix> Affixes = new();
        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Direct weapon use — captures melee, ranged, magic, minions, and sentries.
            if (source is EntitySource_ItemUse itemSource
                && !itemSource.Item.consumable
                && itemSource.Item.fishingPole <= 0)
            {
                if (itemSource.Item.TryGetGlobalItem<WeaponManager>(out var wm))
                    Affixes = wm.Affixes.ToList();
                return;
            }

            // Projectiles fired BY a sentry or minion (EntitySource_Parent where the parent is a
            // projectile that already has affix data). Propagates affixes to child shots so
            // sentry bolts and minion sub-projectiles inherit the summoning weapon's affixes.
            if (source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parentProj
                && parentProj.TryGetGlobalProjectile<ProjectileManager>(out var parentPm)
                && parentPm.Affixes.Count > 0)
            {
                Affixes = parentPm.Affixes.ToList();
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Affixes.Count == 0) return;

            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.CritMultiplier)
                    modifiers.CritDamage += a.Magnitude / 100f;
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, Main.player[projectile.owner], target, ref modifiers);
        }
    }
}
