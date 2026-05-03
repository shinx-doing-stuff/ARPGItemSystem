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

        // Handles enemy-projectile → player hits: apply player elemental resistance.
        // Mirrors ElementalHitFromNPCGlobalNPC but for projectile hits (contact hits are handled there).
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            // Read NPC elemental profile from ARPGEnemySystem's ProjectileManager on the same projectile.
            // That class stores modNPC/modBossNPC captured at spawn time from the firing NPC.
            if (!projectile.TryGetGlobalProjectile<EnemyProjectileManager>(out var enemyPm))
                return;

            float firePct, coldPct, lightPct;

            if (enemyPm.modNPC != null)
            {
                firePct  = enemyPm.modNPC.FireDamagePct;
                coldPct  = enemyPm.modNPC.ColdDamagePct;
                lightPct = enemyPm.modNPC.LightningDamagePct;
            }
            else if (enemyPm.modBossNPC != null)
            {
                firePct  = enemyPm.modBossNPC.FireDamagePct;
                coldPct  = enemyPm.modBossNPC.ColdDamagePct;
                lightPct = enemyPm.modBossNPC.LightningDamagePct;
            }
            else
            {
                return; // projectile not from a managed NPC — no elemental handling
            }

            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            var playerData = target.GetModPlayer<PlayerElementalPlayer>();
            float physRes  = playerData.PhysRes;
            float fireRes  = playerData.FireRes;
            float coldRes  = playerData.ColdRes;
            float lightRes = playerData.LightningRes;

            // projectile.damage is already scaled by the NPC's level/modifiers in ARPGEnemySystem.
            // Apply DamageVar for hit variance, then split and apply resistance — same pattern as contact hits.
            float totalElemPct = (firePct + coldPct + lightPct) / 100f;
            float baseDamage   = Main.DamageVar(projectile.damage);
            float physPortion  = baseDamage * Math.Max(0f, 1f - totalElemPct);
            float firePortion  = baseDamage * firePct  / 100f;
            float coldPortion  = baseDamage * coldPct  / 100f;
            float lightPortion = baseDamage * lightPct / 100f;

            float physFinal  = ElementalMath.ApplyResistance(physPortion, physRes,  cap);
            float fireFinal  = ElementalMath.ApplyResistance(firePortion,  fireRes,  cap);
            float coldFinal  = ElementalMath.ApplyResistance(coldPortion,  coldRes,  cap);
            float lightFinal = ElementalMath.ApplyResistance(lightPortion, lightRes, cap);

            int finalDamage = Math.Max(1, (int)Math.Round(physFinal + fireFinal + coldFinal + lightFinal));

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
                    Main.NewText($"← [proj] {npcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {physFinal,6:F1}  (raw:{physPortion,5:F1}  res:{physRes:F1}%)", Color.Silver);
                    if (firePct  > 0) Main.NewText($"  Fire:  {fireFinal,6:F1}  (raw:{firePortion,5:F1}  res:{fireRes:F1}%)",  new Color(255, 120, 50));
                    if (coldPct  > 0) Main.NewText($"  Cold:  {coldFinal,6:F1}  (raw:{coldPortion,5:F1}  res:{coldRes:F1}%)",  new Color(100, 200, 255));
                    if (lightPct > 0) Main.NewText($"  Light: {lightFinal,6:F1}  (raw:{lightPortion,5:F1}  res:{lightRes:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {info.Damage}", Color.OrangeRed);
                }
            };
        }
    }
}
