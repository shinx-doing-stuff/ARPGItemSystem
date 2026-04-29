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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool over = ContainsPoint(Main.MouseScreen);

            // Debug: show state every frame mouse is over slot
            if (over)
            {
                Main.NewText($"over slot | mouseLeft={Main.mouseLeft} mouseLeftRelease={Main.mouseLeftRelease} cursor={Main.mouseItem.Name}",
                    Microsoft.Xna.Framework.Color.Cyan);
                Main.LocalPlayer.mouseInterface = true;
            }

            if (over && Main.mouseLeft && Main.mouseLeftRelease
                && (Main.mouseItem.IsAir || Main.mouseItem.maxStack == 1))
            {
                Main.NewText("SWAP!", Microsoft.Xna.Framework.Color.Yellow);
                Item temp = Main.mouseItem;
                Main.mouseItem = SlotItem;
                SlotItem = temp;
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseLeft = false;
                Main.mouseLeftRelease = false;
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // Purely visual. Interaction is handled in Update above.
            // Suppress mouse coords so ItemSlot.Draw doesn't also play hover sounds.
            var pos = GetDimensions().Position();
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;
            ItemSlot.Draw(spriteBatch, ref SlotItem, ItemSlot.Context.InventoryItem, pos);
            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
