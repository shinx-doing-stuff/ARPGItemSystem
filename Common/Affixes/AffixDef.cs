using System.Collections.Generic;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public class AffixDef
    {
        public AffixId Id;
        public AffixKind Kind;

        // Format string with {0} placeholder for magnitude.
        // Looked up at tooltip-draw time; not stored on Affix instances or in saves.
        public string TooltipFormat;

        // Per-category tier tables. Each list MUST contain exactly 10 Tier entries.
        public Dictionary<ItemCategory, List<Tier>> Tiers;

        // Restricts which weapon DamageClasses this affix can roll on.
        // null = unrestricted. Only consulted when category == ItemCategory.Weapon.
        public HashSet<DamageClass> AllowedDamageClasses;
    }
}
