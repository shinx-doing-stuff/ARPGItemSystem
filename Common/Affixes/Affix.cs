namespace ARPGItemSystem.Common.Affixes
{
    public readonly struct Affix
    {
        public readonly AffixId Id;
        public readonly int Magnitude;
        public readonly int Magnitude2;  // 0 for all non-hybrid affixes
        public readonly int Tier;

        public Affix(AffixId id, int magnitude, int magnitude2, int tier)
        {
            Id = id;
            Magnitude = magnitude;
            Magnitude2 = magnitude2;
            Tier = tier;
        }
    }
}
