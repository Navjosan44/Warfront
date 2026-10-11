using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Inventory screen: the field pack.
    ///
    /// The grid mirrors InventoryModel slot for slot (including empty ones) so the player can see exactly
    /// how much room is left, and discarding goes through the model rather than the save document, which
    /// keeps capacity and stacking rules in one place.
    /// </summary>
    public class InventoryScreen : UIScreen
    {
        public override string Title { get { return "Field Pack"; } }

        private const int Columns = 4;
        private const float CellHeight = 112f;

        private RectTransform _grid;
        private Text _capacityText;
        private Text _detailName;
        private Text _detailBody;
        private Text _detailAction;
        private Button _discardButton;
        private RectTransform _ammoPanel;
        private RectTransform _weaponPanel;
        private int _selectedSlot = -1;

        protected override void OnBuild()
        {
            Image left = UIFactory.CreatePanel(Content, "PackPanel", Theme.Panel);
            UIFactory.SetAnchor(left.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 1f), new Vector2(0f, 0f),
                Vector2.zero, Vector2.zero);
            left.rectTransform.offsetMin = new Vector2(0f, 0f);
            left.rectTransform.offsetMax = new Vector2(-16f, 0f);

            _capacityText = UIFactory.CreateText(left.transform, "Capacity", string.Empty, 24, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_capacityText.rectTransform, 22f, 12f, 700f, 30f);

            ScrollRect scroll = UIFactory.CreateScrollView(left.transform, "Grid", out _grid);
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20f, 20f);
            scrollRect.offsetMax = new Vector2(-20f, -50f);

            Image right = UIFactory.CreatePanel(Content, "DetailPanel", Theme.PanelLight);
            UIFactory.SetAnchor(right.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                Vector2.zero, Vector2.zero);
            right.rectTransform.offsetMin = new Vector2(16f, 0f);
            right.rectTransform.offsetMax = new Vector2(0f, 0f);

            Text detailHeader = UIFactory.CreateText(right.transform, "Header", "SELECTED ITEM", 22, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(detailHeader.rectTransform, 22f, 14f, 400f, 26f);

            _detailName = UIFactory.CreateText(right.transform, "Name", "NOTHING SELECTED", 30, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_detailName.rectTransform, 22f, 42f, 660f, 38f);

            _detailBody = UIFactory.CreateText(right.transform, "Body", "Tap a slot to inspect it.", 22, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_detailBody.rectTransform, 22f, 84f, 660f, 200f);

            _discardButton = UIFactory.CreateButton(right.transform, "Discard", "DISCARD ITEM", new Color(0.34f, 0.16f, 0.15f, 0.97f), OnDiscard, 24);
            UIFactory.SetAnchor(_discardButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 60f), new Vector2(0f, -292f));
            _discardButton.GetComponent<RectTransform>().offsetMin = new Vector2(22f, -352f);
            _discardButton.GetComponent<RectTransform>().offsetMax = new Vector2(-22f, -292f);
            _discardButton.interactable = false;

            _detailAction = UIFactory.CreateText(right.transform, "Action", string.Empty, 20, Theme.Warning, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_detailAction.rectTransform, 22f, 356f, 660f, 60f);

            Text ammoHeader = UIFactory.CreateText(right.transform, "AmmoHeader", "RESERVE AMMUNITION", 22, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(ammoHeader.rectTransform, 22f, 424f, 400f, 26f);
            _ammoPanel = UIFactory.TopLeft(UIFactory.CreateRect(right.transform, "Ammo"), 22f, 454f, 660f, 120f);

            Text weaponHeader = UIFactory.CreateText(right.transform, "WeaponHeader", "ARSENAL", 22, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(weaponHeader.rectTransform, 22f, 588f, 400f, 26f);
            _weaponPanel = UIFactory.TopLeft(UIFactory.CreateRect(right.transform, "Weapons"), 22f, 618f, 660f, 200f);
            VerticalLayoutGroup weaponLayout = _weaponPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            weaponLayout.spacing = 4f;
            weaponLayout.childForceExpandHeight = false;
            weaponLayout.childForceExpandWidth = true;
            weaponLayout.childControlHeight = true;
            weaponLayout.childControlWidth = true;
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Inventory == null) return;

            RebuildGrid();
            RefreshAmmo();
            RefreshArsenal();
            RefreshDetail();

            SetHeaderSubtitle("Items carried out of a mission stay in the pack. Discard anything you do not need to free a slot.");
        }

        // -------------------------------------------------------------------- grid

        private void RebuildGrid()
        {
            ClearChildren(_grid);

            InventoryModel inventory = Context.Inventory;
            List<ItemStack> slots = inventory.SnapshotSlots();

            _capacityText.text = "SLOTS USED   " + inventory.UsedSlots + " / " + inventory.Capacity;

            RectTransform row = null;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i % Columns == 0)
                {
                    row = UIFactory.CreateRow(_grid, "SlotRow" + (i / Columns), CellHeight, 10f);
                }
                CreateCell(row, i, slots[i]);
            }

            if (inventory.UsedSlots == 0)
            {
                CreateEmptyLabel(_grid, "The pack is empty.\nSearch loot containers in a mission to fill it.");
            }
        }

        private void CreateCell(RectTransform row, int index, ItemStack stack)
        {
            bool empty = !stack.IsValid;
            Color background = _selectedSlot == index ? new Color(0.22f, 0.30f, 0.18f, 0.98f) : new Color(0.11f, 0.12f, 0.12f, 0.92f);

            Button cell = UIFactory.CreateButton(row, "Slot" + index, string.Empty, background, null, 22);
            int captured = index;
            cell.onClick.AddListener(() => SelectSlot(captured));

            if (empty)
            {
                Text emptyLabel = UIFactory.CreateText(cell.transform, "EmptyLabel", "-", 30, new Color(0.35f, 0.36f, 0.36f, 1f), TextAnchor.MiddleCenter);
                UIFactory.Stretch(emptyLabel.rectTransform, 6f, 6f, 6f, 6f);
                return;
            }

            ItemDef item = Context.Content.GetItem(stack.ItemId);
            Color tint = item != null ? RarityRules.Color(stack.Rarity) : Color.white;

            Text glyph = UIFactory.CreateText(cell.transform, "Glyph", item != null ? item.IconGlyph : "?", 46, tint, TextAnchor.MiddleCenter);
            UIFactory.SetAnchor(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 52f), new Vector2(0f, 10f));

            RectTransform nameBand = UIFactory.CreateBandBottom(cell.transform, "NameBand", 4f, 26f, 6f);
            Text name = UIFactory.CreateText(nameBand, "Name", item != null ? ShortName(item.DisplayName) : stack.ItemId, 18,
                Theme.Text, TextAnchor.LowerCenter);
            UIFactory.Stretch(name.rectTransform, 0f, 0f, 0f, 0f);

            Text quantity = UIFactory.CreateText(cell.transform, "Quantity", "x" + stack.Quantity, 22, Theme.Text, TextAnchor.UpperRight);
            UIFactory.SetAnchor(quantity.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, 26f), new Vector2(-6f, -4f));
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Length <= 16 ? name : name.Substring(0, 15) + ".";
        }

        private void SelectSlot(int index)
        {
            _selectedSlot = _selectedSlot == index ? -1 : index;
            if (Context.Audio != null) Context.Audio.PlayUi(AudioCue.Click);
            Refresh();
        }

        // ------------------------------------------------------------------ detail

        private void RefreshDetail()
        {
            ItemStack stack = _selectedSlot >= 0 ? Context.Inventory.GetSlot(_selectedSlot) : default(ItemStack);
            if (!stack.IsValid)
            {
                _detailName.text = "NOTHING SELECTED";
                _detailName.color = Theme.TextDim;
                _detailBody.text = "Tap a slot to inspect it.";
                _detailAction.text = string.Empty;
                _discardButton.interactable = false;
                return;
            }

            ItemDef item = Context.Content.GetItem(stack.ItemId);
            if (item == null)
            {
                _detailName.text = "UNKNOWN ITEM";
                _detailName.color = Theme.Warning;
                _detailBody.text = "This item id is not present in the content database and can be discarded safely.";
                _detailAction.text = string.Empty;
                _discardButton.interactable = true;
                return;
            }

            _detailName.text = item.DisplayName.ToUpperInvariant();
            _detailName.color = RarityRules.Color(stack.Rarity);

            string body =
                "CATEGORY    " + item.Category + "\n" +
                "QUANTITY    " + stack.Quantity + (item.IsStackable ? " / " + item.MaxStack + " per slot" : " (not stackable)") + "\n" +
                "RARITY      " + RarityRules.Name(stack.Rarity) + "\n" +
                "UNIT VALUE  " + item.Value + " CR    STACK VALUE " + (item.Value * stack.Quantity) + " CR\n";

            if (item.Category == ItemCategory.Weapon && !string.IsNullOrEmpty(item.WeaponId))
            {
                WeaponDef def = Context.Content.GetWeapon(item.WeaponId);
                if (def != null)
                {
                    WeaponStats stats = WeaponStatRules.Build(def, stack.Rarity, Mathf.Max(1, stack.UpgradeLevel));
                    body += "WEAPON      " + def.DisplayName + "  MK" + Mathf.Max(1, stack.UpgradeLevel) +
                            "    DMG " + stats.Damage.ToString("0.#") + "    MAG " + stats.MagazineSize + "\n";
                }
            }

            if (item.HealAmount > 0f) body += "RESTORES    " + item.HealAmount.ToString("0") + " health when used\n";
            if (item.ArmorAmount > 0f) body += "GRANTS      " + item.ArmorAmount.ToString("0") + " armour when used\n";

            body += "\n" + item.Description;
            _detailBody.text = body;

            bool questItem = item.Category == ItemCategory.Objective;
            _detailAction.text = questItem
                ? "Mission item - it stays in the pack until you extract. It is what the contract is paying for, so do not discard it."
                : "Discarding frees the slot and is irreversible. Loot left behind never reaches the menu screens.";
            _discardButton.interactable = !questItem;
        }

        private void OnDiscard()
        {
            if (Context == null || _selectedSlot < 0) return;

            ItemStack stack = Context.Inventory.GetSlot(_selectedSlot);
            if (!stack.IsValid) return;

            ItemDef item = Context.Content.GetItem(stack.ItemId);
            if (item != null && item.Category == ItemCategory.Objective)
            {
                Context.Audio.PlayUi(AudioCue.Deny);
                GameEvents.RaiseNotice("Mission items cannot be discarded");
                return;
            }

            if (!Context.Inventory.DiscardSlot(_selectedSlot))
            {
                Context.Audio.PlayUi(AudioCue.Deny);
                return;
            }

            Context.Audio.PlayUi(AudioCue.Back);
            GameEvents.RaiseNotice("Discarded " + (item != null ? item.DisplayName : stack.ItemId));
            _selectedSlot = -1;
            Refresh();
        }

        // ----------------------------------------------------------------- readouts

        private void RefreshAmmo()
        {
            ClearChildren(_ammoPanel);
            AmmoType[] types = { AmmoType.Light, AmmoType.Heavy, AmmoType.Shell, AmmoType.Sniper };
            for (int i = 0; i < types.Length; i++)
            {
                AmmoType type = types[i];
                Text row = UIFactory.CreateText(_ammoPanel, "Ammo" + type,
                    AmmoLabel(type) + "     " + Context.Inventory.AmmoReserve(type) + " rounds",
                    22, Theme.Text, TextAnchor.MiddleLeft);
                UIFactory.TopLeft(row.rectTransform, 0f, i * 28f, 660f, 26f);
            }
        }

        private static string AmmoLabel(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Heavy: return "HEAVY      ";
                case AmmoType.Shell: return "SHELL      ";
                case AmmoType.Sniper: return "PRECISION  ";
                default: return "LIGHT      ";
            }
        }

        private void RefreshArsenal()
        {
            ClearChildren(_weaponPanel);
            List<ItemStack> weapons = Context.Inventory.Weapons;

            for (int i = 0; i < weapons.Count; i++)
            {
                ItemStack stack = weapons[i];
                ItemDef item = Context.Content.GetItem(stack.ItemId);
                string weaponId = item != null ? item.WeaponId : null;
                WeaponDef def = Context.Content.GetWeapon(weaponId);
                if (def == null) continue;

                Text row = UIFactory.CreateText(_weaponPanel, "Weapon_" + def.Id,
                    def.DisplayName + "   " + RarityRules.Name(stack.Rarity) + "  MK" + Mathf.Max(1, stack.UpgradeLevel) +
                    "     " + def.Class,
                    21, RarityRules.Color(stack.Rarity), TextAnchor.MiddleLeft);
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 26f;
                element.minHeight = 26f;
            }

            Text slots = UIFactory.CreateText(_weaponPanel, "WeaponSlots",
                "WEAPON STORAGE  " + weapons.Count + " / " + Context.Inventory.WeaponCapacity,
                21, Theme.TextDim, TextAnchor.MiddleLeft);
            LayoutElement slotsElement = slots.gameObject.AddComponent<LayoutElement>();
            slotsElement.preferredHeight = 26f;

            if (weapons.Count == 0)
            {
                Text none = UIFactory.CreateText(_weaponPanel, "None", "No weapons in the pack.", 21, Theme.TextDim, TextAnchor.MiddleLeft);
                LayoutElement element = none.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 26f;
            }
        }
    }
}
