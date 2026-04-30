using System;
using System.Collections.Generic;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using ARPGItemSystem.Common.Affixes;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Elements
{
    public static class ElementalDamageCalculator
    {
        // Returns the magnitude of the given affix id, or 0 if absent.
        // Uses a loop — safe for readonly struct Affix (avoids LINQ issues).
        private static int GetMagnitude(List<Affix> affixes, AffixId id)
        {
            foreach (var a in affixes)
                if (a.Id == id) return a.Magnitude;
            return 0;
        }

        // Registers a ModifyHitInfo callback that computes elemental damage and applies all resistances.
        //
        // Hook ordering guarantee:
        //   1. ModifyHitNPC (attacker) — this method runs here; reads target.defense BEFORE it is zeroed
        //   2. ModifyIncomingHit (defender) — NPCManager zeros modifiers.Defense
        //   3. Vanilla math — info.Damage = weapon output × crit (defense = 0, no subtraction)
        //   4. ModifyHitInfo callback — our override runs here
        //
        // Elemental base = info.Damage (crit, ammo, class bonuses all already included).
        // Crit propagates to elemental proportionally — no undo needed.
        public static void ApplyToHit(
            List<Affix> affixes,
            Player player,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap   = cfg.ElementalResistanceCap;
            float ratio = cfg.DefenseToPhysResRatio;

            // --- Read enemy elemental resistances ---
            float fireRes = 0f, coldRes = 0f, lightRes = 0f;
            if (target.TryGetGlobalNPC<NPCManager>(out var npcData))
            {
                fireRes  = npcData.FireResistance;
                coldRes  = npcData.ColdResistance;
                lightRes = npcData.LightningResistance;
            }
            else if (target.TryGetGlobalNPC<BossManager>(out var bossData))
            {
                fireRes  = bossData.FireResistance;
                coldRes  = bossData.ColdResistance;
                lightRes = bossData.LightningResistance;
            }

            // --- Compute enemy physRes from defense (read BEFORE ModifyIncomingHit zeroes it) ---
            float physRes = ElementalMath.ConvertDefenseToResistance(target.defense, ratio, cap);

            // --- Apply armor pen affixes to enemy physRes (before cap) ---
            float flatArmorPen = GetMagnitude(affixes, AffixId.FlatArmorPen);
            float percArmorPen = GetMagnitude(affixes, AffixId.PercentageArmorPen);
            physRes -= flatArmorPen;
            if (percArmorPen > 0f)
                physRes *= (1f - percArmorPen / 100f);

            // --- Read elemental gain and increased% affixes ---
            float gainFire  = GetMagnitude(affixes, AffixId.GainPercentAsFire);
            float gainCold  = GetMagnitude(affixes, AffixId.GainPercentAsCold);
            float gainLight = GetMagnitude(affixes, AffixId.GainPercentAsLightning);
            float incFire   = GetMagnitude(affixes, AffixId.IncreasedFireDamage);
            float incCold   = GetMagnitude(affixes, AffixId.IncreasedColdDamage);
            float incLight  = GetMagnitude(affixes, AffixId.IncreasedLightningDamage);

            // --- Register callback (all pre-computed — no modifiers capture needed) ---
            modifiers.ModifyHitInfo += (ref NPC.HitInfo info) =>
            {
                // info.Damage = engine-computed physical after defense zeroed; crit applied if crit
                float phys = info.Damage;

                // Elemental = phys × gainPct × (1 + increasedPct)
                // crit is already in phys, so elemental inherits crit proportionally
                float rawFire  = phys * gainFire  / 100f * (1f + incFire  / 100f);
                float rawCold  = phys * gainCold  / 100f * (1f + incCold  / 100f);
                float rawLight = phys * gainLight / 100f * (1f + incLight / 100f);

                float physFinal  = ElementalMath.ApplyResistance(phys,     physRes,  cap);
                float fireFinal  = ElementalMath.ApplyResistance(rawFire,  fireRes,  cap);
                float coldFinal  = ElementalMath.ApplyResistance(rawCold,  coldRes,  cap);
                float lightFinal = ElementalMath.ApplyResistance(rawLight, lightRes, cap);

                info.Damage = Math.Max(1, (int)(physFinal + fireFinal + coldFinal + lightFinal));
            };
        }
    }
}
