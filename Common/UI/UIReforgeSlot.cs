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
            // Reject stackable items — only weapons, armor, accessories can be reforged
            if (!Main.mouseItem.IsAir && Main.mouseItem.maxStack > 1)
                return;

            Item temp = Main.reforgeItem;
            Main.reforgeItem = Main.mouseItem;
            Main.mouseItem = temp;

            SoundEngine.PlaySound(SoundID.Grab);
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (Main.reforgeItem.IsAir) return;

            if (Main.mouseItem.IsAir)
            {
                Main.mouseItem = Main.reforgeItem;
                Main.reforgeItem = new Item();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            // Suppress mouse state so ItemSlot.Draw is draw-only.
            // Without this, ItemSlot.Draw also processes clicks and double-swaps with LeftClick above.
            bool savedLeft = Main.mouseLeft;
            bool savedLeftRelease = Main.mouseLeftRelease;
            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;

            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);

            Main.mouseLeft = savedLeft;
            Main.mouseLeftRelease = savedLeftRelease;
        }
    }
}
