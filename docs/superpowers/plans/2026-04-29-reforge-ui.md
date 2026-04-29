# Reforge UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the vanilla Goblin Tinkerer reforge panel with a custom per-affix ARPG modifier UI, removing the existing keybind-based reroll system.

**Architecture:** `ModifyInterfaceLayers` swaps out `"Vanilla: Reforge Menu"` with our `GameInterfaceLayer`; vanilla continues managing `Main.reforgeItem` (item slot state, ESC close, inventory return). Rerolls are server-authoritative via `ModPacket`; in single-player the roll is applied directly with no packet overhead.

**Tech Stack:** tModLoader 2026-02 / net8.0, `Terraria.GameContent.UI.Elements`, `Terraria.UI`, `System.IO.BinaryWriter/Reader`

---

## File Map

**Create:**
- `Common/Config/ReforgeConfig.cs` — cost formula constants + `CalculateCost(itemValue, tier)`
- `Common/Network/ReforgePacketHandler.cs` — packet enums, client send helpers, server handler, direct single-player reroll
- `Common/Systems/UISystem.cs` — `ModSystem`: owns `UserInterface` + `ReforgePanel`, implements `ModifyInterfaceLayers` + `UpdateUI`
- `Common/UI/UIReforgeSlot.cs` — `UIElement` wrapper around `ItemSlot.Draw(ref Main.reforgeItem, ...)`
- `Common/UI/AffixLine.cs` — `UIElement`: hammer button + affix text + cost text for one modifier
- `Common/UI/ReforgePanel.cs` — `UIState`: title, item slot, affix lines, placeholder text

**Modify:**
- `Common/GlobalItems/Weapon/WeaponModifier.cs` — add `tier` field; update both constructors + `GenerateModifier`
- `Common/GlobalItems/Armor/ArmorModifier.cs` — same
- `Common/GlobalItems/Accessory/AccessoryModifier.cs` — same
- `Common/GlobalItems/Weapon/WeaponManager.cs` — add tier to `SerializeData`, `SaveData`, `LoadData`, `NetSend`, `NetReceive`
- `Common/GlobalItems/Armor/ArmorManager.cs` — same
- `Common/GlobalItems/Accessory/AccessoryManager.cs` — same
- `ARPGItemSystem.cs` — add `HandlePacket` override
- `Localization/en-US_Mods.ARPGItemSystem.hjson` — add `UI.ReforgePanel.*` keys

**Delete:**
- `Common/Players/Keybind.cs`
- `Common/Systems/KeybindSystem.cs`
- `Common/UI/CraftingPanel.cs`

---

### Task 1: Add `tier` to WeaponModifier

**Files:**
- Modify: `Common/GlobalItems/Weapon/WeaponModifier.cs`

- [ ] **Step 1: Add `tier` field and update both constructors + `GenerateModifier`**

Replace the struct body. Key changes: add `public int tier = 9;`, add `int tier = 9` param to the deserialization constructor, and set `this.tier = tier` inside `GenerateModifier`.

```csharp
public struct WeaponModifier
{
    public ModifierType modifierType;
    public PrefixType prefixType = PrefixType.None;
    public SuffixType suffixType = SuffixType.None;
    public int magnitude = 0;
    public int tier = 9;
    public string tooltip = "";

    public List<int> meleeWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
    public List<int> rangedWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
    public List<int> magicWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
    public List<int> summonWeaponPrefixType = new List<int>() { 0, 1, 2, 3, 4, 6 };
    public List<int> meleeWeaponSuffixType = new List<int>() { 0, 1, 2, 3 };
    public List<int> rangedWeaponSuffixType = new List<int>() { 0, 1, 2, 3, 5 };
    public List<int> magicWeaponSuffixType = new List<int>() { 0, 1, 2, 3, 4, 5 };
    public List<int> summonWeaponSuffixType = new List<int>() { 0, 1, 2, 3 };

    // Used when deserializing (SaveData/LoadData/NetReceive)
    public WeaponModifier(ModifierType type, int magnitude, string tooltip, PrefixType prefixType = PrefixType.None, SuffixType suffixType = SuffixType.None, int tier = 9)
    {
        modifierType = type;
        this.magnitude = magnitude;
        this.tooltip = tooltip;
        this.prefixType = prefixType;
        this.suffixType = suffixType;
        this.tier = tier;
    }

    // Used when generating a new modifier
    public WeaponModifier(ModifierType type, List<int> excludeList, DamageClass damageType, int tier = 0)
    {
        modifierType = type;
        GenerateModifier(modifierType, excludeList, damageType, tier);
    }

    public void GenerateModifier(ModifierType type, List<int> excludeList, DamageClass damageType, int tier = 0)
    {
        List<int> IDs = new List<int>();
        Random random = new Random();

        if (type == ModifierType.Prefix)
        {
            if (damageType == DamageClass.Melee || damageType == DamageClass.MeleeNoSpeed || damageType == DamageClass.SummonMeleeSpeed) { IDs = new List<int>(meleeWeaponPrefixType); }
            else if (damageType == DamageClass.Ranged) { IDs = new List<int>(rangedWeaponPrefixType); }
            else if (damageType == DamageClass.Magic || damageType == DamageClass.MagicSummonHybrid) { IDs = new List<int>(magicWeaponPrefixType); }
            else if (damageType == DamageClass.Summon) { IDs = new List<int>(summonWeaponPrefixType); }
            else { IDs = new List<int>(summonWeaponPrefixType); }

            IDs = IDs.Where(val => !excludeList.Contains(val) && val != 0).ToList();
            prefixType = (PrefixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[prefixType][tier].minValue, TierDatabase.modifierTierDatabase[prefixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[prefixType];
            this.tier = tier;
        }
        if (type == ModifierType.Suffix)
        {
            if (damageType == DamageClass.Melee || damageType == DamageClass.MeleeNoSpeed || damageType == DamageClass.SummonMeleeSpeed) { IDs = new List<int>(meleeWeaponSuffixType); }
            else if (damageType == DamageClass.Ranged) { IDs = new List<int>(rangedWeaponSuffixType); }
            else if (damageType == DamageClass.Magic || damageType == DamageClass.MagicSummonHybrid) { IDs = new List<int>(magicWeaponSuffixType); }
            else if (damageType == DamageClass.Summon) { IDs = new List<int>(summonWeaponSuffixType); }
            else { IDs = new List<int>(summonWeaponSuffixType); }

            IDs = IDs.Where(val => !excludeList.Contains(val) && val != 0).ToList();
            suffixType = (SuffixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[suffixType][tier].minValue, TierDatabase.modifierTierDatabase[suffixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[suffixType];
            this.tier = tier;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
cd "c:/Users/nguon/Documents/My Games/Terraria/tModLoader/ModSources/ARPGItemSystem"
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/GlobalItems/Weapon/WeaponModifier.cs
git commit -m "feat: add tier field to WeaponModifier"
```

---

### Task 2: Add `tier` to ArmorModifier and AccessoryModifier

**Files:**
- Modify: `Common/GlobalItems/Armor/ArmorModifier.cs`
- Modify: `Common/GlobalItems/Accessory/AccessoryModifier.cs`

- [ ] **Step 1: Update ArmorModifier**

```csharp
public struct ArmorModifier
{
    public ModifierType modifierType;
    public PrefixType prefixType = PrefixType.None;
    public SuffixType suffixType = SuffixType.None;
    public int magnitude = 0;
    public int tier = 9;
    public string tooltip = "";

    public ArmorModifier(ModifierType type, int magnitude, string tooltip, PrefixType prefixType = PrefixType.None, SuffixType suffixType = SuffixType.None, int tier = 9)
    {
        modifierType = type;
        this.magnitude = magnitude;
        this.tooltip = tooltip;
        this.prefixType = prefixType;
        this.suffixType = suffixType;
        this.tier = tier;
    }

    public ArmorModifier(ModifierType type, List<int> excludeList, int tier = 0)
    {
        modifierType = type;
        GenerateModifier(modifierType, excludeList, tier);
    }

    public void GenerateModifier(ModifierType type, List<int> excludeList, int tier = 0)
    {
        List<int> IDs = new List<int>();
        Random random = new Random();

        if (type == ModifierType.Prefix)
        {
            IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(PrefixType)).Length - 1));
            IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
            prefixType = (PrefixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[prefixType][tier].minValue, TierDatabase.modifierTierDatabase[prefixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[prefixType];
            this.tier = tier;
        }
        if (type == ModifierType.Suffix)
        {
            IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(SuffixType)).Length - 1));
            IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
            suffixType = (SuffixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[suffixType][tier].minValue, TierDatabase.modifierTierDatabase[suffixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[suffixType];
            this.tier = tier;
        }
    }
}
```

- [ ] **Step 2: Update AccessoryModifier**

```csharp
public struct AccessoryModifier
{
    public ModifierType modifierType;
    public PrefixType prefixType = PrefixType.None;
    public SuffixType suffixType = SuffixType.None;
    public int magnitude = 0;
    public int tier = 9;
    public string tooltip = "";

    public AccessoryModifier(ModifierType type, int magnitude, string tooltip, PrefixType prefixType = PrefixType.None, SuffixType suffixType = SuffixType.None, int tier = 9)
    {
        modifierType = type;
        this.magnitude = magnitude;
        this.tooltip = tooltip;
        this.prefixType = prefixType;
        this.suffixType = suffixType;
        this.tier = tier;
    }

    public AccessoryModifier(ModifierType type, List<int> excludeList, int tier = 0)
    {
        modifierType = type;
        GenerateModifier(modifierType, excludeList, tier);
    }

    public void GenerateModifier(ModifierType type, List<int> excludeList, int tier = 0)
    {
        List<int> IDs = new List<int>();
        Random random = new Random();

        if (type == ModifierType.Prefix)
        {
            IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(PrefixType)).Length - 1));
            IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
            prefixType = (PrefixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[prefixType][tier].minValue, TierDatabase.modifierTierDatabase[prefixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[prefixType];
            this.tier = tier;
        }
        if (type == ModifierType.Suffix)
        {
            IDs.AddRange(Enumerable.Range(1, Enum.GetNames(typeof(SuffixType)).Length - 1));
            IDs = IDs.Where(val => !excludeList.Contains(val)).ToList();
            suffixType = (SuffixType)IDs[random.Next(0, IDs.Count)];
            magnitude = random.Next(TierDatabase.modifierTierDatabase[suffixType][tier].minValue, TierDatabase.modifierTierDatabase[suffixType][tier].maxValue + 1);
            tooltip = TooltipDatabase.modifierTooltipDatabase[suffixType];
            this.tier = tier;
        }
    }
}
```

- [ ] **Step 3: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Common/GlobalItems/Armor/ArmorModifier.cs Common/GlobalItems/Accessory/AccessoryModifier.cs
git commit -m "feat: add tier field to ArmorModifier and AccessoryModifier"
```

---

### Task 3: Plumb `tier` through WeaponManager persistence

**Files:**
- Modify: `Common/GlobalItems/Weapon/WeaponManager.cs`

- [ ] **Step 1: Update `SerializeData` to extract tier lists**

Replace the entire `SerializeData` method:

```csharp
private void SerializeData(
    out List<int> prefixIDList, out List<int> prefixMagnitudeList,
    out List<string> prefixTooltipList, out List<int> prefixTierList,
    out List<int> suffixIDList, out List<int> suffixMagnitudeList,
    out List<string> suffixTooltipList, out List<int> suffixTierList)
{
    prefixIDList = new List<int>();
    prefixMagnitudeList = new List<int>();
    prefixTooltipList = new List<string>();
    prefixTierList = new List<int>();
    suffixIDList = new List<int>();
    suffixMagnitudeList = new List<int>();
    suffixTooltipList = new List<string>();
    suffixTierList = new List<int>();

    foreach (var modifier in modifierList)
    {
        if (modifier.modifierType == ModifierType.Prefix)
        {
            prefixIDList.Add((int)modifier.prefixType);
            prefixMagnitudeList.Add(modifier.magnitude);
            prefixTooltipList.Add(modifier.tooltip);
            prefixTierList.Add(modifier.tier);
        }
        else
        {
            suffixIDList.Add((int)modifier.suffixType);
            suffixMagnitudeList.Add(modifier.magnitude);
            suffixTooltipList.Add(modifier.tooltip);
            suffixTierList.Add(modifier.tier);
        }
    }
}
```

- [ ] **Step 2: Update `SaveData`**

```csharp
public override void SaveData(Item item, TagCompound tag)
{
    List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
    List<string> prefixTooltipList;
    List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
    List<string> suffixTooltipList;
    SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                  out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

    tag["PrefixIDList"] = prefixIDList;
    tag["PrefixMagnitudeList"] = prefixMagnitudeList;
    tag["PrefixTooltipList"] = prefixTooltipList;
    tag["PrefixTierList"] = prefixTierList;

    tag["SuffixIDList"] = suffixIDList;
    tag["SuffixMagnitudeList"] = suffixMagnitudeList;
    tag["SuffixTooltipList"] = suffixTooltipList;
    tag["SuffixTierList"] = suffixTierList;
}
```

- [ ] **Step 3: Update `LoadData` with backward-compat default**

```csharp
public override void LoadData(Item item, TagCompound tag)
{
    var prefixIDList = tag.GetList<int>("PrefixIDList").ToList();
    var prefixMagnitudeList = tag.GetList<int>("PrefixMagnitudeList").ToList();
    var prefixTooltipList = tag.GetList<string>("PrefixTooltipList").ToList();
    var prefixTierList = tag.ContainsKey("PrefixTierList")
        ? tag.GetList<int>("PrefixTierList").ToList()
        : Enumerable.Repeat(9, prefixIDList.Count).ToList();

    var suffixIDList = tag.GetList<int>("SuffixIDList").ToList();
    var suffixMagnitudeList = tag.GetList<int>("SuffixMagnitudeList").ToList();
    var suffixTooltipList = tag.GetList<string>("SuffixTooltipList").ToList();
    var suffixTierList = tag.ContainsKey("SuffixTierList")
        ? tag.GetList<int>("SuffixTierList").ToList()
        : Enumerable.Repeat(9, suffixIDList.Count).ToList();

    for (int i = 0; i < prefixIDList.Count; i++)
        modifierList.Add(new WeaponModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));

    for (int i = 0; i < suffixIDList.Count; i++)
        modifierList.Add(new WeaponModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
}
```

- [ ] **Step 4: Update `NetSend`**

```csharp
public override void NetSend(Item item, BinaryWriter writer)
{
    List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
    List<string> prefixTooltipList;
    List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
    List<string> suffixTooltipList;
    SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                  out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

    writer.Write(prefixIDList.Count);
    foreach (var v in prefixIDList) writer.Write(v);
    writer.Write(prefixMagnitudeList.Count);
    foreach (var v in prefixMagnitudeList) writer.Write(v);
    writer.Write(prefixTooltipList.Count);
    foreach (var v in prefixTooltipList) writer.Write(v);
    writer.Write(prefixTierList.Count);
    foreach (var v in prefixTierList) writer.Write(v);

    writer.Write(suffixIDList.Count);
    foreach (var v in suffixIDList) writer.Write(v);
    writer.Write(suffixMagnitudeList.Count);
    foreach (var v in suffixMagnitudeList) writer.Write(v);
    writer.Write(suffixTooltipList.Count);
    foreach (var v in suffixTooltipList) writer.Write(v);
    writer.Write(suffixTierList.Count);
    foreach (var v in suffixTierList) writer.Write(v);
}
```

- [ ] **Step 5: Update `NetReceive`**

```csharp
public override void NetReceive(Item item, BinaryReader reader)
{
    var prefixIDList = new List<int>();
    var prefixMagnitudeList = new List<int>();
    var prefixTooltipList = new List<string>();
    var prefixTierList = new List<int>();
    var suffixIDList = new List<int>();
    var suffixMagnitudeList = new List<int>();
    var suffixTooltipList = new List<string>();
    var suffixTierList = new List<int>();

    int c;
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTierList.Add(reader.ReadInt32());

    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTierList.Add(reader.ReadInt32());

    modifierList.Clear();
    for (int i = 0; i < prefixIDList.Count; i++)
        modifierList.Add(new WeaponModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
    for (int i = 0; i < suffixIDList.Count; i++)
        modifierList.Add(new WeaponModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
}
```

- [ ] **Step 6: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Common/GlobalItems/Weapon/WeaponManager.cs
git commit -m "feat: plumb tier through WeaponManager persistence"
```

---

### Task 4: Plumb `tier` through ArmorManager and AccessoryManager persistence

**Files:**
- Modify: `Common/GlobalItems/Armor/ArmorManager.cs`
- Modify: `Common/GlobalItems/Accessory/AccessoryManager.cs`

Apply the exact same changes as Task 3 to both files. The only differences are the type names:
- `ArmorManager`: use `ArmorModifier`, `Armor.ModifierType`, `Armor.PrefixType`, `Armor.SuffixType`
- `AccessoryManager`: use `AccessoryModifier`, `Accessory.ModifierType`, `Accessory.PrefixType`, `Accessory.SuffixType`

- [ ] **Step 1: Update ArmorManager `SerializeData`**

```csharp
private void SerializeData(
    out List<int> prefixIDList, out List<int> prefixMagnitudeList,
    out List<string> prefixTooltipList, out List<int> prefixTierList,
    out List<int> suffixIDList, out List<int> suffixMagnitudeList,
    out List<string> suffixTooltipList, out List<int> suffixTierList)
{
    prefixIDList = new List<int>(); prefixMagnitudeList = new List<int>();
    prefixTooltipList = new List<string>(); prefixTierList = new List<int>();
    suffixIDList = new List<int>(); suffixMagnitudeList = new List<int>();
    suffixTooltipList = new List<string>(); suffixTierList = new List<int>();

    foreach (var modifier in modifierList)
    {
        if (modifier.modifierType == ModifierType.Prefix)
        {
            prefixIDList.Add((int)modifier.prefixType);
            prefixMagnitudeList.Add(modifier.magnitude);
            prefixTooltipList.Add(modifier.tooltip);
            prefixTierList.Add(modifier.tier);
        }
        else
        {
            suffixIDList.Add((int)modifier.suffixType);
            suffixMagnitudeList.Add(modifier.magnitude);
            suffixTooltipList.Add(modifier.tooltip);
            suffixTierList.Add(modifier.tier);
        }
    }
}
```

- [ ] **Step 2: Update ArmorManager `SaveData`**

```csharp
public override void SaveData(Item item, TagCompound tag)
{
    List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
    List<string> prefixTooltipList;
    List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
    List<string> suffixTooltipList;
    SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                  out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

    tag["PrefixIDList"] = prefixIDList; tag["PrefixMagnitudeList"] = prefixMagnitudeList;
    tag["PrefixTooltipList"] = prefixTooltipList; tag["PrefixTierList"] = prefixTierList;
    tag["SuffixIDList"] = suffixIDList; tag["SuffixMagnitudeList"] = suffixMagnitudeList;
    tag["SuffixTooltipList"] = suffixTooltipList; tag["SuffixTierList"] = suffixTierList;
}
```

- [ ] **Step 3: Update ArmorManager `LoadData`**

```csharp
public override void LoadData(Item item, TagCompound tag)
{
    var prefixIDList = tag.GetList<int>("PrefixIDList").ToList();
    var prefixMagnitudeList = tag.GetList<int>("PrefixMagnitudeList").ToList();
    var prefixTooltipList = tag.GetList<string>("PrefixTooltipList").ToList();
    var prefixTierList = tag.ContainsKey("PrefixTierList")
        ? tag.GetList<int>("PrefixTierList").ToList()
        : Enumerable.Repeat(9, prefixIDList.Count).ToList();

    var suffixIDList = tag.GetList<int>("SuffixIDList").ToList();
    var suffixMagnitudeList = tag.GetList<int>("SuffixMagnitudeList").ToList();
    var suffixTooltipList = tag.GetList<string>("SuffixTooltipList").ToList();
    var suffixTierList = tag.ContainsKey("SuffixTierList")
        ? tag.GetList<int>("SuffixTierList").ToList()
        : Enumerable.Repeat(9, suffixIDList.Count).ToList();

    for (int i = 0; i < prefixIDList.Count; i++)
        modifierList.Add(new ArmorModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
    for (int i = 0; i < suffixIDList.Count; i++)
        modifierList.Add(new ArmorModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
}
```

- [ ] **Step 4: Update ArmorManager `NetSend` and `NetReceive`**

```csharp
public override void NetSend(Item item, BinaryWriter writer)
{
    List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
    List<string> prefixTooltipList;
    List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
    List<string> suffixTooltipList;
    SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                  out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

    writer.Write(prefixIDList.Count);
    foreach (var v in prefixIDList) writer.Write(v);
    writer.Write(prefixMagnitudeList.Count);
    foreach (var v in prefixMagnitudeList) writer.Write(v);
    writer.Write(prefixTooltipList.Count);
    foreach (var v in prefixTooltipList) writer.Write(v);
    writer.Write(prefixTierList.Count);
    foreach (var v in prefixTierList) writer.Write(v);

    writer.Write(suffixIDList.Count);
    foreach (var v in suffixIDList) writer.Write(v);
    writer.Write(suffixMagnitudeList.Count);
    foreach (var v in suffixMagnitudeList) writer.Write(v);
    writer.Write(suffixTooltipList.Count);
    foreach (var v in suffixTooltipList) writer.Write(v);
    writer.Write(suffixTierList.Count);
    foreach (var v in suffixTierList) writer.Write(v);
}

public override void NetReceive(Item item, BinaryReader reader)
{
    var prefixIDList = new List<int>(); var prefixMagnitudeList = new List<int>();
    var prefixTooltipList = new List<string>(); var prefixTierList = new List<int>();
    var suffixIDList = new List<int>(); var suffixMagnitudeList = new List<int>();
    var suffixTooltipList = new List<string>(); var suffixTierList = new List<int>();

    int c;
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTierList.Add(reader.ReadInt32());

    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTierList.Add(reader.ReadInt32());

    modifierList.Clear();
    for (int i = 0; i < prefixIDList.Count; i++)
        modifierList.Add(new ArmorModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
    for (int i = 0; i < suffixIDList.Count; i++)
        modifierList.Add(new ArmorModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
}
```

- [ ] **Step 5: Update AccessoryManager `NetSend` and `NetReceive`**

```csharp
public override void NetSend(Item item, BinaryWriter writer)
{
    List<int> prefixIDList, prefixMagnitudeList, prefixTierList;
    List<string> prefixTooltipList;
    List<int> suffixIDList, suffixMagnitudeList, suffixTierList;
    List<string> suffixTooltipList;
    SerializeData(out prefixIDList, out prefixMagnitudeList, out prefixTooltipList, out prefixTierList,
                  out suffixIDList, out suffixMagnitudeList, out suffixTooltipList, out suffixTierList);

    writer.Write(prefixIDList.Count);
    foreach (var v in prefixIDList) writer.Write(v);
    writer.Write(prefixMagnitudeList.Count);
    foreach (var v in prefixMagnitudeList) writer.Write(v);
    writer.Write(prefixTooltipList.Count);
    foreach (var v in prefixTooltipList) writer.Write(v);
    writer.Write(prefixTierList.Count);
    foreach (var v in prefixTierList) writer.Write(v);

    writer.Write(suffixIDList.Count);
    foreach (var v in suffixIDList) writer.Write(v);
    writer.Write(suffixMagnitudeList.Count);
    foreach (var v in suffixMagnitudeList) writer.Write(v);
    writer.Write(suffixTooltipList.Count);
    foreach (var v in suffixTooltipList) writer.Write(v);
    writer.Write(suffixTierList.Count);
    foreach (var v in suffixTierList) writer.Write(v);
}

public override void NetReceive(Item item, BinaryReader reader)
{
    var prefixIDList = new List<int>(); var prefixMagnitudeList = new List<int>();
    var prefixTooltipList = new List<string>(); var prefixTierList = new List<int>();
    var suffixIDList = new List<int>(); var suffixMagnitudeList = new List<int>();
    var suffixTooltipList = new List<string>(); var suffixTierList = new List<int>();

    int c;
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) prefixTierList.Add(reader.ReadInt32());

    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixIDList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixMagnitudeList.Add(reader.ReadInt32());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTooltipList.Add(reader.ReadString());
    c = reader.ReadInt32(); for (int i = 0; i < c; i++) suffixTierList.Add(reader.ReadInt32());

    modifierList.Clear();
    for (int i = 0; i < prefixIDList.Count; i++)
        modifierList.Add(new AccessoryModifier(ModifierType.Prefix, prefixMagnitudeList[i], prefixTooltipList[i], (PrefixType)prefixIDList[i], SuffixType.None, prefixTierList[i]));
    for (int i = 0; i < suffixIDList.Count; i++)
        modifierList.Add(new AccessoryModifier(ModifierType.Suffix, suffixMagnitudeList[i], suffixTooltipList[i], PrefixType.None, (SuffixType)suffixIDList[i], suffixTierList[i]));
}
```

- [ ] **Step 6: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Common/GlobalItems/Armor/ArmorManager.cs Common/GlobalItems/Accessory/AccessoryManager.cs
git commit -m "feat: plumb tier through ArmorManager and AccessoryManager persistence"
```

---

### Task 5: Remove keybind system and empty stub

**Files:**
- Delete: `Common/Players/Keybind.cs`
- Delete: `Common/Systems/KeybindSystem.cs`
- Delete: `Common/UI/CraftingPanel.cs`

- [ ] **Step 1: Delete the three files**

```bash
rm "Common/Players/Keybind.cs"
rm "Common/Systems/KeybindSystem.cs"
rm "Common/UI/CraftingPanel.cs"
```

- [ ] **Step 2: Remove the keybind localization key**

In `Localization/en-US_Mods.ARPGItemSystem.hjson`, remove the `Keybinds` block entirely:

```hjson
// Delete this entire block:
Keybinds: {
    Craft: {
        DisplayName: Reroll all modifiers of an item
    }
}
```

The file should be empty (or just `{}`) after removal.

- [ ] **Step 3: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors. (No remaining references to `KeybindSystem` or `CraftKeyBind`.)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: remove keybind-based reroll system"
```

---

### Task 6: Add ReforgeConfig

**Files:**
- Create: `Common/Config/ReforgeConfig.cs`

- [ ] **Step 1: Create the file**

```csharp
using System;

namespace ARPGItemSystem.Common.Config
{
    public static class ReforgeConfig
    {
        public const float Scale = 1.0f;
        public const float Base = 2.0f;

        public static int CalculateCost(int itemValue, int tier)
        {
            return (int)(itemValue * Scale * Math.Pow(Base, 9 - tier));
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/Config/ReforgeConfig.cs
git commit -m "feat: add ReforgeConfig with exponential cost formula"
```

---

### Task 7: Add packet infrastructure

**Files:**
- Create: `Common/Network/ReforgePacketHandler.cs`
- Modify: `ARPGItemSystem.cs`

- [ ] **Step 1: Create `ReforgePacketHandler.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Systems;
using Microsoft.Xna.Framework;
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

    public enum ItemCategory : byte
    {
        Weapon = 0,
        Armor = 1,
        Accessory = 2
    }

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
                case ReforgePacketType.RerollRequest:
                    HandleRerollRequest(reader, whoAmI);
                    break;
                case ReforgePacketType.RerollResult:
                    HandleRerollResult(reader);
                    break;
                case ReforgePacketType.RerollRejected:
                    HandleRerollRejected(reader);
                    break;
            }
        }

        // Called by client AffixLine click handler
        public static void SendRerollRequest(int affixIndex, bool isPrefix, ItemCategory cat, WeaponDamageCategory damCat, int itemValue, List<int> excludeList)
        {
            var packet = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            packet.Write((byte)ReforgePacketType.RerollRequest);
            packet.Write((byte)affixIndex);
            packet.Write(isPrefix);
            packet.Write((byte)cat);
            packet.Write((byte)damCat);
            packet.Write(itemValue);
            packet.Write((byte)excludeList.Count);
            foreach (var id in excludeList) packet.Write((byte)id);
            packet.Send();
        }

        // Server: receive request, validate cost, roll, send result
        private static void HandleRerollRequest(BinaryReader reader, int whoAmI)
        {
            byte affixIndex = reader.ReadByte();
            bool isPrefix = reader.ReadBoolean();
            var cat = (ItemCategory)reader.ReadByte();
            var damCat = (WeaponDamageCategory)reader.ReadByte();
            int itemValue = reader.ReadInt32();
            byte excludeCount = reader.ReadByte();
            var excludeList = new List<int>();
            for (int i = 0; i < excludeCount; i++) excludeList.Add(reader.ReadByte());

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

            RollNewModifier(cat, damCat, isPrefix, excludeList, tier,
                out int newTypeID, out int newMagnitude, out string newTooltip);

            var result = ModContent.GetInstance<ARPGItemSystem>().GetPacket();
            result.Write((byte)ReforgePacketType.RerollResult);
            result.Write(affixIndex);
            result.Write(isPrefix);
            result.Write(newTypeID);
            result.Write(newMagnitude);
            result.Write(tier);
            result.Write(newTooltip);
            result.Send(whoAmI);
        }

        // Client: apply result to Main.reforgeItem and refresh UI
        private static void HandleRerollResult(BinaryReader reader)
        {
            byte affixIndex = reader.ReadByte();
            bool isPrefix = reader.ReadBoolean();
            int newTypeID = reader.ReadInt32();
            int newMagnitude = reader.ReadInt32();
            int newTier = reader.ReadInt32();
            string newTooltip = reader.ReadString();

            var item = Main.reforgeItem;
            if (item.IsAir) return;

            ApplyModifierToItem(item, affixIndex, isPrefix, newTypeID, newMagnitude, newTier, newTooltip);
            ModContent.GetInstance<UISystem>().Panel.RefreshAffix(affixIndex);
        }

        // Client: re-enable buttons on rejection
        private static void HandleRerollRejected(BinaryReader reader)
        {
            reader.ReadByte(); // affixIndex — not needed, re-enable all
            ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
        }

        // Single-player: roll and apply directly, no packet
        public static void DoRerollDirectly(Item item, int affixIndex, bool isPrefix, ItemCategory cat, WeaponDamageCategory damCat, List<int> excludeList)
        {
            int tier = utils.GetTier();
            int cost = ReforgeConfig.CalculateCost(item.value, tier);

            if (!Main.LocalPlayer.BuyItem(cost))
            {
                ModContent.GetInstance<UISystem>().Panel.SetAllPending(false);
                return;
            }

            RollNewModifier(cat, damCat, isPrefix, excludeList, tier,
                out int newTypeID, out int newMagnitude, out string newTooltip);

            ApplyModifierToItem(item, affixIndex, isPrefix, newTypeID, newMagnitude, tier, newTooltip);
        }

        private static void RollNewModifier(ItemCategory cat, WeaponDamageCategory damCat, bool isPrefix, List<int> excludeList, int tier,
            out int newTypeID, out int newMagnitude, out string newTooltip)
        {
            newTypeID = 0; newMagnitude = 0; newTooltip = "";

            switch (cat)
            {
                case ItemCategory.Weapon:
                {
                    var damageClass = GetDamageClass(damCat);
                    var modType = isPrefix ? Weapon.ModifierType.Prefix : Weapon.ModifierType.Suffix;
                    var m = new WeaponModifier(modType, excludeList, damageClass, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
                case ItemCategory.Armor:
                {
                    var modType = isPrefix ? Armor.ModifierType.Prefix : Armor.ModifierType.Suffix;
                    var m = new ArmorModifier(modType, excludeList, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
                case ItemCategory.Accessory:
                {
                    var modType = isPrefix ? Accessory.ModifierType.Prefix : Accessory.ModifierType.Suffix;
                    var m = new AccessoryModifier(modType, excludeList, tier);
                    newTypeID = isPrefix ? (int)m.prefixType : (int)m.suffixType;
                    newMagnitude = m.magnitude;
                    newTooltip = m.tooltip;
                    break;
                }
            }
        }

        private static void ApplyModifierToItem(Item item, int affixIndex, bool isPrefix, int newTypeID, int newMagnitude, int newTier, string newTooltip)
        {
            if (item.damage > 0 && item.maxStack == 1)
            {
                var manager = item.GetGlobalItem<WeaponManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Weapon.PrefixType)newTypeID;
                else mod.suffixType = (Weapon.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
            else if (item.accessory)
            {
                var manager = item.GetGlobalItem<AccessoryManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Accessory.PrefixType)newTypeID;
                else mod.suffixType = (Accessory.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
            else
            {
                var manager = item.GetGlobalItem<ArmorManager>();
                var mod = manager.modifierList[affixIndex];
                if (isPrefix) mod.prefixType = (Armor.PrefixType)newTypeID;
                else mod.suffixType = (Armor.SuffixType)newTypeID;
                mod.magnitude = newMagnitude; mod.tier = newTier; mod.tooltip = newTooltip;
                manager.modifierList[affixIndex] = mod;
            }
        }

        public static ItemCategory GetItemCategory(Item item)
        {
            if (item.damage > 0 && item.maxStack == 1) return ItemCategory.Weapon;
            if (item.accessory) return ItemCategory.Accessory;
            return ItemCategory.Armor;
        }

        public static WeaponDamageCategory GetDamageCategory(Item item)
        {
            if (item.DamageType == DamageClass.Melee || item.DamageType == DamageClass.MeleeNoSpeed || item.DamageType == DamageClass.SummonMeleeSpeed)
                return WeaponDamageCategory.Melee;
            if (item.DamageType == DamageClass.Ranged) return WeaponDamageCategory.Ranged;
            if (item.DamageType == DamageClass.Magic || item.DamageType == DamageClass.MagicSummonHybrid)
                return WeaponDamageCategory.Magic;
            if (item.DamageType == DamageClass.Summon) return WeaponDamageCategory.Summon;
            return WeaponDamageCategory.Other;
        }

        public static List<int> GetExcludeList(Item item, int affixIndex, bool isPrefix)
        {
            var result = new List<int>();
            if (item.damage > 0 && item.maxStack == 1)
            {
                var list = item.GetGlobalItem<WeaponManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Weapon.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Weapon.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
            }
            else if (item.accessory)
            {
                var list = item.GetGlobalItem<AccessoryManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Accessory.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Accessory.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
            }
            else
            {
                var list = item.GetGlobalItem<ArmorManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i == affixIndex) continue;
                    var m = list[i];
                    if (isPrefix && m.modifierType == Armor.ModifierType.Prefix) result.Add((int)m.prefixType);
                    else if (!isPrefix && m.modifierType == Armor.ModifierType.Suffix) result.Add((int)m.suffixType);
                }
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

- [ ] **Step 2: Add `HandlePacket` to `ARPGItemSystem.cs`**

```csharp
using System.IO;
using ARPGItemSystem.Common.Network;
using Terraria.ModLoader;

namespace ARPGItemSystem
{
    public class ARPGItemSystem : Mod
    {
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            ReforgePacketHandler.HandlePacket(reader, whoAmI);
        }
    }
}
```

- [ ] **Step 3: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Common/Network/ReforgePacketHandler.cs ARPGItemSystem.cs
git commit -m "feat: add packet infrastructure for server-authoritative affix reroll"
```

---

### Task 8: Create UIReforgeSlot

**Files:**
- Create: `Common/UI/UIReforgeSlot.cs`

- [ ] **Step 1: Create the file**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class UIReforgeSlot : UIElement
    {
        public UIReforgeSlot()
        {
            Width.Set(52, 0f);
            Height.Set(52, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();
            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/UI/UIReforgeSlot.cs
git commit -m "feat: add UIReforgeSlot wrapping Main.reforgeItem"
```

---

### Task 9: Create AffixLine

**Files:**
- Create: `Common/UI/AffixLine.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Network;
using ARPGItemSystem.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class AffixLine : UIElement
    {
        private UIImageButton _hammerButton;
        private UIText _affixText;
        private UIText _costText;
        private bool _isPending;
        private readonly int _modifierIndex;
        private readonly bool _isPrefix;

        public AffixLine(string displayText, int tier, int modifierIndex, bool isPrefix)
        {
            _modifierIndex = modifierIndex;
            _isPrefix = isPrefix;
            Height.Set(28, 0f);

            var hammerTexture = Main.Assets.Request<Texture2D>("Images/UI/ReforgeButton", AssetRequestMode.ImmediateLoad);
            _hammerButton = new UIImageButton(hammerTexture);
            _hammerButton.Width.Set(22, 0f);
            _hammerButton.Height.Set(22, 0f);
            _hammerButton.Left.Set(0, 0f);
            _hammerButton.VAlign = 0.5f;
            _hammerButton.OnLeftClick += OnHammerClicked;
            Append(_hammerButton);

            _affixText = new UIText(displayText, 0.85f);
            _affixText.TextColor = isPrefix ? Color.LightGreen : Color.DeepSkyBlue;
            _affixText.Left.Set(28, 0f);
            _affixText.VAlign = 0.5f;
            Append(_affixText);

            _costText = new UIText(FormatCost(ReforgeConfig.CalculateCost(Main.reforgeItem.IsAir ? 0 : Main.reforgeItem.value, tier)), 0.85f);
            _costText.HAlign = 1f;
            _costText.VAlign = 0.5f;
            Append(_costText);
        }

        private void OnHammerClicked(UIMouseEvent evt, UIElement listeningElement)
        {
            if (_isPending || Main.reforgeItem.IsAir) return;

            var item = Main.reforgeItem;
            var cat = ReforgePacketHandler.GetItemCategory(item);
            var damCat = ReforgePacketHandler.GetDamageCategory(item);
            var excludeList = ReforgePacketHandler.GetExcludeList(item, _modifierIndex, _isPrefix);

            ModContent.GetInstance<UISystem>().Panel.SetAllPending(true);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoRerollDirectly(item, _modifierIndex, _isPrefix, cat, damCat, excludeList);
                ModContent.GetInstance<UISystem>().Panel.RefreshAffix(_modifierIndex);
            }
            else
            {
                ReforgePacketHandler.SendRerollRequest(_modifierIndex, _isPrefix, cat, damCat, item.value, excludeList);
            }
        }

        public void SetPending(bool pending)
        {
            _isPending = pending;
            _hammerButton.SetVisibility(pending ? 0.4f : 1f, pending ? 0.4f : 1f);
        }

        public void Refresh()
        {
            var item = Main.reforgeItem;
            if (item.IsAir) return;

            string displayText;
            int tier;

            if (item.damage > 0 && item.maxStack == 1)
            {
                var mod = item.GetGlobalItem<WeaponManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }
            else if (item.accessory)
            {
                var mod = item.GetGlobalItem<AccessoryManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }
            else
            {
                var mod = item.GetGlobalItem<ArmorManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }

            _affixText.SetText(displayText);
            _costText.SetText(FormatCost(ReforgeConfig.CalculateCost(item.value, tier)));
        }

        private static string FormatCost(int cost)
        {
            int platinum = cost / 1000000;
            int gold = (cost / 10000) % 100;
            int silver = (cost / 100) % 100;
            int copper = cost % 100;

            string result = "";
            if (platinum > 0) result += $"{platinum}p ";
            if (gold > 0) result += $"{gold}g ";
            if (silver > 0) result += $"{silver}s ";
            if (copper > 0 || result == "") result += $"{copper}c";
            return result.Trim();
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/UI/AffixLine.cs
git commit -m "feat: add AffixLine UI element with hammer button and cost display"
```

---

### Task 10: Create ReforgePanel

**Files:**
- Create: `Common/UI/ReforgePanel.cs`

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections.Generic;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class ReforgePanel : UIState
    {
        private UIPanel _panel;
        private UIText _itemName;
        private UIText _placeholder;
        private readonly List<AffixLine> _affixLines = new();
        private Item _lastItem;

        public override void OnInitialize()
        {
            _panel = new UIPanel();
            _panel.Width.Set(320, 0f);
            _panel.Height.Set(420, 0f);
            _panel.HAlign = 0.5f;
            _panel.VAlign = 0.5f;
            Append(_panel);

            var title = new UITextPanel<string>(Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Title"));
            title.HAlign = 0.5f;
            title.Top.Set(-12, 0f);
            _panel.Append(title);

            var slot = new UIReforgeSlot();
            slot.HAlign = 0.5f;
            slot.Top.Set(24, 0f);
            _panel.Append(slot);

            _itemName = new UIText("", 0.9f);
            _itemName.HAlign = 0.5f;
            _itemName.Top.Set(84, 0f);
            _panel.Append(_itemName);

            _placeholder = new UIText(Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Placeholder"), 0.85f);
            _placeholder.TextColor = Color.Gray;
            _placeholder.HAlign = 0.5f;
            _placeholder.Top.Set(120, 0f);
            _panel.Append(_placeholder);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool hasItem = !Main.reforgeItem.IsAir;

            if (hasItem && Main.reforgeItem != _lastItem)
            {
                RefreshAffixLines();
                _lastItem = Main.reforgeItem;
            }
            else if (!hasItem && _affixLines.Count > 0)
            {
                ClearAffixLines();
            }

            _placeholder.TextColor = hasItem ? Color.Transparent : Color.Gray;
            _itemName.SetText(hasItem ? Main.reforgeItem.Name : "");
        }

        public void RefreshAffix(int index)
        {
            if (index >= 0 && index < _affixLines.Count)
                _affixLines[index].Refresh();
            SetAllPending(false);
        }

        public void SetAllPending(bool pending)
        {
            foreach (var line in _affixLines)
                line.SetPending(pending);
        }

        private void RefreshAffixLines()
        {
            ClearAffixLines();
            var item = Main.reforgeItem;
            var lines = GetModifierLines(item);

            float yOffset = 110f;
            foreach (var (text, tier, index, isPrefix) in lines)
            {
                var line = new AffixLine(text, tier, index, isPrefix);
                line.Top.Set(yOffset, 0f);
                line.Width.Set(-20, 1f);
                line.Left.Set(10, 0f);
                _panel.Append(line);
                _affixLines.Add(line);
                yOffset += 32f;
            }
        }

        private void ClearAffixLines()
        {
            foreach (var line in _affixLines)
                _panel.RemoveChild(line);
            _affixLines.Clear();
            _lastItem = null;
        }

        private static List<(string text, int tier, int index, bool isPrefix)> GetModifierLines(Item item)
        {
            var result = new List<(string, int, int, bool)>();

            if (item.damage > 0 && item.maxStack == 1)
            {
                var list = item.GetGlobalItem<WeaponManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Weapon.ModifierType.Prefix));
                }
            }
            else if (item.accessory)
            {
                var list = item.GetGlobalItem<AccessoryManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Accessory.ModifierType.Prefix));
                }
            }
            else
            {
                var list = item.GetGlobalItem<ArmorManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Armor.ModifierType.Prefix));
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/UI/ReforgePanel.cs
git commit -m "feat: add ReforgePanel UIState"
```

---

### Task 11: Create UISystem and wire the interface layer

**Files:**
- Create: `Common/Systems/UISystem.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using ARPGItemSystem.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace ARPGItemSystem.Common.Systems
{
    public class UISystem : ModSystem
    {
        private UserInterface _reforgeInterface;
        internal ReforgePanel Panel;

        public override void Load()
        {
            if (!Main.dedServer)
            {
                Panel = new ReforgePanel();
                Panel.Activate();
                _reforgeInterface = new UserInterface();
                _reforgeInterface.SetState(Panel);
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.InReforgeMenu)
                _reforgeInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name == "Vanilla: Reforge Menu");
            if (index < 0)
            {
                Mod.Logger.Warn("[ARPGItemSystem] Could not find 'Vanilla: Reforge Menu' interface layer.");
                return;
            }

            layers[index] = new LegacyGameInterfaceLayer(
                "ARPGItemSystem: Reforge Panel",
                () =>
                {
                    if (Main.InReforgeMenu)
                        _reforgeInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI
            );
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Common/Systems/UISystem.cs
git commit -m "feat: add UISystem replacing vanilla reforge layer with ARPG panel"
```

---

### Task 12: Add localization keys

**Files:**
- Modify: `Localization/en-US_Mods.ARPGItemSystem.hjson`

- [ ] **Step 1: Update the hjson file**

```hjson
UI: {
    ReforgePanel: {
        Title: Modifier Reforge
        Placeholder: Place an item to begin
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Localization/en-US_Mods.ARPGItemSystem.hjson
git commit -m "feat: add reforge panel localization keys"
```

---

### Task 13: In-game testing

Build and reload the mod via tModLoader → Workshop → Mod Sources → ARPGItemSystem → Build & Reload.

- [ ] **Single-player smoke test**
  1. Start a single-player world. Find a Goblin Tinkerer NPC (spawn one with a Goblin Army event or use a test world).
  2. Talk to the Goblin Tinkerer and click "Reforge". Verify our panel opens (not the vanilla reforge UI).
  3. Hold a weapon in your hotbar, drag it into the item slot in the panel. Verify the item name updates and affix lines appear with green (prefix) and blue (suffix) colored rows.
  4. Note the coin cost shown next to each affix. Verify it matches the formula: a tier 0 affix on a 10g item should cost ~51g 20s.
  5. Click a hammer button with enough gold. Verify: the modifier on that row changes, the cost updates, buttons re-enable after the reroll.
  6. Click a hammer button without enough gold. Verify: the buttons re-enable immediately and no modifier changes.
  7. Press ESC. Verify: the panel closes and the item returns to your inventory with the updated modifiers still applied.
  8. Save, quit, reload. Verify the rerolled modifiers are still present (tier persisted correctly).

- [ ] **Armor and accessory test**
  1. Drag an armor piece into the slot. Verify affix lines appear correctly.
  2. Drag an accessory into the slot. Verify affix lines appear correctly.
  3. Reroll one affix on each and confirm the change persists after save/reload.

- [ ] **Multiplayer test**
  1. Host a local server and join with a second client.
  2. Open the Goblin Tinkerer on the client, drag a weapon into the slot, click a hammer.
  3. Verify the cost is deducted from the client's coins and the modifier updates correctly.
  4. Verify other clients see the updated modifier on the item when the client picks it up.
