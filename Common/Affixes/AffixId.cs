namespace ARPGItemSystem.Common.Affixes
{
    public enum AffixId
    {
        None = 0,

        // Weapon-only
        FlatDamageIncrease,
        PercentageDamageIncrease,
        FlatArmorPen,
        PercentageArmorPen,
        AttackSpeedIncrease,
        KnockbackIncrease,
        PercentageCritChance,
        CritMultiplier,
        VelocityIncrease,

        // Armor-only
        PercentageDefenseIncrease,

        // Armor + Accessory
        FlatLifeIncrease,
        FlatDefenseIncrease,
        FlatManaIncrease,
        PercentageGenericDamageIncrease,
        PercentageMeleeDamageIncrease,
        PercentageRangedDamageIncrease,
        PercentageMagicDamageIncrease,
        PercentageSummonDamageIncrease,

        // All categories
        FlatCritChance,
        ManaCostReduction
    }
}
