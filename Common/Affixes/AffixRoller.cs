using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public static class AffixRoller
    {
        public static Affix? Roll(
            ItemCategory category,
            AffixKind kind,
            Item item,
            IReadOnlyCollection<Affix> existing,
            int tier)
        {
            var weaponClass = category == ItemCategory.Weapon ? item.DamageType : null;
            var existingIds = new HashSet<AffixId>(existing.Select(a => a.Id));

            var pool = AffixRegistry
                .RollPool(category, kind, weaponClass)
                .Where(def => !existingIds.Contains(def.Id))
                .ToList();

            if (pool.Count == 0) return null;

            var def = pool[Main.rand.Next(pool.Count)];
            var range = def.Tiers[category][tier];
            int magnitude = Main.rand.Next(range.Min, range.Max + 1);

            int magnitude2 = 0;
            if (def.SecondaryTiers != null && def.SecondaryTiers.TryGetValue(category, out var secTiers))
                magnitude2 = Main.rand.Next(secTiers[tier].Min, secTiers[tier].Max + 1);

            return new Affix(def.Id, magnitude, magnitude2, tier);
        }
    }
}
