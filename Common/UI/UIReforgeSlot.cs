using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class UIReforgeSlot : UIElement
    {
        // Own item field — vanilla never touches this directly.
        public Item SlotItem = new Item();

        // Persistent single-element array so the array overload of ItemSlot.Draw
        // (which handles clicks) can be used without allocating each frame.
        private readonly Item[] _arr = new Item[1] { new Item() };

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
            // Use the array overload — this is what vanilla uses for all inventory slots
            // and is the overload that definitively handles click interaction.
            _arr[0] = SlotItem;
            var pos = GetDimensions().Position();
            ItemSlot.Draw(spriteBatch, _arr, ItemSlot.Context.InventoryItem, 0, pos);
            SlotItem = _arr[0];
        }
    }
}
