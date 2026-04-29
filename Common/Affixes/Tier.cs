namespace ARPGItemSystem.Common.Affixes
{
    public readonly struct Tier
    {
        public readonly int Min;
        public readonly int Max;

        public Tier(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}
