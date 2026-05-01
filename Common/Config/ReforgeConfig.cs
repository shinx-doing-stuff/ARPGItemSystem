using System;

namespace ARPGItemSystem.Common.Config
{
    public static class ReforgeConfig
    {
        public const float Scale = 1.0f;
        public const float Base = 2.0f;

        // Multiplier applied to the empty-slot fill cost on top of CalculateCost.
        public const float EmptySlotMultiplier = 5.0f;

        public static int CalculateCost(int itemValue, int tier)
        {
            return (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));
        }

        // Cost-multiplier table for "Reforge All Unlocked" based on how many
        // affixes the player has locked. Locking many lines and rerolling few
        // is intentionally taxed so surgical play is expensive.
        public static float LockMultiplier(int locks) => locks switch
        {
            0 => 1.0f,
            1 => 1.5f,
            2 => 2.25f,
            3 => 3.5f,
            4 => 5.5f,
            _ => 9.0f
        };
    }
}
