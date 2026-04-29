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
        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse_WithAmmo itemSource
                && !itemSource.Item.consumable
                && !(itemSource.Item.fishingPole > 0))
            {
                if (itemSource.Item.TryGetGlobalItem<WeaponManager>(out var wm))
                    Affixes = wm.Affixes.ToList();
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.PercentageArmorPen:
                        modifiers.ScalingArmorPenetration += a.Magnitude / 100f;
                        break;
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                }
            }
        }
    }
}
