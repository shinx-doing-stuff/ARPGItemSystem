using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Microsoft.Xna.Framework;
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

            var player = Main.player[projectile.owner];

            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.NearbyDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) <= 256f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.DistantDamageBonus:
                        if (Vector2.Distance(player.Center, target.Center) >= 768f)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.LowHpDamageBonus:
                    {
                        float hpPct = player.statLifeMax2 > 0
                            ? player.statLife / (float)player.statLifeMax2
                            : 1f;
                        float factor = MathHelper.Clamp((0.70f - hpPct) / 0.45f, 0f, 1f);
                        modifiers.SourceDamage += a.Magnitude * factor / 100f;
                        break;
                    }
                    case AffixId.FullHpDamageBonus:
                        if (player.statLife >= player.statLifeMax2)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        break;
                }
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }

    }
}
