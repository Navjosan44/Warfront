using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Workshop screen: spends credits and materials on weapon unlocks and upgrade levels.
    ///
    /// Nothing here mutates state directly - every action is a ProgressionService call, which routes
    /// through ProgressionModel where cost, affordability and level caps are validated and unit tested.
    /// A rejected upgrade therefore reports the model's own reason rather than a guessed message.
    /// </summary>
    public class UpgradesScreen : UIScreen
    {
        public override string Title { get { return "Workshop"; } }

        private RectTransform _list;
        private Text _summaryText;

        protected override void OnBuild()
        {
            Image header = UIFactory.CreatePanel(Content, "Summary", Theme.Panel);
            UIFactory.SetAnchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 96f), Vector2.zero);
            header.rectTransform.offsetMin = new Vector2(0f, -96f);
            header.rectTransform.offsetMax = new Vector2(0f, 0f);

            _summaryText = UIFactory.CreateText(header.transform, "Text", string.Empty, 24, Theme.Text, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_summaryText.rectTransform, 22f, 22f, 8f, 8f);

            ScrollRect scroll = UIFactory.CreateScrollView(Content, "WeaponList", out _list);
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(0f, 0f);
            scrollRect.offsetMax = new Vector2(0f, -108f);
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Content == null) return;

            ClearChildren(_list);

            ProgressionModel model = Context.Progression.Model;
            List<WeaponDef> weapons = Context.Content.AllWeapons();
            weapons.Sort((a, b) =>
            {
                int order = a.UnlockLevel.CompareTo(b.UnlockLevel);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });

            for (int i = 0; i < weapons.Count; i++)
            {
                CreateRow(weapons[i], model);
            }

            if (_summaryText != null)
            {
                _summaryText.text =
                    "CREDITS " + model.Credits.ToString("N0") + " CR     MATERIALS " + model.Materials +
                    "     OPERATOR LEVEL " + model.Level + " / " + ProgressionModel.MaxLevel +
                    "\nUpgrades raise damage, magazine size and reload speed. Unlocks are bought once and are permanent.";
            }

            SetHeaderSubtitle("Every upgrade is permanent and shared by every copy of that weapon you carry.");
        }

        private void CreateRow(WeaponDef def, ProgressionModel model)
        {
            bool unlocked = model.IsWeaponUnlocked(def.Id);
            int level = model.GetWeaponUpgradeLevel(def.Id);
            int maxLevel = Mathf.Min(def.MaxUpgradeLevel, ProgressionModel.MaxWeaponUpgrade);
            Rarity rarity = model.GetWeaponRarity(def.Id);

            int credits;
            int materials;
            WeaponStatRules.UpgradeCost(def, level, out credits, out materials);

            bool atMax = level >= maxLevel;
            bool affordable = model.CanAfford(credits) && model.Materials >= materials;

            Image row = UIFactory.CreatePanel(_list, "Weapon_" + def.Id,
                unlocked ? new Color(0.11f, 0.12f, 0.12f, 0.92f) : new Color(0.09f, 0.09f, 0.09f, 0.8f));
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 150f;
            element.minHeight = 150f;

            Text name = UIFactory.CreateText(row.transform, "Name",
                def.DisplayName.ToUpperInvariant() + "    " + def.Class, 28,
                unlocked ? RarityRules.Color(rarity) : Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(name.rectTransform, 20f, 10f, 700f, 34f);

            WeaponStats current = WeaponStatRules.Build(def, rarity, unlocked ? level : 1);
            WeaponStats upgraded = WeaponStatRules.Build(def, rarity, Mathf.Min(maxLevel, level + 1));

            string levelLine = "MK " + (unlocked ? level : 1) + " / " + maxLevel + "    " + RarityRules.Name(rarity);
            if (unlocked && !atMax)
            {
                levelLine += "    next: DMG " + current.Damage.ToString("0.#") + " -> " + upgraded.Damage.ToString("0.#") +
                             "    MAG " + current.MagazineSize + " -> " + upgraded.MagazineSize +
                             "    RELOAD " + current.ReloadTime.ToString("0.0") + "s -> " + upgraded.ReloadTime.ToString("0.0") + "s";
            }
            else if (unlocked)
            {
                levelLine += "    fully upgraded: DMG " + current.Damage.ToString("0.#") +
                             "    MAG " + current.MagazineSize + "    RELOAD " + current.ReloadTime.ToString("0.0") + "s";
            }

            Text stats = UIFactory.CreateText(row.transform, "Stats", levelLine, 21, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(stats.rectTransform, 20f, 48f, 900f, 28f);

            Text cost = UIFactory.CreateText(row.transform, "Cost", CostLine(def, unlocked, atMax, level, credits, materials, model), 22,
                unlocked ? (atMax ? Theme.Primary : affordable ? Theme.Text : Theme.Warning) : Theme.Warning,
                TextAnchor.UpperLeft);
            UIFactory.TopLeft(cost.rectTransform, 20f, 82f, 900f, 28f);

            Text flavour = UIFactory.CreateText(row.transform, "Flavour",
                "UNLOCK LEVEL " + def.UnlockLevel + "    LOOT WEIGHT " + def.LootWeight +
                "    headshot x" + current.HeadshotMultiplier.ToString("0.0") +
                "    penetration " + (current.ArmorPenetration * 100f).ToString("0") + "%",
                20, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(flavour.rectTransform, 20f, 112f, 900f, 26f);

            if (!unlocked)
            {
                Button unlock = UIFactory.CreateButton(row.transform, "Unlock",
                    def.UnlockCost > 0 ? "UNLOCK " + def.UnlockCost.ToString("N0") + " CR" : "LEVEL " + def.UnlockLevel,
                    affordable && model.Level >= def.UnlockLevel ? Theme.PrimaryDim : Theme.Panel, null, 24);
                UIFactory.SetAnchor(unlock.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(300f, 66f), new Vector2(-20f, 0f));
                unlock.interactable = affordable && model.Level >= def.UnlockLevel;
                string weaponId = def.Id;
                unlock.onClick.AddListener(() => OnUnlock(weaponId));
                return;
            }

            Button upgrade = UIFactory.CreateButton(row.transform, "Upgrade",
                atMax ? "MAXIMUM LEVEL" : "UPGRADE  " + credits.ToString("N0") + " CR + " + materials + " MAT",
                atMax ? Theme.PrimaryDim : affordable ? Theme.PrimaryDim : Theme.Panel, null, 24);
            UIFactory.SetAnchor(upgrade.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(300f, 66f), new Vector2(-20f, 0f));
            upgrade.interactable = !atMax && affordable;
            string id = def.Id;
            upgrade.onClick.AddListener(() => OnUpgrade(id));
        }

        private static string CostLine(WeaponDef def, bool unlocked, bool atMax, int level, int credits, int materials, ProgressionModel model)
        {
            if (!unlocked)
            {
                if (def.UnlockCost <= 0) return "ISSUED FREE AT OPERATOR LEVEL " + def.UnlockLevel + "    current level " + model.Level;
                return "UNLOCK COST  " + def.UnlockCost.ToString("N0") + " CR    requires operator level " + def.UnlockLevel +
                       " (you are " + model.Level + ")";
            }
            if (atMax) return "This weapon is at its maximum upgrade level.";
            return "UPGRADE " + level + " -> " + (level + 1) + "    COST " + credits.ToString("N0") + " CR + " +
                   materials + " MATERIALS    you hold " + model.Credits.ToString("N0") + " CR + " + model.Materials + " MAT";
        }

        private void OnUpgrade(string weaponId)
        {
            UpgradeResult result = Context.Progression.UpgradeWeapon(weaponId);
            if (result.Success)
            {
                Context.Audio.PlayUi(AudioCue.Confirm);
                GameEvents.RaiseNotice("Upgraded to MK" + result.NewLevel + "  (-" + result.CreditsSpent + " CR, -" + result.MaterialsSpent + " MAT)");
            }
            else
            {
                Context.Audio.PlayUi(AudioCue.Deny);
                GameEvents.RaiseNotice(string.IsNullOrEmpty(result.Reason) ? "Upgrade refused" : result.Reason);
            }
            Refresh();
        }

        private void OnUnlock(string weaponId)
        {
            bool unlocked = Context.Progression.UnlockWeapon(weaponId);
            if (unlocked)
            {
                Context.Audio.PlayUi(AudioCue.Confirm);
                WeaponDef def = Context.Content.GetWeapon(weaponId);
                GameEvents.RaiseNotice((def != null ? def.DisplayName : weaponId) + " unlocked");
            }
            else
            {
                Context.Audio.PlayUi(AudioCue.Deny);
                GameEvents.RaiseNotice("Unlock refused - check level and credits");
            }
            Refresh();
        }
    }
}
