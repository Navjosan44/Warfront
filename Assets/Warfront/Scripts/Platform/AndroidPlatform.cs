using UnityEngine;

namespace Warfront.Game
{
    /// <summary>
    /// Platform glue for Android (and safe no-ops elsewhere).
    ///
    /// WARFRONT needs no Android permissions at all: no network, no storage access beyond the app's own
    /// sandbox, no sensors. This class only handles display behaviour, the hardware back button and
    /// device capability detection.
    /// </summary>
    public class AndroidPlatform
    {
        public AndroidPlatform()
        {
            IsAndroid = Application.platform == RuntimePlatform.Android;
        }

        public bool IsAndroid { get; private set; }

        /// <summary>True when the UI should show touch controls.</summary>
        public bool IsTouchDevice
        {
            get
            {
                if (Application.isMobilePlatform) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.touchSupported;
#elif ENABLE_INPUT_SYSTEM
                return UnityEngine.InputSystem.Touchscreen.current != null;
#else
                return false;
#endif
            }
        }

        public Rect SafeArea { get { return Screen.safeArea; } }
        public float AspectRatio { get { return Screen.height > 0 ? (float)Screen.width / Screen.height : 1.777f; } }
        public bool IsTablet { get { return IsTouchDevice && Mathf.Min(Screen.width, Screen.height) >= 900; } }
        public bool IsWide { get { return AspectRatio > 2.05f; } }

        public void Configure()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = false;

            // A third person shooter needs landscape: restrict rotation to the two landscape modes.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            if (IsAndroid) Screen.fullScreen = true;
        }

        /// <summary>
        /// Reads the hardware back button. Uses the legacy Input Manager when it is available because
        /// the new Input System does not expose the Android back key on every device.
        /// </summary>
        public bool BackButtonPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        /// <summary>Refresh rate the display reports, used to pick a sensible default frame cap.</summary>
        public int RecommendedFrameCap()
        {
            int refresh = Screen.currentResolution.refreshRate;
            if (refresh <= 0) refresh = 60;
            if (refresh >= 120) return 120;
            if (refresh >= 90) return 60;
            return Mathf.Clamp(refresh, 30, 60);
        }

        public string Describe()
        {
            int refresh = Screen.currentResolution.refreshRate;
            return "Device: " + SystemInfo.deviceModel +
                   "\nOS: " + SystemInfo.operatingSystem +
                   "\nGPU: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")" +
                   "\nCPU: " + SystemInfo.processorType + " x" + SystemInfo.processorCount +
                   "\nMemory: " + SystemInfo.systemMemorySize + " MB" +
                   "\nScreen: " + Screen.width + "x" + Screen.height + " @ " + refresh + "Hz" +
                   "\nSafe area: " + Screen.safeArea;
        }
    }
}
