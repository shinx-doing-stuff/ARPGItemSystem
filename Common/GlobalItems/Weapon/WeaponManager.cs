using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Terraria;
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
        // ARPGCharacterSystem.Common.Combat.ElementalDamageCalculator's pre-resistance formula (raw = phys × gain × (1+inc)).
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
