using System;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    // One row in the reforge panel for an existing affix on the slotted item.
    // Shows a lock toggle on the left and the affix text on the right.
    // Locks are session-only — reset whenever the row is rebuilt.
    public class AffixLine : UIElement
    {
        private readonly LockToggleButton _lockButton;
        private readonly UIText _affixText;
        private readonly UIText _maxText;
        private bool _isPending;
        private readonly int _affixIndex;
        private readonly bool _isPrefix;

        public bool Locked => _lockButton.Locked;
        public int AffixIndex => _affixIndex;
        public bool IsPrefix => _isPrefix;

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (!_lockButton.Locked) return;
            var dim = GetDimensions();
            sb.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,
                new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height),
                new Color(0, 0, 0, 130));
        }

        public AffixLine(string displayText, string maxText, int affixIndex, bool isPrefix)
        {
            _affixIndex = affixIndex;
            _isPrefix = isPrefix;
            Height.Set(28, 0f);

            _lockButton = new LockToggleButton();
            _lockButton.Width.Set(22, 0f);
            _lockButton.Height.Set(22, 0f);
            _lockButton.Left.Set(0, 0f);
            _lockButton.VAlign = 0.5f;
            Append(_lockButton);

            _affixText = new UIText(displayText, 0.85f);
            _affixText.TextColor = isPrefix ? Color.LightGreen : Color.DeepSkyBlue;
            _affixText.Left.Set(28, 0f);
            _affixText.VAlign = 0.5f;
            Append(_affixText);

            _maxText = new UIText(maxText, 0.8f);
            _maxText.TextColor = new Color(140, 140, 140);
            _maxText.HAlign = 1f;
            _maxText.VAlign = 0.5f;
            Append(_maxText);
        }

        public void SetPending(bool pending)
        {
            _isPending = pending;
            _lockButton.SetEnabled(!pending);
        }

        public void Refresh()
        {
            var item = Main.reforgeItem;
            if (item.IsAir) return;

            AffixItemManager mgr = item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();

            if (mgr == null || _affixIndex < 0 || _affixIndex >= mgr.Affixes.Count) return;

            var a = mgr.Affixes[_affixIndex];
            string displayText = Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{a.Id}", a.Magnitude);
            _affixText.SetText(displayText);

            int bestMax = AffixRegistry.Get(a.Id).Tiers[mgr.Category][utils.GetBestTier()].Max;
            string maxText = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.BestFormat", bestMax);
            _maxText.SetText(maxText);
        }

        // Clickable lock icon using vanilla Terraria lock textures.
        // Lock_0 = locked (padlock closed), Lock_1 = unlocked (padlock open).
        private sealed class LockToggleButton : UIElement
        {
            public bool Locked { get; private set; }
            private bool _enabled = true;

            public LockToggleButton()
            {
                OnLeftClick += (_, _) =>
                {
                    if (!_enabled) return;
                    Locked = !Locked;
                    SoundEngine.PlaySound(SoundID.Unlock);
                };
            }

            public void SetEnabled(bool enabled) => _enabled = enabled;

            protected override void DrawSelf(SpriteBatch sb)
            {
                var dim = GetDimensions();

                Texture2D tex = Main.Assets.Request<Texture2D>(
                    Locked ? "Images/Lock_0" : "Images/Lock_1").Value;

                // The texture is a horizontal sprite sheet; each frame is tex.Height wide.
                var frame = new Rectangle(0, 0, tex.Height, tex.Height);
                float scale = Math.Min(dim.Width / frame.Width, dim.Height / frame.Height);
                Color color = _enabled ? Color.White : Color.White * 0.5f;
                var origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
                var pos = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);

                sb.Draw(tex, pos, frame, color, 0f, origin, scale, SpriteEffects.None, 0f);

                if (IsMouseHovering && _enabled)
                {
                    string key = Locked
                        ? "Mods.ARPGItemSystem.UI.ReforgePanel.UnlockTooltip"
                        : "Mods.ARPGItemSystem.UI.ReforgePanel.LockTooltip";
                    Main.instance.MouseText(Language.GetTextValue(key));
                }
            }
        }
    }
}
