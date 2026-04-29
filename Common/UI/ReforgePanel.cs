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

            // Sync our slot's item to Main.reforgeItem so AffixLine / packet
            // handler code that reads Main.reforgeItem still works correctly.
            Main.reforgeItem = _slot.SlotItem;

            bool hasItem = !_slot.SlotItem.IsAir;
            int currentType = hasItem ? _slot.SlotItem.type : -1;
            int currentNetID = hasItem ? _slot.SlotItem.netID : -1;

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
            _itemName.SetText(hasItem ? _slot.SlotItem.Name : "");
        }

        // Called by UISystem when the menu closes so we can return the held item.
        public void ReturnItemToPlayer()
        {
            if (_slot.SlotItem.IsAir) return;
            if (Main.mouseItem.IsAir)
                Main.mouseItem = _slot.SlotItem;
            else
            {
                for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++)
                {
                    if (Main.LocalPlayer.inventory[i].IsAir)
                    {
                        Main.LocalPlayer.inventory[i] = _slot.SlotItem;
                        break;
                    }
                }
            }
            _slot.SlotItem = new Item();
            Main.reforgeItem = new Item();
            ClearAffixLines();
        }

        public bool SlotIsEmpty => _slot.SlotItem.IsAir;

        public void ClearSlot()
        {
            if (!_slot.SlotItem.IsAir)
            {
                // Main.reforgeItem is synced to _slot.SlotItem each frame.
                // If vanilla already returned and cleared Main.reforgeItem (new Item()),
                // the references diverge — reforgeItem.IsAir=true, SlotItem still has the weapon.
                // In that case vanilla already handled the return; we just clear visually.
                // If reforgeItem still has the weapon, vanilla didn't return it — we do it now.
                if (!Main.reforgeItem.IsAir)
                {
                    if (Main.mouseItem.IsAir)
                        Main.mouseItem = _slot.SlotItem;
                    else
                    {
                        for (int i = 0; i < Main.LocalPlayer.inventory.Length; i++)
                        {
                            if (Main.LocalPlayer.inventory[i].IsAir)
                            {
                                Main.LocalPlayer.inventory[i] = _slot.SlotItem;
                                break;
                            }
                        }
                    }
                }
            }
            _slot.SlotItem = new Item();
            Main.reforgeItem = new Item();
            ClearAffixLines();
        }

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
            var item = _slot.SlotItem;
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
