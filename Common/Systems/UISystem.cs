using System;
using System.Collections.Generic;
using System.Reflection;
using ARPGItemSystem.Common.UI;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
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
        private static Hook _drawInventoryHook;

        public override void Load()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                Panel = new ReforgePanel();
                Panel.Activate();
                _reforgeInterface = new UserInterface();
                _reforgeInterface.SetState(Panel);

                var method = typeof(Main).GetMethod("DrawInventory",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                    _drawInventoryHook = new Hook(method,
                        new Action<Action<Main>, Main>((orig, self) =>
                        {
                            bool wasReforge = Main.InReforgeMenu;
                            if (wasReforge) Main.InReforgeMenu = false;
                            orig(self);
                            if (wasReforge) Main.InReforgeMenu = true;
                        }));
            }
        }

        public override void Unload()
        {
            _drawInventoryHook?.Dispose();
            _drawInventoryHook = null;
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
