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
            // Match EntitySource_ItemUse (base class) to capture melee projectiles,
            // all ranged weapons, magic, and summons — not just ammo weapons.
            if (source is EntitySource_ItemUse itemSource
                && !itemSource.Item.consumable
                && itemSource.Item.fishingPole <= 0)
            {
                if (itemSource.Item.TryGetGlobalItem<WeaponManager>(out var wm))
                    Affixes = wm.Affixes.ToList();
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
