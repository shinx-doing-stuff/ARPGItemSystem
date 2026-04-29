using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
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

            if (!Main.mouseItem.IsAir)
            {
                // Place cursor item into slot — clone to avoid any reference aliasing
                Main.reforgeItem = Main.mouseItem.Clone();
                Main.mouseItem = new Item();
            }
            else if (!Main.reforgeItem.IsAir)
            {
                // Pick slot item up to cursor
                Main.mouseItem = Main.reforgeItem.Clone();
                Main.reforgeItem = new Item();
            }

            SoundEngine.PlaySound(SoundID.Grab);
            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (Main.reforgeItem.IsAir || !Main.mouseItem.IsAir) return;

            Main.mouseItem = Main.reforgeItem.Clone();
            Main.reforgeItem = new Item();
            SoundEngine.PlaySound(SoundID.Grab);
            Main.mouseRight = false;
            Main.mouseRightRelease = false;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            // Push mouse coords out of range so ItemSlot.Draw is purely visual:
            // it won't detect hover (no sounds) and won't process clicks (no double-swap).
            // Interaction is handled by LeftClick/RightClick above.
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;

            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);

            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
