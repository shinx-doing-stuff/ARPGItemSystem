using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            // evt.Target == this prevents firing when a child element was clicked
            if (evt.Target != this) return;
            if (!Main.mouseItem.IsAir && Main.mouseItem.maxStack > 1) return;

            // ItemSlot.Handle is the correct tModLoader API for slot interaction
            ItemSlot.Handle(ref SlotItem, ItemSlot.Context.InventoryItem);

            // Consume so DrawInventory doesn't also process an inventory slot here
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
