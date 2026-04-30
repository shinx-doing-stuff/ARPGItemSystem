using ARPGEnemySystem.Common.Elements;
using EnemyConfig = ARPGEnemySystem.Common.Configs.Config;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    public class PlayerElementalPlayer : ModPlayer
    {
        public float PhysRes;
        public float FireRes;
        public float ColdRes;
        public float LightningRes;

        public float GetResistance(Element element) => element switch
        {
            Element.Fire      => FireRes,
            Element.Cold      => ColdRes,
            Element.Lightning => LightningRes,
            _                 => PhysRes,
        };

        public override void PostUpdateEquips()
        {
            var cfg = ModContent.GetInstance<EnemyConfig>();
            float cap = cfg.ElementalResistanceCap;

            // Base physRes from vanilla defense (includes FlatDefenseIncrease and PercentageDefenseIncrease
            // affix contributions — they run in UpdateEquip/UpdateAccessory before PostUpdateEquips).
            PhysRes      = ElementalMath.ConvertDefenseToResistance(Player.statDefense, cfg.DefenseToPhysResRatio, cap);
            FireRes      = 0f;
            ColdRes      = 0f;
            LightningRes = 0f;

            // Elemental resistance from affix rolls and the PhysicalResistance affix bonus
            for (int i = 0; i < Player.armor.Length; i++)
            {
                var item = Player.armor[i];
                if (item.IsAir) continue;

                if (item.TryGetGlobalItem<ArmorManager>(out var am))
                    ApplyResistanceAffixes(am.Affixes);
                else if (item.TryGetGlobalItem<AccessoryManager>(out var acc))
                    ApplyResistanceAffixes(acc.Affixes);
            }
        }

        private void ApplyResistanceAffixes(List<Affix> affixes)
        {
            foreach (var a in affixes)
            {
                switch (a.Id)
                {
                    case AffixId.PhysicalResistance:  PhysRes      += a.Magnitude; break;
                    case AffixId.FireResistance:       FireRes      += a.Magnitude; break;
                    case AffixId.ColdResistance:       ColdRes      += a.Magnitude; break;
                    case AffixId.LightningResistance:  LightningRes += a.Magnitude; break;
                    // FlatDefenseIncrease and PercentageDefenseIncrease are NOT handled here —
                    // they already boosted Player.statDefense in UpdateEquip/UpdateAccessory,
                    // which feeds into the ConvertDefenseToResistance call above.
                }
            }
        }
    }
}
