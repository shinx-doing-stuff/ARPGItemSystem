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

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var dim = GetDimensions();
            var pos = dim.Position();

            // Handle interaction here (single execution point).
            // UIElement.LeftClick fires in the update phase with no override = no-op.
            // ItemSlot.Draw fires in the draw phase — we suppress its mouse checks below.
            // Doing the swap here, between the two, means it runs exactly once per click.
            if (dim.ToRectangle().Contains(Main.mouseX, Main.mouseY))
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeftRelease && (Main.mouseItem.IsAir || Main.mouseItem.maxStack == 1))
                {
                    Item held = Main.reforgeItem;
                    Main.reforgeItem = Main.mouseItem;
                    Main.mouseItem = held;
                    SoundEngine.PlaySound(SoundID.Grab);
                    // Consume the click so nothing else processes it this frame
                    Main.mouseLeft = false;
                    Main.mouseLeftRelease = false;
                }
                else if (Main.mouseRightRelease && !Main.reforgeItem.IsAir && Main.mouseItem.IsAir)
                {
                    Main.mouseItem = Main.reforgeItem;
                    Main.reforgeItem = new Item();
                    SoundEngine.PlaySound(SoundID.Grab);
                    Main.mouseRight = false;
                    Main.mouseRightRelease = false;
                }
            }

            // Save mouse state AFTER the interaction block above (captures any consumed state),
            // suppress so ItemSlot.Draw is draw-only, then restore so later UI elements
            // (hammer buttons) still receive their own click events.
            bool ml = Main.mouseLeft, mlr = Main.mouseLeftRelease;
            bool mr = Main.mouseRight, mrr = Main.mouseRightRelease;
            Main.mouseLeft = false; Main.mouseLeftRelease = false;
            Main.mouseRight = false; Main.mouseRightRelease = false;

            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);

            Main.mouseLeft = ml; Main.mouseLeftRelease = mlr;
            Main.mouseRight = mr; Main.mouseRightRelease = mrr;
        }
    }
}
