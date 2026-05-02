using System;
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
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,66), new(55,60), new(49,54), new(43,48), new(37,42),
                            new(31,36), new(25,30), new(19,24), new(13,18), new(6,12)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,66), new(55,60), new(49,54), new(43,48), new(37,42),
                            new(31,36), new(25,30), new(19,24), new(13,18), new(6,12)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatArmorPen,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(14,17), new(13,14), new(10,13), new(9,10), new(7,9),
                            new(6,7),   new(4,6),   new(3,4),   new(1,3),  new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageArmorPen,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,7), new(6,7), new(6,6), new(4,6),
                            new(4,4), new(3,4), new(3,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.AttackSpeedIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(37,42), new(32,36), new(28,31), new(23,26), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = allMeleeAndAbove
                },
                new AffixDef {
                    Id = AffixId.KnockbackIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(109,120), new(97,108), new(85,96), new(73,84), new(61,72),
                            new(49,60),   new(37,48),  new(25,36), new(13,24), new(1,12)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== WEAPON PREFIXES — ELEMENTAL ==============
                new AffixDef {
                    Id = AffixId.GainPercentAsFire,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.GainPercentAsCold,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.GainPercentAsLightning,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== WEAPON SUFFIXES ==============
                new AffixDef {
                    Id = AffixId.PercentageCritChance,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(44,50), new(38,43), new(32,37), new(28,31), new(23,26),
                            new(18,22), new(13,17), new(8,12),  new(5,7),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.CritMultiplier,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(109,120), new(97,108), new(85,96), new(73,84), new(61,72),
                            new(49,60),   new(37,48),  new(25,36), new(13,24), new(1,12)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.VelocityIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(73,84), new(61,72), new(49,60), new(37,48), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = rangedAndMagicOnly
                },

                // ============== WEAPON SUFFIXES — ELEMENTAL ==============
                new AffixDef {
                    Id = AffixId.IncreasedFireDamage,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,66), new(55,60), new(49,54), new(43,48), new(37,42),
                            new(31,36), new(25,30), new(19,24), new(13,18), new(6,12)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.IncreasedColdDamage,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,66), new(55,60), new(49,54), new(43,48), new(37,42),
                            new(31,36), new(25,30), new(19,24), new(13,18), new(6,12)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.IncreasedLightningDamage,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(61,66), new(55,60), new(49,54), new(43,48), new(37,42),
                            new(31,36), new(25,30), new(19,24), new(13,18), new(6,12)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ARMOR-ONLY ==============
                new AffixDef {
                    Id = AffixId.PercentageDefenseIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ARMOR + ACCESSORY ==============
                new AffixDef {
                    Id = AffixId.FlatLifeIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(19,22), new(19,22), new(14,17), new(14,17), new(7,13),
                            new(7,13),  new(6,9),   new(6,9),   new(1,4),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatDefenseIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,7), new(6,6), new(6,6), new(4,4),
                            new(4,4), new(3,3), new(3,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.FlatManaIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(55,60), new(49,54), new(43,48), new(37,42), new(31,36),
                            new(25,30), new(19,24), new(13,18), new(7,12),  new(1,6)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(40,43), new(36,39), new(32,35), new(27,30), new(23,26),
                            new(19,22), new(14,17), new(10,13), new(6,9),   new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageGenericDamageIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,18), new(13,14), new(11,12), new(8,10), new(7,7),
                            new(6,6),   new(5,5),   new(4,4),   new(2,2),  new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(6,7), new(4,6), new(4,6), new(3,4),
                            new(3,4), new(1,3), new(1,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMeleeDamageIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(9,10), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6),  new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageRangedDamageIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(9,10), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6),  new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMagicDamageIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(9,10), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6),  new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageSummonDamageIncrease,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(9,10), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6),  new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ARMOR + ACCESSORY — ELEMENTAL RESISTANCE ==============
                new AffixDef {
                    Id = AffixId.FireResistance,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6), new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.ColdResistance,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6), new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.LightningResistance,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,9), new(6,7), new(6,7), new(4,6),
                            new(4,6), new(3,4), new(3,4), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== WEAPON + ACCESSORY — ELEMENTAL PENETRATION ==============
                new AffixDef {
                    Id = AffixId.FirePenetration,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,7), new(6,7), new(6,6), new(4,6),
                            new(4,4), new(3,4), new(3,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.ColdPenetration,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,7), new(6,7), new(6,6), new(4,6),
                            new(4,4), new(3,4), new(3,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.LightningPenetration,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,9), new(7,7), new(6,7), new(6,6), new(4,6),
                            new(4,4), new(3,4), new(3,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.AllElementalPenetration,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(19,22), new(17,18), new(14,16), new(12,14), new(11,12),
                            new(8,10),  new(6,7),   new(5,6),   new(2,4),   new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(4,4), new(3,4), new(3,4), new(3,3), new(3,3),
                            new(1,3), new(1,3), new(1,1), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // ============== ALL CATEGORIES ==============
                new AffixDef {
                    Id = AffixId.FlatCritChance,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(23,24), new(20,22), new(18,19), new(16,17), new(13,14),
                            new(11,12), new(8,10),  new(6,7),   new(4,5),   new(1,2)
                        },
                        [ItemCategory.Armor] = new List<Tier> {
                            new(6,12), new(6,12), new(5,10), new(5,10), new(4,7),
                            new(4,7),  new(2,5),  new(2,5),  new(1,2),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(4,6), new(4,6), new(4,6), new(3,4), new(3,4),
                            new(3,4), new(1,3), new(1,3), new(1,3), new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.ManaCostReduction,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(34,36), new(30,32), new(26,29), new(23,25), new(19,22),
                            new(16,18), new(12,14), new(8,11),  new(5,7),   new(1,4)
                        },
                        [ItemCategory.Armor] = new List<Tier> {
                            new(6,12), new(5,10), new(4,7), new(4,7), new(4,7),
                            new(2,5),  new(2,5),  new(2,5), new(1,2), new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,7), new(4,6), new(4,6), new(3,4), new(3,4),
                            new(3,3), new(1,3), new(1,3), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = magicOnly
                }
            };

            foreach (var def in defs)
                foreach (var (cat, list) in def.Tiers)
                    if (list.Count != 10)
                        throw new Exception($"AffixDef {def.Id} category {cat} has {list.Count} tier entries, expected 10");

            return defs.ToDictionary(d => d.Id);
        }
    }
}
