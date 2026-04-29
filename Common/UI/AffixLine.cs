using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Network;
using ARPGItemSystem.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class AffixLine : UIElement
    {
        private UIImageButton _hammerButton;
        private UIText _affixText;
        private UIText _costText;
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

            _costText = new UIText(FormatCost(ReforgeConfig.CalculateCost(Main.reforgeItem.IsAir ? 0 : Main.reforgeItem.value, tier)), 0.85f);
            _costText.HAlign = 1f;
            _costText.VAlign = 0.5f;
            Append(_costText);
        }

        private void OnHammerClicked(UIMouseEvent evt, UIElement listeningElement)
        {
            if (_isPending || Main.reforgeItem.IsAir) return;

            var item = Main.reforgeItem;
            var cat = ReforgePacketHandler.GetItemCategory(item);
            var damCat = ReforgePacketHandler.GetDamageCategory(item);
            var excludeList = ReforgePacketHandler.GetExcludeList(item, _modifierIndex, _isPrefix);

            ModContent.GetInstance<UISystem>().Panel.SetAllPending(true);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoRerollDirectly(item, _modifierIndex, _isPrefix, cat, damCat, excludeList);
                ModContent.GetInstance<UISystem>().Panel.RefreshAffix(_modifierIndex);
            }
            else
            {
                ReforgePacketHandler.SendRerollRequest(_modifierIndex, _isPrefix, cat, damCat, item.value, excludeList);
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

            string displayText;
            int tier;

            if (item.damage > 0 && item.maxStack == 1)
            {
                var mod = item.GetGlobalItem<WeaponManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }
            else if (item.accessory)
            {
                var mod = item.GetGlobalItem<AccessoryManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }
            else
            {
                var mod = item.GetGlobalItem<ArmorManager>().modifierList[_modifierIndex];
                displayText = string.Format(mod.tooltip, mod.magnitude);
                tier = mod.tier;
            }

            _affixText.SetText(displayText);
            _costText.SetText(FormatCost(ReforgeConfig.CalculateCost(item.value, tier)));
        }

        private static string FormatCost(int cost)
        {
            int platinum = cost / 1000000;
            int gold = (cost / 10000) % 100;
            int silver = (cost / 100) % 100;
            int copper = cost % 100;

            string p = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Currency.Platinum");
            string g = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Currency.Gold");
            string s = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Currency.Silver");
            string c = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.Currency.Copper");

            string result = "";
            if (platinum > 0) result += $"{platinum}{p} ";
            if (gold > 0) result += $"{gold}{g} ";
            if (silver > 0) result += $"{silver}{s} ";
            if (copper > 0 || result == "") result += $"{copper}{c}";
            return result.Trim();
        }
    }
}
