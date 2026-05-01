using System;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using ARPGItemSystem.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalNPCs
{
    // Applies player elemental resistance to NPC direct contact damage.
    // NPC-sourced projectile elemental hits are handled in ProjectileManager.ModifyHitPlayer.
    public class ElementalHitFromNPCGlobalNPC : GlobalNPC
    {
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            // Read the NPC's multi-element damage profile
            float firePct, coldPct, lightPct;

            if (npc.TryGetGlobalNPC<NPCManager>(out var npcData))
            {
                firePct  = npcData.FireDamagePct;
                coldPct  = npcData.ColdDamagePct;
                lightPct = npcData.LightningDamagePct;
            }
            else if (npc.TryGetGlobalNPC<BossManager>(out var bossData))
            {
                firePct  = bossData.FireDamagePct;
                coldPct  = bossData.ColdDamagePct;
                lightPct = bossData.LightningDamagePct;
            }
            else
            {
                return;
            }

            // Read the player's resistances (computed in PlayerElementalPlayer.PostUpdateEquips)
            var playerData = target.GetModPlayer<PlayerElementalPlayer>();
            float physRes  = playerData.PhysRes;
            float fireRes  = playerData.FireRes;
            float coldRes  = playerData.ColdRes;
            float lightRes = playerData.LightningRes;

            // Compute final damage from npc.damage (already scaled by level/rarity/modifiers in PreAI).
            // Apply vanilla damage variation (±15%) so hits don't deal exactly the same value every time.
            float totalElemPct = (firePct + coldPct + lightPct) / 100f;
            float baseDamage   = Main.DamageVar(npc.damage);
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

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;

                if (logEnabled)
                {
                    Main.NewText($"← {npc.GivenOrTypeName} hit you", Color.OrangeRed);
                    Main.NewText($"  Phys:  {physFinal,6:F0}  (raw:{physPortion,5:F0}  res:{physRes:F1}%)", Color.Silver);
                    if (firePct  > 0) Main.NewText($"  Fire:  {fireFinal,6:F0}  (raw:{firePortion,5:F0}  res:{fireRes:F1}%)",  new Color(255, 120, 50));
                    if (coldPct  > 0) Main.NewText($"  Cold:  {coldFinal,6:F0}  (raw:{coldPortion,5:F0}  res:{coldRes:F1}%)",  new Color(100, 200, 255));
                    if (lightPct > 0) Main.NewText($"  Light: {lightFinal,6:F0}  (raw:{lightPortion,5:F0}  res:{lightRes:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {info.Damage}", Color.OrangeRed);
                }
            };
        }
    }
}
