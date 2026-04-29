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

            // When vanilla closes the menu (ESC, NPC walked away, etc.) it returns
            // Main.reforgeItem to the player's inventory but never touches our
            // _slot.SlotItem. Detect the close here and clear the slot.
            if (!Main.InReforgeMenu && Panel != null && !Panel.SlotIsEmpty)
                Panel.ClearSlot();

            if (Main.InReforgeMenu)
                _reforgeInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(l => l.Name == "Vanilla: Inventory");
            if (inventoryIndex < 0) return;

            // Suppress vanilla's reforge slot draw so ours is the only one shown.
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
                        Main.InReforgeMenu = true;
                        _reforgeInterface.Draw(Main.spriteBatch, _lastGameTime);
                    }
                    else
                    {
                        // Menu closed — return item and clean up.
                        Panel?.ReturnItemToPlayer();
                        Main.InReforgeMenu = false;
                    }
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }
    }
}
