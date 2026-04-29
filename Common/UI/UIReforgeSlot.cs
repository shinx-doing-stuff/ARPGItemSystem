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
            if (ContainsPoint(Main.MouseScreen))
                Main.LocalPlayer.mouseInterface = true;
        }

        // LeftMouseDown fires when button goes DOWN on this element — same timing
        // as vanilla's item slot interaction in DrawInventory.
        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);
            if (!Main.mouseItem.IsAir && Main.mouseItem.maxStack > 1) return;

            Item temp = Main.mouseItem;
            Main.mouseItem = SlotItem;
            SlotItem = temp;
            SoundEngine.PlaySound(SoundID.Grab);

            // Consume so DrawInventory doesn't also process a slot at this position.
            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
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
