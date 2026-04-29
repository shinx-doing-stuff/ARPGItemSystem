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

            // Draw our panel after inventory so it renders on top of vanilla's reforge UI.
            // We do NOT suppress Main.InReforgeMenu — that caused vanilla's close logic
            // to misfire and required two ESC presses to exit.
            // Instead, our UIPanel physically covers vanilla's reforge slot/button area,
            // and UIReforgeSlot suppresses mouse state in DrawSelf so vanilla's slot
            // underneath cannot steal clicks.
            layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                "ARPGItemSystem: Reforge Panel",
                () =>
                {
                    if (Main.InReforgeMenu)
                        _reforgeInterface.Draw(Main.spriteBatch, _lastGameTime);
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }
    }
}
