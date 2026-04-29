using System.Collections.Generic;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public class AffixDef
    {
        public AffixId Id { get; init; }
        public AffixKind Kind { get; init; }

        // Per-category tier tables. Each list MUST contain exactly 10 Tier entries.
        public Dictionary<ItemCategory, List<Tier>> Tiers { get; init; }

        // Restricts which weapon DamageClasses this affix can roll on.
        // null = unrestricted. Only consulted when category == ItemCategory.Weapon.
        public HashSet<DamageClass> AllowedDamageClasses { get; init; }
    }
}
