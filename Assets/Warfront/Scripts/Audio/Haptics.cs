using UnityEngine;

namespace Warfront.Game
{
    /// <summary>
    /// Haptic feedback. Android devices vibrate via Handheld.Vibrate (no permission required, no
    /// AndroidJavaObject dependency). Rate limited and settings gated so a firefight cannot turn the
    /// phone into a rattle and the whole feature can be switched off in Settings.
    /// </summary>
    public class Haptics
    {
        private SettingsService _settings;
        private float _lastPulse;
        private float _lastDamagePulse;
        private float _intensityScale = 1f;

        public Haptics(SettingsService settings)
        {
            _settings = settings;
        }

        public bool Supported
        {
            get { return Application.isMobilePlatform; }
        }

        public bool Enabled
        {
            get { return _settings == null || _settings.Data.Vibration; }
        }

        private bool CanPulse(float minimumInterval)
        {
            if (!Enabled || !Supported) return false;
            return Time.unscaledTime - _lastPulse >= minimumInterval;
        }

        private void Pulse(float minimumInterval)
        {
            _lastPulse = Time.unscaledTime;
            Handheld.Vibrate();
        }

        /// <summary>Short tick on weapon fire. Deliberately infrequent on long bursts.</summary>
        public void Fire()
        {
            if (_intensityScale <= 0.05f) return;
            if (!CanPulse(0.09f)) return;
            Pulse(0.09f);
        }

        public void HitConfirm()
        {
            if (!CanPulse(0.12f)) return;
            Pulse(0.12f);
        }

        public void Damage(float amount)
        {
            if (!Enabled || !Supported) return;
            if (Time.unscaledTime - _lastDamagePulse < 0.25f) return;
            _lastDamagePulse = Time.unscaledTime;
            Handheld.Vibrate();
        }

        public void ExtractionPulse()
        {
            if (!CanPulse(0.6f)) return;
            Pulse(0.6f);
        }

        public void Objective()
        {
            if (!CanPulse(0.3f)) return;
            Pulse(0.3f);
        }

        /// <summary>Reduces haptic intensity when the device is thermally throttled.</summary>
        public void SetIntensityScale(float scale)
        {
            _intensityScale = Mathf.Clamp01(scale);
        }
    }
}
