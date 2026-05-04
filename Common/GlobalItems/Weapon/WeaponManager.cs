using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Elements;
using EnemyConfigClient = ARPGEnemySystem.Common.Configs.ConfigClient;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Weapon
{
    public class WeaponManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Weapon;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage > 0 && entity.maxStack <= 1;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesWeapon();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesWeapon();

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatDamageIncrease:
                        damage.Base += a.Magnitude / 100f * item.OriginalDamage;
                        break;
                    case AffixId.PercentageDamageIncrease:
                        damage *= 1 + a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatCritChance:
                        crit += a.Magnitude;
                        break;
                    case AffixId.PercentageCritChance:
                        crit *= 1 + a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
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
                            Main.NewText($"  [Nearby/melee] dist={dist / 16f:F1}t (≤16t)  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    case AffixId.DistantDamageBonus:
                    {
                        float dist = Vector2.Distance(player.Center, target.Center);
                        bool applied = dist >= 608f;
                        if (applied)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        if (logEnabled)
                            Main.NewText($"  [Distant/melee] dist={dist / 16f:F1}t (≥48t)  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
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
                            Main.NewText($"  [LowHp/melee] hp={hpPct:P0}  factor={factor:F2}  bonus=+{bonus * 100f:F1}% (max +{a.Magnitude}%)", factor > 0 ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    case AffixId.FullHpDamageBonus:
                    {
                        bool applied = player.statLife >= player.statLifeMax2;
                        if (applied)
                            modifiers.SourceDamage += a.Magnitude / 100f;
                        if (logEnabled)
                            Main.NewText($"  [FullHp/melee] hp={player.statLife}/{player.statLifeMax2}  applied={applied}  +{a.Magnitude}%", applied ? Color.LightGreen : Color.Gray);
                        break;
                    }
                }
            }

            ElementalDamageCalculator.ApplyToHit(Affixes, player, target, ref modifiers);
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.KnockbackIncrease)
                    knockback += a.Magnitude / 100f;
            }
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.VelocityIncrease)
                    velocity *= 1 + a.Magnitude / 100f;
            }
        }

        public override float UseSpeedMultiplier(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.AttackSpeedIncrease)
                    return base.UseSpeedMultiplier(item, player) + a.Magnitude / 100f;
            }
            return base.UseSpeedMultiplier(item, player);
        }

        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.ManaCostReduction)
                    reduce -= a.Magnitude / 100f;
            }
        }

        // Inserts +X Fire/Cold/Lightning lines directly under the vanilla "Damage" line.
        // Gained number is derived from the integer already shown in the Damage line so the
        // math is verifiable: 20% of displayed 175 = 35. Increased% is folded in to match
        // ElementalDamageCalculator's pre-resistance formula (raw = phys × gain × (1+inc)).
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            base.ModifyTooltips(item, tooltips);

            int damageIdx = tooltips.FindIndex(t => t.Mod == "Terraria" && t.Name == "Damage");
            if (damageIdx < 0)
                return;

            var match = Regex.Match(tooltips[damageIdx].Text, @"\d+");
            if (!match.Success || !int.TryParse(match.Value, out int displayedDamage) || displayedDamage <= 0)
                return;

            int gainFire = 0, gainCold = 0, gainLight = 0;
            int incFire = 0, incCold = 0, incLight = 0;
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.GainPercentAsFire:        gainFire  = a.Magnitude; break;
                    case AffixId.GainPercentAsCold:        gainCold  = a.Magnitude; break;
                    case AffixId.GainPercentAsLightning:   gainLight = a.Magnitude; break;
                    case AffixId.IncreasedFireDamage:      incFire   = a.Magnitude; break;
                    case AffixId.IncreasedColdDamage:      incCold   = a.Magnitude; break;
                    case AffixId.IncreasedLightningDamage: incLight  = a.Magnitude; break;
                }
            }

            int idx = damageIdx + 1;
            idx = TryInsertElementalLine(tooltips, idx, displayedDamage, gainFire,  incFire,  "GainedFire",      new Color(255, 120,  50));
            idx = TryInsertElementalLine(tooltips, idx, displayedDamage, gainCold,  incCold,  "GainedCold",      new Color(100, 200, 255));
            idx = TryInsertElementalLine(tooltips, idx, displayedDamage, gainLight, incLight, "GainedLightning", new Color(255, 240,  80));
        }

        private int TryInsertElementalLine(List<TooltipLine> tooltips, int idx, int displayed, int gainPct, int incPct, string key, Color color)
        {
            if (gainPct <= 0)
                return idx;

            int gained = (int)Math.Round(displayed * gainPct / 100f * (1f + incPct / 100f));
            if (gained <= 0)
                return idx;

            string text = Language.GetTextValue($"Mods.ARPGItemSystem.WeaponTooltip.{key}", gained);
            tooltips.Insert(idx, new TooltipLine(Mod, $"ARPG_{key}", text) { OverrideColor = color });
            return idx + 1;
        }
    }
}
