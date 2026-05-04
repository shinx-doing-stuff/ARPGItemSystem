using System;

namespace ARPGItemSystem.Common.Config
{
    public static class ReforgeConfig
    {
        // Linear scale applied to all costs. Lower = cheaper across the board.
        public const float Scale = 0.45f;

        // Base of the exponential curve. Lower = flatter late-game cost increase.
        public const float Base = 1.4f;

        // Multiplier per locked affix: cost x LockTax^locks.
        public const float LockTax = 1.5f;

        // Fill-empty-slot costs this many times the standard per-affix cost.
        public const float EmptySlotMultiplier = 3.0f;

        // Cost for one affix at the given quality tier.
        // tier 0 = best quality (most expensive), tier 9 = worst (cheapest).
        public static int CalculateCost(int itemValue, int tier)
            => (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));

        // Cost multiplier based on how many affixes the player is locking.
        // Locking is a premium: each additional lock multiplies total cost by LockTax.
        public static float LockMultiplier(int locks)
            => (float)Math.Pow(LockTax, locks);
    }
}
