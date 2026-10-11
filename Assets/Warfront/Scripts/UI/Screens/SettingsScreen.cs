using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Settings screen.
    ///
    /// Every control writes through SettingsService, which applies the value to the engine immediately
    /// and stores it in the save document - there is no "apply" button because there is nothing to defer.
    /// Live changes reach the running mission: render scale, frame cap, sensitivity, haptics and the
    /// touch layout all react without a reload.
    /// </summary>
    public class SettingsScreen : UIScreen
    {
        public override string Title { get { return "Settings"; } }

        private const int QualityPresetCount = 4;
        private static readonly int[] FrameCaps = { 30, 45, 60, 90, 120 };
        private static readonly string[] QualityNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        /// <summary>Pairs a slider with the numeric readout that mirrors it.</summary>
        private class SliderBinding
        {
            public Slider Slider;
            public Text Readout;
            public bool Percent;
        }

        private RectTransform _content;
        private Text _deviceText;
        private Text _performanceText;
        private Text _saveText;

        private Slider _master;
        private Slider _music;
        private Slider _sfx;
        private Slider _sensitivity;
        private Slider _stickScale;
        private Toggle _invertY;
        private Toggle _vibration;
        private Toggle _aimAssist;
        private Toggle _autoFire;
        private Toggle _leftHanded;
        private Toggle _dynamicResolution;
        private Toggle _damageNumbers;

        private readonly Button[] _qualityButtons = new Button[QualityPresetCount];
        private readonly Button[] _frameButtons = new Button[FrameCaps.Length];
        private readonly List<SliderBinding> _bindings = new List<SliderBinding>(6);

        private bool _fromPause;
        private bool _suspend;

        public void SetOrigin(bool fromPause)
        {
            _fromPause = fromPause;
            if (Root != null && Root.gameObject.activeSelf) Refresh();
        }

        protected override void OnBuild()
        {
            ScrollRect scroll = UIFactory.CreateScrollView(Content, "SettingsScroll", out _content);
            UIFactory.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 0f);

            SectionHeader("AUDIO");
            _master = AddSlider("Master volume", 0f, 1f, true, OnMaster);
            _music = AddSlider("Music volume", 0f, 1f, true, OnMusic);
            _sfx = AddSlider("Effects volume", 0f, 1f, true, OnSfx);

            SectionHeader("CONTROLS");
            _sensitivity = AddSlider("Look sensitivity", 0.3f, 3f, false, OnSensitivity);
            _stickScale = AddSlider("Touch stick size", 0.7f, 1.4f, false, OnStickScale);
            _invertY = AddToggle("Invert vertical look", OnInvertY);
            _leftHanded = AddToggle("Left handed touch layout", OnLeftHanded);
            _autoFire = AddToggle("Fire while aiming", OnAutoFire);
            _aimAssist = AddToggle("Aim assist", OnAimAssist);
            _vibration = AddToggle("Haptic feedback", OnVibration);
            _damageNumbers = AddToggle("Damage feedback", OnDamageNumbers);

            SectionHeader("GRAPHICS");
            RectTransform qualityRow = UIFactory.CreateRow(_content, "QualityRow", 72f, 10f);
            for (int i = 0; i < QualityPresetCount; i++)
            {
                int preset = i;
                Button button = UIFactory.CreateButton(qualityRow, "Quality" + i, QualityNames[i], Theme.PanelLight, null, 26);
                button.onClick.AddListener(() => OnQuality(preset));
                _qualityButtons[i] = button;
            }

            RectTransform frameRow = UIFactory.CreateRow(_content, "FrameRow", 72f, 10f);
            for (int i = 0; i < FrameCaps.Length; i++)
            {
                int cap = FrameCaps[i];
                Button button = UIFactory.CreateButton(frameRow, "Cap" + cap, cap + " FPS", Theme.PanelLight, null, 26);
                button.onClick.AddListener(() => OnFrameCap(cap));
                _frameButtons[i] = button;
            }

            _dynamicResolution = AddToggle("Dynamic render scaling", OnDynamicResolution);

            SectionHeader("DIAGNOSTICS");
            _deviceText = AddReadout("Device");
            _performanceText = AddReadout("Performance");
            _saveText = AddReadout("Save", 100f);

            RectTransform resetRow = UIFactory.CreateRow(_content, "ResetRow", 76f, 12f);
            UIFactory.CreateButton(resetRow, "ResetSettings", "RESET SETTINGS", Theme.PanelLight, OnResetSettings, 26);
            UIFactory.CreateButton(resetRow, "ResetProgress", "DELETE PROGRESS", new Color(0.34f, 0.16f, 0.15f, 0.97f), OnResetProgress, 26);
        }

        // ------------------------------------------------------------- row helpers

        private void SectionHeader(string title)
        {
            RectTransform row = UIFactory.CreateRect(_content, "Section_" + title);
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 46f;
            element.minHeight = 46f;

            Text text = UIFactory.CreateText(row, "Label", title, 26, Theme.Accent, TextAnchor.LowerLeft);
            UIFactory.Stretch(text.rectTransform, 4f, 4f, 8f, 0f);
        }

        private Slider AddSlider(string label, float min, float max, bool percent, UnityEngine.Events.UnityAction<float> onChanged)
        {
            RectTransform row = UIFactory.CreateRow(_content, "Slider_" + label, 62f, 16f);
            UIFactory.RowLabel(row, label, 25, Theme.Text, 300f);

            Slider slider = UIFactory.CreateSlider(row, "Slider", min, max, percent ? 0.85f : 1f, false, onChanged);
            LayoutElement sliderElement = slider.gameObject.AddComponent<LayoutElement>();
            sliderElement.preferredWidth = 420f;
            sliderElement.minWidth = 220f;

            Text readout = UIFactory.RowValue(row, string.Empty, 24, Theme.TextDim);

            SliderBinding binding = new SliderBinding();
            binding.Slider = slider;
            binding.Readout = readout;
            binding.Percent = percent;
            _bindings.Add(binding);

            Slider captured = slider;
            Text capturedReadout = readout;
            bool capturedPercent = percent;
            slider.onValueChanged.AddListener(value => { capturedReadout.text = Format(value, capturedPercent); });
            capturedReadout.text = Format(captured.value, capturedPercent);

            return slider;
        }

        private static string Format(float value, bool percent)
        {
            return percent ? Mathf.RoundToInt(value * 100f) + "%" : value.ToString("0.00");
        }

        private Toggle AddToggle(string label, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            RectTransform row = UIFactory.CreateRow(_content, "Toggle_" + label, 56f, 16f);
            return UIFactory.CreateToggle(row, "Toggle", label, false, onChanged);
        }

        private Text AddReadout(string id)
        {
            return AddReadout(id, 74f);
        }

        private Text AddReadout(string id, float height)
        {
            Text text = UIFactory.CreateText(_content, "Readout_" + id, string.Empty, 22, Theme.TextDim, TextAnchor.UpperLeft);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return text;
        }

        // ----------------------------------------------------------------- refresh

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Settings == null) return;

            SettingsSave data = Context.Settings.Data;
            _suspend = true;

            if (_master != null) _master.value = data.MasterVolume;
            if (_music != null) _music.value = data.MusicVolume;
            if (_sfx != null) _sfx.value = data.SfxVolume;
            if (_sensitivity != null) _sensitivity.value = data.LookSensitivity;
            if (_stickScale != null) _stickScale.value = data.JoystickScale;
            if (_invertY != null) _invertY.isOn = data.InvertY;
            if (_leftHanded != null) _leftHanded.isOn = data.LeftHandedControls;
            if (_autoFire != null) _autoFire.isOn = data.AutoFire;
            if (_aimAssist != null) _aimAssist.isOn = data.AimAssist;
            if (_vibration != null) _vibration.isOn = data.Vibration;
            if (_damageNumbers != null) _damageNumbers.isOn = data.ShowDamageNumbers;
            if (_dynamicResolution != null) _dynamicResolution.isOn = data.DynamicResolution;

            for (int i = 0; i < _qualityButtons.Length; i++)
            {
                Paint(_qualityButtons[i], i == Mathf.Clamp(data.QualityPreset, 0, QualityPresetCount - 1));
            }
            for (int i = 0; i < _frameButtons.Length; i++)
            {
                Paint(_frameButtons[i], FrameCaps[i] == data.TargetFrameRate);
            }

            _suspend = false;

            SettleReadouts();
            RefreshDiagnostics();

            SetHeaderSubtitle(_fromPause
                ? "Changes apply instantly. Back returns to the paused mission."
                : "Changes apply instantly and are stored in your save file.");
        }

        /// <summary>Pushes every slider readout back in sync with its slider.</summary>
        private void SettleReadouts()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                SliderBinding binding = _bindings[i];
                if (binding == null || binding.Slider == null || binding.Readout == null) continue;
                binding.Readout.text = Format(binding.Slider.value, binding.Percent);
            }
        }

        private void RefreshDiagnostics()
        {
            if (_deviceText != null)
            {
                _deviceText.text =
                    "DEVICE      " + (Context.Performance != null ? Context.Performance.DescribeDevice() : SystemInfo.deviceModel) + "\n" +
                    "SCREEN      " + Screen.width + "x" + Screen.height + "   safe area " +
                    Mathf.RoundToInt(Screen.safeArea.width) + "x" + Mathf.RoundToInt(Screen.safeArea.height) +
                    (Context.Platform != null && Context.Platform.IsTablet ? "   tablet" : string.Empty) +
                    (Context.Platform != null && Context.Platform.IsWide ? "   ultrawide" : string.Empty) +
                    (Context.Platform != null && Context.Platform.IsTouchDevice ? "   touch" : "   mouse and keyboard");
            }

            if (_performanceText != null && Context.Performance != null)
            {
                PerformanceManager performance = Context.Performance;
                _performanceText.text =
                    "FRAME RATE  " + performance.AverageFps.ToString("0.0") + " fps average   " +
                    performance.AverageFrameMs.ToString("0.00") + " ms   peak " + performance.WorstFrameMs.ToString("0.0") + " ms\n" +
                    "SESSION     " + performance.SessionAverageFps.ToString("0.0") + " fps over " +
                    performance.SessionSeconds.ToString("0") + "s   quality step-downs " + performance.DowngradeCount +
                    "   render scale " + performance.RenderScale.ToString("0.00") + (performance.Throttled ? "   THERMAL LIMIT" : string.Empty) + "\n" +
                    "MEMORY      " + (performance.AllocatedMemoryBytes / (1024 * 1024)) + " MB allocated   " +
                    (performance.ReservedMemoryBytes / (1024 * 1024)) + " MB reserved   preset " + Context.Settings.QualityName;
            }

            if (_saveText != null && Context.Save != null)
            {
                _saveText.text =
                    "SAVE        " + (Context.Save.FileSizeBytes / 1024) + " KB   schema v" + Context.Save.Data.SchemaVersion +
                    "   writes " + Context.Save.SaveCount + "\n" +
                    "LAST LOAD   " + Context.Save.LastLoad.Status +
                    (string.IsNullOrEmpty(Context.Save.LastLoad.Message) ? string.Empty : "  -  " + Context.Save.LastLoad.Message) + "\n" +
                    "LOCATION    " + Context.Save.SaveDirectory;
            }
        }

        private static void Paint(Button button, bool selected)
        {
            if (button == null) return;
            Image image = button.targetGraphic as Image;
            if (image != null) image.color = selected ? new Color(0.30f, 0.44f, 0.22f, 0.98f) : new Color(0.14f, 0.15f, 0.15f, 0.97f);
        }

        // ----------------------------------------------------------------- actions

        private bool Blocked { get { return _suspend || Context == null || Context.Settings == null; } }

        private void OnMaster(float value)
        {
            if (Blocked) return;
            Context.Settings.SetMasterVolume(value);
            Context.Audio.ApplyVolumes();
        }

        private void OnMusic(float value)
        {
            if (Blocked) return;
            Context.Settings.SetMusicVolume(value);
            Context.Audio.ApplyVolumes();
        }

        private void OnSfx(float value)
        {
            if (Blocked) return;
            Context.Settings.SetSfxVolume(value);
            Context.Audio.ApplyVolumes();
        }

        private void OnSensitivity(float value)
        {
            if (Blocked) return;
            Context.Settings.SetLookSensitivity(value);
        }

        private void OnStickScale(float value)
        {
            if (Blocked) return;
            Context.Settings.SetJoystickScale(value);
            if (Context.Mobile != null) Context.Mobile.RefreshLayout();
        }

        private void OnInvertY(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetInvertY(value);
        }

        private void OnLeftHanded(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetLeftHanded(value);
            if (Context.Mobile != null) Context.Mobile.RefreshLayout();
        }

        private void OnAutoFire(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetAutoFire(value);
        }

        private void OnAimAssist(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetAimAssist(value);
        }

        private void OnVibration(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetVibration(value);
            if (value && Context.Haptics != null) Context.Haptics.HitConfirm();
        }

        private void OnDamageNumbers(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetShowDamageNumbers(value);
        }

        private void OnDynamicResolution(bool value)
        {
            if (Blocked) return;
            Context.Settings.SetDynamicResolution(value);
        }

        private void OnQuality(int preset)
        {
            if (Blocked) return;
            Context.Settings.SetQualityPreset(preset);
            Context.Audio.PlayUi(AudioCue.Click);
            Refresh();
        }

        private void OnFrameCap(int cap)
        {
            if (Blocked) return;
            Context.Settings.SetFrameRateCap(cap);
            Context.Audio.PlayUi(AudioCue.Click);
            Refresh();
        }

        private void OnResetSettings()
        {
            if (Context == null || Context.Settings == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            Context.Settings.ResetToDefaults();
            if (Context.Mobile != null) Context.Mobile.RefreshLayout();
            Refresh();
            GameEvents.RaiseNotice("Settings restored to defaults");
        }

        private void OnResetProgress()
        {
            if (Context == null) return;
            Context.Audio.PlayUi(AudioCue.Deny);
            Context.ResetProfile();
            GameEvents.RaiseNotice("Profile deleted - a new campaign starts now");
            if (Context.Flow != null) Context.Flow.EnterMainMenu();
        }

        protected override void OnBackPressed()
        {
            if (Context == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            if (_fromPause) Context.Flow.Resume();
            else Context.Flow.EnterMainMenu();
        }
    }
}
