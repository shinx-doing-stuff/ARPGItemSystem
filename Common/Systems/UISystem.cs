using System.Collections.Generic;
using ARPGItemSystem.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ARPGItemSystem.Common.Systems
{
    public class UISystem : ModSystem
    {
        private UserInterface _reforgeInterface;
        internal ReforgePanel Panel;
        private GameTime _lastGameTime = new GameTime();
        private bool _reforgeWasOpen;

        public override void Load()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                Panel = new ReforgePanel();
                Panel.Activate();
                _reforgeInterface = new UserInterface();
                _reforgeInterface.SetState(Panel);
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            _lastGameTime = gameTime;
            if (Main.InReforgeMenu)
                _reforgeInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(l => l.Name == "Vanilla: Inventory");
            if (inventoryIndex < 0) return;

            // Hide vanilla reforge slot/button by suppressing Main.InReforgeMenu
            // for the duration of the inventory draw only.
            layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer(
                "ARPGItemSystem: Suppress Vanilla Reforge",
                () =>
                {
                    _reforgeWasOpen = Main.InReforgeMenu;
                    if (_reforgeWasOpen) Main.InReforgeMenu = false;
                    return true;
                },
                InterfaceScaleType.UI
            ));

            layers.Insert(inventoryIndex + 2, new LegacyGameInterfaceLayer(
                "ARPGItemSystem: Reforge Panel",
                () =>
                {
                    if (!_reforgeWasOpen) return true;

                    if (Main.playerInventory)
                    {
                        // Inventory still open — restore and draw our panel normally.
                        Main.InReforgeMenu = true;
                        _reforgeInterface.Draw(Main.spriteBatch, _lastGameTime);
                    }
                    else
                    {
                        // Inventory was closed (ESC / NPC walked away / etc).
                        // Don't restore InReforgeMenu so the close propagates.
                        // Return any held item in case vanilla's cleanup was
                        // inside DrawInventory and our suppress skipped it.
                        if (!Main.reforgeItem.IsAir)
                        {
                            if (Main.mouseItem.IsAir)
                            {
                                Main.mouseItem = Main.reforgeItem;
                            }
                            else
                            {
                                for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++)
                                {
                                    if (Main.LocalPlayer.inventory[i].IsAir)
                                    {
                                        Main.LocalPlayer.inventory[i] = Main.reforgeItem;
                                        break;
                                    }
                                }
                            }
                            Main.reforgeItem = new Item();
                        }
                        Main.InReforgeMenu = false;
                    }
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }
    }
}
