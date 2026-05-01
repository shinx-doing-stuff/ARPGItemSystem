using System;
using System.Collections.Generic;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using ARPGEnemySystem.Common.Elements;
using ARPGEnemySystem.Common.GlobalNPCs;
using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
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
            float cap       = cfg.ElementalResistanceCap;
            float halfPoint = cfg.PhysResHalfPoint;

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

            // --- Apply elemental penetration (subtracts from raw resistance before cap) ---
            // Semantics: pen reduces the stored % directly, then ApplyResistance clamps to cap.
            // e.g. 100% raw fire res - 20% pen = 80% effective, clamped to 75% cap → still 75%.
            // This preserves the over-cap intent: pen only matters when enemy res is near/below cap.
            var playerElem = player.GetModPlayer<PlayerElementalPlayer>();
            float firePen  = GetMagnitude(affixes, AffixId.FirePenetration)  + playerElem.FirePen;
            float coldPen  = GetMagnitude(affixes, AffixId.ColdPenetration)  + playerElem.ColdPen;
            float lightPen = GetMagnitude(affixes, AffixId.LightningPenetration) + playerElem.LightningPen;
            fireRes  -= firePen;
            coldRes  -= coldPen;
            lightRes -= lightPen;

            // --- Apply armor pen to effective defense BEFORE converting to physRes ---
            // FlatArmorPen reduces the enemy's defense value (e.g. FlatArmorPen=20 on a 60-defense enemy
            // → effectiveDefense=40 → physRes=40×ratio, not physRes-20%). Semantics: pen reduces defense,
            // resistance is derived from the reduced defense, not subtracted from resistance directly.
            float flatArmorPen = GetMagnitude(affixes, AffixId.FlatArmorPen);
            float percArmorPen = GetMagnitude(affixes, AffixId.PercentageArmorPen);
            float effectiveDefense = Math.Max(0f, target.defense - flatArmorPen);
            if (percArmorPen != 0)
                effectiveDefense *= (1f - percArmorPen / 100f);

            // --- Compute enemy physRes from effective defense (read BEFORE ModifyIncomingHit zeroes it) ---
            float physRes = ElementalMath.ConvertDefenseToResistance(effectiveDefense, halfPoint, cap);

            // --- Read elemental gain and increased% affixes ---
            float gainFire  = GetMagnitude(affixes, AffixId.GainPercentAsFire);
            float gainCold  = GetMagnitude(affixes, AffixId.GainPercentAsCold);
            float gainLight = GetMagnitude(affixes, AffixId.GainPercentAsLightning);
            float incFire   = GetMagnitude(affixes, AffixId.IncreasedFireDamage);
            float incCold   = GetMagnitude(affixes, AffixId.IncreasedColdDamage);
            float incLight  = GetMagnitude(affixes, AffixId.IncreasedLightningDamage);

            bool logEnabled = player.whoAmI == Main.myPlayer
                && Main.netMode != NetmodeID.Server
                && ModContent.GetInstance<EnemyConfigClient>()?.EnableElementalDamageLog == true;

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

                info.Damage = Math.Max(1, (int)Math.Round(physFinal + fireFinal + coldFinal + lightFinal));

                if (logEnabled)
                {
                    string crit = info.Crit ? " [CRIT]" : "";
                    Main.NewText($"→ {target.GivenOrTypeName}{crit}", Color.White);
                    Main.NewText($"  Phys:  {physFinal,6:F1}  (raw:{phys,5:F0}  res:{physRes:F1}%)", Color.Silver);
                    if (rawFire  > 0) Main.NewText($"  Fire:  {fireFinal,6:F1}  (raw:{rawFire,5:F1}  res:{fireRes:F1}%)", new Color(255, 120, 50));
                    if (rawCold  > 0) Main.NewText($"  Cold:  {coldFinal,6:F1}  (raw:{rawCold,5:F1}  res:{coldRes:F1}%)", new Color(100, 200, 255));
                    if (rawLight > 0) Main.NewText($"  Light: {lightFinal,6:F1}  (raw:{rawLight,5:F1}  res:{lightRes:F1}%)", new Color(255, 240, 80));
                    Main.NewText($"  Total: {info.Damage}", Color.GreenYellow);
                }
            };
        }
    }
}
