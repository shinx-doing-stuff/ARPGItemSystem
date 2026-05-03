using System;
using System.Collections.Generic;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    public class PlayerSurvivalPlayer : ModPlayer
    {
        public float ThornsPercent;       // capped at 80%
        public float ManaAbsorbPercent;   // capped at 40% (per spec §4.2)

        public override void PostUpdateEquips()
        {
            ThornsPercent = 0f;
            ManaAbsorbPercent = 0f;

            for (int i = 0; i < Player.armor.Length; i++)
            {
                var item = Player.armor[i];
                if (item.IsAir) continue;

                if (item.TryGetGlobalItem<ArmorManager>(out var am))
                    Apply(am.Affixes);
                else if (item.TryGetGlobalItem<AccessoryManager>(out var acc))
                    Apply(acc.Affixes);
            }

            ThornsPercent     = Math.Min(ThornsPercent,     80f);
            ManaAbsorbPercent = Math.Min(ManaAbsorbPercent, 40f);
        }

        private void Apply(List<Affix> affixes)
        {
            foreach (var a in affixes)
            {
                switch (a.Id)
                {
                    case AffixId.ThornDamage:
                        ThornsPercent += a.Magnitude;
                        break;
                    case AffixId.DamageToManaBeforeLife:
                        ManaAbsorbPercent += a.Magnitude;
                        break;
                }
            }
        }
    }
}
