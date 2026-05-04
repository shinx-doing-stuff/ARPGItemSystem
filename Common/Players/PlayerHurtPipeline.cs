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
                float firePen, coldPen, lightPen, sunderingPct;
                string sourceName;
                if (pm.modNPC != null)
                {
                    firePct  = pm.modNPC.FireDamagePct;
                    coldPct  = pm.modNPC.ColdDamagePct;
                    lightPct = pm.modNPC.LightningDamagePct;
                    firePen      = pm.modNPC.FirePen;
                    coldPen      = pm.modNPC.ColdPen;
                    lightPen     = pm.modNPC.LightningPen;
                    sunderingPct = pm.modNPC.SunderingPct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else if (pm.modBossNPC != null)
                {
                    firePct  = pm.modBossNPC.FireDamagePct;
                    coldPct  = pm.modBossNPC.ColdDamagePct;
                    lightPct = pm.modBossNPC.LightningDamagePct;
                    firePen      = pm.modBossNPC.FirePen;
                    coldPen      = pm.modBossNPC.ColdPen;
                    lightPen     = pm.modBossNPC.LightningPen;
                    sunderingPct = pm.modBossNPC.SunderingPct;
                    sourceName = pm.npcIndex >= 0 && pm.npcIndex < Main.npc.Length
                        ? Main.npc[pm.npcIndex].GivenOrTypeName : "Unknown";
                }
                else return;

                // Projectile damage is pre-scaled in ARPGEnemySystem; vanilla doesn't apply DamageVar to it.
                RegisterHandler(ref modifiers, proj.damage,
                    firePct, coldPct, lightPct,
                    firePen, coldPen, lightPen, sunderingPct,
                    sourceName, isProj: true);
                return;
            }

            // Branch B: NPC direct contact
            if (src.SourceNPCIndex >= 0)
            {
                var npc = Main.npc[src.SourceNPCIndex];
                if (!npc.active) return;

                float firePct, coldPct, lightPct;
                float firePen, coldPen, lightPen, sunderingPct;
                if (npc.TryGetGlobalNPC<NPCManager>(out var nd))
                {
                    firePct  = nd.FireDamagePct;
                    coldPct  = nd.ColdDamagePct;
                    lightPct = nd.LightningDamagePct;
                    firePen      = nd.FirePen;
                    coldPen      = nd.ColdPen;
                    lightPen     = nd.LightningPen;
                    sunderingPct = nd.SunderingPct;
                }
                else if (npc.TryGetGlobalNPC<BossManager>(out var bd))
                {
                    firePct  = bd.FireDamagePct;
                    coldPct  = bd.ColdDamagePct;
                    lightPct = bd.LightningDamagePct;
                    firePen      = bd.FirePen;
                    coldPen      = bd.ColdPen;
                    lightPen     = bd.LightningPen;
                    sunderingPct = bd.SunderingPct;
                }
                else return;

                // Contact hits use vanilla ±15% damage variance.
                RegisterHandler(ref modifiers, Main.DamageVar(npc.damage),
                    firePct, coldPct, lightPct,
                    firePen, coldPen, lightPen, sunderingPct,
                    npc.GivenOrTypeName, isProj: false);
                return;
            }

            // Branch C: lava / fall / drown / PvP / custom — vanilla math runs unchanged.
        }

        private void RegisterHandler(ref Player.HurtModifiers modifiers,
                                      float baseDamage,
                                      float firePct, float coldPct, float lightPct,
                                      float firePen, float coldPen, float lightPen, float sunderingPct,
                                      string sourceName, bool isProj)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            var elem = Player.GetModPlayer<PlayerElementalPlayer>();

            // Bonus model: physical portion is full base damage; elemental portions are added on top.
            // (Was: physPortion = baseDamage * (1 - totalElemPct/100). Flipped per spec §3.)
            float physPortion  = baseDamage;
            float firePortion  = baseDamage * firePct  / 100f;
            float coldPortion  = baseDamage * coldPct  / 100f;
            float lightPortion = baseDamage * lightPct / 100f;

            // Effective player defense after Sundering (% subtraction). Recompute physRes from it.
            int   effDef       = Math.Max(0, (int)(Player.statDefense * (1f - sunderingPct / 100f)));
            float effPhysRes   = ElementalMath.ConvertDefenseToResistance(
                                     effDef, cfg.PhysResHalfPoint, cfg.PlayerPhysResCap);

            // Effective elemental resistances after pen (flat-point subtraction, floored at 0).
            float effFireRes  = Math.Max(0f, elem.FireRes      - firePen);
            float effColdRes  = Math.Max(0f, elem.ColdRes      - coldPen);
            float effLightRes = Math.Max(0f, elem.LightningRes - lightPen);

            float physFinal  = ElementalMath.ApplyResistance(physPortion,  effPhysRes,  cap);
            float fireFinal  = ElementalMath.ApplyResistance(firePortion,  effFireRes,  cap);
            float coldFinal  = ElementalMath.ApplyResistance(coldPortion,  effColdRes,  cap);
            float lightFinal = ElementalMath.ApplyResistance(lightPortion, effLightRes, cap);

            int finalDamage = Math.Max(1, (int)Math.Round(physFinal + fireFinal + coldFinal + lightFinal));

            bool logEnabled = Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

            // Capture log values for the closure.
            float pf = physFinal,  ff = fireFinal,  cf = coldFinal,  lf = lightFinal;
            float pp = physPortion, fp = firePortion, cp = coldPortion, lp = lightPortion;
            float pr = effPhysRes,  fr = effFireRes,  cr = effColdRes,  lr = effLightRes;
            float fpct = firePct, cpct = coldPct, lpct = lightPct;
            float fpen = firePen, cpen = coldPen, lpen = lightPen, spct = sunderingPct;
            int   ed = effDef;
            string srcName = sourceName;
            bool wasProj = isProj;

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                // Mana-absorb (spec §4.6 of affix-pool batch 1): % of damage routed to mana, capped by per-hit ceiling
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
                    Player.manaRegenDelay  = Math.Max(Player.manaRegenDelay, 60);
                    info.Damage           -= absorbed;
                }

                if (logEnabled)
                {
                    string tag = wasProj ? "[proj] " : "";
                    Main.NewText($"← {tag}{srcName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {pf,6:F1}  (raw:{pp,5:F1}  res:{pr:F1}%  effDef:{ed}  sunder:{spct:F0}%)", Color.Silver);
                    if (fpct > 0) Main.NewText($"  Fire:  {ff,6:F1}  (raw:{fp,5:F1}  res:{fr:F1}%  pen:{fpen:F0})",  new Color(255, 120, 50));
                    if (cpct > 0) Main.NewText($"  Cold:  {cf,6:F1}  (raw:{cp,5:F1}  res:{cr:F1}%  pen:{cpen:F0})",  new Color(100, 200, 255));
                    if (lpct > 0) Main.NewText($"  Light: {lf,6:F1}  (raw:{lp,5:F1}  res:{lr:F1}%  pen:{lpen:F0})", new Color(255, 240, 80));
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
