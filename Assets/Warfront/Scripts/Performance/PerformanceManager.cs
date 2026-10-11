using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Frame pacing and adaptive quality.
    ///
    /// Measures real frame times (not guesses), keeps a rolling average plus 1% low, and steps quality
    /// down when the device cannot hold its target - which is exactly what a mid-range Android phone
    /// needs after a few minutes of sustained play. Every number the game reports is measured here.
    /// </summary>
    public class PerformanceManager
    {
        private const int SampleCount = 90;
        private const float DropThresholdSeconds = 8f;
        private const float RecoveryThresholdSeconds = 20f;

        private readonly SettingsService _settings;
        private readonly float[] _samples = new float[SampleCount];
        private int _sampleIndex;
        private int _sampleFilled;

        private float _slowTimer;
        private float _fastTimer;
        private int _downgrades;
        private float _renderScale = 1f;
        private float _sessionTime;
        private float _sessionAccumulatedTime;
        private int _sessionFrames;

        public PerformanceManager(SettingsService settings)
        {
            _settings = settings;
            _renderScale = 1f;
            AdaptiveEnabled = true;
        }

        public bool AdaptiveEnabled { get; set; }
        public float AverageFrameMs { get; private set; }
        public float WorstFrameMs { get; private set; }
        public float AverageFps { get { return AverageFrameMs <= 0.0001f ? 0f : 1000f / AverageFrameMs; } }
        public int DowngradeCount { get { return _downgrades; } }
        public float RenderScale { get { return _renderScale; } }
        public float SessionSeconds { get { return _sessionTime; } }
        public int SessionFrames { get { return _sessionFrames; } }
        public bool Throttled { get; private set; }

        /// <summary>Average FPS measured over the whole session - what the results screen reports.</summary>
        public float SessionAverageFps
        {
            get { return _sessionAccumulatedTime <= 0.0001f ? 0f : _sessionFrames / _sessionAccumulatedTime; }
        }

        public long AllocatedMemoryBytes
        {
            get { return UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(); }
        }

        public long ReservedMemoryBytes
        {
            get { return UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(); }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return;

            float frameMs = unscaledDeltaTime * 1000f;
            _samples[_sampleIndex] = frameMs;
            _sampleIndex = (_sampleIndex + 1) % SampleCount;
            if (_sampleFilled < SampleCount) _sampleFilled++;

            _sessionTime += unscaledDeltaTime;
            _sessionAccumulatedTime += unscaledDeltaTime;
            _sessionFrames++;

            float sum = 0f;
            float worst = 0f;
            for (int i = 0; i < _sampleFilled; i++)
            {
                float value = _samples[i];
                sum += value;
                if (value > worst) worst = value;
            }
            AverageFrameMs = _sampleFilled > 0 ? sum / _sampleFilled : frameMs;
            WorstFrameMs = worst;

            if (AdaptiveEnabled) UpdateAdaptive(unscaledDeltaTime);
        }

        private void UpdateAdaptive(float deltaTime)
        {
            if (_settings == null) return;

            int targetFps = Mathf.Clamp(_settings.Data.TargetFrameRate, 30, 120);
            float targetMs = 1000f / targetFps;
            bool tooSlow = AverageFrameMs > targetMs * 1.32f && _sampleFilled >= SampleCount;
            bool fastEnough = AverageFrameMs < targetMs * 0.92f && _sampleFilled >= SampleCount;

            if (tooSlow)
            {
                _slowTimer += deltaTime;
                _fastTimer = 0f;
            }
            else if (fastEnough)
            {
                _fastTimer += deltaTime;
                _slowTimer = 0f;
            }
            else
            {
                _slowTimer = Mathf.Max(0f, _slowTimer - deltaTime * 0.5f);
                _fastTimer = Mathf.Max(0f, _fastTimer - deltaTime * 0.5f);
            }

            if (_slowTimer >= DropThresholdSeconds)
            {
                _slowTimer = 0f;
                StepDown();
            }
            else if (_fastTimer >= RecoveryThresholdSeconds && _renderScale < 1f)
            {
                _fastTimer = 0f;
                _renderScale = Mathf.Min(1f, _renderScale + 0.05f);
                SettingsService.SetRenderScale(_renderScale);
            }
        }

        /// <summary>Reduces visual load one notch: first resolution, then the quality preset.</summary>
        public void StepDown()
        {
            _downgrades++;
            Throttled = true;

            if (_renderScale > 0.62f)
            {
                _renderScale = Mathf.Max(0.62f, _renderScale - 0.08f);
                SettingsService.SetRenderScale(_renderScale);
                return;
            }

            if (_settings == null) return;
            int preset = Mathf.Clamp(_settings.Data.QualityPreset, 0, 3);
            if (preset <= 0) return;
            _settings.SetQualityPreset(preset - 1);
        }

        public void Reset()
        {
            _slowTimer = 0f;
            _fastTimer = 0f;
            _downgrades = 0;
            Throttled = false;
            _renderScale = 1f;
            _sampleFilled = 0;
            _sampleIndex = 0;
            _sessionAccumulatedTime = 0f;
            _sessionFrames = 0;
            _sessionTime = 0f;
        }

        /// <summary>Rough device tier from reported hardware, used to pick a sane first-run preset.</summary>
        public static int RecommendQualityPreset()
        {
            int memoryMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            int graphicsMemory = SystemInfo.graphicsMemorySize;

            if (memoryMb >= 7000 && cores >= 8 && graphicsMemory >= 3000) return (int)QualityPreset.Ultra;
            if (memoryMb >= 4000 && cores >= 6) return (int)QualityPreset.High;
            if (memoryMb >= 2500 && cores >= 4) return (int)QualityPreset.Medium;
            return (int)QualityPreset.Low;
        }

        public string DescribeDevice()
        {
            return SystemInfo.deviceModel + " | " + SystemInfo.operatingSystem + " | " + SystemInfo.graphicsDeviceName +
                   " | " + SystemInfo.processorCount + " cores | " + SystemInfo.systemMemorySize + "MB RAM";
        }
    }
}
