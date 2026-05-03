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
                            new(17,20), new(16,17), new(12,16), new(11,12), new(8,11),
                            new(7,8),   new(5,7),   new(4,5),   new(1,4),   new(1,1)
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
                            new(8,11), new(8,8), new(7,8), new(7,7), new(5,7),
                            new(5,5),  new(4,5), new(4,4), new(1,1), new(1,1)
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
                            new(47,53), new(41,45), new(33,39), new(30,32), new(24,27),
                            new(20,23), new(14,18), new(9,12),  new(6,8),   new(2,5)
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
                            new(71,78), new(63,70), new(55,62), new(47,54), new(40,46),
                            new(32,39), new(24,31), new(16,23), new(8,15),  new(1,7)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.VelocityIncrease,
                    Kind = AffixKind.Prefix,
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
                    Id = AffixId.IncreasedColdDamage,
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
                    Id = AffixId.IncreasedLightningDamage,
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
                            new(23,26), new(23,26), new(17,20), new(17,20), new(8,16),
                            new(8,16),  new(7,11),  new(7,11),  new(1,5),   new(1,5)
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
                            new(8,11), new(8,8), new(7,7), new(7,7), new(5,5),
                            new(5,5),  new(4,4), new(4,4), new(1,1), new(1,1)
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
                            new(48,52), new(43,47), new(38,42), new(32,36), new(28,31),
                            new(23,26), new(17,20), new(12,16), new(7,11),  new(1,5)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageGenericDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,18), new(13,14), new(11,12), new(8,10), new(7,7),
                            new(6,6),   new(5,5),   new(4,4),   new(2,2),  new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(7,8), new(7,8), new(5,7), new(5,7), new(4,5),
                            new(4,5), new(1,4), new(1,4), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMeleeDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(11,12), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),   new(4,5),  new(4,5), new(1,4), new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageRangedDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(11,12), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),   new(4,5),  new(4,5), new(1,4), new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageMagicDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(11,12), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),   new(4,5),  new(4,5), new(1,4), new(1,4)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.PercentageSummonDamageIncrease,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(16,24), new(13,22), new(11,19), new(8,17), new(7,14),
                            new(6,12),  new(5,10),  new(4,7),   new(2,5),  new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(11,12), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),   new(4,5),  new(4,5), new(1,4), new(1,4)
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
                            new(8,11), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),  new(4,5),  new(4,5), new(1,4), new(1,4)
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
                            new(8,11), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),  new(4,5),  new(4,5), new(1,4), new(1,4)
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
                            new(8,11), new(8,11), new(7,8), new(7,8), new(5,7),
                            new(5,7),  new(4,5),  new(4,5), new(1,4), new(1,4)
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
                            new(8,11), new(8,8), new(7,8), new(7,7), new(5,7),
                            new(5,5),  new(4,5), new(4,4), new(1,1), new(1,1)
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
                            new(8,11), new(8,8), new(7,8), new(7,7), new(5,7),
                            new(5,5),  new(4,5), new(4,4), new(1,1), new(1,1)
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
                            new(8,11), new(8,8), new(7,8), new(7,7), new(5,7),
                            new(5,5),  new(4,5), new(4,4), new(1,1), new(1,1)
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
                            new(5,5), new(4,5), new(4,5), new(4,4), new(4,4),
                            new(1,4), new(1,4), new(1,1), new(1,1), new(1,1)
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
                            new(10,11), new(9,10), new(8,8),  new(7,8), new(6,7),
                            new(5,5),   new(4,5),  new(3,3),  new(2,3), new(1,1)
                        },
                        [ItemCategory.Armor] = new List<Tier> {
                            new(3,5), new(3,5), new(3,4), new(3,4), new(2,3),
                            new(2,3), new(1,2), new(1,2), new(1,1), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(3,3), new(3,3), new(3,3), new(2,3), new(2,3),
                            new(2,3), new(1,2), new(1,2), new(1,2), new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },
                new AffixDef {
                    Id = AffixId.ManaCostReduction,
                    Kind = AffixKind.Prefix,
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
                            new(7,8), new(5,7), new(5,7), new(4,5), new(4,5),
                            new(4,4), new(1,4), new(1,4), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = magicOnly
                },

                // ============== BATCH-1 AFFIXES (2026-05-03) ==============

                // A.1 — LifeRegeneration: Armor + Accessory, Prefix.
                // Magnitude is in vanilla Player.lifeRegen units (2 = 1 HP/second).
                new AffixDef {
                    Id = AffixId.LifeRegeneration,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(5,6), new(4,5), new(4,4), new(3,4), new(3,3),
                            new(2,3), new(2,2), new(1,2), new(1,1), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(3,4), new(3,3), new(2,3), new(2,3), new(2,2),
                            new(1,2), new(1,2), new(1,1), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // A.2 — ManaRegeneration: Armor + Accessory, Prefix.
                // Magnitude is in vanilla Player.manaRegen units.
                new AffixDef {
                    Id = AffixId.ManaRegeneration,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(10,12), new(8,10), new(7,9), new(6,8), new(5,7),
                            new(4,6),   new(3,5),  new(2,4), new(1,2), new(1,1)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(6,8), new(5,7), new(4,6), new(3,5), new(3,4),
                            new(2,3), new(2,3), new(1,2), new(1,2), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // A.3 — ThornDamage: Armor + Accessory, Suffix. Aggregate cap 80%.
                // Magnitude is percent of incoming damage reflected to attacker.
                new AffixDef {
                    Id = AffixId.ThornDamage,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(30,35), new(25,30), new(20,25), new(16,20), new(13,16),
                            new(10,13), new(7,10),  new(5,7),   new(3,5),   new(1,3)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(30,35), new(25,30), new(20,25), new(16,20), new(13,16),
                            new(10,13), new(7,10),  new(5,7),   new(3,5),   new(1,3)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // A.4 — DamageToManaBeforeLife: Armor + Accessory, PREFIX (per spec §4.1).
                // Aggregate cap 40% (§4.2). Magnitudes ≈0.22× parent (§4.3).
                // Per-hit cap = 25% of statManaMax2 applied at hit time (see PlayerHurtPipeline).
                new AffixDef {
                    Id = AffixId.DamageToManaBeforeLife,
                    Kind = AffixKind.Prefix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Armor] = new List<Tier> {
                            new(8,9), new(7,8), new(6,7), new(5,6), new(4,5),
                            new(4,4), new(3,4), new(2,3), new(2,2), new(1,2)
                        },
                        [ItemCategory.Accessory] = new List<Tier> {
                            new(5,6), new(4,5), new(3,4), new(2,3), new(2,2),
                            new(2,2), new(1,2), new(1,1), new(1,1), new(1,1)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // D.1 — NearbyDamageBonus: Weapon, Suffix. Bonus when target ≤ 256px (16 tiles).
                new AffixDef {
                    Id = AffixId.NearbyDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(15,18), new(13,15), new(11,13), new(9,11), new(7,9),
                            new(5,7),   new(4,5),   new(3,4),   new(2,3),  new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // Same magnitudes as NearbyDamageBonus — coexistence creates a mid-range dead zone.
                new AffixDef {
                    Id = AffixId.DistantDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(15,18), new(13,15), new(11,13), new(9,11), new(7,9),
                            new(5,7),   new(4,5),   new(3,4),   new(2,3),  new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // C.1 — LowHpDamageBonus: Weapon, Suffix. Graduated ramp — full magnitude at HP ≤25%,
                // zero at HP ≥70%, linear between (see §3.1 of spec).
                new AffixDef {
                    Id = AffixId.LowHpDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(16,19), new(13,16), new(11,13), new(9,11), new(7,9),
                            new(5,7),   new(4,5),   new(3,4),   new(2,3),  new(1,2)
                        }
                    },
                    AllowedDamageClasses = null
                },

                // C.2 — FullHpDamageBonus: Weapon, Suffix. Bonus when statLife >= statLifeMax2.
                // Magnitudes verbatim from parent spec (60-70 down to 1-6) — kept asymmetric with C.1
                // because C.1's ramped scaling reduces its effective average; binary FullHp keeps full value.
                new AffixDef {
                    Id = AffixId.FullHpDamageBonus,
                    Kind = AffixKind.Suffix,
                    Tiers = new Dictionary<ItemCategory, List<Tier>>
                    {
                        [ItemCategory.Weapon] = new List<Tier> {
                            new(60,70), new(50,60), new(42,50), new(35,42), new(28,35),
                            new(22,28), new(16,22), new(11,16), new(6,11),  new(1,6)
                        }
                    },
                    AllowedDamageClasses = null
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
