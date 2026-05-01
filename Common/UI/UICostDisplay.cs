using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // Draws a coin cost as platinum/gold/silver/copper item icons followed by their counts.
    // The text tint goes red when the local player can't afford the cost.
    // LeftAligned=false (default): renders right-to-left from the element's right edge (suits row end-caps).
    // LeftAligned=true: renders left-to-right from the element's left edge (suits inline labels next to a button).
    public sealed class UICostDisplay : UIElement
    {
        public int Cost;
        public bool LeftAligned;

        public UICostDisplay(int cost)
        {
            Cost = cost;
            Width.Set(130, 0f);
            Height.Set(20, 0f);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (Cost <= 0) return;

            int platinum = Cost / 1000000;
            int gold     = (Cost / 10000) % 100;
            int silver   = (Cost / 100) % 100;
            int copper   = Cost % 100;

            var dim = GetDimensions();
            float y = dim.Y + dim.Height / 2f - 8f;
            var textTint = Main.LocalPlayer.CanAfford(Cost) ? Color.White : Color.Red;

            if (LeftAligned)
            {
                // Left-to-right: most significant coin first, starting from the left edge.
                float x = dim.X;
                if (platinum > 0) x = DrawCoinLTR(sb, x, y, platinum, ItemID.PlatinumCoin, textTint);
                if (gold > 0)     x = DrawCoinLTR(sb, x, y, gold,     ItemID.GoldCoin,     textTint);
                if (silver > 0)   x = DrawCoinLTR(sb, x, y, silver,   ItemID.SilverCoin,   textTint);
                if (copper > 0 || (platinum == 0 && gold == 0 && silver == 0))
                    DrawCoinLTR(sb, x, y, copper, ItemID.CopperCoin, textTint);
            }
            else
            {
                // Right-to-left: least significant coin on the far right.
                float x = dim.X + dim.Width;
                if (copper > 0 || (platinum == 0 && gold == 0 && silver == 0))
                    x = DrawCoinRTL(sb, x, y, copper, ItemID.CopperCoin, textTint);
                if (silver > 0)   x = DrawCoinRTL(sb, x, y, silver,   ItemID.SilverCoin,   textTint);
                if (gold > 0)     x = DrawCoinRTL(sb, x, y, gold,     ItemID.GoldCoin,     textTint);
                if (platinum > 0) x = DrawCoinRTL(sb, x, y, platinum, ItemID.PlatinumCoin, textTint);
            }
        }

        // Returns the new x position after the coin (advances rightward).
        private static float DrawCoinLTR(SpriteBatch sb, float leftX, float y, int amount, int coinItemId, Color textTint)
        {
            Main.instance.LoadItem(coinItemId);
            Texture2D tex = TextureAssets.Item[coinItemId].Value;

            const float IconSize = 16f;
            float scale = IconSize / Math.Max(tex.Width, tex.Height);
            sb.Draw(tex, new Vector2(leftX, y + (IconSize - tex.Height * scale) / 2f),
                null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            string text = amount.ToString();
            Utils.DrawBorderString(sb, text, new Vector2(leftX + IconSize + 2f, y), textTint, 0.75f);

            float textWidth = FontAssets.MouseText.Value.MeasureString(text).X * 0.75f;
            return leftX + IconSize + 2f + textWidth + 4f;
        }

        // Returns the new x position after the coin (advances leftward).
        private static float DrawCoinRTL(SpriteBatch sb, float rightX, float y, int amount, int coinItemId, Color textTint)
        {
            Main.instance.LoadItem(coinItemId);
            Texture2D tex = TextureAssets.Item[coinItemId].Value;

            string text = amount.ToString();
            float textWidth = FontAssets.MouseText.Value.MeasureString(text).X * 0.75f;
            const float IconSize = 16f;
            float totalWidth = IconSize + 2f + textWidth + 4f;
            float startX = rightX - totalWidth;

            float scale = IconSize / Math.Max(tex.Width, tex.Height);
            sb.Draw(tex, new Vector2(startX, y + (IconSize - tex.Height * scale) / 2f),
                null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            Utils.DrawBorderString(sb, text,
                new Vector2(startX + IconSize + 2f, y), textTint, 0.75f);

            return startX - 2f;
        }
    }
}
