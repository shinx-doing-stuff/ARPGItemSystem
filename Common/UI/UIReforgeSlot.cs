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

            Item held = Main.reforgeItem;
            Main.reforgeItem = Main.mouseItem;
            Main.mouseItem = held;
            SoundEngine.PlaySound(SoundID.Grab);
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (Main.reforgeItem.IsAir || !Main.mouseItem.IsAir) return;

            Main.mouseItem = Main.reforgeItem;
            Main.reforgeItem = new Item();
            SoundEngine.PlaySound(SoundID.Grab);
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
