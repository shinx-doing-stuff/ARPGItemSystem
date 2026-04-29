namespace ARPGItemSystem.Common.Affixes
{
    public struct Affix
    {
        public AffixId Id;
        public int Magnitude;
        public int Tier;

        public Affix(AffixId id, int magnitude, int tier)
        {
            Id = id;
            Magnitude = magnitude;
            Tier = tier;
        }
    }
}
