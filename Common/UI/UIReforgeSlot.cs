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

        public override void LeftClick(UIMouseEvent evt)
        {
            if (!Main.mouseItem.IsAir && Main.mouseItem.maxStack > 1) return;

            // Use vanilla's own exchange logic — same code DrawInventory uses for slots.
            // Wrap Main.reforgeItem in a one-element array, let ItemSlot.LeftClick do the swap,
            // then write back. This handles all hidden state (netID, cursor icon, etc.) correctly.
            Item[] arr = { Main.reforgeItem };
            ItemSlot.LeftClick(arr, ItemSlot.Context.BankItem, 0);
            Main.reforgeItem = arr[0];

            // Consume the click so DrawInventory doesn't also process an inventory slot
            // at the same screen position in the draw phase.
            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (Main.reforgeItem.IsAir || !Main.mouseItem.IsAir) return;

            Item[] arr = { Main.reforgeItem };
            ItemSlot.RightClick(arr, ItemSlot.Context.BankItem, 0);
            Main.reforgeItem = arr[0];

            Main.mouseRight = false;
            Main.mouseRightRelease = false;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            // Push mouse coords out of range so ItemSlot.Draw is purely visual.
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;

            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);

            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
