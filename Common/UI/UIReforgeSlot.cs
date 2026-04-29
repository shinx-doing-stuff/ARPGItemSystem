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

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // Let ItemSlot.Draw handle drawing AND interaction natively.
            // No custom click handling — vanilla's ItemSlot.Draw is battle-tested
            // for all the cursor state management we've been fighting with.
            var pos = GetDimensions().Position();
            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.InventoryItem, pos);
        }
    }
}
