using System;
using ARPGItemSystem.Common.Affixes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // One row representing a missing prefix or suffix slot the player may pay to fill.
    // Shows a "+" button on the left and a muted placeholder text on the right.
    public class EmptySlotRow : UIElement
    {
        private readonly PlusButton _plusButton;
        private readonly UIText _placeholderText;
        private readonly AffixKind _kind;
        private bool _enabled = true;

        public AffixKind Kind => _kind;

        public EmptySlotRow(AffixKind kind, int cost, Action onPlusClicked)
        {
            _kind = kind;
            Height.Set(28, 0f);

            _plusButton = new PlusButton(() =>
            {
                if (!_enabled) return;
                onPlusClicked?.Invoke();
            });
            _plusButton.Width.Set(22, 0f);
            _plusButton.Height.Set(22, 0f);
            _plusButton.Left.Set(0, 0f);
            _plusButton.VAlign = 0.5f;
            Append(_plusButton);

            string placeholderKey = kind == AffixKind.Prefix
                ? "Mods.ARPGItemSystem.UI.ReforgePanel.EmptyPrefixSlot"
                : "Mods.ARPGItemSystem.UI.ReforgePanel.EmptySuffixSlot";

            _placeholderText = new UIText(Language.GetTextValue(placeholderKey), 0.85f);
            _placeholderText.TextColor = new Color(140, 140, 140);
            _placeholderText.Left.Set(28, 0f);
            _placeholderText.VAlign = 0.5f;
            Append(_placeholderText);

            var costDisplay = new UICostDisplay(cost);
            costDisplay.HAlign = 1f;
            costDisplay.VAlign = 0.5f;
            Append(costDisplay);
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _plusButton.SetEnabled(enabled);
        }

        // A small clickable square with a "+" glyph. Click invokes the supplied action.
        private sealed class PlusButton : UIElement
        {
            private readonly Action _onClick;
            private bool _enabled = true;

            public PlusButton(Action onClick)
            {
                _onClick = onClick;
                OnLeftClick += (_, _) =>
                {
                    if (!_enabled) return;
                    _onClick?.Invoke();
                };
            }

            public void SetEnabled(bool enabled) => _enabled = enabled;

            protected override void DrawSelf(SpriteBatch sb)
            {
                var dim = GetDimensions();
                var rect = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);

                Color fill = new Color(40, 90, 50);
                Color border = new Color(120, 200, 130);
                if (!_enabled) { fill *= 0.5f; border *= 0.5f; }

                DrawRect(sb, rect, fill);
                DrawBorder(sb, rect, border);

                Color textColor = _enabled ? Color.White : Color.Gray;
                Utils.DrawBorderString(sb,
                    "+",
                    new Vector2(dim.X + dim.Width / 2f - 4f, dim.Y + dim.Height / 2f - 8f),
                    textColor,
                    0.95f);

                if (IsMouseHovering && _enabled)
                {
                    Main.instance.MouseText(
                        Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.AddAffixTooltip"));
                }
            }

            private static void DrawRect(SpriteBatch sb, Rectangle rect, Color color)
            {
                sb.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, rect, color);
            }

            private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color color)
            {
                var tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
                sb.Draw(tex, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
                sb.Draw(tex, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
            }
        }
    }
}
