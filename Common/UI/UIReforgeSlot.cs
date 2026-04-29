using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class UIReforgeSlot : UIElement
    {
        public Item SlotItem = new Item();

        public UIReforgeSlot()
        {
            Width.Set(52, 0f);
            Height.Set(52, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;

                // mouseLeft = currently pressed
                // mouseLeftRelease = was NOT pressed last update
                // Both true = first frame of press = a click
                bool isClick = Main.mouseLeft && Main.mouseLeftRelease;
                bool canPlace = Main.mouseItem.IsAir || Main.mouseItem.maxStack == 1;

                if (isClick && canPlace)
                {
                    Item temp = Main.mouseItem;
                    Main.mouseItem = SlotItem;
                    SlotItem = temp;
                    SoundEngine.PlaySound(SoundID.Grab);
                    // Do NOT touch mouseLeft here — setting it false makes the next
                    // frame compute mouseLeftRelease=true again, causing repeated swaps.
                    // mouseLeftRelease naturally becomes false on frame 2+ of holding.
                }
            }

            // Draw-only: suppress mouse so ItemSlot.Draw doesn't also try to interact
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;
            ItemSlot.Draw(spriteBatch, ref SlotItem, ItemSlot.Context.ChestItem, pos);
            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
