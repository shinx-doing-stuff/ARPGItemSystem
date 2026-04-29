using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public static class AffixRegistry
    {
        private static readonly Dictionary<AffixId, AffixDef> _all = BuildRegistry();

        public static IReadOnlyDictionary<AffixId, AffixDef> All => _all;

        public static AffixDef Get(AffixId id) => _all[id];

        public static IEnumerable<AffixDef> RollPool(
            ItemCategory category,
            AffixKind kind,
            DamageClass weaponClass = null)
        {
            foreach (var def in _all.Values)
            {
                if (def.Id == AffixId.None) continue;
                if (def.Kind != kind) continue;
                if (!def.Tiers.ContainsKey(category)) continue;
                if (category == ItemCategory.Weapon && def.AllowedDamageClasses != null)
                {
                    if (weaponClass == null || !def.AllowedDamageClasses.Contains(weaponClass))
                        continue;
                }
                yield return def;
            }
        }

        private static Dictionary<AffixId, AffixDef> BuildRegistry()
        {
            var allMeleeAndAbove = new HashSet<DamageClass>
            {
                DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.SummonMeleeSpeed,
                DamageClass.Ranged,
                DamageClass.Magic, DamageClass.MagicSummonHybrid
            };
            var rangedAndMagicOnly = new HashSet<DamageClass>
            {
                DamageClass.Ranged,
                DamageClass.Magic, DamageClass.MagicSummonHybrid
            };
            var magicOnly = new HashSet<DamageClass>
            {
                DamageClass.Magic, DamageClass.MagicSummonHybrid
            };

            var defs = new List<AffixDef>
            {
                // ============== WEAPON PREFIXES ==============
                new AffixDef {
                    Id = AffixId.FlatDamageIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0}% Increased Base Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(51,55), new(46,50), new(41,45), new(36,40), new(31,35),
                            new(26,30), new(21,25), new(16,20), new(11,15), new(5,10)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageDamageIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0}% Increased Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(51,55), new(46,50), new(41,45), new(36,40), new(31,35),
                            new(26,30), new(21,25), new(16,20), new(11,15), new(5,10)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatArmorPen,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0} Added Armor Penetration",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(46,50), new(41,45), new(36,40), new(31,35), new(26,30),
                            new(21,25), new(16,20), new(11,15), new(6,10),  new(1,5)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageArmorPen,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "Ignore {0}% of target defense",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.AttackSpeedIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0}% Increased Attack Speed",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(31,35), new(27,30), new(23,26), new(19,22), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = allMeleeAndAbove
                },
                new AffixDef {
                    Id = AffixId.KnockbackIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0}% Increased Knockback",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(91,100), new(81,90), new(71,80), new(61,70), new(51,60),
                            new(41,50),  new(31,40), new(21,30), new(11,20), new(1,10)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== WEAPON SUFFIXES ==============
                new AffixDef {
                    Id = AffixId.PercentageCritChance,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Critical Strike Chance",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(37,42), new(32,36), new(27,31), new(23,26), new(19,22),
                            new(15,18), new(11,14), new(7,10),  new(4,6),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.CritMultiplier,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Critical Strike Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(91,100), new(81,90), new(71,80), new(61,70), new(51,60),
                            new(41,50),  new(31,40), new(21,30), new(11,20), new(1,10)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.VelocityIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Projectile Velocity",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,70), new(51,60), new(41,50), new(31,40), new(26,30),
                            new(21,25), new(16,20), new(11,15), new(6,10),  new(1,5)
                        }
                    },
                    AllowedDamageClasses = rangedAndMagicOnly
                },

                // ============== ARMOR-ONLY ==============
                new AffixDef {
                    Id = AffixId.PercentageDefenseIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "{0}% Increased Defense",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ARMOR + ACCESSORY ==============
                new AffixDef {
                    Id = AffixId.FlatLifeIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "+{0} Maximum Life",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(46,50), new(41,45), new(36,40), new(31,35), new(26,30),
                            new(21,25), new(16,20), new(11,15), new(6,10),  new(1,5)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(13,15), new(13,15), new(10,12), new(10,12), new(5,9),
                            new(5,9),   new(4,6),   new(4,6),   new(1,3),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatDefenseIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "+{0} Additional Defense",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(5,6), new(5,5), new(4,4), new(4,4), new(3,3),
                            new(3,3), new(2,2), new(2,2), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatManaIncrease,
                    Kind = AffixKind.Prefix,
                    TooltipFormat = "+{0} Maximum Mana",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(46,50), new(41,45), new(36,40), new(31,35), new(26,30),
                            new(21,25), new(16,20), new(11,15), new(6,10),  new(1,5)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageGenericDamageIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(13,15), new(11,12), new(9,10), new(7,8), new(6,6),
                            new(5,5),   new(4,4),   new(3,3),  new(2,2), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(4,5), new(4,5), new(3,4), new(3,4), new(2,3),
                            new(2,3), new(1,2), new(1,2), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMeleeDamageIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Melee Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(13,20), new(11,18), new(9,16), new(7,14), new(6,12),
                            new(5,10),  new(4,8),   new(3,6),  new(2,4),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(5,6), new(4,5), new(4,5), new(3,4),
                            new(3,4), new(2,3), new(2,3), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageRangedDamageIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Ranged Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(13,20), new(11,18), new(9,16), new(7,14), new(6,12),
                            new(5,10),  new(4,8),   new(3,6),  new(2,4),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(5,6), new(4,5), new(4,5), new(3,4),
                            new(3,4), new(2,3), new(2,3), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMagicDamageIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Magic Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(13,20), new(11,18), new(9,16), new(7,14), new(6,12),
                            new(5,10),  new(4,8),   new(3,6),  new(2,4),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(5,6), new(4,5), new(4,5), new(3,4),
                            new(3,4), new(2,3), new(2,3), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageSummonDamageIncrease,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Increased Summon Damage",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(13,20), new(11,18), new(9,16), new(7,14), new(6,12),
                            new(5,10),  new(4,8),   new(3,6),  new(2,4),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(5,6), new(4,5), new(4,5), new(3,4),
                            new(3,4), new(2,3), new(2,3), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ALL CATEGORIES ==============
                new AffixDef {
                    Id = AffixId.FlatCritChance,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Additional Critical Strike Chance",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(19,20), new(17,18), new(15,16), new(13,14), new(11,12),
                            new(9,10),  new(7,8),   new(5,6),   new(3,4),   new(1,2)
                        },
                        [ItemCategory.Armor] = new List<Tier> {
                            new(5,10), new(5,10), new(4,8), new(4,8), new(3,6),
                            new(3,6),  new(2,4),  new(2,4), new(1,2), new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(3,4), new(3,4), new(3,4), new(2,3), new(2,3),
                            new(2,3), new(1,2), new(1,2), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.ManaCostReduction,
                    Kind = AffixKind.Suffix,
                    TooltipFormat = "{0}% Reduced Mana Cost",
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(28,30), new(25,27), new(22,24), new(19,21), new(16,18),
                            new(13,15), new(10,12), new(7,9),   new(4,6),   new(1,3)
                        },
                        [ItemCategory.Armor] = new List<Tier> {
                            new(5,10), new(4,8), new(3,6), new(3,6), new(3,6),
                            new(2,4),  new(2,4), new(2,4), new(1,2), new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(4,5), new(3,4), new(3,4), new(2,3), new(2,3),
                            new(2,2), new(1,2), new(1,2), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = magicOnly
                }
            };

            return defs.ToDictionary(d => d.Id);
        }
    }
}
