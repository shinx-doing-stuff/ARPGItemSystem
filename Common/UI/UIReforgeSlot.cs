using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class UIReforgeSlot : UIElement
    {
        // Own item field — vanilla never touches this, so ItemSlot.Draw
        // can handle clicks without any interference from the reforge system.
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

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();
            ItemSlot.Draw(spriteBatch, ref SlotItem, ItemSlot.Context.InventoryItem, pos);
        }
    }
}
