using System;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Network;
using ARPGItemSystem.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class AffixLine : UIElement
    {
        private UIImageButton _hammerButton;
        private UIText _affixText;
        private UICostDisplay _costDisplay;
        private bool _isPending;
        private readonly int _modifierIndex;
        private readonly bool _isPrefix;

        public AffixLine(string displayText, int tier, int modifierIndex, bool isPrefix)
        {
            _modifierIndex = modifierIndex;
            _isPrefix = isPrefix;
            Height.Set(28, 0f);

            _hammerButton = new UIImageButton(TextureAssets.Reforge[0]);
            _hammerButton.Width.Set(22, 0f);
            _hammerButton.Height.Set(22, 0f);
            _hammerButton.Left.Set(0, 0f);
            _hammerButton.VAlign = 0.5f;
            _hammerButton.OnLeftClick += OnHammerClicked;
            Append(_hammerButton);

            _affixText = new UIText(displayText, 0.85f);
            _affixText.TextColor = isPrefix ? Color.LightGreen : Color.DeepSkyBlue;
            _affixText.Left.Set(28, 0f);
            _affixText.VAlign = 0.5f;
            Append(_affixText);

            _costDisplay = new UICostDisplay(ReforgeConfig.CalculateCost(
                Main.reforgeItem.IsAir ? 0 : Main.reforgeItem.value, tier));
            _costDisplay.HAlign = 1f;
            _costDisplay.VAlign = 0.5f;
            Append(_costDisplay);
        }

        private void OnHammerClicked(UIMouseEvent evt, UIElement listeningElement)
        {
            if (_isPending || Main.reforgeItem.IsAir) return;

            SoundEngine.PlaySound(SoundID.Item37);

            var item = Main.reforgeItem;
            var cat = ReforgePacketHandler.GetItemCategory(item);
            var damCat = ReforgePacketHandler.GetDamageCategory(item);
            var excludeIds = ReforgePacketHandler.GetExcludeIds(item, _modifierIndex);
            var kind = _isPrefix ? AffixKind.Prefix : AffixKind.Suffix;

            ModContent.GetInstance<UISystem>().Panel.SetAllPending(true);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoRerollDirectly(item, _modifierIndex, kind, cat, damCat, excludeIds);
                ModContent.GetInstance<UISystem>().Panel.RefreshAffix(_modifierIndex);
            }
            else
            {
                ReforgePacketHandler.SendRerollRequest(_modifierIndex, kind, cat, damCat, item.value, excludeIds);
            }
        }

        public void SetPending(bool pending)
        {
            _isPending = pending;
            _hammerButton.SetVisibility(pending ? 0.4f : 1f, pending ? 0.4f : 1f);
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

            if (mgr == null || _modifierIndex < 0 || _modifierIndex >= mgr.Affixes.Count) return;

            var a = mgr.Affixes[_modifierIndex];
            var def = AffixRegistry.Get(a.Id);
            string displayText = string.Format(def.TooltipFormat, a.Magnitude);

            _affixText.SetText(displayText);
            _costDisplay.Cost = ReforgeConfig.CalculateCost(item.value, a.Tier);
        }

        // Draws the cost as coin icons (platinum/gold/silver/copper) instead of text abbreviations.
        private sealed class UICostDisplay : UIElement
        {
            public int Cost;

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
                float x = dim.X + dim.Width;
                float y = dim.Y + dim.Height / 2f - 8f;

                // Draw right-to-left so the least significant coin is on the far right
                if (copper > 0 || (platinum == 0 && gold == 0 && silver == 0))
                    x = DrawCoin(sb, x, y, copper, ItemID.CopperCoin);
                if (silver > 0)
                    x = DrawCoin(sb, x, y, silver, ItemID.SilverCoin);
                if (gold > 0)
                    x = DrawCoin(sb, x, y, gold, ItemID.GoldCoin);
                if (platinum > 0)
                    x = DrawCoin(sb, x, y, platinum, ItemID.PlatinumCoin);
            }

            private static float DrawCoin(SpriteBatch sb, float rightX, float y, int amount, int coinItemId)
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
                    new Vector2(startX + IconSize + 2f, y), Color.White, 0.75f);

                return startX - 2f;
            }
        }
    }
}
