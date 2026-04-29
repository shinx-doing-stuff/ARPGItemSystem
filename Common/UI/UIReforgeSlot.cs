using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class UIReforgeSlot : UIElement
    {
        public UIReforgeSlot()
        {
            Width.Set(52, 0f);
            Height.Set(52, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // Interaction is handled in UISystem.UpdateUI (update phase) before
            // DrawInventory can steal the click. This method is purely visual.
            var pos = GetDimensions().Position();
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;
            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.InventoryItem, pos);
            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
