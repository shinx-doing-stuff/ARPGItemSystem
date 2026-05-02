using System.Collections.Generic;
using ARPGItemSystem.Common.Affixes;
using ARPGItemSystem.Common.Config;
using ARPGItemSystem.Common.GlobalItems;
using ARPGItemSystem.Common.GlobalItems.Accessory;
using ARPGItemSystem.Common.GlobalItems.Armor;
using ARPGItemSystem.Common.GlobalItems.Weapon;
using ARPGItemSystem.Common.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace ARPGItemSystem.Common.UI
{
    public class ReforgePanel : UIState
    {
        private const float NearMaxThreshold = 0.85f;

        private static readonly SoundStyle BestReforgeSound =
            new("ARPGItemSystem/Assets/Sounds/Best_reforge");

        private UIPanel _panel;
        private UIReforgeSlot _slot;
        private UIText _itemName;
        private UIText _placeholder;

        private readonly List<AffixLine> _affixLines = new();
        private readonly List<EmptySlotRow> _emptyRows = new();

        private UIImageButton _reforgeButton;
        private UICostDisplay _reforgeCost;
        private UIText _reforgeHint;

        private int _lastItemType = -1;
        private int _lastItemNetID = -1;
        private int _lastAffixCount = -1;
        private bool _reforgeButtonEnabled = true;

        public override void OnInitialize()
        {
            _panel = new UIPanel();
            _panel.Width.Set(420, 0f);
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

            // Bottom bar: hammer button + cost display, centered together.
            var bottomBar = new UIElement();
            bottomBar.Width.Set(170, 0f);   // 28 button + 8 gap + 130 cost + 4 slack
            bottomBar.Height.Set(30, 0f);
            bottomBar.HAlign = 0.5f;
            bottomBar.Top.Set(-44, 1f);
            _panel.Append(bottomBar);

            _reforgeButton = new UIImageButton(TextureAssets.Reforge[0]);
            _reforgeButton.Width.Set(28, 0f);
            _reforgeButton.Height.Set(28, 0f);
            _reforgeButton.Left.Set(0, 0f);
            _reforgeButton.VAlign = 0.5f;
            _reforgeButton.OnLeftClick += OnReforgeClicked;
            bottomBar.Append(_reforgeButton);

            _reforgeCost = new UICostDisplay(0) { LeftAligned = true };
            _reforgeCost.Left.Set(36, 0f);   // 28 button + 8 gap
            _reforgeCost.VAlign = 0.5f;
            bottomBar.Append(_reforgeCost);

            _reforgeHint = new UIText("", 0.8f);
            _reforgeHint.HAlign = 0.5f;
            _reforgeHint.Top.Set(-12, 1f);
            _reforgeHint.TextColor = new Color(180, 180, 180);
            _panel.Append(_reforgeHint);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Main.reforgeItem = _slot.SlotItem;

            bool hasItem = !_slot.SlotItem.IsAir;
            int currentType = hasItem ? _slot.SlotItem.type : -1;
            int currentNetID = hasItem ? _slot.SlotItem.netID : -1;
            int currentAffixCount = hasItem ? GetAffixCount(_slot.SlotItem) : -1;

            if (hasItem && (currentType != _lastItemType
                            || currentNetID != _lastItemNetID
                            || currentAffixCount != _lastAffixCount))
            {
                RefreshRows();
                _lastItemType = currentType;
                _lastItemNetID = currentNetID;
                _lastAffixCount = currentAffixCount;
            }
            else if (!hasItem && (_affixLines.Count > 0 || _emptyRows.Count > 0))
            {
                ClearRows();
            }

            _placeholder.TextColor = hasItem ? Color.Transparent : Color.Gray;
            _itemName.SetText(hasItem ? _slot.SlotItem.Name : "");

            UpdateReforgeButtonState();
        }

        public bool SlotIsEmpty => _slot.SlotItem.IsAir;

        public void ClearSlot()
        {
            if (!_slot.SlotItem.IsAir)
            {
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
            ClearRows();
        }

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
            ClearRows();
        }

        public void RefreshAffix(int index)
        {
            if (index >= 0 && index < _affixLines.Count)
            {
                _affixLines[index].Refresh();
                TryPlayNearMaxSound(index);
            }
            SetAllPending(false);
        }

        private void TryPlayNearMaxSound(int index)
        {
            var mgr = GetManager(_slot.SlotItem);
            if (mgr == null || index < 0 || index >= mgr.Affixes.Count) return;

            var a = mgr.Affixes[index];
            int bestMax = AffixRegistry.Get(a.Id).Tiers[mgr.Category][utils.GetBestTier()].Max;
            if (a.Magnitude >= bestMax * NearMaxThreshold)
                SoundEngine.PlaySound(BestReforgeSound);
        }

        public void SetAllPending(bool pending)
        {
            foreach (var line in _affixLines) line.SetPending(pending);
            foreach (var row in _emptyRows) row.SetEnabled(!pending);
            // Reforge button visibility is managed solely by UpdateReforgeButtonState
            // to avoid fighting with UIImageButton's native click animation.
        }

        public void RebuildRowsAfterFill()
        {
            SetAllPending(false);
        }

        private void RefreshRows()
        {
            ClearRows();
            var item = _slot.SlotItem;
            if (item.IsAir) return;

            var mgr = GetManager(item);
            if (mgr == null) return;

            float yOffset = 110f;

            int bestTier = utils.GetBestTier();
            for (int i = 0; i < mgr.Affixes.Count; i++)
            {
                var a = mgr.Affixes[i];
                var def = AffixRegistry.Get(a.Id);
                string text = Language.GetTextValue($"Mods.ARPGItemSystem.Affixes.{a.Id}", a.Magnitude);
                int bestMax = def.Tiers[mgr.Category][bestTier].Max;
                string maxText = Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.BestFormat", bestMax);
                bool isPrefix = def.Kind == AffixKind.Prefix;

                var line = new AffixLine(text, maxText, i, isPrefix);
                line.Top.Set(yOffset, 0f);
                line.Width.Set(-20, 1f);
                line.Left.Set(10, 0f);
                _panel.Append(line);
                _affixLines.Add(line);
                yOffset += 32f;
            }

            int existingPrefixes = CountByKind(mgr.Affixes, AffixKind.Prefix);
            int existingSuffixes = CountByKind(mgr.Affixes, AffixKind.Suffix);
            int maxPrefixes = GetMaxPrefixes(mgr.Category);
            int maxSuffixes = GetMaxSuffixes(mgr.Category);

            int emptyCost = ReforgePacketHandler.ComputeEmptySlotCost(_slot.SlotItem.value);

            for (int i = existingPrefixes; i < maxPrefixes; i++)
            {
                var row = new EmptySlotRow(AffixKind.Prefix, emptyCost, () => OnFillEmptyClicked(AffixKind.Prefix));
                row.Top.Set(yOffset, 0f);
                row.Width.Set(-20, 1f);
                row.Left.Set(10, 0f);
                _panel.Append(row);
                _emptyRows.Add(row);
                yOffset += 32f;
            }

            for (int i = existingSuffixes; i < maxSuffixes; i++)
            {
                var row = new EmptySlotRow(AffixKind.Suffix, emptyCost, () => OnFillEmptyClicked(AffixKind.Suffix));
                row.Top.Set(yOffset, 0f);
                row.Width.Set(-20, 1f);
                row.Left.Set(10, 0f);
                _panel.Append(row);
                _emptyRows.Add(row);
                yOffset += 32f;
            }
        }

        private void ClearRows()
        {
            foreach (var line in _affixLines) _panel.RemoveChild(line);
            foreach (var row in _emptyRows) _panel.RemoveChild(row);
            _affixLines.Clear();
            _emptyRows.Clear();
            _lastItemType = -1;
            _lastItemNetID = -1;
            _lastAffixCount = -1;
        }

        private void UpdateReforgeButtonState()
        {
            int unlockedCount = 0;
            int lockedCount = 0;
            foreach (var line in _affixLines)
            {
                if (line.Locked) lockedCount++;
                else unlockedCount++;
            }

            bool enabled = !(_slot.SlotItem.IsAir || _affixLines.Count == 0 || unlockedCount == 0);

            // Only call SetVisibility on transition — calling it every frame interferes
            // with UIImageButton's native click animation.
            if (enabled != _reforgeButtonEnabled)
            {
                _reforgeButtonEnabled = enabled;
                _reforgeButton.SetVisibility(enabled ? 1.0f : 0.4f, enabled ? 0.7f : 0.4f);
            }

            if (_slot.SlotItem.IsAir || _affixLines.Count == 0)
            {
                _reforgeCost.Cost = 0;
                _reforgeHint.SetText("");
                return;
            }

            if (unlockedCount == 0)
            {
                _reforgeCost.Cost = 0;
                _reforgeHint.SetText(Language.GetTextValue("Mods.ARPGItemSystem.UI.ReforgePanel.AllLockedHint"));
                return;
            }

            int itemValue = _slot.SlotItem.value;
            int numAffixes = GetManager(_slot.SlotItem)?.Affixes.Count ?? 0;
            _reforgeCost.Cost = (int)(ReforgeConfig.CalculateCost(itemValue, utils.GetBestTier())
                                      * numAffixes
                                      * ReforgeConfig.LockMultiplier(lockedCount));
            _reforgeHint.SetText("");
        }

        private void OnReforgeClicked(UIMouseEvent evt, UIElement listening)
        {
            if (_slot.SlotItem.IsAir || _affixLines.Count == 0) return;

            var mgr = GetManager(_slot.SlotItem);
            if (mgr == null) return;

            var unlocked = new List<(byte index, AffixKind kind)>();
            var locked = new List<(AffixKind kind, AffixId id)>();

            for (int i = 0; i < _affixLines.Count && i < mgr.Affixes.Count; i++)
            {
                var a = mgr.Affixes[i];
                var kind = AffixRegistry.Get(a.Id).Kind;
                if (_affixLines[i].Locked)
                    locked.Add((kind, a.Id));
                else
                    unlocked.Add(((byte)i, kind));
            }

            if (unlocked.Count == 0) return;

            if (!Main.LocalPlayer.CanAfford(_reforgeCost.Cost))
            {
                SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/Item_194"));
                return;
            }

            SoundEngine.PlaySound(SoundID.Item37);
            SetAllPending(true);

            var cat = ReforgePacketHandler.GetItemCategory(_slot.SlotItem);
            var damCat = ReforgePacketHandler.GetDamageCategory(_slot.SlotItem);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoRerollAllUnlockedDirectly(
                    _slot.SlotItem, cat, damCat, _slot.SlotItem.value, unlocked, locked);
            }
            else
            {
                ReforgePacketHandler.SendRerollAllUnlockedRequest(
                    cat, damCat, _slot.SlotItem.value, unlocked, locked);
            }
        }

        private void OnFillEmptyClicked(AffixKind kind)
        {
            if (_slot.SlotItem.IsAir) return;
            var mgr = GetManager(_slot.SlotItem);
            if (mgr == null) return;

            int cost = ReforgePacketHandler.ComputeEmptySlotCost(_slot.SlotItem.value);
            if (!Main.LocalPlayer.CanAfford(cost))
            {
                SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/Item_194"));
                return;
            }

            var excludeIds = new List<AffixId>();
            foreach (var a in mgr.Affixes)
            {
                if (AffixRegistry.Get(a.Id).Kind == kind)
                    excludeIds.Add(a.Id);
            }

            SoundEngine.PlaySound(SoundID.Item37);
            SetAllPending(true);

            var cat = ReforgePacketHandler.GetItemCategory(_slot.SlotItem);
            var damCat = ReforgePacketHandler.GetDamageCategory(_slot.SlotItem);

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ReforgePacketHandler.DoFillEmptySlotDirectly(
                    _slot.SlotItem, cat, damCat, kind, _slot.SlotItem.value, excludeIds);
            }
            else
            {
                ReforgePacketHandler.SendFillEmptySlotRequest(
                    cat, damCat, kind, _slot.SlotItem.value, excludeIds);
            }
        }

        private static AffixItemManager GetManager(Item item)
        {
            if (item.IsAir) return null;
            return item.damage > 0 && item.maxStack <= 1
                ? (AffixItemManager)item.GetGlobalItem<WeaponManager>()
                : item.accessory
                    ? (AffixItemManager)item.GetGlobalItem<AccessoryManager>()
                    : (AffixItemManager)item.GetGlobalItem<ArmorManager>();
        }

        private static int GetAffixCount(Item item)
        {
            var mgr = GetManager(item);
            return mgr?.Affixes.Count ?? 0;
        }

        private static int CountByKind(List<Affix> list, AffixKind kind)
        {
            int n = 0;
            foreach (var a in list)
                if (AffixRegistry.Get(a.Id).Kind == kind) n++;
            return n;
        }

        private static int GetMaxPrefixes(ItemCategory cat) => cat switch
        {
            ItemCategory.Weapon => utils.GetMaxPrefixesWeapon(),
            ItemCategory.Armor => utils.GetMaxPrefixesArmor(),
            ItemCategory.Accessory => utils.GetMaxPrefixesAccessory(),
            _ => 0
        };

        private static int GetMaxSuffixes(ItemCategory cat) => cat switch
        {
            ItemCategory.Weapon => utils.GetMaxSuffixesWeapon(),
            ItemCategory.Armor => utils.GetMaxSuffixesArmor(),
            ItemCategory.Accessory => utils.GetMaxSuffixesAccessory(),
            _ => 0
        };
    }
}
