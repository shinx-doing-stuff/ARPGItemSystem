using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
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
            bool logEnabled = player.whoAmI == Main.myPlayer
                && Main.netMode != NetmodeID.Server
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                    case AffixId.NearbyDamageBonus:
                    {
                        float dist = Vector2.Distance(player.Center, target.Center);
                        bool applied = dist <= 256f;
                        if (applied)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        if (logEnabled)
                            Main.NewText($"  [Nearby] dist={dist / 16f:F1}t (≤16t)  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    case AffixId.DistantDamageBonus:
                    {
                        float dist = Vector2.Distance(player.Center, target.Center);
                        bool applied = dist >= 768f;
                        if (applied)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        if (logEnabled)
                            Main.NewText($"  [Distant] dist={dist / 16f:F1}t (≥48t)  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    case AffixId.LowHpDamageBonus:
                    {
                        float hpPct = player.statLifeMax2 > 0
                            ? player.statLife / (float)player.statLifeMax2
                            : 1f;
                        // Linear ramp: 0 bonus at ≥70% HP, full bonus at ≤25% HP.
                        // Dividing by 0.45 (= 0.70 - 0.25) maps the [0.25, 0.70] range onto [0, 1].
                        float factor = MathHelper.Clamp((0.70f - hpPct) / 0.45f, 0f, 1f);
                        float bonus = a.Magnitude * factor / 100f;
                        modifiers.SourceDamage += bonus;
                        if (logEnabled)
                            Main.NewText($"  [LowHp] hp={hpPct:P0}  factor={factor:F2}  bonus=+{bonus * 100f:F1}% (max +{a.Magnitude}%)", factor > 0 ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    case AffixId.FullHpDamageBonus:
                    {
                        bool applied = player.statLife >= player.statLifeMax2;
                        if (applied)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        if (logEnabled)
                            Main.NewText($"  [FullHp] hp={player.statLife}/{player.statLifeMax2}  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
                        break;
                    }
                }
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }

    }
}
