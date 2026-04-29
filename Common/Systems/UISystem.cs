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

            // Before inventory draws: save InReforgeMenu state and suppress it
            // so the vanilla reforge slot/button don't render
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

            // After inventory draws: restore InReforgeMenu (so vanilla close/ESC
            // logic still works next frame) and draw our panel
            layers.Insert(inventoryIndex + 2, new LegacyGameInterfaceLayer(
                "ARPGItemSystem: Reforge Panel",
                () =>
                {
                    if (_reforgeWasOpen)
                    {
                        Main.InReforgeMenu = true;
                        _reforgeInterface.Draw(Main.spriteBatch, _lastGameTime);
                    }
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }
    }
}
