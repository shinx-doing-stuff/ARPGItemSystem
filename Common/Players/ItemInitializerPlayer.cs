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

                if (item.damage > 0 && item.maxStack == 1)
                {
                    var mgr = item.GetGlobalItem<WeaponManager>();
                    if (mgr.modifierList.Count == 0)
                    {
                        mgr.Reroll(item);
                        mgr._initialized = true;
                    }
                }
                else if (item.accessory)
                {
                    var mgr = item.GetGlobalItem<AccessoryManager>();
                    if (mgr.modifierList.Count == 0)
                    {
                        mgr.Reroll(item);
                        mgr._initialized = true;
                    }
                }
                else if (!item.vanity && item.maxStack == 1 && item.damage < 1)
                {
                    if (item.TryGetGlobalItem<ArmorManager>(out var mgr) && mgr.modifierList.Count == 0)
                    {
                        mgr.Reroll(item);
                        mgr._initialized = true;
                    }
                }
            }
        }
    }
}
