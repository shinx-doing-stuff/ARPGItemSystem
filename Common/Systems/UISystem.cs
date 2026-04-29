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

            // Replace the vanilla inventory layer with a wrapper that suppresses
            // Main.InReforgeMenu during the DrawInventory call so vanilla doesn't
            // render the reforge slot/button. ESC is processed in the update phase
            // (before draw) where Main.InReforgeMenu is always true, so one ESC works.
            layers[inventoryIndex] = new LegacyGameInterfaceLayer(
                "Vanilla: Inventory",
                () =>
                {
                    if (!Main.ingameOptionsWindow && !Main.gameMenu)
                    {
                        bool wasReforge = Main.InReforgeMenu;
                        if (wasReforge) Main.InReforgeMenu = false;
                        Main.DrawInventory();
                        if (wasReforge) Main.InReforgeMenu = true;
                    }
                    return true;
                },
                InterfaceScaleType.UI
            );

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
