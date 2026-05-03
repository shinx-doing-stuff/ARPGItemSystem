using System;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using EnemyProjectileManager = ARPGEnemySystem.Common.GlobalProjectiles.ProjectileManager;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    // Single owner of the player's incoming-damage modification pipeline.
    // ModifyHurt dispatches on modifiers.DamageSource:
    //   - SourceProjectileLocalIndex >= 0 → enemy projectile branch
    //   - SourceNPCIndex >= 0           → NPC contact branch
    //   - else                          → vanilla math runs unchanged (lava/fall/drown/PvP)
    //
    // Replaces the old per-hook implementations:
    //   - ProjectileManager.ModifyHitPlayer (deleted)
    //   - ElementalHitFromNPCGlobalNPC.cs (deleted)
    //
    // Mana-absorb (Task 8) and thorns (Task 9) extend this file in subsequent tasks.
    public class PlayerHurtPipeline : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            var src = modifiers.DamageSource;

            // Branch A: enemy projectile
            if (src.SourceProjectileLocalIndex >= 0)
            {
                var proj = Main.projectile[src.SourceProjectileLocalIndex];
                if (!proj.active) return;
                if (!proj.TryGetGlobalProjectile<EnemyProjectileManager>(out var pm)) return;

                float firePct, coldPct, lightPct;
                string sourceName;
                if (pm.modNPC != null)
                {
                    firePct  = pm.modNPC.FireDamagePct;
                    coldPct  = pm.modNPC.ColdDamagePct;
                    lightPct = pm.modNPC.LightningDamagePct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else if (pm.modBossNPC != null)
                {
                    firePct  = pm.modBossNPC.FireDamagePct;
                    coldPct  = pm.modBossNPC.ColdDamagePct;
                    lightPct = pm.modBossNPC.LightningDamagePct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else return;

                // Projectile damage is pre-scaled in ARPGEnemySystem; vanilla doesn't apply DamageVar to it.
                RegisterHandler(ref modifiers, proj.damage, firePct, coldPct, lightPct, sourceName, isProj: true);
                return;
            }

            // Branch B: NPC direct contact
            if (src.SourceNPCIndex >= 0)
            {
                var npc = Main.npc[src.SourceNPCIndex];
                if (!npc.active) return;

                float firePct, coldPct, lightPct;
                if (npc.TryGetGlobalNPC<NPCManager>(out var nd))
                {
                    firePct  = nd.FireDamagePct;
                    coldPct  = nd.ColdDamagePct;
                    lightPct = nd.LightningDamagePct;
                }
                else if (npc.TryGetGlobalNPC<BossManager>(out var bd))
                {
                    firePct  = bd.FireDamagePct;
                    coldPct  = bd.ColdDamagePct;
                    lightPct = bd.LightningDamagePct;
                }
                else return;

                // Contact hits use vanilla ±15% damage variance.
                RegisterHandler(ref modifiers, Main.DamageVar(npc.damage), firePct, coldPct, lightPct, npc.GivenOrTypeName, isProj: false);
                return;
            }

            // Branch C: lava / fall / drown / PvP / custom — vanilla math runs unchanged.
        }

        private void RegisterHandler(ref Player.HurtModifiers modifiers,
                                      float baseDamage,
                                      float firePct, float coldPct, float lightPct,
                                      string sourceName, bool isProj)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            var elem = Player.GetModPlayer<PlayerElementalPlayer>();

            float totalElemPct = (firePct + coldPct + lightPct) / 100f;
            float physPortion  = baseDamage * Math.Max(0f, 1f - totalElemPct);
            float firePortion  = baseDamage * firePct  / 100f;
            float coldPortion  = baseDamage * coldPct  / 100f;
            float lightPortion = baseDamage * lightPct / 100f;

            float physFinal  = ElementalMath.ApplyResistance(physPortion,  elem.PhysRes,      cap);
            float fireFinal  = ElementalMath.ApplyResistance(firePortion,  elem.FireRes,      cap);
            float coldFinal  = ElementalMath.ApplyResistance(coldPortion,  elem.ColdRes,      cap);
            float lightFinal = ElementalMath.ApplyResistance(lightPortion, elem.LightningRes, cap);

            int finalDamage = Math.Max(1, (int)Math.Round(physFinal + fireFinal + coldFinal + lightFinal));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

            // Capture log values for the closure.
            float pf = physFinal,  ff = fireFinal,  cf = coldFinal,  lf = lightFinal;
            float pp = physPortion, fp = firePortion, cp = coldPortion, lp = lightPortion;
            float pr = elem.PhysRes, fr = elem.FireRes, cr = elem.ColdRes, lr = elem.LightningRes;
            float fpct = firePct, cpct = coldPct, lpct = lightPct;
            string srcName = sourceName;
            bool wasProj = isProj;

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                // Mana-absorb (spec §4.6): % of damage routed to mana, capped by per-hit ceiling
                // (25% of statManaMax2) and by current mana available. Triggers regen delay.
                var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
                int absorbed = 0;
                if (sp.ManaAbsorbPercent > 0 && info.Damage > 0 && Player.statManaMax2 > 0)
                {
                    int routed      = (int)(info.Damage * sp.ManaAbsorbPercent / 100f);
                    int perHitCap   = (int)(Player.statManaMax2 * 0.25f);
                    int cappedRoute = Math.Min(routed, perHitCap);
                    absorbed        = Math.Min(cappedRoute, Player.statMana);

                    Player.statMana       -= absorbed;
                    Player.manaRegenDelay  = Math.Max(Player.manaRegenDelay, 40);
                    info.Damage           -= absorbed;
                }

                if (logEnabled)
                {
                    string tag = wasProj ? "[proj] " : "";
                    Main.NewText($"← {tag}{srcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {pf,6:F1}  (raw:{pp,5:F1}  res:{pr:F1}%)", Color.Silver);
                    if (fpct > 0) Main.NewText($"  Fire:  {ff,6:F1}  (raw:{fp,5:F1}  res:{fr:F1}%)",  new Color(255, 120, 50));
                    if (cpct > 0) Main.NewText($"  Cold:  {cf,6:F1}  (raw:{cp,5:F1}  res:{cr:F1}%)",  new Color(100, 200, 255));
                    if (lpct > 0) Main.NewText($"  Light: {lf,6:F1}  (raw:{lp,5:F1}  res:{lr:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {finalDamage}", Color.OrangeRed);
                    if (absorbed > 0)
                    {
                        Main.NewText($"  Absorb: {absorbed} (mana: {Player.statMana + absorbed} → {Player.statMana})", new Color(180, 100, 200));
                        Main.NewText($"  After absorb: {info.Damage}", Color.OrangeRed);
                    }
                }
            };
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            var sp = Player.GetModPlayer<PlayerSurvivalPlayer>();
            if (sp.ThornsPercent <= 0) return;

            // Direct NPC contact only — skip projectile hits per spec A.3
            if (info.DamageSource.SourceProjectileLocalIndex >= 0) return;
            int npcIdx = info.DamageSource.SourceNPCIndex;
            if (npcIdx < 0) return;
            var npc = Main.npc[npcIdx];
            if (!npc.active || npc.whoAmI != npcIdx) return;

            int reflected = (int)(info.Damage * sp.ThornsPercent / 100f);
            if (reflected <= 0) return;

            // StrikeNPC auto-broadcasts in MP; no custom packet needed.
            npc.StrikeNPC(npc.CalculateHitInfo(reflected, 0, false, 0f, DamageClass.Default, true));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;
            if (logEnabled)
                Main.NewText($"  Thorns: {reflected} → {npc.GivenOrTypeName}", Color.LightGreen);
        }
    }
}
