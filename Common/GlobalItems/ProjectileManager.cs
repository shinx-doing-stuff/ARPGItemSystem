using System;
using System.Collections.Generic;
using System.Linq;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using EnemyProjectileManager = ARPGEnemySystem.Common.GlobalProjectiles.ProjectileManager;
using ARPGEnemySystem.Common.Elements;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Players;
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

            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.CritMultiplier)
                    modifiers.CritDamage += a.Magnitude / 100f;
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, Main.player[projectile.owner], target, ref modifiers);
        }

        // Handles enemy-projectile → player hits: apply player elemental resistance.
        // Mirrors ElementalHitFromNPCGlobalNPC but for projectile hits (contact hits are handled there).
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            // Read NPC elemental profile from ARPGEnemySystem's ProjectileManager on the same projectile.
            // That class stores modNPC/modBossNPC captured at spawn time from the firing NPC.
            if (!projectile.TryGetGlobalProjectile<EnemyProjectileManager>(out var enemyPm))
                return;

            float elemDamagePct;
            Element elemType;

            if (enemyPm.modNPC != null)
            {
                elemDamagePct = enemyPm.modNPC.ElementalDamagePct;
                elemType      = enemyPm.modNPC.ElementalDamageType;
            }
            else if (enemyPm.modBossNPC != null)
            {
                elemDamagePct = enemyPm.modBossNPC.ElementalDamagePct;
                elemType      = enemyPm.modBossNPC.ElementalDamageType;
            }
            else
            {
                return; // projectile not from a managed NPC — no elemental handling
            }

            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            var playerData = target.GetModPlayer<PlayerElementalPlayer>();
            float physRes  = playerData.PhysRes;
            float elemRes  = playerData.GetResistance(elemType);

            // projectile.damage is already scaled by the NPC's level/modifiers in ARPGEnemySystem.
            // Apply DamageVar for hit variance, then split and apply resistance — same pattern as contact hits.
            float baseDamage  = Main.DamageVar(projectile.damage);
            float physPortion = baseDamage * (1f - elemDamagePct / 100f);
            float elemPortion = baseDamage * elemDamagePct / 100f;

            float physFinal = ElementalMath.ApplyResistance(physPortion, physRes, cap);
            float elemFinal = ElementalMath.ApplyResistance(elemPortion, elemRes, cap);

            int finalDamage = Math.Max(1, (int)Math.Round(physFinal + elemFinal));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && target.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

            string npcName = enemyPm.npcIndex >= 0 && enemyPm.npcIndex < Main.npc.Length
                ? Main.npc[enemyPm.npcIndex].GivenOrTypeName
                : "Unknown";

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                if (logEnabled)
                {
                    Color elemColor = elemType switch
                    {
                        Element.Fire      => new Color(255, 120, 50),
                        Element.Cold      => new Color(100, 200, 255),
                        Element.Lightning => new Color(255, 240, 80),
                        _                 => Color.Silver,
                    };
                    Main.NewText($"← [proj] {npcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {physFinal,6:F1}  (res:{physRes:F1}%)", Color.Silver);
                    if (elemDamagePct > 0)
                        Main.NewText($"  {elemType,-8} {elemFinal,6:F1}  (res:{elemRes:F1}%)", elemColor);
                    Main.NewText($"  Total: {info.Damage}", Color.OrangeRed);
                }
            };
        }
    }
}
