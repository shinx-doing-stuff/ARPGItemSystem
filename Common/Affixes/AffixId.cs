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

        // Weapon-only — elemental damage
        GainPercentAsFire,
        GainPercentAsCold,
        GainPercentAsLightning,
        IncreasedFireDamage,
        IncreasedColdDamage,
        IncreasedLightningDamage,

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

        // Armor + Accessory — elemental resistance
        PhysicalResistance,
        FireResistance,
        ColdResistance,
        LightningResistance,

        // All categories
        FlatCritChance,
        ManaCostReduction
    }
}
