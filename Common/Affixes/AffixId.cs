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

        // Weapon + Accessory — elemental penetration (subtracts from enemy resistance before cap)
        FirePenetration,
        ColdPenetration,
        LightningPenetration,

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
        FireResistance,
        ColdResistance,
        LightningResistance,

        // All categories
        FlatCritChance,
        ManaCostReduction
    }
}
