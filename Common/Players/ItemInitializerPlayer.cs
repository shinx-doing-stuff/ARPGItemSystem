using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Players
{
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
