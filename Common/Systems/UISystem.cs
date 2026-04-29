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

                // Suppress vanilla reforge slot/button rendering only for the duration
                // of DrawInventory. This is atomic: ESC handling runs in the update phase
                // (before draw) and always sees Main.InReforgeMenu = true, so one ESC
                // still closes the menu correctly.
                On.Terraria.Main.DrawInventory += HideVanillaReforge;
            }
        }

        public override void Unload()
        {
            On.Terraria.Main.DrawInventory -= HideVanillaReforge;
        }

        private static void HideVanillaReforge(On.Terraria.Main.orig_DrawInventory orig, Main self)
        {
            bool wasReforge = Main.InReforgeMenu;
            if (wasReforge) Main.InReforgeMenu = false;
            orig(self);
            if (wasReforge) Main.InReforgeMenu = true;
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
