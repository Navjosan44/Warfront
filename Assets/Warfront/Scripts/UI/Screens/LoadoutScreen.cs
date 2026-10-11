using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Loadout screen: chooses the three equipped weapons and shows what the player is carrying.
    ///
    /// Picking a slot and then picking a weapon writes the choice straight into the save document, which
    /// is exactly what GameFlow reads when a mission deploys - so what is shown here is what is carried.
    /// </summary>
    public class LoadoutScreen : UIScreen
    {
        public override string Title { get { return "Loadout"; } }

        private static readonly string[] SlotNames = { "PRIMARY", "SECONDARY", "SIDEARM" };

        private readonly Text[] _slotWeaponLabels = new Text[PlayerLoadout.SlotCount];
        private readonly Text[] _slotStatLabels = new Text[PlayerLoadout.SlotCount];
        private readonly Image[] _slotBackgrounds = new Image[PlayerLoadout.SlotCount];
        private readonly Button[] _slotButtons = new Button[PlayerLoadout.SlotCount];

        private RectTransform _weaponList;
        private Text _detailTitle;
        private Text _detailStats;
        private Text _equipmentText;
        private Text _ammoText;
        private int _selectedSlot;

        protected override void OnBuild()
        {
            // ---- left column: the three equipped slots
            Image left = UIFactory.CreatePanel(Content, "Slots", Theme.Panel);
            UIFactory.SetAnchor(left.rectTransform, new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(0f, 0f),
                Vector2.zero, Vector2.zero);
            left.rectTransform.offsetMin = new Vector2(0f, 0f);
            left.rectTransform.offsetMax = new Vector2(-16f, 0f);

            Text header = UIFactory.CreateText(left.transform, "Header", "EQUIPPED", 24, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(header.rectTransform, 24f, 12f, 400f, 30f);

            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
            {
                int index = i;
                RectTransform row = UIFactory.CreateBandTop(left.transform, "Slot" + i, 50f + i * 146f, 132f, 20f);
                Image background = row.gameObject.AddComponent<Image>();
                background.color = Theme.PanelLight;
                background.raycastTarget = false;
                _slotBackgrounds[i] = background;

                Text slotName = UIFactory.CreateText(row, "SlotName", SlotNames[i], 20, Theme.TextDim, TextAnchor.UpperLeft);
                UIFactory.TopLeft(slotName.rectTransform, 20f, 8f, 200f, 24f);

                Text weapon = UIFactory.CreateText(row, "Weapon", "-", 32, Theme.Text, TextAnchor.UpperLeft);
                UIFactory.TopLeft(weapon.rectTransform, 20f, 32f, 460f, 40f);
                _slotWeaponLabels[i] = weapon;

                Text stats = UIFactory.CreateText(row, "Stats", string.Empty, 20, Theme.TextDim, TextAnchor.UpperLeft);
                UIFactory.TopLeft(stats.rectTransform, 20f, 76f, 560f, 48f);
                _slotStatLabels[i] = stats;

                Button select = UIFactory.CreateButton(row, "Select", "SELECT", Theme.Panel, null, 24);
                UIFactory.SetAnchor(select.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(130f, 48f), new Vector2(-14f, -10f));
                select.onClick.AddListener(() => SelectSlot(index));
                _slotButtons[i] = select;

                // The secondary and sidearm slots may deliberately be left empty.
                if (i > 0)
                {
                    Button clear = UIFactory.CreateButton(row, "Clear", "EMPTY", Theme.Panel, null, 22);
                    UIFactory.SetAnchor(clear.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                        new Vector2(130f, 44f), new Vector2(-14f, 12f));
                    clear.onClick.AddListener(() => ClearSlot(index));
                }
            }

            RectTransform equipmentBand = UIFactory.CreateBandBottom(left.transform, "EquipmentBand", 20f, 96f, 24f);
            Image equipmentBack = UIFactory.CreatePanel(equipmentBand, "Back", new Color(0.08f, 0.09f, 0.09f, 0.7f));
            UIFactory.Stretch(equipmentBack.rectTransform, 0f, 0f, 0f, 0f);
            Text equipment = UIFactory.CreateText(equipmentBand, "Equipment", string.Empty, 22, Theme.Text, TextAnchor.MiddleLeft);
            UIFactory.Stretch(equipment.rectTransform, 16f, 16f, 8f, 8f);
            _equipmentText = equipment;

            // ---- right column: the arsenal
            Image right = UIFactory.CreatePanel(Content, "Arsenal", Theme.PanelLight);
            UIFactory.SetAnchor(right.rectTransform, new Vector2(0.56f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                Vector2.zero, Vector2.zero);
            right.rectTransform.offsetMin = new Vector2(16f, 0f);
            right.rectTransform.offsetMax = new Vector2(0f, 0f);

            Text arsenalHeader = UIFactory.CreateText(right.transform, "Header", "AVAILABLE WEAPONS", 24, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(arsenalHeader.rectTransform, 22f, 12f, 400f, 30f);

            ScrollRect scroll = UIFactory.CreateScrollView(right.transform, "WeaponScroll", out _weaponList);
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(22f, 180f);
            scrollRect.offsetMax = new Vector2(-22f, -52f);

            RectTransform detailBand = UIFactory.CreateBandBottom(right.transform, "Detail", 34f, 138f, 22f);
            Text detailHeader = UIFactory.CreateText(detailBand, "DetailHeader", "SELECTED WEAPON", 18, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(detailHeader.rectTransform, 2f, 0f, 400f, 22f);

            _detailTitle = UIFactory.CreateText(detailBand, "DetailTitle", string.Empty, 26, Theme.Primary, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_detailTitle.rectTransform, 2f, 22f, 620f, 32f);

            _detailStats = UIFactory.CreateText(detailBand, "DetailStats", string.Empty, 19, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_detailStats.rectTransform, 2f, 54f, 640f, 84f);

            _ammoText = UIFactory.CreateText(right.transform, "Ammo", string.Empty, 19, Theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetAnchor(_ammoText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), Vector2.zero);
            _ammoText.rectTransform.offsetMin = new Vector2(22f, 4f);
            _ammoText.rectTransform.offsetMax = new Vector2(-22f, 30f);
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Content == null) return;

            LoadoutSave loadout = Context.Save.Data.Loadout;
            string[] equipped = { loadout.PrimaryWeaponId, loadout.SecondaryWeaponId, loadout.SidearmWeaponId };

            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
            {
                bool isTarget = i == _selectedSlot;
                if (_slotBackgrounds[i] != null)
                {
                    _slotBackgrounds[i].color = isTarget ? new Color(0.20f, 0.26f, 0.16f, 0.98f) : Theme.PanelLight;
                }
                if (_slotButtons[i] != null)
                {
                    Text label = _slotButtons[i].GetComponentInChildren<Text>();
                    if (label != null) label.text = isTarget ? "SELECTED" : "SELECT";
                }

                WeaponDef def = Context.Content.GetWeapon(equipped[i]);
                if (def == null)
                {
                    _slotWeaponLabels[i].text = i == 0 ? "NOT SET" : "EMPTY";
                    _slotWeaponLabels[i].color = i == 0 ? Theme.Warning : Theme.TextDim;
                    _slotStatLabels[i].text = i == 0
                        ? "A primary weapon is mandatory - the VK-7 is issued automatically."
                        : "Nothing equipped in this slot.";
                    continue;
                }

                Rarity rarity = Context.Progression.Model.GetWeaponRarity(def.Id);
                int upgrade = Context.Progression.Model.GetWeaponUpgradeLevel(def.Id);
                WeaponStats stats = WeaponStatRules.Build(def, rarity, upgrade);

                _slotWeaponLabels[i].text = def.DisplayName.ToUpperInvariant() + "   " + RarityRules.Name(rarity).ToUpperInvariant() + "  MK" + upgrade;
                _slotWeaponLabels[i].color = RarityRules.Color(rarity);
                _slotStatLabels[i].text =
                    "DMG " + stats.Damage.ToString("0.#") + "    RPM " + stats.RoundsPerMinute.ToString("0") +
                    "    MAG " + stats.MagazineSize + "    RELOAD " + stats.ReloadTime.ToString("0.0") + "s\n" +
                    "CLASS " + def.Class + "    AMMO " + AmmoName(def.AmmoType) +
                    "    RESERVE " + Context.Inventory.AmmoReserve(def.AmmoType);
            }

            _equipmentText.text =
                "FIELD EQUIPMENT    bandages " + Context.Inventory.CountOf("med_field_bandage") +
                "    trauma kits " + Context.Inventory.CountOf("med_trauma_kit") +
                "    ceramic plates " + Context.Inventory.CountOf("plate_ceramic") + "\n" +
                "The MED control in a mission uses the best item in the pack first.";

            _ammoText.text =
                "RESERVE    light " + Context.Inventory.AmmoReserve(AmmoType.Light) +
                "    heavy " + Context.Inventory.AmmoReserve(AmmoType.Heavy) +
                "    shell " + Context.Inventory.AmmoReserve(AmmoType.Shell) +
                "    precision " + Context.Inventory.AmmoReserve(AmmoType.Sniper);

            RebuildWeaponList();

            MissionDef selected = Context.Content.GetMission(Context.Progression.Model.SelectedMissionId);
            SetHeaderSubtitle("Equip the kit you will carry into " +
                (selected != null ? selected.DisplayName : "the next contract") +
                ". Reserve ammunition is shared across every weapon of the same calibre.");
        }

        private void RebuildWeaponList()
        {
            ClearChildren(_weaponList);

            LoadoutSave loadout = Context.Save.Data.Loadout;
            string selectedEquipped = _selectedSlot == 0 ? loadout.PrimaryWeaponId
                : _selectedSlot == 1 ? loadout.SecondaryWeaponId : loadout.SidearmWeaponId;

            List<ItemStack> weapons = Context.Inventory.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                ItemStack stack = weapons[i];
                ItemDef item = Context.Content.GetItem(stack.ItemId);
                if (item == null || string.IsNullOrEmpty(item.WeaponId)) continue;

                WeaponDef def = Context.Content.GetWeapon(item.WeaponId);
                if (def == null) continue;

                bool equippedHere = def.Id == selectedEquipped;
                Rarity rarity = stack.Rarity;
                int upgrade = stack.UpgradeLevel <= 0 ? 1 : stack.UpgradeLevel;

                Image row = UIFactory.CreatePanel(_weaponList, "Weapon_" + def.Id,
                    equippedHere ? new Color(0.20f, 0.26f, 0.16f, 0.95f) : new Color(0.11f, 0.12f, 0.12f, 0.9f));
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 78f;
                element.minHeight = 78f;

                Text name = UIFactory.CreateText(row.transform, "Name", def.DisplayName.ToUpperInvariant(), 26,
                    RarityRules.Color(rarity), TextAnchor.MiddleLeft);
                UIFactory.TopLeft(name.rectTransform, 16f, 6f, 430f, 32f);

                WeaponStats stats = WeaponStatRules.Build(def, rarity, upgrade);
                Text detail = UIFactory.CreateText(row.transform, "Detail",
                    RarityRules.Name(rarity) + "  MK" + upgrade + "    DMG " + stats.Damage.ToString("0.#") +
                    "    RPM " + stats.RoundsPerMinute.ToString("0") + "    MAG " + stats.MagazineSize,
                    20, Theme.TextDim, TextAnchor.MiddleLeft);
                UIFactory.TopLeft(detail.rectTransform, 16f, 40f, 460f, 26f);

                Button equip = UIFactory.CreateButton(row.transform, "Equip",
                    equippedHere ? "EQUIPPED" : "EQUIP", equippedHere ? Theme.PrimaryDim : Theme.Panel, null, 24);
                UIFactory.SetAnchor(equip.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(170f, 54f), new Vector2(-14f, 0f));

                string weaponId = def.Id;
                equip.onClick.AddListener(() => EquipWeapon(weaponId));
            }

            if (_weaponList.childCount == 0)
            {
                CreateEmptyLabel(_weaponList, "No weapons in the pack.\nLoot a weapon in a mission to bring it home.");
            }

            if (!string.IsNullOrEmpty(selectedEquipped)) ShowWeaponDetail(selectedEquipped);
            else
            {
                _detailTitle.text = "NOTHING EQUIPPED";
                _detailTitle.color = Theme.TextDim;
                _detailStats.text = "Choose a weapon from the list above to fill this slot.";
            }
        }

        private void ShowWeaponDetail(string weaponId)
        {
            WeaponDef def = Context.Content.GetWeapon(weaponId);
            if (def == null) return;

            Rarity rarity = Context.Progression.Model.GetWeaponRarity(def.Id);
            int upgrade = Context.Progression.Model.GetWeaponUpgradeLevel(def.Id);
            WeaponStats stats = WeaponStatRules.Build(def, rarity, upgrade);

            _detailTitle.text = def.DisplayName.ToUpperInvariant() + "    " + RarityRules.Name(rarity).ToUpperInvariant() + "  MK" + upgrade;
            _detailTitle.color = RarityRules.Color(rarity);
            _detailStats.text =
                "DAMAGE       " + stats.Damage.ToString("0.#") + "   headshot x" + stats.HeadshotMultiplier.ToString("0.0") +
                "   crit " + (stats.CritChance * 100f).ToString("0") + "% x" + stats.CritMultiplier.ToString("0.0") + "\n" +
                "RATE         " + stats.RoundsPerMinute.ToString("0") + " rpm  " + def.FireMode +
                (stats.PelletsPerShot > 1 ? "  x" + stats.PelletsPerShot + " pellets" : string.Empty) +
                "   magazine " + stats.MagazineSize + "\n" +
                "RANGE        " + stats.FalloffStart.ToString("0") + "m full  " + stats.Range.ToString("0") + "m max" +
                "   penetration " + (stats.ArmorPenetration * 100f).ToString("0") + "%\n" +
                "HANDLING     spread " + stats.BaseSpread.ToString("0.00") + "   recoil " +
                stats.RecoilVertical.ToString("0.00") + " / " + stats.RecoilHorizontal.ToString("0.00") + "   reload " +
                stats.ReloadTime.ToString("0.0") + "s";
        }

        private void SelectSlot(int index)
        {
            _selectedSlot = Mathf.Clamp(index, 0, PlayerLoadout.SlotCount - 1);
            Context.Audio.PlayUi(AudioCue.Click);
            Refresh();
        }

        private void EquipWeapon(string weaponId)
        {
            if (Context == null || string.IsNullOrEmpty(weaponId)) return;
            WeaponDef def = Context.Content.GetWeapon(weaponId);
            if (def == null) return;

            LoadoutSave loadout = Context.Save.Data.Loadout;
            switch (_selectedSlot)
            {
                case 0: loadout.PrimaryWeaponId = weaponId; break;
                case 1: loadout.SecondaryWeaponId = weaponId; break;
                default: loadout.SidearmWeaponId = weaponId; break;
            }

            Context.Save.MarkDirty();
            Context.Audio.PlayUi(AudioCue.Confirm);
            GameEvents.RaiseNotice(def.DisplayName + " assigned to " + SlotNames[_selectedSlot].ToLowerInvariant());
            Refresh();
        }

        private void ClearSlot(int index)
        {
            if (index <= 0) return; // a primary weapon is mandatory
            LoadoutSave loadout = Context.Save.Data.Loadout;
            if (index == 1) loadout.SecondaryWeaponId = null;
            else loadout.SidearmWeaponId = null;

            Context.Save.MarkDirty();
            Context.Audio.PlayUi(AudioCue.Back);
            Refresh();
        }

        private static string AmmoName(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Heavy: return "heavy";
                case AmmoType.Shell: return "shell";
                case AmmoType.Sniper: return "precision";
                default: return "light";
            }
        }

        protected override void OnBackPressed()
        {
            if (Context == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            if (!string.IsNullOrEmpty(Context.Flow.CurrentMissionId)) Context.Flow.ShowBriefing(Context.Flow.CurrentMissionId);
            else Context.Flow.EnterMainMenu();
        }
    }
}
