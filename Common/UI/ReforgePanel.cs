using System.Collections.Generic;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using Microsoft.Xna.Framework;
using Accessory = ARPGItemSystem.Common.GlobalItems.Accessory;
using Armor = ARPGItemSystem.Common.GlobalItems.Armor;
using Weapon = ARPGItemSystem.Common.GlobalItems.Weapon;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class ReforgePanel : UIState
    {
        private UIPanel _panel;
        private UIReforgeSlot _slot;
        private UIText _itemName;
        private UIText _placeholder;
        private readonly List<AffixLine> _affixLines = new();
        private int _lastItemType = -1;
        private int _lastItemNetID = -1;

        public override void OnInitialize()
        {
            _panel = new UIPanel();
            _panel.Width.Set(320, 0f);
            _panel.Height.Set(420, 0f);
            _panel.HAlign = 0.5f;
            _panel.VAlign = 0.5f;
            Append(_panel);

            var titleText = new UIText(Language.GetText("Mods.ARPGItemSystem.UI.ReforgePanel.Title"));
            var title = new UIPanel();
            title.Width.Set(0, 1f);
            title.Height.Set(30, 0f);
            title.Top.Set(-12, 0f);
            titleText.HAlign = 0.5f;
            titleText.VAlign = 0.5f;
            title.Append(titleText);
            _panel.Append(title);

            _slot = new UIReforgeSlot();
            _slot.HAlign = 0.5f;
            _slot.Top.Set(24, 0f);
            _panel.Append(_slot);

            _itemName = new UIText("", 0.9f);
            _itemName.HAlign = 0.5f;
            _itemName.Top.Set(84, 0f);
            _panel.Append(_itemName);

            _placeholder = new UIText(Language.GetText("Mods.ARPGItemSystem.UI.ReforgePanel.Placeholder"), 0.85f)
            {
                TextColor = Color.Gray,
                HAlign = 0.5f
            };
            _placeholder.Top.Set(110, 0f);
            _panel.Append(_placeholder);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool hasItem = !Main.reforgeItem.IsAir;
            int currentType = hasItem ? Main.reforgeItem.type : -1;
            int currentNetID = hasItem ? Main.reforgeItem.netID : -1;

            if (hasItem && (currentType != _lastItemType || currentNetID != _lastItemNetID))
            {
                RefreshAffixLines();
                _lastItemType = currentType;
                _lastItemNetID = currentNetID;
            }
            else if (!hasItem && _affixLines.Count > 0)
            {
                ClearAffixLines();
            }

            _placeholder.TextColor = hasItem ? Color.Transparent : Color.Gray;
            _itemName.SetText(hasItem ? Main.reforgeItem.Name : "");
        }

        public Rectangle GetSlotBounds() =>
            _slot?.GetDimensions().ToRectangle() ?? Rectangle.Empty;

        public void RefreshAffix(int index)
        {
            if (index >= 0 && index < _affixLines.Count)
                _affixLines[index].Refresh();
            SetAllPending(false);
        }

        public void SetAllPending(bool pending)
        {
            foreach (var line in _affixLines)
                line.SetPending(pending);
        }

        private void RefreshAffixLines()
        {
            ClearAffixLines();
            var item = Main.reforgeItem;
            var lines = GetModifierLines(item);

            float yOffset = 110f;
            foreach (var (text, tier, index, isPrefix) in lines)
            {
                var line = new AffixLine(text, tier, index, isPrefix);
                line.Top.Set(yOffset, 0f);
                line.Width.Set(-20, 1f);
                line.Left.Set(10, 0f);
                _panel.Append(line);
                _affixLines.Add(line);
                yOffset += 32f;
            }
        }

        private void ClearAffixLines()
        {
            foreach (var line in _affixLines)
                _panel.RemoveChild(line);
            _affixLines.Clear();
            _lastItemType = -1;
            _lastItemNetID = -1;
        }

        private static List<(string text, int tier, int index, bool isPrefix)> GetModifierLines(Item item)
        {
            var result = new List<(string, int, int, bool)>();

            if (item.damage > 0 && item.maxStack == 1)
            {
                var list = item.GetGlobalItem<WeaponManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Weapon.ModifierType.Prefix));
                }
            }
            else if (item.accessory)
            {
                var list = item.GetGlobalItem<AccessoryManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Accessory.ModifierType.Prefix));
                }
            }
            else
            {
                var list = item.GetGlobalItem<ArmorManager>().modifierList;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    result.Add((string.Format(m.tooltip, m.magnitude), m.tier, i, m.modifierType == Armor.ModifierType.Prefix));
                }
            }

            return result;
        }
    }
}
