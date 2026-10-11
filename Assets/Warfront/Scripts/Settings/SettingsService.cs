using System;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public enum QualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Owns player settings and applies them to the engine (quality level, frame rate, resolution
    /// scaling, audio volumes). Settings persist in the save document, and any change is reported on the
    /// event bus so the perf manager and audio service can react immediately.
    /// </summary>
    public class SettingsService
    {
        private readonly SaveService _save;

        public event Action Changed;

        public SettingsService(SaveService save)
        {
            _save = save;
            Data = save != null && save.Data != null && save.Data.Settings != null
                ? save.Data.Settings
                : new SettingsSave();
        }

        public SettingsSave Data { get; private set; }

        public void Load()
        {
            Data = _save != null && _save.Data != null && _save.Data.Settings != null
                ? _save.Data.Settings
                : new SettingsSave();
            ApplyAll();
        }

        public void ApplyAll()
        {
            ApplyQuality();
            ApplyFrameRate();
            ApplyAudio();
            Changed?.Invoke();
            GameEvents.RaiseSettingsChanged();
            if (_save != null) _save.MarkDirty();
        }

        public void ApplyQuality()
        {
            int target = Mathf.Clamp(Data.QualityPreset, 0, 3);

            // Map the four WARFRONT presets onto whatever quality levels this device exposes.
            int available = QualitySettings.names != null ? QualitySettings.names.Length : 1;
            int resolved = available <= 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(target / 3f * (available - 1)), 0, available - 1);
            QualitySettings.SetQualityLevel(resolved, true);

            bool highQualityShadows = target >= (int)QualityPreset.High;
            QualitySettings.shadowDistance = highQualityShadows ? 45f : 22f;
            QualitySettings.shadows = target == (int)QualityPreset.Low ? ShadowQuality.Disable : ShadowQuality.HardOnly;
            QualitySettings.antiAliasing = target >= (int)QualityPreset.Ultra ? 4 : target >= (int)QualityPreset.High ? 2 : 0;
            QualitySettings.vSyncCount = 0;
            QualitySettings.skinWeights = SkinWeights.OneBone;
            Application.targetFrameRate = Mathf.Clamp(Data.TargetFrameRate, 30, 120);

            // Render scale: keeps mobile fill rate under control on weaker GPUs.
            float scale = target == (int)QualityPreset.Low ? 0.78f
                : target == (int)QualityPreset.Medium ? 0.9f
                : 1f;
            if (Data.DynamicResolution && target <= (int)QualityPreset.Medium) scale -= 0.05f;
            SetRenderScale(WfMath.Clamp(scale, 0.6f, 1.2f));
        }

        public void ApplyFrameRate()
        {
            Application.targetFrameRate = Mathf.Clamp(Data.TargetFrameRate, 30, 120);
        }

        public void ApplyAudio()
        {
            AudioListener.volume = WfMath.Clamp01(Data.MasterVolume);
        }

        /// <summary>Applies URP render scale when the pipeline is available; no-op otherwise.</summary>
        public static void SetRenderScale(float scale)
        {
            try
            {
                UnityEngine.Rendering.RenderPipelineAsset asset = QualitySettings.renderPipeline;
                if (asset == null) return;

                System.Type type = asset.GetType();
                System.Reflection.PropertyInfo property = type.GetProperty("renderScale");
                if (property != null && property.CanWrite)
                {
                    property.SetValue(asset, scale);
                    return;
                }

                System.Reflection.MethodInfo setter = type.GetMethod("SetRenderScale");
                if (setter != null)
                {
                    setter.Invoke(asset, new object[] { scale });
                }
            }
            catch (Exception)
            {
                // Render scale is best effort: a missing URP asset must never break boot.
            }
        }

        public void SetQualityPreset(int preset)
        {
            Data.QualityPreset = Mathf.Clamp(preset, 0, 3);
            ApplyAll();
        }

        public void SetMasterVolume(float volume)
        {
            Data.MasterVolume = WfMath.Clamp01(volume);
            ApplyAudio();
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetMusicVolume(float volume)
        {
            Data.MusicVolume = WfMath.Clamp01(volume);
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetSfxVolume(float volume)
        {
            Data.SfxVolume = WfMath.Clamp01(volume);
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetLookSensitivity(float sensitivity)
        {
            Data.LookSensitivity = WfMath.Clamp(sensitivity, 0.2f, 4f);
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetInvertY(bool invert)
        {
            Data.InvertY = invert;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetVibration(bool enabled)
        {
            Data.Vibration = enabled;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetFrameRateCap(int fps)
        {
            Data.TargetFrameRate = Mathf.Clamp(fps, 30, 120);
            ApplyFrameRate();
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetDynamicResolution(bool enabled)
        {
            Data.DynamicResolution = enabled;
            ApplyAll();
        }

        public void SetAimAssist(bool enabled)
        {
            Data.AimAssist = enabled;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetAutoFire(bool enabled)
        {
            Data.AutoFire = enabled;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetShowDamageNumbers(bool enabled)
        {
            Data.ShowDamageNumbers = enabled;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        /// <summary>Swaps the thumbstick to the right half. The touch layer re-lays itself out on change.</summary>
        public void SetLeftHanded(bool enabled)
        {
            Data.LeftHandedControls = enabled;
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        public void SetJoystickScale(float scale)
        {
            Data.JoystickScale = WfMath.Clamp(scale, 0.7f, 1.4f);
            Changed?.Invoke();
            if (_save != null) _save.MarkDirty();
        }

        /// <summary>Restores the shipped defaults (used by the settings screen).</summary>
        public void ResetToDefaults()
        {
            SettingsSave defaults = new SettingsSave();
            if (_save != null && _save.Data != null) _save.Data.Settings = defaults;
            Data = defaults;
            ApplyAll();
        }

        public string QualityName
        {
            get
            {
                switch (Mathf.Clamp(Data.QualityPreset, 0, 3))
                {
                    case 0: return "Low";
                    case 1: return "Medium";
                    case 2: return "High";
                    default: return "Ultra";
                }
            }
        }
    }
}
