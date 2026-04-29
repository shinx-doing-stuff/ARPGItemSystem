# Affix Pool Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the three duplicated per-category modifier systems (Weapon/Armor/Accessory) with a single registry-driven affix system, lifting Save/Load/NetSend/NetReceive into a shared base class and looking up tooltip text at draw time.

**Architecture:** New namespace `ARPGItemSystem.Common.Affixes` holds a unified `AffixId` enum, an `Affix` struct, an `AffixDef` registry entry, an `AffixRegistry` (built from a single declaration list), an `AffixRoller`, and an `AffixItemManager` base class. The three concrete managers (`WeaponManager`/`ArmorManager`/`AccessoryManager`) become thin subclasses that own only their stat-apply hooks. Migration is implicit: new save tag keys (`"AffixIds"`, etc.) replace the old per-category keys. Old saves trigger a fresh reroll because `tag.ContainsKey("AffixIds")` returns false.

**Tech Stack:** tModLoader for Terraria (.NET 6, C#), no automated test framework — verification is `dotnet build` plus in-game manual testing.

**Verification model:** This project has no test harness ([CLAUDE.md](../../CLAUDE.md): "There are no automated tests"). Each task ends with either a build check (`dotnet build`) or a deliberate "no build yet — interim state" note. A manual in-game test plan runs at the end.

**Spec:** [docs/superpowers/specs/2026-04-29-affix-pool-refactor-design.md](../specs/2026-04-29-affix-pool-refactor-design.md)

---

## Phase 1 — Add new infrastructure (builds continuously alongside old code)

The new types in `Common/Affixes/` are additive. Old code (`Weapon/Armor/Accessory` namespaces) remains unchanged; `dotnet build` should succeed after every task.

### Task 1: Create the namespace folder and basic enum/struct files

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/ItemCategory.cs`
- Create: `ARPGItemSystem/Common/Affixes/AffixKind.cs`
- Create: `ARPGItemSystem/Common/Affixes/Tier.cs`

**Why three small files instead of one:** Each type is conceptually independent. They are referenced from many other files; small files reduce merge friction and make navigation easier in the Solution Explorer.

- [ ] **Step 1.1: Create ItemCategory.cs**

Content of `ARPGItemSystem/Common/Affixes/ItemCategory.cs`:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    // Byte values must match Common.Network.ItemCategory for wire-format compatibility:
    // 0=Weapon, 1=Armor, 2=Accessory. The network enum is removed in Phase 2.
    public enum ItemCategory : byte
    {
        Weapon = 0,
        Armor = 1,
        Accessory = 2
    }
}
```

- [ ] **Step 1.2: Create AffixKind.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixKind.cs`:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    public enum AffixKind : byte
    {
        Prefix = 0,
        Suffix = 1
    }
}
```

- [ ] **Step 1.3: Create Tier.cs**

Content of `ARPGItemSystem/Common/Affixes/Tier.cs`:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    public readonly struct Tier
    {
        public readonly int Min;
        public readonly int Max;

        public Tier(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}
```

- [ ] **Step 1.4: Build check**

Run from the `ARPGItemSystem` folder:
```
dotnet build
```
Expected: build succeeds with no errors.

- [ ] **Step 1.5: Commit**

```
git add Common/Affixes/ItemCategory.cs Common/Affixes/AffixKind.cs Common/Affixes/Tier.cs
git commit -m "feat(affixes): add ItemCategory, AffixKind, Tier primitive types"
```

---

### Task 2: Create AffixId enum

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/AffixId.cs`

- [ ] **Step 2.1: Create AffixId.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixId.cs`:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    // Single source of truth for all affix identities across weapons, armor, and accessories.
    // Adding a new entry here requires a corresponding registration in AffixRegistry and a
    // case in the relevant manager's stat-apply switch.
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
```

- [ ] **Step 2.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 2.3: Commit**

```
git add Common/Affixes/AffixId.cs
git commit -m "feat(affixes): add unified AffixId enum"
```

---

### Task 3: Create Affix struct (rolled instance) and AffixDef class (registry entry)

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/Affix.cs`
- Create: `ARPGItemSystem/Common/Affixes/AffixDef.cs`

- [ ] **Step 3.1: Create Affix.cs**

Content of `ARPGItemSystem/Common/Affixes/Affix.cs`:

```csharp
namespace ARPGItemSystem.Common.Affixes
{
    // A rolled affix instance attached to an item.
    // Kind/tooltip/value-range come from AffixRegistry.Get(Id) at use time.
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
```

- [ ] **Step 3.2: Create AffixDef.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixDef.cs`:

```csharp
using System.Collections.Generic;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    // Static metadata describing a single affix in the registry.
    public class AffixDef
    {
        public AffixId Id;
        public AffixKind Kind;

        // Format string with {0} placeholder for magnitude. e.g. "{0}% Increased Damage".
        // Looked up at tooltip-draw time; not stored on Affix instances or in saves.
        public string TooltipFormat;

        // Per-category tier tables. Each list MUST contain exactly 10 Tier entries
        // (matching the tier indexing used by utils.GetTier()).
        public Dictionary<ItemCategory, List<Tier>> Tiers;

        // Restricts which weapon DamageClasses this affix can roll on.
        // null = unrestricted. Only consulted when category == ItemCategory.Weapon.
        public HashSet<DamageClass> AllowedDamageClasses;
    }
}
```

- [ ] **Step 3.3: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 3.4: Commit**

```
git add Common/Affixes/Affix.cs Common/Affixes/AffixDef.cs
git commit -m "feat(affixes): add Affix struct and AffixDef registry-entry class"
```

---

### Task 4: Create AffixRegistry skeleton (lookup methods, empty data)

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/AffixRegistry.cs`

This task creates the public API and an empty data list. Task 5 fills in all 20 affix entries.

- [ ] **Step 4.1: Create AffixRegistry.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixRegistry.cs`:

```csharp
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

        // Returns all affix definitions eligible to roll for the given (category, kind),
        // honoring AllowedDamageClasses for weapons. Excludes None.
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
            var defs = new List<AffixDef>
            {
                // Populated in Task 5
            };

            return defs.ToDictionary(d => d.Id);
        }
    }
}
```

- [ ] **Step 4.2: Build check**

Run `dotnet build` — expect success (the registry is empty but compiles).

- [ ] **Step 4.3: Commit**

```
git add Common/Affixes/AffixRegistry.cs
git commit -m "feat(affixes): add AffixRegistry skeleton with lookup API"
```

---

### Task 5: Populate AffixRegistry with all 20 affix declarations

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixRegistry.cs` — replace the `BuildRegistry` method body

Tier values are copied verbatim from [Common/GlobalItems/Database/TierDatabase.cs](../../Common/GlobalItems/Database/TierDatabase.cs). Damage-class restrictions are derived from the four hardcoded lists in [Common/GlobalItems/Weapon/WeaponModifier.cs:53-60](../../Common/GlobalItems/Weapon/WeaponModifier.cs#L53-L60).

- [ ] **Step 5.1: Replace the empty defs list with all declarations**

Replace the entire `BuildRegistry()` method in `AffixRegistry.cs` with:

```csharp
private static Dictionary<AffixId, AffixDef> BuildRegistry()
{
    // Damage-class sets reused below
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
            AllowedDamageClasses = allMeleeAndAbove   // not Summon
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
            AllowedDamageClasses = rangedAndMagicOnly   // not Melee*, not Summon
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
            // NOTE: same id is used for the armor "+% defense via flat" *and* the
            // accessory "+ flat defense points". Both use Prefix kind. Tooltips below
            // intentionally differ per intent; we match the armor version because
            // the existing tooltip is shared in the original code path.
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
            // Magic-only on weapons; unrestricted on armor/accessory (DamageClass check
            // only fires for ItemCategory.Weapon).
            AllowedDamageClasses = magicOnly
        }
    };

    return defs.ToDictionary(d => d.Id);
}
```

- [ ] **Step 5.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 5.3: Sanity-check the registry contents at startup**

Add a temporary diagnostic to confirm the registry loads correctly. In `ARPGItemSystem/ARPGItemSystem.cs`, modify the `Load()` method (or add one if absent) to log the registry size:

```csharp
public override void Load()
{
    Logger.Info($"Affix registry loaded: {Affixes.AffixRegistry.All.Count} entries");
}
```

Also add `using ARPGItemSystem.Common;` if needed (check if `Affixes` resolves; if the namespace is `ARPGItemSystem.Common.Affixes`, use `Common.Affixes.AffixRegistry`).

- [ ] **Step 5.4: Build and confirm**

Run `dotnet build`. If you can launch tModLoader and load the mod, check `client.log` (or the in-game logs) for `Affix registry loaded: 20 entries`. Otherwise, the build success alone is sufficient — the static-constructor would throw on load if duplicate IDs existed.

- [ ] **Step 5.5: Remove the diagnostic Load() override**

Delete or revert the temporary logging change to `ARPGItemSystem.cs`. (Skip this step if the Load() override existed before; in that case just remove the added Logger.Info line.)

- [ ] **Step 5.6: Commit**

```
git add Common/Affixes/AffixRegistry.cs
git commit -m "feat(affixes): populate AffixRegistry with 20 affix declarations"
```

---

### Task 6: Create AffixRoller

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/AffixRoller.cs`

- [ ] **Step 6.1: Create AffixRoller.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixRoller.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Affixes
{
    public static class AffixRoller
    {
        // Rolls a fresh affix for the given (category, kind, weaponClass) context.
        // Returns null when the filtered pool is empty (e.g. summon weapon with no
        // remaining unique-allowed prefixes after exclusions).
        //
        // existing: every affix already on the item; any AffixId already present is
        // excluded so the same affix never appears twice.
        // tier: the 0..9 tier index from utils.GetTier(). Caller is responsible for
        // bounds; the registry's per-category tier lists always have 10 entries.
        public static Affix? Roll(
            ItemCategory category,
            AffixKind kind,
            Item item,
            IReadOnlyCollection<Affix> existing,
            int tier)
        {
            var weaponClass = category == ItemCategory.Weapon ? item.DamageType : null;
            var existingIds = new HashSet<AffixId>(existing.Select(a => a.Id));

            var pool = AffixRegistry
                .RollPool(category, kind, weaponClass)
                .Where(def => !existingIds.Contains(def.Id))
                .ToList();

            if (pool.Count == 0) return null;

            var def = pool[Main.rand.Next(pool.Count)];
            var range = def.Tiers[category][tier];
            int magnitude = Main.rand.Next(range.Min, range.Max + 1);

            return new Affix(def.Id, magnitude, tier);
        }
    }
}
```

- [ ] **Step 6.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 6.3: Commit**

```
git add Common/Affixes/AffixRoller.cs
git commit -m "feat(affixes): add AffixRoller for picking + rolling a single affix"
```

---

### Task 7: Create AffixItemManager base class — lifecycle, Reroll, Clone, PrefixChance

**Files:**
- Create: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`

This task lays down the structure. Save/Load come in Task 8, NetSend/NetReceive in Task 9, and ModifyTooltips in Task 10.

- [ ] **Step 7.1: Create AffixItemManager.cs**

Content of `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.GlobalItems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace ARPGItemSystem.Common.Affixes
{
    // Shared base for WeaponManager / ArmorManager / AccessoryManager.
    // Carries the affix list + lifecycle (creation, pickup, save, load, network);
    // subclasses provide category, applies-to filter, prefix/suffix counts, and
    // category-specific stat-apply hooks.
    public abstract class AffixItemManager : GlobalItem
    {
        public List<Affix> Affixes = new();
        public bool Initialized;

        public override bool InstancePerEntity => true;

        public abstract ItemCategory Category { get; }
        protected abstract int RollPrefixCount();
        protected abstract int RollSuffixCount();

        // Suppress vanilla prefixes; pre == -3 is our "no vanilla prefix" sentinel.
        public override bool? PrefixChance(Item item, int pre, UnifiedRandom rand)
            => pre == -3;

        public override GlobalItem Clone(Item from, Item to)
        {
            var clone = (AffixItemManager)base.Clone(from, to);
            clone.Affixes = Affixes.ToList();
            clone.Initialized = Initialized;
            return clone;
        }

        public override void OnCreated(Item item, ItemCreationContext context)
        {
            Reroll(item);
            Initialized = true;
        }

        public override bool OnPickup(Item item, Player player)
        {
            if (Affixes.Count == 0) Reroll(item);
            Initialized = true;
            return true;
        }

        public override void UpdateInventory(Item item, Player player)
        {
            if (Initialized) return;
            Reroll(item);
            Initialized = true;
        }

        public void Reroll(Item item)
        {
            Affixes.Clear();
            for (int i = 0; i < RollPrefixCount(); i++) AddRoll(item, AffixKind.Prefix);
            for (int i = 0; i < RollSuffixCount(); i++) AddRoll(item, AffixKind.Suffix);
        }

        private void AddRoll(Item item, AffixKind kind)
        {
            // utils lives in ARPGItemSystem.Common.GlobalItems; we lean on its
            // existing GetTier helper to keep boss-progression logic in one place.
            int tier = utils.GetTier();
            var rolled = AffixRoller.Roll(Category, kind, item, Affixes, tier);
            if (rolled.HasValue) Affixes.Add(rolled.Value);
        }
    }
}
```

Note: the `Initialized` field is intentionally public (was `internal bool _initialized` previously) because [Common/Players/ItemInitializerPlayer.cs](../../Common/Players/ItemInitializerPlayer.cs) sets it after a manual reroll. Phase 2 will update that file too.

- [ ] **Step 7.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 7.3: Commit**

```
git add Common/Affixes/AffixItemManager.cs
git commit -m "feat(affixes): add AffixItemManager base class (lifecycle + Reroll)"
```

---

### Task 8: Add SaveData / LoadData to AffixItemManager (with migration)

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`

- [ ] **Step 8.1: Add SaveData and LoadData**

Append the following methods inside the `AffixItemManager` class, after `Reroll`/`AddRoll`:

```csharp
public override void SaveData(Item item, TagCompound tag)
{
    int n = Affixes.Count;
    var ids = new List<int>(n);
    var magnitudes = new List<int>(n);
    var tiers = new List<int>(n);
    var kinds = new List<byte>(n);

    foreach (var a in Affixes)
    {
        ids.Add((int)a.Id);
        magnitudes.Add(a.Magnitude);
        tiers.Add(a.Tier);
        kinds.Add((byte)AffixRegistry.Get(a.Id).Kind);
    }

    tag["AffixIds"] = ids;
    tag["Magnitudes"] = magnitudes;
    tag["Tiers"] = tiers;
    tag["Kinds"] = kinds;
}

public override void LoadData(Item item, TagCompound tag)
{
    // Migration: pre-refactor saves used "PrefixIDList"/"SuffixIDList" etc.
    // Absence of "AffixIds" means an old save (or a brand-new one with no data) —
    // wipe and reroll. tModLoader's TagCompound returns empty/default for missing
    // keys, so reading the old keys silently produces nothing — but we don't even
    // try; we just treat the absent discriminator as "regenerate".
    if (!tag.ContainsKey("AffixIds"))
    {
        Reroll(item);
        Initialized = true;
        return;
    }

    var ids = tag.GetList<int>("AffixIds").ToList();
    var magnitudes = tag.GetList<int>("Magnitudes").ToList();
    var tiers = tag.GetList<int>("Tiers").ToList();
    // Kinds is currently informational; the registry is authoritative for kind on
    // load. We read it only to advance the byte stream parity with NetReceive.
    var kindsList = tag.GetList<byte>("Kinds").ToList();

    Affixes.Clear();
    for (int i = 0; i < ids.Count; i++)
    {
        Affixes.Add(new Affix((AffixId)ids[i], magnitudes[i], tiers[i]));
    }

    // Belt-and-braces: if a future schema change leaves us with zero usable affixes,
    // make sure the item still gets fresh rolls via UpdateInventory rather than
    // staying permanently empty.
    if (Affixes.Count == 0 && (RollPrefixCount() > 0 || RollSuffixCount() > 0))
    {
        Initialized = false;
    }
    else
    {
        Initialized = true;
    }
}
```

Add the `using` directives at the top of the file if not already present:

```csharp
using Terraria.ModLoader.IO;
```

- [ ] **Step 8.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 8.3: Commit**

```
git add Common/Affixes/AffixItemManager.cs
git commit -m "feat(affixes): add Save/Load with implicit migration via tag rename"
```

---

### Task 9: Add NetSend / NetReceive to AffixItemManager

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`

- [ ] **Step 9.1: Add NetSend and NetReceive**

Append the following methods inside the `AffixItemManager` class, after `LoadData`:

```csharp
public override void NetSend(Item item, BinaryWriter writer)
{
    writer.Write(Affixes.Count);
    foreach (var a in Affixes)
    {
        writer.Write((int)a.Id);
        writer.Write(a.Magnitude);
        writer.Write(a.Tier);
        writer.Write((byte)AffixRegistry.Get(a.Id).Kind);
    }
}

public override void NetReceive(Item item, BinaryReader reader)
{
    int count = reader.ReadInt32();
    Affixes.Clear();
    for (int i = 0; i < count; i++)
    {
        var id = (AffixId)reader.ReadInt32();
        int magnitude = reader.ReadInt32();
        int tier = reader.ReadInt32();
        // Read Kind byte for parity with NetSend; registry is authoritative on consume.
        _ = reader.ReadByte();
        Affixes.Add(new Affix(id, magnitude, tier));
    }
    Initialized = true;
}
```

Add the `using` directives at the top of the file if not already present:

```csharp
using System.IO;
```

- [ ] **Step 9.2: Build check**

Run `dotnet build` — expect success.

- [ ] **Step 9.3: Commit**

```
git add Common/Affixes/AffixItemManager.cs
git commit -m "feat(affixes): add NetSend/NetReceive for Affix list"
```

---

### Task 10: Add ModifyTooltips to AffixItemManager

**Files:**
- Modify: `ARPGItemSystem/Common/Affixes/AffixItemManager.cs`

- [ ] **Step 10.1: Add ModifyTooltips**

Append the following inside the `AffixItemManager` class. Note this preserves the existing UseMana fix from the per-manager versions ([WeaponManager.cs:188-191](../../Common/GlobalItems/Weapon/WeaponManager.cs#L188-L191)):

```csharp
public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
{
    var useManaTip = tooltips.FirstOrDefault(tip => tip.Name == "UseMana" && tip.Mod == "Terraria");
    if (useManaTip is not null)
    {
        useManaTip.Text = Terraria.Localization.Language.GetTextValue(
            "CommonItemTooltip.UsesMana", Main.LocalPlayer.GetManaCost(item));
    }

    foreach (var affix in Affixes)
    {
        var def = AffixRegistry.Get(affix.Id);
        var text = string.Format(def.TooltipFormat, affix.Magnitude);
        var color = def.Kind == AffixKind.Prefix
            ? Microsoft.Xna.Framework.Color.LightGreen
            : Microsoft.Xna.Framework.Color.DeepSkyBlue;
        tooltips.Add(new TooltipLine(Mod, "CustomAffix", text) { OverrideColor = color });
    }
}
```

- [ ] **Step 10.2: Build check**

Run `dotnet build` — expect success. Phase 1 infrastructure is complete.

- [ ] **Step 10.3: Commit**

```
git add Common/Affixes/AffixItemManager.cs
git commit -m "feat(affixes): add ModifyTooltips with registry-driven lookup"
```

---

## Phase 2 — Migrate consumers (project does not build mid-phase; final build at end)

In Phase 2 we replace the three concrete managers and update everything that referenced their old types. **The project will not compile** between the start of Task 11 and the end of Task 17. That is expected — we only run `dotnet build` at the end of the phase. If it's important to maintain compilability, do all steps in Phase 2 in a single session before running the build check.

### Task 11: Rewrite WeaponManager as an AffixItemManager subclass

**Files:**
- Modify: `ARPGItemSystem/Common/GlobalItems/Weapon/WeaponManager.cs` (full rewrite)

- [ ] **Step 11.1: Replace the file contents**

Replace the entire contents of `Common/GlobalItems/Weapon/WeaponManager.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Weapon
{
    public class WeaponManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Weapon;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage > 0 && entity.maxStack <= 1;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesWeapon();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesWeapon();

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatDamageIncrease:
                        damage.Base += a.Magnitude / 100f * item.OriginalDamage;
                        break;
                    case AffixId.PercentageDamageIncrease:
                        damage *= 1 + a.Magnitude / 100f;
                        break;
                    case AffixId.FlatArmorPen:
                        player.GetArmorPenetration(DamageClass.Generic) += a.Magnitude;
                        break;
                }
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatCritChance:
                        crit += a.Magnitude;
                        break;
                    case AffixId.PercentageCritChance:
                        crit *= 1 + a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.PercentageArmorPen:
                        modifiers.ScalingArmorPenetration += a.Magnitude / 100f;
                        break;
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                }
            }
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.KnockbackIncrease)
                    knockback += a.Magnitude / 100f;
            }
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.VelocityIncrease)
                    velocity *= 1 + a.Magnitude / 100f;
            }
        }

        public override float UseSpeedMultiplier(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.AttackSpeedIncrease)
                    return base.UseSpeedMultiplier(item, player) + a.Magnitude / 100f;
            }
            return base.UseSpeedMultiplier(item, player);
        }

        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
        {
            foreach (var a in Affixes)
            {
                if (a.Id == AffixId.ManaCostReduction)
                    reduce -= a.Magnitude / 100f;
            }
        }
    }
}
```

(No commit yet — project does not build.)

---

### Task 12: Rewrite ArmorManager as an AffixItemManager subclass

**Files:**
- Modify: `ARPGItemSystem/Common/GlobalItems/Armor/ArmorManager.cs` (full rewrite)

- [ ] **Step 12.1: Replace the file contents**

Replace the entire contents of `Common/GlobalItems/Armor/ArmorManager.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Armor
{
    public class ArmorManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Armor;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.damage < 1 && entity.maxStack == 1
               && !entity.accessory && !entity.vanity;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesArmor();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesArmor();

        public override void UpdateEquip(Item item, Player player)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatLifeIncrease:
                        player.statLifeMax2 += a.Magnitude;
                        break;
                    case AffixId.FlatDefenseIncrease:
                        // pseudo "increased" applied first
                        item.defense = (int)(item.OriginalDefense * (1 + a.Magnitude / 100f));
                        break;
                    case AffixId.PercentageDefenseIncrease:
                        // pseudo "more" applied after flat
                        item.defense = (int)(item.OriginalDefense * (1 + a.Magnitude / 100f));
                        break;
                    case AffixId.FlatManaIncrease:
                        player.statManaMax2 += a.Magnitude;
                        break;
                    case AffixId.PercentageGenericDamageIncrease:
                        player.GetDamage<GenericDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMeleeDamageIncrease:
                        player.GetDamage<MeleeDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageRangedDamageIncrease:
                        player.GetDamage<RangedDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMagicDamageIncrease:
                        player.GetDamage<MagicDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageSummonDamageIncrease:
                        player.GetDamage<SummonDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.FlatCritChance:
                        player.GetCritChance(DamageClass.Generic) += a.Magnitude;
                        break;
                    case AffixId.ManaCostReduction:
                        player.manaCost -= a.Magnitude / 100f;
                        break;
                }
            }
        }
    }
}
```

(No commit yet — project does not build.)

---

### Task 13: Rewrite AccessoryManager as an AffixItemManager subclass

**Files:**
- Modify: `ARPGItemSystem/Common/GlobalItems/Accessory/AccessoryManager.cs` (full rewrite)

- [ ] **Step 13.1: Replace the file contents**

Replace the entire contents of `Common/GlobalItems/Accessory/AccessoryManager.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems.Accessory
{
    public class AccessoryManager : AffixItemManager
    {
        public override ItemCategory Category => ItemCategory.Accessory;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.accessory;

        protected override int RollPrefixCount() => utils.GetAmountOfPrefixesAccessory();
        protected override int RollSuffixCount() => utils.GetAmountOfSuffixesAccessory();

        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.FlatLifeIncrease:
                        player.statLifeMax2 += a.Magnitude;
                        break;
                    case AffixId.FlatDefenseIncrease:
                        player.statDefense += a.Magnitude;
                        break;
                    case AffixId.FlatManaIncrease:
                        player.statManaMax2 += a.Magnitude;
                        break;
                    case AffixId.PercentageGenericDamageIncrease:
                        player.GetDamage<GenericDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMeleeDamageIncrease:
                        player.GetDamage<MeleeDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageRangedDamageIncrease:
                        player.GetDamage<RangedDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageMagicDamageIncrease:
                        player.GetDamage<MagicDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.PercentageSummonDamageIncrease:
                        player.GetDamage<SummonDamageClass>() += a.Magnitude / 100f;
                        break;
                    case AffixId.FlatCritChance:
                        player.GetCritChance(DamageClass.Generic) += a.Magnitude;
                        break;
                    case AffixId.ManaCostReduction:
                        player.manaCost -= a.Magnitude / 100f;
                        break;
                }
            }
        }
    }
}
```

(No commit yet — project does not build.)

---

### Task 14: Rewrite ProjectileManager to use Affix struct

**Files:**
- Modify: `ARPGItemSystem/Common/GlobalItems/ProjectileManager.cs` (full rewrite)

- [ ] **Step 14.1: Replace the file contents**

Replace the entire contents of `Common/GlobalItems/ProjectileManager.cs` with:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.GlobalItems
{
    public class ProjectileManager : GlobalProjectile
    {
        public List<Affix> Affixes = new();
        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse_WithAmmo itemSource
                && !itemSource.Item.consumable
                && !(itemSource.Item.fishingPole > 0))
            {
                // Snapshot the source weapon's affix list so projectile-side hooks
                // see the same modifiers regardless of any later weapon mutation.
                Affixes = itemSource.Item.GetGlobalItem<WeaponManager>().Affixes.ToList();
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            foreach (var a in Affixes)
            {
                switch (a.Id)
                {
                    case AffixId.PercentageArmorPen:
                        modifiers.ScalingArmorPenetration += a.Magnitude / 100f;
                        break;
                    case AffixId.CritMultiplier:
                        modifiers.CritDamage += a.Magnitude / 100f;
                        break;
                }
            }
        }
    }
}
```

(No commit yet — project does not build.)

---

### Task 15: Rewrite ReforgePacketHandler to use Affix struct + drop redundant ItemCategory

**Files:**
- Modify: `ARPGItemSystem/Common/Network/ReforgePacketHandler.cs` (full rewrite)

The existing file declares its own `ItemCategory` (byte values 0/1/2). We drop it and use `Common.Affixes.ItemCategory` (which has matching byte values). `WeaponDamageCategory` stays — it remains a coarse wire-format primitive.

- [ ] **Step 15.1: Replace the file contents**

Replace the entire contents of `Common/Network/ReforgePacketHandler.cs` with:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Network
{
    public enum ReforgePacketType : byte
    {
        RerollRequest = 0,
        RerollResult = 1,
        RerollRejected = 2
    }

    // Coarse damage-class wire format. Decoded server-side back to a representative
    // DamageClass instance for the AffixRoller's per-class filter.
    public enum WeaponDamageCategory : byte
    {
        Melee = 0,
        Ranged = 1,
        Magic = 2,
        Summon = 3,
        Other = 4
    }

    public static class ReforgePacketHandler
    {
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var type = (ReforgePacketType)reader.ReadByte();
            switch (type)
            {
                case ReforgePacketType.RerollRequest:  HandleRerollRequest(reader, whoAmI); break;
                case ReforgePacketType.RerollResult:   HandleRerollResult(reader);          break;
                case ReforgePacketType.RerollRejected: HandleRerollRejected(reader);        break;
            }
        }

        public static void SendRerollRequest(int affixIndex, AffixKind kind, ItemCategory cat,
            WeaponDamageCategory damCat, int itemValue, List<AffixId> excludeIds)
        {
            var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            packet.Write((byte)ReforgePacketType.RerollRequest);
            packet.Write((byte)affixIndex);
            packet.Write((byte)kind);
            packet.Write((byte)cat);
            packet.Write((byte)damCat);
            packet.Write(itemValue);
            packet.Write((byte)excludeIds.Count);
            foreach (var id in excludeIds) packet.Write((int)id);
            packet.Send();
        }

        private static void HandleRerollRequest(BinaryReader reader, int whoAmI)
        {
            byte affixIndex = reader.ReadByte();
            var kind = (AffixKind)reader.ReadByte();
            var cat = (ItemCategory)reader.ReadByte();
            var damCat = (WeaponDamageCategory)reader.ReadByte();
            int itemValue = reader.ReadInt32();
            byte excludeCount = reader.ReadByte();
            var excludeIds = new List<AffixId>(excludeCount);
            for (int i = 0; i < excludeCount; i++) excludeIds.Add((AffixId)reader.ReadInt32());

            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(itemValue, tier);
            var player = Main.player[whoAmI];

            if (!player.BuyItem(cost))
            {
                var rejection = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
                rejection.Write((byte)ReforgePacketType.RerollRejected);
                rejection.Write(affixIndex);
                rejection.Send(whoAmI);
                return;
            }

            RollReplacement(cat, kind, damCat, excludeIds, tier,
                out AffixId newId, out int newMagnitude);

            var result = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            result.Write((byte)ReforgePacketType.RerollResult);
            result.Write(affixIndex);
            result.Write((int)newId);
            result.Write(newMagnitude);
            result.Write(tier);
            result.Send(whoAmI);
        }

        private static void HandleRerollResult(BinaryReader reader)
        {
            if (Main.netMode == NetmodeID.Server) return;

            byte affixIndex = reader.ReadByte();
            var newId = (AffixId)reader.ReadInt32();
            int newMagnitude = reader.ReadInt32();
            int newTier = reader.ReadInt32();

            var item = Main.reforgeItem;
            if (item.IsAir) return;

            ApplyAffixReplacement(item, affixIndex, newId, newMagnitude, newTier);
            ModContent.GetInstance<UISystem>().Panel.RefreshAffix(affixIndex);
        }

        private static void HandleRerollRejected(BinaryReader reader)
        {
            if (Main.netMode == NetmodeID.Server) return;
            reader.ReadByte();
            ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
        }

        public static void DoRerollDirectly(Item item, int affixIndex, AffixKind kind,
            ItemCategory cat, WeaponDamageCategory damCat, List<AffixId> excludeIds)
        {
            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(item.value, tier);

            if (!Main.LocalPlayer.BuyItem(cost))
            {
                ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
                return;
            }

            RollReplacement(cat, kind, damCat, excludeIds, tier,
                out AffixId newId, out int newMagnitude);

            ApplyAffixReplacement(item, affixIndex, newId, newMagnitude, tier);
        }

        private static void RollReplacement(ItemCategory cat, AffixKind kind,
            WeaponDamageCategory damCat, List<AffixId> excludeIds, int tier,
            out AffixId newId, out int newMagnitude)
        {
            newId = AffixId.None;
            newMagnitude = 0;

            DamageClass weaponClass = cat == ItemCategory.Weapon ? GetDamageClass(damCat) : null;

            var pool = AffixRegistry
                .RollPool(cat, kind, weaponClass)
                .Where(def => !excludeIds.Contains(def.Id))
                .ToList();

            if (pool.Count == 0) return;

            var def = pool[Main.rand.Next(pool.Count)];
            var range = def.Tiers[cat][tier];
            newId = def.Id;
            newMagnitude = Main.rand.Next(range.Min, range.Max + 1);
        }

        private static void ApplyAffixReplacement(Item item, int affixIndex,
            AffixId newId, int newMagnitude, int newTier)
        {
            if (newId == AffixId.None) return;

            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            if (mgr == null || affixIndex < 0 || affixIndex >= mgr.Affixes.Count) return;
            mgr.Affixes[affixIndex] = new Affix(newId, newMagnitude, newTier);
        }

        public static ItemCategory GetItemCategory(Item item)
        {
            if (item.damage > 0 && item.maxStack <= 1) return ItemCategory.Weapon;
            if (item.accessory) return ItemCategory.Accessory;
            return ItemCategory.Armor;
        }

        public static WeaponDamageCategory GetDamageCategory(Item item)
        {
            if (item.DamageType == DamageClass.Melee
                || item.DamageType == DamageClass.MeleeNoSpeed
                || item.DamageType == DamageClass.SummonMeleeSpeed)
                return WeaponDamageCategory.Melee;
            if (item.DamageType == DamageClass.Ranged) return WeaponDamageCategory.Ranged;
            if (item.DamageType == DamageClass.Magic
                || item.DamageType == DamageClass.MagicSummonHybrid)
                return WeaponDamageCategory.Magic;
            if (item.DamageType == DamageClass.Summon) return WeaponDamageCategory.Summon;
            return WeaponDamageCategory.Other;
        }

        // Returns AffixIds of every other affix of the same kind on the item,
        // so the reroll cannot duplicate any existing same-kind affix.
        public static List<AffixId> GetExcludeIds(Item item, int affixIndex)
        {
            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            var result = new List<AffixId>();
            if (mgr == null || affixIndex < 0 || affixIndex >= mgr.Affixes.Count) return result;

            var targetKind = AffixRegistry.Get(mgr.Affixes[affixIndex].Id).Kind;
            for (int i = 0; i < mgr.Affixes.Count; i++)
            {
                if (i == affixIndex) continue;
                var a = mgr.Affixes[i];
                if (AffixRegistry.Get(a.Id).Kind == targetKind)
                    result.Add(a.Id);
            }
            return result;
        }

        private static DamageClass GetDamageClass(WeaponDamageCategory cat) => cat switch
        {
            WeaponDamageCategory.Melee => DamageClass.Melee,
            WeaponDamageCategory.Ranged => DamageClass.Ranged,
            WeaponDamageCategory.Magic => DamageClass.Magic,
            WeaponDamageCategory.Summon => DamageClass.Summon,
            _ => DamageClass.Generic
        };
    }
}
```

**Note on the embedded comments:** The big multi-line comment block inside `HandleRerollRequest` is preserved from a deliberate design conversation in the original file. The clean version is what `RollReplacement` does: derive `kind` from any existing excluded id (since clients only ever exclude same-kind affixes). Edge case: if the item has only one prefix-or-suffix slot and that's what's being rerolled, `excludeIds` is empty and we default to `Prefix`. The actual kind in that case is the kind of the slot being rerolled, which the client knows; if the chosen pool is wrong, it returns empty and the reroll is a no-op (treated as "nothing changed"). **Cleanup decision:** We accept this small edge case because it doesn't crash and is recoverable; a perfect fix requires bumping the wire format to include `kind` explicitly. Leave the comment block in place to mark this decision.

(No commit yet — project does not build.)

---

### Task 16: Rewrite AffixLine UI to use Affix struct

**Files:**
- Modify: `ARPGItemSystem/Common/UI/AffixLine.cs` (touch the methods that read modifier lists)

- [ ] **Step 16.1: Replace the consumer methods**

Open `Common/UI/AffixLine.cs`. Replace the entire `OnHammerClicked` method and the entire `Refresh` method with the following. Other methods (`SetPending`, `UICostDisplay`) are unchanged.

Replace `OnHammerClicked`:

```csharp
private void OnHammerClicked(UIMouseEvent evt, UIElement listeningElement)
{
    if (_isPending || Main.reforgeItem.IsAir) return;

    SoundEngine.PlaySound(SoundID.Item37);

    var item = Main.reforgeItem;
    var cat = ReforgePacketHandler.GetItemCategory(item);
    var damCat = ReforgePacketHandler.GetDamageCategory(item);
    var excludeIds = ReforgePacketHandler.GetExcludeIds(item, _modifierIndex);
    var kind = _isPrefix ? AffixKind.Prefix : AffixKind.Suffix;

    ModContent.GetInstance<UISystem>().Panel.SetAllPending(true);

    if (Main.netMode == NetmodeID.SinglePlayer)
    {
        ReforgePacketHandler.DoRerollDirectly(item, _modifierIndex, kind, cat, damCat, excludeIds);
        ModContent.GetInstance<UISystem>().Panel.RefreshAffix(_modifierIndex);
    }
    else
    {
        ReforgePacketHandler.SendRerollRequest(_modifierIndex, kind, cat, damCat, item.value, excludeIds);
    }
}
```

Replace `Refresh`:

```csharp
public void Refresh()
{
    var item = Main.reforgeItem;
    if (item.IsAir) return;

    AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
        ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
        : item.accessory
            ? item.GetGlobalItem<AccessoryManager>()
            : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

    if (mgr == null || _modifierIndex < 0 || _modifierIndex >= mgr.Affixes.Count) return;

    var a = mgr.Affixes[_modifierIndex];
    var def = AffixRegistry.Get(a.Id);
    string displayText = string.Format(def.TooltipFormat, a.Magnitude);

    _affixText.SetText(displayText);
    _costDisplay.Cost = ReforgeConfig.CalculateCost(item.value, a.Tier);
}
```

Update the `using` section at the top of the file. Replace:
```csharp
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
```
with:
```csharp
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
```

Also: the constructor signature `AffixLine(string displayText, int tier, int modifierIndex, bool isPrefix)` still accepts `bool isPrefix` because `ReforgePanel` still passes it. The `_isPrefix` field remains used to color the text in the constructor body (`isPrefix ? Color.LightGreen : Color.DeepSkyBlue`). No constructor changes needed.

(No commit yet — project does not build.)

---

### Task 17: Rewrite ReforgePanel.GetModifierLines to use AffixItemManager.Affixes

**Files:**
- Modify: `ARPGItemSystem/Common/UI/ReforgePanel.cs`

- [ ] **Step 17.1: Replace the GetModifierLines method**

In `Common/UI/ReforgePanel.cs`, replace the entire `GetModifierLines` method with:

```csharp
private static List<(string text, int tier, int index, bool isPrefix)> GetModifierLines(Item item)
{
    var result = new List<(string, int, int, bool)>();

    AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
        ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
        : item.accessory
            ? item.GetGlobalItem<AccessoryManager>()
            : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

    if (mgr == null) return result;

    for (int i = 0; i < mgr.Affixes.Count; i++)
    {
        var a = mgr.Affixes[i];
        var def = AffixRegistry.Get(a.Id);
        string text = string.Format(def.TooltipFormat, a.Magnitude);
        bool isPrefix = def.Kind == AffixKind.Prefix;
        result.Add((text, a.Tier, i, isPrefix));
    }

    return result;
}
```

Update the `using` section at the top. Add:
```csharp
using ARPGItemSystem.Common.Affixes;
```
Remove the alias-style usings:
```csharp
using Accessory = ARPGItemSystem.Common.GlobalItems.Accessory;
using Armor = ARPGItemSystem.Common.GlobalItems.Armor;
using Weapon = ARPGItemSystem.Common.GlobalItems.Weapon;
```
Keep the other usings as-is (they may already be needed by methods we did not touch).

(No commit yet — project does not build.)

---

### Task 18: Update ItemInitializerPlayer to use new API

**Files:**
- Modify: `ARPGItemSystem/Common/Players/ItemInitializerPlayer.cs`

- [ ] **Step 18.1: Replace the file contents**

Replace the entire contents of `Common/Players/ItemInitializerPlayer.cs` with:

```csharp
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
    // Ensures all inventory items have affixes when entering a world.
    // Covers starter items on new characters and pre-mod items in old saves.
    public class ItemInitializerPlayer : ModPlayer
    {
        public override void OnEnterWorld()
        {
            for (int i = 0; i < Player.inventory.Length; i++)
            {
                Item item = Player.inventory[i];
                if (item.IsAir) continue;

                AffixItemManager mgr = null;
                if (item.damage > 0 && item.maxStack <= 1)
                    mgr = item.GetGlobalItem<WeaponManager>();
                else if (item.accessory)
                    mgr = item.GetGlobalItem<AccessoryManager>();
                else if (!item.vanity && item.maxStack == 1 && item.damage < 1)
                    mgr = item.TryGetGlobalItem<ArmorManager>(out var am) ? am : null;

                if (mgr != null && mgr.Affixes.Count == 0)
                {
                    mgr.Reroll(item);
                    mgr.Initialized = true;
                }
            }
        }
    }
}
```

(No commit yet — project does not build.)

---

### Task 19: Clean up utils.cs (remove triple CreateExcludeList overloads)

**Files:**
- Modify: `ARPGItemSystem/Common/GlobalItems/utils.cs`

- [ ] **Step 19.1: Replace utils.cs**

The existing `utils.cs` had three `CreateExcludeList` overloads (one per old `*Modifier` type) that are no longer used by any consumer (the new code uses `AffixRoller`'s built-in deduplication and `ReforgePacketHandler.GetExcludeIds`). Remove them. Keep `GetTier` and the six `GetAmountOf*` methods.

Replace the entire contents of `Common/GlobalItems/utils.cs` with:

```csharp
using System;
using Terraria;

namespace ARPGItemSystem.Common.GlobalItems
{
    internal static class utils
    {
        internal static int GetAmountOfSuffixesWeapon()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedBoss2) maxCount += 1;
            if (Main.hardMode) minCount += 1;
            if (NPC.downedMechBossAny) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetAmountOfPrefixesWeapon()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedBoss3) maxCount += 1;
            if (Main.hardMode) minCount += 1;
            if (NPC.downedGolemBoss) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetAmountOfSuffixesArmor()
        {
            int maxCount = 1;
            int minCount = 0;
            if (NPC.downedBoss2) minCount += 1;
            if (Main.hardMode) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetAmountOfPrefixesArmor()
        {
            int maxCount = 1;
            int minCount = 1;
            if (NPC.downedGolemBoss) maxCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetAmountOfSuffixesAccessory()
        {
            int maxCount = 1;
            int minCount = 0;
            if (Main.hardMode) minCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetAmountOfPrefixesAccessory()
        {
            int maxCount = 1;
            int minCount = 0;
            if (NPC.downedGolemBoss) minCount += 1;

            Random random = new Random();
            return random.Next(minCount, maxCount + 1);
        }

        internal static int GetTier()
        {
            Random random = new Random();
            int bestTier = 8;
            int worstTier = 10;

            if (NPC.downedSlimeKing) bestTier -= 1;
            if (NPC.downedBoss2) worstTier -= 1;
            if (NPC.downedBoss3) bestTier -= 1;
            if (Main.hardMode) bestTier -= 1;
            if (NPC.downedQueenSlime) worstTier -= 1;
            if (NPC.downedMechBossAny) bestTier -= 1;
            if (NPC.downedGolemBoss) worstTier -= 1;
            if (NPC.downedPlantBoss) bestTier -= 1;
            if (NPC.downedFishron) worstTier -= 1;
            if (NPC.downedEmpressOfLight) { bestTier -= 1; worstTier -= 1; }
            if (NPC.downedAncientCultist) bestTier -= 1;
            if (NPC.downedMoonlord) { bestTier -= 1; worstTier -= 1; }

            bestTier = Math.Max(0, bestTier);
            worstTier = Math.Max(bestTier + 1, worstTier);
            return random.Next(bestTier, worstTier);
        }
    }
}
```

(No commit yet — final build/commit comes after deleting old files.)

---

### Task 20: Delete the obsolete *Modifier.cs and *Database.cs files

**Files:**
- Delete: `ARPGItemSystem/Common/GlobalItems/Weapon/WeaponModifier.cs`
- Delete: `ARPGItemSystem/Common/GlobalItems/Armor/ArmorModifier.cs`
- Delete: `ARPGItemSystem/Common/GlobalItems/Accessory/AccessoryModifier.cs`
- Delete: `ARPGItemSystem/Common/GlobalItems/Database/TierDatabase.cs`
- Delete: `ARPGItemSystem/Common/GlobalItems/Database/TooltipDatabase.cs`

- [ ] **Step 20.1: Delete the five files**

Run from the `ARPGItemSystem` folder:

```
rm Common/GlobalItems/Weapon/WeaponModifier.cs
rm Common/GlobalItems/Armor/ArmorModifier.cs
rm Common/GlobalItems/Accessory/AccessoryModifier.cs
rm Common/GlobalItems/Database/TierDatabase.cs
rm Common/GlobalItems/Database/TooltipDatabase.cs
```

- [ ] **Step 20.2: Remove the (now empty) Database folder if it's empty**

```
rmdir Common/GlobalItems/Database 2>/dev/null || true
```

(Skip if other files were added to the folder; verify with `ls Common/GlobalItems/Database`.)

- [ ] **Step 20.3: Build the project**

Run `dotnet build`. The project should now compile cleanly.

If errors appear, the most likely causes are:
1. A residual `using ARPGItemSystem.Common.GlobalItems.Weapon` (or `.Armor`, `.Accessory`) that referenced one of the deleted modifier types — search for any remaining references via Grep.
2. A reference to `modifierList` (the old field name) that should be `Affixes` — search the project.
3. A reference to `_initialized` (old internal field) that should be `Initialized`.

Fix each error by replacing with the new API (`AffixItemManager.Affixes`, `AffixItemManager.Initialized`). Re-run `dotnet build` until clean.

- [ ] **Step 20.4: Commit Phase 2**

```
git add Common/Affixes/ Common/GlobalItems/ Common/Network/ReforgePacketHandler.cs Common/UI/AffixLine.cs Common/UI/ReforgePanel.cs Common/Players/ItemInitializerPlayer.cs
git commit -m "refactor: migrate all consumers to unified Affix system; delete legacy *Modifier/Database files"
```

---

## Phase 3 — Verify in-game

This project has no automated tests. The following manual test plan establishes confidence that the refactor preserves behaviour and the migration is non-fatal.

### Task 21: Smoke-test in a fresh world

- [ ] **Step 21.1: Build & deploy**

Build the mod via tModLoader's Mod Sources UI (or `dotnet build` if linked into the dev folder) and reload.

- [ ] **Step 21.2: Create a fresh small world + character**

Use a "New" character on a "New" small world. Pick up a copper sword from inventory.

- [ ] **Step 21.3: Verify weapon affixes roll**

Hover the copper sword's tooltip. Expected: 1 prefix line (light green) and 1 suffix line (sky blue), each formatted via the registry's tooltip strings (e.g. "12% Increased Base Damage", "5% Additional Critical Strike Chance"). Roll values should be in the lowest-tier ranges (since no bosses are downed).

- [ ] **Step 21.4: Verify armor affixes roll**

Craft or spawn (`/give` is not standard; use cheat sheet or test against a wooden helmet from a chest). Equip, hover. Expected: 1 prefix line, possibly no suffix (suffix min count is 0 pre-bosses).

- [ ] **Step 21.5: Verify accessory affixes**

Use a starter item like Magic Mirror (or any accessory from inventory). Hover. Expected: optionally a prefix line and/or suffix line based on roll.

### Task 22: Migration test — load a pre-refactor world

- [ ] **Step 22.1: Use a backup save from before the refactor (if available)**

If you have a saved character/world from before this refactor, copy them in. Otherwise, skip — Task 21 already validates the fresh-roll path, and the migration code path (`!tag.ContainsKey("AffixIds")` → `Reroll`) is straightforward.

- [ ] **Step 22.2: Load the pre-refactor world**

Open the world. Expected: no crashes during world load, character load, or first inventory tick.

- [ ] **Step 22.3: Inspect the inventory**

Hover each existing weapon/armor/accessory. Expected: each item displays new affixes (rerolled fresh). No item shows the old per-category enum values or stale tooltip text. Pre-existing chests in the world likely contain unrolled items; pick one up to confirm `OnPickup` triggers a reroll.

### Task 23: Reforge UI test

- [ ] **Step 23.1: Open the reforge panel**

Talk to the Goblin Tinkerer (or use the keybind) and open the reforge panel.

- [ ] **Step 23.2: Place an item with multiple affixes**

Drag a weapon with 2+ rolled affixes into the reforge slot. Expected: `AffixLine`s render for each, with correct text and coin-cost display.

- [ ] **Step 23.3: Reroll an individual affix**

Click the hammer button on one affix line. Expected:
- Sound plays
- Coin cost is deducted
- Affix text updates to reflect a new roll
- Other affixes on the same item are unchanged
- The new affix is **not** the same as any other affix of the same kind on the item

Repeat 5+ times to spot-check that rerolls produce different IDs / magnitudes.

### Task 24: Multiplayer test

- [ ] **Step 24.1: Host a multiplayer session**

Host as one player; have another player join.

- [ ] **Step 24.2: Verify item sync**

Drop an item from the host's inventory; the joining player picks it up. Expected: the picked-up item shows the same affix tooltips as the host saw.

- [ ] **Step 24.3: Reroll over network**

Have the joining player open the reforge panel and reroll an affix. Expected:
- Server deducts coin cost
- New affix appears on the item
- The host (if able to see the item) sees the same updated affix
- No console errors / packet errors

### Task 25: Sanity-check edge cases

- [ ] **Step 25.1: Summon weapon — no AttackSpeedIncrease, no VelocityIncrease, no ManaCostReduction**

Roll a summon weapon (e.g. Slime Staff). Re-roll its affixes 10+ times via the reforge panel. Expected: never rolls `AttackSpeedIncrease`, `VelocityIncrease`, or `ManaCostReduction`.

- [ ] **Step 25.2: Magic weapon — can roll ManaCostReduction**

Roll a magic weapon (e.g. Wand of Sparking). Reroll suffixes 10+ times. Expected: `ManaCostReduction` appears at least once in the spread.

- [ ] **Step 25.3: Crit-only suffix on melee weapon**

Roll a melee weapon. Reroll suffixes 10+ times. Expected: only suffix variants from `{FlatCritChance, PercentageCritChance, CritMultiplier}` appear.

### Task 26: Final commit (if anything tweaked during testing)

- [ ] **Step 26.1: If Phase 3 surfaced bugs**

Fix in place (likely small `using` cleanups or off-by-one in the registry). Run `dotnet build`. Commit with a descriptive message: `fix(affixes): <what was wrong>`.

- [ ] **Step 26.2: If everything passes, no further commits needed.**

---

## Self-Review Checklist (run by the implementer before finishing)

- [ ] All 20 affixes from the spec's table are present in `AffixRegistry.BuildRegistry()`.
- [ ] Each affix's tier values match `TierDatabase.cs` exactly (cross-check spreadsheet-style).
- [ ] `AllowedDamageClasses` matches the spec's table (only `AttackSpeedIncrease`, `VelocityIncrease`, `ManaCostReduction` are restricted; all others are `null`).
- [ ] No file references `WeaponModifier`, `ArmorModifier`, `AccessoryModifier`, `TierDatabase`, or `TooltipDatabase`.
- [ ] No file references `modifierList` or `_initialized` (use `Affixes` and `Initialized`).
- [ ] `dotnet build` is clean.
- [ ] Manual tests in Tasks 21-25 all pass.
