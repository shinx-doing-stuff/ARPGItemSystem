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
            Main.NewText($"[ReforgeSlot] LeftClick: mouse={Main.mouseItem.Name} reforge={Main.reforgeItem.Name}", Microsoft.Xna.Framework.Color.Yellow);

            if (!Main.mouseItem.IsAir && Main.mouseItem.maxStack > 1) return;

            Item held = Main.reforgeItem;
            Main.reforgeItem = Main.mouseItem;
            Main.mouseItem = held;
            SoundEngine.PlaySound(SoundID.Grab);

            // Consume the click so DrawInventory (draw phase) doesn't also process
            // the inventory slot that happens to be at the same screen position.
            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;
        }

        public override void RightClick(UIMouseEvent evt)
        {
            if (Main.reforgeItem.IsAir || !Main.mouseItem.IsAir) return;

            Main.mouseItem = Main.reforgeItem;
            Main.reforgeItem = new Item();
            SoundEngine.PlaySound(SoundID.Grab);
            Main.mouseRight = false;
            Main.mouseRightRelease = false;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var pos = GetDimensions().Position();

            // Push mouse coords out of range so ItemSlot.Draw is purely visual:
            // it won't detect hover (no sounds) and won't process clicks (no double-swap).
            // Interaction is handled by LeftClick/RightClick above.
            int mx = Main.mouseX, my = Main.mouseY;
            Main.mouseX = -9999;
            Main.mouseY = -9999;

            ItemSlot.Draw(spriteBatch, ref Main.reforgeItem, ItemSlot.Context.BankItem, pos);

            Main.mouseX = mx;
            Main.mouseY = my;
        }
    }
}
