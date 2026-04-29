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
            var pos = GetDimensions().Position();
            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);
        }
    }
}
