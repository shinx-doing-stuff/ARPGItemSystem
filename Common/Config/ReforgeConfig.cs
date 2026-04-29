using System;

namespace ARPGItemSystem.Common.Config
{
    public static class ReforgeConfig
    {
        public const float Scale = 1.0f;
        public const float Base = 2.0f;

        public static int CalculateCost(int itemValue, int tier)
        {
            return (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));
        }
    }
}
