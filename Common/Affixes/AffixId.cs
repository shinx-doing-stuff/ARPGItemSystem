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
        AllElementalPenetration,

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
        ManaCostReduction,

        // Batch-1 (2026-05-03): hurt-pipeline + conditional + distance affixes
        LifeRegeneration,
        ManaRegeneration,
        ThornDamage,
        DamageToManaBeforeLife,
        NearbyDamageBonus,
        DistantDamageBonus,
        LowHpDamageBonus,
        FullHpDamageBonus,

        // Chaos damage type (2026-05-13 — magnitudes ~50% of F/C/L per spec)
        GainPercentAsChaos,
        IncreasedChaosDamage,
        ChaosResistance,
        ChaosPenetration,

        // Hybrid affixes (2026-05-13)
        FortifiedBody,    // +HP (boosted), −Mana
        BalancedGrowth    // +HP, +Mana (both at ~65% of standalone)
    }
}
