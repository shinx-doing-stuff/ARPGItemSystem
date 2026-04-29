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
