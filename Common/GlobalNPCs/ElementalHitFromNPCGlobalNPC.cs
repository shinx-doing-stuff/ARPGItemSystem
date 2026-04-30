using System;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using ARPGItemSystem.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalNPCs
{
    // Applies player elemental resistance to NPC direct contact damage.
    // NPC-sourced projectile elemental hits are deferred to a follow-up spec.
    public class ElementalHitFromNPCGlobalNPC : GlobalNPC
    {
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap   = cfg.ElementalResistanceCap;
            float ratio = cfg.DefenseToPhysResRatio;

            // Read the NPC's elemental damage profile
            float elemDamagePct;
            Element elemType;

            if (npc.TryGetGlobalNPC<NPCManager>(out var npcData))
            {
                elemDamagePct = npcData.ElementalDamagePct;
                elemType      = npcData.ElementalDamageType;
            }
            else if (npc.TryGetGlobalNPC<BossManager>(out var bossData))
            {
                elemDamagePct = bossData.ElementalDamagePct;
                elemType      = bossData.ElementalDamageType;
            }
            else
            {
                return;
            }

            // Read the player's resistance (computed from player.statDefense + affix bonuses in PostUpdateEquips)
            var playerData = target.GetModPlayer<PlayerElementalPlayer>();
            float physRes  = playerData.PhysRes;
            float elemRes  = playerData.GetResistance(elemType);

            // Compute final damage from npc.damage (already scaled by level/rarity/modifiers in PreAI).
            // ModifyHurtInfo callback fires AFTER vanilla statDefense subtraction but overrides the result.
            // Apply vanilla damage variation (±15%) so hits don't deal exactly the same value every time.
            // npc.damage is the post-PreAI scaled base (level/rarity already applied).
            float baseDamage = Main.DamageVar(npc.damage);
            float physPortion = baseDamage * (1f - elemDamagePct / 100f);
            float elemPortion = baseDamage * elemDamagePct / 100f;

            float physFinal = ElementalMath.ApplyResistance(physPortion, physRes, cap);
            float elemFinal = ElementalMath.ApplyResistance(elemPortion, elemRes, cap);

            int finalDamage = Math.Max(1, (int)(physFinal + elemFinal));

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                info.Damage = finalDamage;
            };
        }
    }
}
