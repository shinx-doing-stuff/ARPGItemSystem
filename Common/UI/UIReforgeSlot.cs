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

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
                // ItemSlot.Handle processes clicks (checks mouseLeft && mouseLeftRelease
                // internally) and swaps SlotItem with Main.mouseItem when clicked.
                ItemSlot.Handle(ref SlotItem, ItemSlot.Context.InventoryItem);
            }

            ItemSlot.Draw(spriteBatch, ref SlotItem, ItemSlot.Context.InventoryItem, pos);
        }
    }
}
