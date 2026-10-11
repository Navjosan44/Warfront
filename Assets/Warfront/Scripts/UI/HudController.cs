using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// In-mission heads up display.
    ///
    /// It owns no gameplay state: every value it shows arrives either from the event bus or from a
    /// read of the authoritative model, so the HUD can never disagree with the simulation. Layout is
    /// anchor based against the 1920x1080 reference canvas, which keeps the readouts in the corners
    /// on a 4:3 tablet and a 21:9 phone alike, and the touch controls are drawn on top of it.
    /// </summary>
    public class HudController
    {
        private const int KillFeedEntries = 4;
        private const int NoticeEntries = 4;
        private const float NoticeLifetime = 3.2f;
        private const float KillFeedLifetime = 4f;
        private const float HitMarkerLifetime = 0.28f;
        private const float DamageFlashLifetime = 0.7f;
        private const float CompassDegrees = 62f;
        private const float CompassHalfWidth = 430f;

        private GameContext _context;
        private UIRoot _ui;
        private UITheme _theme;

        private RectTransform _root;
        private bool _built;
        private bool _visible;

        // Vitals
        private Image _healthFill;
        private Image _healthDamageFill;
        private Image _armorFill;
        private Text _healthText;
        private Text _stateText;

        // Weapon
        private Text _weaponName;
        private Text _ammoText;
        private Text _reloadText;
        private Image _reloadFill;
        private RectTransform _reloadBar;
        private RectTransform _weaponPanel;
        private RectTransform _slotColumn;
        private readonly Text[] _slotLabels = new Text[PlayerLoadout.SlotCount];

        // Objectives + compass
        private RectTransform _objectivePanel;
        private readonly List<Text> _objectiveRows = new List<Text>(6);
        private RectTransform _compassStrip;
        private RectTransform _compassTicks;
        private RectTransform _objectiveMarker;
        private RectTransform _extractionMarker;
        private Text _objectiveMarkerLabel;
        private Text _extractionMarkerLabel;
        private Text _headingText;

        // Extraction + mission state
        private Text _extractionLabel;
        private Text _extractionDistance;
        private RectTransform _extractionBarRoot;
        private Image _extractionFill;
        private Text _timerText;
        private Text _threatText;

        // Crosshair + feedback
        private RectTransform _crosshair;
        private readonly RectTransform[] _crosshairArms = new RectTransform[4];
        private Image _hitMarker;
        private Image _damageFlash;
        private Image _lowHealthVignette;
        private Text _hitZoneText;
        private Text _interactPrompt;

        // Feeds
        private RectTransform _noticeColumn;
        private readonly Text[] _noticeRows = new Text[NoticeEntries];
        private readonly float[] _noticeTimers = new float[NoticeEntries];
        private RectTransform _killFeedColumn;
        private readonly Text[] _killFeedRows = new Text[KillFeedEntries];
        private readonly float[] _killFeedTimers = new float[KillFeedEntries];

        private float _hitMarkerTimer;
        private float _damageFlashTimer;
        private float _crosshairBloom;
        private float _health;
        private float _maxHealth = PlayerHealth.BaseHealth;
        private float _armor;
        private float _maxArmor = PlayerHealth.BaseArmor;
        private float _displayedHealth = 1f;
        private PlayerInteractor _interactor;
        private WeaponModel _boundWeapon;
        private UnityEngine.Camera _camera;

        public bool IsVisible { get { return _visible; } }

        // ------------------------------------------------------------------ build

        public void Initialize(GameContext context, UIRoot ui)
        {
            _context = context;
            _ui = ui;
            if (ui == null || ui.OverlayLayer == null) return;

            _theme = ui.Theme;
            _camera = context != null ? context.MenuCamera : null;
            Build();
            Subscribe();
            _built = true;
            Show(false);
        }

        private Text Label(Transform parent, string name, string value, int size, Color color, TextAnchor anchor)
        {
            return UIFactory.CreateText(parent, name, value, size, color, anchor);
        }

        private void Build()
        {
            _root = UIFactory.CreateRect(_ui.OverlayLayer, "Hud");
            UIFactory.Stretch(_root, 0f, 0f, 0f, 0f);

            BuildVitals();
            BuildWeaponPanel();
            BuildObjectivePanel();
            BuildCompass();
            BuildExtraction();
            BuildCrosshair();
            BuildFeeds();
            BuildInteractionPrompt();

            _lowHealthVignette = UIFactory.CreateImage(_root, "LowHealthVignette", new Color(0.5f, 0.05f, 0.05f, 0f), false);
            UIFactory.Stretch(_lowHealthVignette.rectTransform, 0f, 0f, 0f, 0f);

            _damageFlash = UIFactory.CreateImage(_root, "DamageFlash", new Color(0.62f, 0.06f, 0.05f, 0f), false);
            UIFactory.Stretch(_damageFlash.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void BuildVitals()
        {
            RectTransform panel = UIFactory.CreateRect(_root, "Vitals");
            UIFactory.SetAnchor(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(520f, 150f), new Vector2(38f, -34f));

            Image healthBack = UIFactory.CreatePanel(panel, "HealthBar", new Color(0.06f, 0.07f, 0.07f, 0.72f));
            UIFactory.SetAnchor(healthBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(430f, 40f), new Vector2(0f, -26f));
            _healthDamageFill = UIFactory.CreateImage(healthBack.transform, "Delayed", new Color(0.95f, 0.55f, 0.35f, 0.75f), false);
            UIFactory.Stretch(_healthDamageFill.rectTransform, 2f, 2f, 2f, 2f);
            ConfigureFill(_healthDamageFill);
            _healthFill = UIFactory.CreateImage(healthBack.transform, "Fill", _theme.Health, false);
            UIFactory.Stretch(_healthFill.rectTransform, 2f, 2f, 2f, 2f);
            ConfigureFill(_healthFill);

            Image armorBack = UIFactory.CreatePanel(panel, "ArmorBar", new Color(0.06f, 0.07f, 0.07f, 0.72f));
            UIFactory.SetAnchor(armorBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(330f, 24f), new Vector2(0f, -2f));
            _armorFill = UIFactory.CreateImage(armorBack.transform, "Fill", _theme.Armor, false);
            UIFactory.Stretch(_armorFill.rectTransform, 2f, 2f, 2f, 2f);
            ConfigureFill(_armorFill);

            _healthText = Label(panel, "HealthValue", "100", 30, _theme.Text, TextAnchor.MiddleLeft);
            UIFactory.SetAnchor(_healthText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(220f, 40f), new Vector2(444f, -26f));

            _stateText = Label(panel, "StateValue", string.Empty, 22, _theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.SetAnchor(_stateText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(420f, 30f), new Vector2(0f, 16f));
        }

        private static void ConfigureFill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
        }

        private void BuildWeaponPanel()
        {
            _weaponPanel = UIFactory.CreateRect(_root, "WeaponPanel");
            UIFactory.SetAnchor(_weaponPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(470f, 190f), new Vector2(-38f, 190f));

            _weaponName = Label(_weaponPanel, "WeaponName", string.Empty, 30, _theme.Text, TextAnchor.LowerRight);
            UIFactory.SetAnchor(_weaponName.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(470f, 38f), new Vector2(0f, 96f));

            _ammoText = Label(_weaponPanel, "AmmoValue", "-- / --", 54, _theme.Text, TextAnchor.LowerRight);
            UIFactory.SetAnchor(_ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(470f, 62f), new Vector2(0f, 30f));

            _reloadText = Label(_weaponPanel, "ReloadHint", string.Empty, 24, _theme.Warning, TextAnchor.LowerRight);
            UIFactory.SetAnchor(_reloadText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(470f, 30f), new Vector2(0f, 2f));

            _reloadBar = UIFactory.CreateRect(_weaponPanel, "ReloadBar");
            UIFactory.SetAnchor(_reloadBar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(300f, 12f), new Vector2(0f, -14f));
            Image reloadBack = UIFactory.CreatePanel(_reloadBar, "Back", new Color(0.08f, 0.09f, 0.09f, 0.8f));
            UIFactory.Stretch(reloadBack.rectTransform, 0f, 0f, 0f, 0f);
            _reloadFill = UIFactory.CreateImage(reloadBack.transform, "Fill", _theme.Warning, false);
            UIFactory.Stretch(_reloadFill.rectTransform, 1f, 1f, 1f, 1f);
            ConfigureFill(_reloadFill);
            _reloadBar.gameObject.SetActive(false);

            // Weapon slot strip (1/2/3) so switching is legible on a phone.
            _slotColumn = UIFactory.CreateRect(_root, "WeaponSlots");
            UIFactory.SetAnchor(_slotColumn, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(190f, 132f), new Vector2(-38f, 52f));
            VerticalLayoutGroup layout = _slotColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
            {
                Image row = UIFactory.CreatePanel(_slotColumn, "Slot" + (i + 1), new Color(0.08f, 0.09f, 0.09f, 0.62f));
                _slotLabels[i] = Label(row.transform, "Label", (i + 1) + "  -", 22, _theme.TextDim, TextAnchor.MiddleLeft);
                UIFactory.Stretch(_slotLabels[i].rectTransform, 12f, 8f, 2f, 2f);
            }
        }

        private void BuildObjectivePanel()
        {
            _objectivePanel = UIFactory.CreateRect(_root, "Objectives");
            UIFactory.SetAnchor(_objectivePanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(560f, 250f), new Vector2(-38f, -34f));

            Text header = Label(_objectivePanel, "Header", "OBJECTIVES", 22, _theme.Accent, TextAnchor.UpperRight);
            UIFactory.SetAnchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 28f), Vector2.zero);

            _timerText = Label(_objectivePanel, "Timer", string.Empty, 26, _theme.Warning, TextAnchor.UpperRight);
            UIFactory.SetAnchor(_timerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 26f), new Vector2(0f, -30f));

            for (int i = 0; i < 6; i++)
            {
                Text row = Label(_objectivePanel, "Objective" + i, string.Empty, 24, _theme.Text, TextAnchor.UpperRight);
                UIFactory.SetAnchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, 28f), new Vector2(0f, -60f - i * 28f));
                row.gameObject.SetActive(false);
                _objectiveRows.Add(row);
            }

            _threatText = Label(_root, "ThreatReadout", string.Empty, 22, _theme.Danger, TextAnchor.UpperRight);
            UIFactory.SetAnchor(_threatText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(420f, 26f), new Vector2(0f, 0f));
        }

        private void BuildCompass()
        {
            _compassStrip = UIFactory.CreateRect(_root, "Compass");
            UIFactory.SetAnchor(_compassStrip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(CompassHalfWidth * 2f + 40f, 46f), new Vector2(0f, -18f));

            Image back = UIFactory.CreateImage(_compassStrip, "Back", new Color(0.04f, 0.05f, 0.05f, 0.42f), false);
            UIFactory.Stretch(back.rectTransform, 0f, 0f, 8f, 8f);

            _compassTicks = UIFactory.CreateRect(_compassStrip, "Ticks");
            UIFactory.Stretch(_compassTicks, 0f, 0f, 0f, 0f);
            for (int degrees = 0; degrees < 360; degrees += 45)
            {
                Text tick = Label(_compassTicks, "Tick" + degrees, Cardinal(degrees), 22, _theme.TextDim, TextAnchor.MiddleCenter);
                UIFactory.SetAnchor(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(48f, 30f), Vector2.zero);
                CompassTick handle = tick.gameObject.AddComponent<CompassTick>();
                handle.Configure(degrees);
            }

            _headingText = Label(_root, "Heading", string.Empty, 22, _theme.Text, TextAnchor.UpperCenter);
            UIFactory.SetAnchor(_headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(220f, 28f), new Vector2(0f, -66f));

            _objectiveMarker = CreateMarker("ObjectiveMarker", _theme.Warning, out _objectiveMarkerLabel);
            _extractionMarker = CreateMarker("ExtractionMarker", _theme.Primary, out _extractionMarkerLabel);
        }

        private RectTransform CreateMarker(string name, Color color, out Text label)
        {
            Image diamond = UIFactory.CreateImage(_compassStrip, name, color, false);
            UIFactory.SetAnchor(diamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(20f, 32f), Vector2.zero);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            label = Label(diamond.transform, "Label", string.Empty, 18, color, TextAnchor.UpperCenter);
            UIFactory.SetAnchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(200f, 22f), new Vector2(0f, -4f));
            label.rectTransform.localRotation = Quaternion.identity;

            diamond.gameObject.SetActive(false);
            return diamond.rectTransform;
        }

        private static string Cardinal(int degrees)
        {
            if (degrees == 0) return "N";
            if (degrees == 45) return "NE";
            if (degrees == 90) return "E";
            if (degrees == 135) return "SE";
            if (degrees == 180) return "S";
            if (degrees == 225) return "SW";
            if (degrees == 270) return "W";
            return "NW";
        }

        private void BuildExtraction()
        {
            RectTransform panel = UIFactory.CreateRect(_root, "Extraction");
            UIFactory.SetAnchor(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(560f, 120f), new Vector2(38f, 190f));

            _extractionLabel = Label(panel, "Label", string.Empty, 30, _theme.Primary, TextAnchor.LowerLeft);
            UIFactory.SetAnchor(_extractionLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(560f, 34f), new Vector2(0f, 62f));

            _extractionDistance = Label(panel, "Distance", string.Empty, 24, _theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetAnchor(_extractionDistance.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(560f, 30f), new Vector2(0f, 32f));

            _extractionBarRoot = UIFactory.CreateRect(panel, "HoldBar");
            UIFactory.SetAnchor(_extractionBarRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(430f, 18f), Vector2.zero);
            Image back = UIFactory.CreatePanel(_extractionBarRoot, "Back", new Color(0.06f, 0.07f, 0.07f, 0.8f));
            UIFactory.Stretch(back.rectTransform, 0f, 0f, 0f, 0f);
            _extractionFill = UIFactory.CreateImage(back.transform, "Fill", _theme.Primary, false);
            UIFactory.Stretch(_extractionFill.rectTransform, 2f, 2f, 2f, 2f);
            ConfigureFill(_extractionFill);
            _extractionBarRoot.gameObject.SetActive(false);
        }

        private void BuildCrosshair()
        {
            _crosshair = UIFactory.CreateRect(_root, "Crosshair");
            UIFactory.SetAnchor(_crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(80f, 80f), Vector2.zero);

            float[] width = { 3f, 3f, 3f, 3f };
            for (int i = 0; i < 4; i++)
            {
                Image arm = UIFactory.CreateImage(_crosshair, "Arm" + i, new Color(1f, 1f, 1f, 0.75f), false);
                bool vertical = i >= 2;
                arm.rectTransform.sizeDelta = vertical ? new Vector2(width[i], 12f) : new Vector2(12f, width[i]);
                _crosshairArms[i] = arm.rectTransform;
            }

            _hitMarker = UIFactory.CreateImage(_crosshair, "HitMarker", new Color(1f, 1f, 1f, 0f), false);
            UIFactory.SetAnchor(_hitMarker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, 30f), Vector2.zero);
            _hitMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            _hitZoneText = Label(_root, "HitZone", string.Empty, 22, _theme.Warning, TextAnchor.MiddleCenter);
            UIFactory.SetAnchor(_hitZoneText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(420f, 28f), new Vector2(0f, -66f));
        }

        private void BuildFeeds()
        {
            _noticeColumn = UIFactory.CreateRect(_root, "Notices");
            UIFactory.SetAnchor(_noticeColumn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(900f, 160f), new Vector2(0f, 150f));
            for (int i = 0; i < NoticeEntries; i++)
            {
                _noticeRows[i] = Label(_noticeColumn, "Notice" + i, string.Empty, 26, _theme.Text, TextAnchor.LowerCenter);
                UIFactory.SetAnchor(_noticeRows[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 32f), new Vector2(0f, i * 34f));
                _noticeRows[i].gameObject.SetActive(false);
            }

            _killFeedColumn = UIFactory.CreateRect(_root, "KillFeed");
            UIFactory.SetAnchor(_killFeedColumn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(520f, 160f), new Vector2(-38f, -300f));
            for (int i = 0; i < KillFeedEntries; i++)
            {
                _killFeedRows[i] = Label(_killFeedColumn, "Kill" + i, string.Empty, 22, _theme.TextDim, TextAnchor.UpperRight);
                UIFactory.SetAnchor(_killFeedRows[i].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, 26f), new Vector2(0f, -i * 28f));
                _killFeedRows[i].gameObject.SetActive(false);
            }
        }

        private void BuildInteractionPrompt()
        {
            _interactPrompt = Label(_root, "InteractPrompt", string.Empty, 28, _theme.Text, TextAnchor.MiddleCenter);
            UIFactory.SetAnchor(_interactPrompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(700f, 36f), new Vector2(0f, -150f));
        }

        // -------------------------------------------------------------- events

        private void Subscribe()
        {
            GameEvents.PlayerHealthChanged += OnHealthChanged;
            GameEvents.PlayerArmorChanged += OnArmorChanged;
            GameEvents.WeaponChanged += OnWeaponChanged;
            GameEvents.WeaponFired += OnWeaponFired;
            GameEvents.ObjectiveUpdated += OnObjectiveUpdated;
            GameEvents.ObjectiveCompleted += OnObjectiveCompleted;
            GameEvents.ExtractionChanged += OnExtractionChanged;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.Notice += OnNotice;
            GameEvents.HitConfirmed += OnHitConfirmed;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.MissionStarted += OnMissionStarted;
            GameEvents.MissionStateChanged += OnMissionStateChanged;
        }

        private void OnHealthChanged(float health, float max)
        {
            _health = health;
            _maxHealth = Mathf.Max(1f, max);
        }

        private void OnArmorChanged(float armor, float max)
        {
            _armor = armor;
            _maxArmor = Mathf.Max(1f, max);
        }

        private void OnWeaponChanged(WeaponModel weapon, AmmoType ammoType)
        {
            _boundWeapon = weapon;
            RefreshWeaponSlots();
        }

        private void OnWeaponFired()
        {
            _crosshairBloom = Mathf.Min(26f, _crosshairBloom + 5f);
        }

        private void OnObjectiveUpdated(ObjectiveProgress progress)
        {
            RefreshObjectives();
        }

        /// <summary>Called by MissionRuntime the moment an objective flips complete.</summary>
        public void OnObjectiveCompleted(ObjectiveProgress progress)
        {
            RefreshObjectives();
            if (progress != null && progress.Def != null)
            {
                PushNotice(progress.Def.Title + "  -  COMPLETE", _theme.Primary);
            }
        }

        private void OnExtractionChanged(ExtractionStatus status)
        {
            if (_extractionLabel == null) return;
            _extractionLabel.text = status.Label;
            _extractionLabel.color = status.State == ExtractionState.Complete ? _theme.Primary
                : status.State == ExtractionState.Countdown ? _theme.Warning
                : status.State == ExtractionState.Failed ? _theme.Danger
                : _theme.Accent;

            bool showBar = status.State == ExtractionState.Countdown || status.State == ExtractionState.Interrupted;
            if (_extractionBarRoot != null) _extractionBarRoot.gameObject.SetActive(showBar);
            if (_extractionFill != null) _extractionFill.fillAmount = status.Progress01;

            if (_extractionDistance != null)
            {
                if (!status.PlayerInZone && status.State != ExtractionState.Complete)
                {
                    _extractionDistance.text = Mathf.RoundToInt(status.DistanceToZone) + " m to the pad";
                }
                else if (status.State == ExtractionState.Countdown)
                {
                    _extractionDistance.text = "HOLD  " + Mathf.CeilToInt(status.RequiredSeconds - status.HoldSeconds) + "s";
                }
                else
                {
                    _extractionDistance.text = status.PlayerInZone ? "IN THE ZONE" : string.Empty;
                }
            }
        }

        private void OnPlayerDamaged(DamageResult result, HitZone zone)
        {
            if (result.Applied <= 0f) return;
            _damageFlashTimer = DamageFlashLifetime;
            if (_hitZoneText != null)
            {
                _hitZoneText.text = zone == HitZone.Head ? "HEAD HIT" : zone == HitZone.Limbs ? "LIMB HIT" : "HIT TAKEN";
                _hitZoneText.color = _theme.Danger;
            }
        }

        private void OnNotice(string message)
        {
            PushNotice(message, _theme.Text);
        }

        private void OnHitConfirmed(bool killed)
        {
            _hitMarkerTimer = HitMarkerLifetime;
            if (_hitMarker != null)
            {
                _hitMarker.color = killed ? new Color(0.95f, 0.35f, 0.25f, 1f) : new Color(1f, 1f, 1f, 1f);
            }
        }

        private void OnEnemyKilled(EnemyController enemy, bool headshot)
        {
            if (enemy == null) return;
            PushKillFeed(enemy.Callsign + (headshot ? "   [HEADSHOT]" : string.Empty));
        }

        private void OnMissionStarted(MissionDef def)
        {
            ClearFeed(_noticeRows, _noticeTimers);
            ClearFeed(_killFeedRows, _killFeedTimers);
            RefreshObjectives();
            RefreshWeaponSlots();
        }

        private void OnMissionStateChanged(MissionState state)
        {
            if (state == MissionState.Success || state == MissionState.Failed)
            {
                Show(false);
            }
        }

        private static void ClearFeed(Text[] rows, float[] timers)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                timers[i] = 0f;
                if (rows[i] != null) rows[i].gameObject.SetActive(false);
            }
        }

        private void PushNotice(string message, Color color)
        {
            if (string.IsNullOrEmpty(message)) return;
            ShiftFeed(_noticeRows, _noticeTimers, message, color);
        }

        private void PushKillFeed(string message)
        {
            ShiftFeed(_killFeedRows, _killFeedTimers, message, _theme.TextDim);
        }

        /// <summary>Scrolls a feed one row down and writes the newest entry at the top.</summary>
        private static void ShiftFeed(Text[] rows, float[] timers, string message, Color color)
        {
            for (int i = rows.Length - 1; i > 0; i--)
            {
                if (rows[i] == null || rows[i - 1] == null) continue;
                rows[i].text = rows[i - 1].text;
                rows[i].color = rows[i - 1].color;
                rows[i].gameObject.SetActive(rows[i - 1].gameObject.activeSelf);
                timers[i] = timers[i - 1];
            }
            if (rows[0] == null) return;
            rows[0].text = message;
            rows[0].color = color;
            rows[0].gameObject.SetActive(true);
            timers[0] = 1f;
        }

        private static void TickFeed(Text[] rows, float[] timers, float deltaTime, float lifetime)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || !rows[i].gameObject.activeSelf) continue;
                timers[i] -= deltaTime / lifetime;
                if (timers[i] > 0f) continue;
                timers[i] = 0f;
                rows[i].gameObject.SetActive(false);
            }
        }

        // --------------------------------------------------------------- refresh

        private void RefreshObjectives()
        {
            if (_objectivePanel == null) return;
            MissionRuntime mission = _context != null ? _context.Mission : null;

            for (int i = 0; i < _objectiveRows.Count; i++)
            {
                Text row = _objectiveRows[i];
                if (row == null) continue;

                bool has = mission != null && mission.Model != null && i < mission.Model.Objectives.Count;
                if (row.gameObject.activeSelf != has) row.gameObject.SetActive(has);
                if (!has) continue;

                ObjectiveProgress progress = mission.Model.Objectives[i];
                string mark = progress.Completed ? "[x] " : progress.Required ? "[ ] " : "[-] ";
                row.color = progress.Completed ? _theme.Primary : progress.Required ? _theme.Text : _theme.TextDim;
                row.text = mark + progress.Def.Title + "   " + progress.ProgressLabel;
            }
        }

        private void RefreshWeaponSlots()
        {
            PlayerLoadout loadout = _context != null && _context.Flow != null ? _context.Flow.Player.Loadout : null;
            if (loadout == null || _slotLabels[0] == null) return;

            for (int i = 0; i < PlayerLoadout.SlotCount; i++)
            {
                WeaponSlot slot = loadout.GetSlot(i);
                bool equipped = i == loadout.ActiveIndex;
                Text label = _slotLabels[i];
                if (label == null) continue;

                if (slot == null || slot.Def == null)
                {
                    label.text = (i + 1) + "  -";
                    label.color = _theme.TextDim;
                    continue;
                }

                label.text = (i + 1) + "  " + slot.Def.DisplayName + (equipped ? "  <" : string.Empty);
                label.color = equipped ? _theme.Primary : RarityRules.Color(slot.Rarity);
            }
        }

        // ------------------------------------------------------------------ tick

        public void Show(bool visible)
        {
            _visible = visible;
            if (!_built || _root == null) return;
            _root.gameObject.SetActive(visible);
            if (!visible) return;

            RefreshObjectives();
            RefreshWeaponSlots();
            if (_extractionLabel != null) _extractionLabel.text = string.Empty;
            if (_extractionDistance != null) _extractionDistance.text = string.Empty;
            if (_extractionBarRoot != null) _extractionBarRoot.gameObject.SetActive(false);
            if (_interactPrompt != null) _interactPrompt.text = string.Empty;
            if (_hitZoneText != null) _hitZoneText.text = string.Empty;
        }

        public void Tick(float deltaTime)
        {
            if (!_built || _root == null || !_visible) return;
            if (_context == null || _context.Flow == null) return;

            float unscaled = Time.unscaledDeltaTime;
            PlayerController player = _context.Player;

            TickVitals(unscaled);
            TickWeapon(player);
            TickCrosshair(unscaled);
            TickFeedback(unscaled);
            TickCompass(deltaTime);
            TickInteraction(player);
            TickMissionReadouts(deltaTime);

            TickFeed(_noticeRows, _noticeTimers, unscaled, NoticeLifetime);
            TickFeed(_killFeedRows, _killFeedTimers, unscaled, KillFeedLifetime);
        }

        private void TickVitals(float deltaTime)
        {
            float health01 = WfMath.Clamp01(_health / _maxHealth);
            // The delayed bar chases the real one so a burst of damage is readable for a moment.
            _displayedHealth = _displayedHealth > health01
                ? Mathf.MoveTowards(_displayedHealth, health01, deltaTime * 0.55f)
                : health01;

            if (_healthFill != null) _healthFill.fillAmount = health01;
            if (_healthDamageFill != null) _healthDamageFill.fillAmount = _displayedHealth;
            if (_armorFill != null) _armorFill.fillAmount = WfMath.Clamp01(_armor / _maxArmor);
            if (_healthText != null)
            {
                _healthText.text = Mathf.CeilToInt(_health) + " / " + Mathf.CeilToInt(_maxHealth);
                _healthText.color = health01 <= 0.25f ? _theme.Danger : _theme.Text;
            }

            if (_lowHealthVignette != null)
            {
                float intensity = health01 <= 0.34f ? Mathf.Lerp(0.34f, 0f, health01 / 0.34f) : 0f;
                Color color = _lowHealthVignette.color;
                color.a = intensity * 0.42f;
                _lowHealthVignette.color = color;
            }

            if (_stateText != null && _context.Player != null)
            {
                PlayerController player = _context.Player;
                string state = player.IsDead ? "DOWN" : player.IsCrouching ? "CROUCH" : player.IsSprinting ? "SPRINT" : "READY";
                _stateText.text = state + (_armor > 0.5f ? "   ARMOUR " + Mathf.CeilToInt(_armor) : "   NO ARMOUR");
            }
        }

        private void TickWeapon(PlayerController player)
        {
            WeaponModel weapon = player != null && player.Loadout != null ? player.Loadout.CurrentModel : null;
            if (weapon == null)
            {
                if (_weaponName != null) _weaponName.text = "UNARMED";
                if (_ammoText != null) _ammoText.text = "-- / --";
                _boundWeapon = null;
                return;
            }

            if (weapon != _boundWeapon) _boundWeapon = weapon;

            WeaponDef def = weapon.Def;
            int reserve = player.Loadout.ReserveAmmo(def.AmmoType);

            if (_weaponName != null)
            {
                _weaponName.text = def.DisplayName.ToUpperInvariant() + "   " + RarityRules.Name(weapon.Rarity).ToUpperInvariant() +
                                   "  MK" + weapon.UpgradeLevel;
                _weaponName.color = RarityRules.Color(weapon.Rarity);
            }

            if (_ammoText != null)
            {
                _ammoText.text = weapon.AmmoInMagazine + " / " + reserve;
                _ammoText.color = weapon.MagazineEmpty ? _theme.Danger
                    : weapon.AmmoInMagazine <= Mathf.Max(1, def.MagazineSize / 4) ? _theme.Warning
                    : _theme.Text;
            }

            bool reloading = weapon.State == WeaponState.Reloading;
            if (_reloadBar != null && _reloadBar.gameObject.activeSelf != reloading)
            {
                _reloadBar.gameObject.SetActive(reloading);
            }
            if (reloading && _reloadFill != null) _reloadFill.fillAmount = weapon.ReloadProgress01;

            if (_reloadText != null)
            {
                _reloadText.text = reloading ? "RELOADING"
                    : weapon.MagazineEmpty ? (reserve > 0 ? "PRESS R / RELOAD" : "NO AMMUNITION")
                    : string.Empty;
                _reloadText.color = weapon.MagazineEmpty && reserve <= 0 ? _theme.Danger : _theme.Warning;
            }
        }

        private void TickCrosshair(float deltaTime)
        {
            WeaponModel weapon = _boundWeapon;
            float spread = weapon != null ? weapon.CurrentSpread : 0f;
            _crosshairBloom = Mathf.Max(spread * 2.2f, _crosshairBloom - deltaTime * 34f);

            float gap = 12f + _crosshairBloom;
            if (_crosshairArms[0] != null) _crosshairArms[0].anchoredPosition = new Vector2(0f, gap);
            if (_crosshairArms[1] != null) _crosshairArms[1].anchoredPosition = new Vector2(0f, -gap);
            if (_crosshairArms[2] != null) _crosshairArms[2].anchoredPosition = new Vector2(-gap, 0f);
            if (_crosshairArms[3] != null) _crosshairArms[3].anchoredPosition = new Vector2(gap, 0f);

            bool aiming = _context.Input != null && _context.Input.AimHeld;
            Color armColor = aiming ? new Color(0.72f, 0.9f, 0.55f, 0.9f) : new Color(1f, 1f, 1f, 0.7f);
            for (int i = 0; i < _crosshairArms.Length; i++)
            {
                if (_crosshairArms[i] == null) continue;
                Image image = _crosshairArms[i].GetComponent<Image>();
                if (image != null) image.color = armColor;
            }
        }

        private void TickFeedback(float deltaTime)
        {
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer -= deltaTime;
                if (_hitMarker != null)
                {
                    Color color = _hitMarker.color;
                    color.a = Mathf.Clamp01(_hitMarkerTimer / HitMarkerLifetime);
                    _hitMarker.color = color;
                }
            }
            else if (_hitMarker != null && _hitMarker.color.a > 0f)
            {
                Color color = _hitMarker.color;
                color.a = 0f;
                _hitMarker.color = color;
            }

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= deltaTime;
                if (_damageFlash != null)
                {
                    Color color = _damageFlash.color;
                    color.a = Mathf.Clamp01(_damageFlashTimer / DamageFlashLifetime) * 0.4f;
                    _damageFlash.color = color;
                }
            }
            else if (_damageFlash != null && _damageFlash.color.a > 0f)
            {
                Color color = _damageFlash.color;
                color.a = 0f;
                _damageFlash.color = color;
            }

            if (_hitZoneText != null && _damageFlashTimer <= 0f && _hitZoneText.text.Length > 0)
            {
                _hitZoneText.text = string.Empty;
            }
        }

        private void TickCompass(float deltaTime)
        {
            if (_compassStrip == null || _camera == null) return;

            float yaw = _camera.transform.eulerAngles.y;

            if (_headingText != null)
            {
                _headingText.text = Bearing(yaw);
            }

            if (_compassTicks != null)
            {
                for (int i = 0; i < _compassTicks.childCount; i++)
                {
                    RectTransform tick = _compassTicks.GetChild(i) as RectTransform;
                    if (tick == null) continue;
                    CompassTick handle = tick.GetComponent<CompassTick>();
                    if (handle == null) continue;

                    float bearing = Mathf.DeltaAngle(yaw, handle.Degrees);
                    bool visible = Mathf.Abs(bearing) <= CompassDegrees;
                    if (tick.gameObject.activeSelf != visible) tick.gameObject.SetActive(visible);
                    if (!visible) continue;

                    tick.anchoredPosition = new Vector2(bearing / CompassDegrees * CompassHalfWidth, 0f);
                    Text tickLabel = tick.GetComponent<Text>();
                    if (tickLabel != null) tickLabel.color = Mathf.Abs(bearing) < 6f ? _theme.Text : _theme.TextDim;
                }
            }

            MissionRuntime mission = _context.Mission;
            if (mission == null)
            {
                if (_objectiveMarker != null) _objectiveMarker.gameObject.SetActive(false);
                if (_extractionMarker != null) _extractionMarker.gameObject.SetActive(false);
                return;
            }

            Vector3 origin = _camera.transform.position;
            bool primary = mission.TryGetObjectiveTarget(out Vector3 target, out string label);
            PlaceMarker(_objectiveMarker, _objectiveMarkerLabel, origin, primary ? target : Vector3.zero,
                primary ? label : null, yaw);
            PlaceMarker(_extractionMarker, _extractionMarkerLabel, origin, mission.ExtractionPosition,
                "EXFIL", yaw);
        }

        private void PlaceMarker(RectTransform marker, Text label, Vector3 origin, Vector3 target, string text, float yaw)
        {
            if (marker == null) return;

            if (string.IsNullOrEmpty(text))
            {
                if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            Vector3 delta = target - origin;
            delta.y = 0f;
            float distance = delta.magnitude;
            bool visible = distance > 12f;
            if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (!visible) return;

            float targetYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float bearing = Mathf.DeltaAngle(yaw, targetYaw);
            if (Mathf.Abs(bearing) > CompassDegrees)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            marker.anchoredPosition = new Vector2(bearing / CompassDegrees * CompassHalfWidth, 0f);
            if (label != null) label.text = text + "  " + Mathf.RoundToInt(distance) + "m";
        }

        private static string Bearing(float yaw)
        {
            float normalised = Mathf.Repeat(yaw, 360f);
            int heading = Mathf.RoundToInt(normalised / 45f) % 8;
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return names[heading];
        }

        private void TickInteraction(PlayerController player)
        {
            if (_interactPrompt == null) return;
            if (player == null)
            {
                if (_interactPrompt.text.Length > 0) _interactPrompt.text = string.Empty;
                return;
            }

            if (_interactor == null) _interactor = player.GetComponent<PlayerInteractor>();
            string prompt = _interactor != null ? _interactor.CurrentPrompt : null;

            if (string.IsNullOrEmpty(prompt))
            {
                if (_interactPrompt.text.Length > 0) _interactPrompt.text = string.Empty;
                return;
            }

            bool touch = _context.Platform != null && _context.Platform.IsTouchDevice;
            _interactPrompt.text = prompt + (touch ? string.Empty : "   [E]");
        }

        private void TickMissionReadouts(float deltaTime)
        {
            MissionRuntime mission = _context.Mission;
            if (mission == null || mission.Model == null)
            {
                if (_timerText != null) _timerText.text = string.Empty;
                if (_threatText != null) _threatText.text = string.Empty;
                return;
            }

            MissionModel model = mission.Model;

            if (_timerText != null)
            {
                float remaining = model.TimeRemaining;
                if (remaining > 0f)
                {
                    int minutes = Mathf.FloorToInt(remaining / 60f);
                    int seconds = Mathf.FloorToInt(remaining % 60f);
                    _timerText.text = "TIME  " + minutes.ToString("00") + ":" + seconds.ToString("00");
                    _timerText.color = remaining < 60f ? _theme.Danger : _theme.Warning;
                }
                else
                {
                    int minutes = Mathf.FloorToInt(model.ElapsedSeconds / 60f);
                    int seconds = Mathf.FloorToInt(model.ElapsedSeconds % 60f);
                    _timerText.text = "ELAPSED  " + minutes.ToString("00") + ":" + seconds.ToString("00");
                    _timerText.color = _theme.TextDim;
                }
            }

            if (_threatText != null)
            {
                int alive = mission.AliveEnemyCount;
                int budget = mission.Def != null ? mission.Def.EnemyBudget : 0;
                _threatText.text = alive > 0
                    ? "HOSTILES " + alive + (budget > 0 ? " / " + budget : string.Empty)
                    : "AREA CLEAR";
                _threatText.color = alive > 0 ? _theme.Danger : _theme.TextDim;
            }
        }
    }

    /// <summary>Carries the compass degree for a cardinal label so the strip can be positioned per frame.</summary>
    public class CompassTick : MonoBehaviour
    {
        public float Degrees { get; private set; }

        public void Configure(float degrees)
        {
            Degrees = degrees;
        }
    }
}
