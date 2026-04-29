namespace ARPGItemSystem.Common.Affixes
{
    public readonly struct Affix
    {
        public readonly AffixId Id;
        public readonly int Magnitude;
        public readonly int Tier;

        public Affix(AffixId id, int magnitude, int tier)
        {
            Id = id;
            Magnitude = magnitude;
            Tier = tier;
        }
    }
}
