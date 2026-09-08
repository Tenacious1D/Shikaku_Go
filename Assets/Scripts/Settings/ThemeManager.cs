using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Shikaku.Settings
{
    public static class ThemeManager
    {
        public const string LightClass = "theme-light";
        public const string DarkClass = "theme-dark";

        public static event Action Changed;

        private static bool _initialized;
        private static bool _systemDark;
        private static bool _isDark;

        public static bool IsDark
        {
            get
            {
                EnsureInitialized();
                return _isDark;
            }
        }

        public static bool SystemForcesDark
        {
            get
            {
                EnsureInitialized();
                return _systemDark;
            }
        }

        public static bool DarkModePreferred
        {
            get
            {
                EnsureInitialized();
                return AppSettings.DarkModePreferred;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            AppSettings.EnsureLoaded();
            _systemDark = DetectSystemDarkMode();
            _isDark = ResolveDarkMode();
            AppSettings.Changed += HandleSettingsChanged;
            EnsureRuntimeBridge();
        }

        public static void ApplyTo(VisualElement root)
        {
            if (root == null)
                return;

            EnsureInitialized();
            ApplyClasses(root);

            // UIDocument adds an internal wrapper above the UXML content.
            // Applying the class to the visible content roots too keeps theme
            // selectors reliable regardless of where Unity attaches the sheet.
            ApplyClasses(root.Q<VisualElement>("screen-root"));
            ApplyClasses(root.Q<VisualElement>("gameplay-root"));
            ApplyClasses(root.Q<VisualElement>("gameplay-overlay-root"));
        }

        private static void ApplyClasses(VisualElement element)
        {
            if (element == null)
                return;

            element.EnableInClassList(DarkClass, _isDark);
            element.EnableInClassList(LightClass, !_isDark);
        }

        internal static void RefreshSystemTheme()
        {
            SetSystemDarkMode(DetectSystemDarkMode());
        }

        internal static void SetSystemDarkMode(bool dark)
        {
            EnsureInitialized();

            if (_systemDark == dark)
                return;

            _systemDark = dark;
            RefreshResolvedTheme(true);
        }

        private static void HandleSettingsChanged()
        {
            RefreshResolvedTheme();
        }

        private static void RefreshResolvedTheme(
    bool systemStateChanged = false)
        {
            bool resolvedDark = ResolveDarkMode();
            bool themeChanged = _isDark != resolvedDark;
            _isDark = resolvedDark;

            // Before the user has manually selected Light/Dark, changes to
            // the phone appearance should update the Settings toggle too.
            bool shouldRefreshForSystem =
                systemStateChanged &&
                !AppSettings.HasDarkModeUserSelection;

            if (themeChanged || shouldRefreshForSystem)
                Changed?.Invoke();
        }

        private static bool ResolveDarkMode()
        {
            switch (AppSettings.ThemeMode)
            {
                case AppThemeMode.Light:
                    return false;

                case AppThemeMode.Dark:
                    return true;

                default:
                    return _systemDark;
            }
        }

        private static bool DetectSystemDarkMode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidConfiguration configuration =
                AndroidApplication.currentConfiguration;
            return configuration != null &&
                configuration.uiModeNight == AndroidUIModeNight.Yes;
#elif UNITY_IOS && !UNITY_EDITOR
            return Shikaku_IsSystemDarkMode() != 0;
#else
            return false;
#endif
        }

        private static void EnsureRuntimeBridge()
        {
            const string objectName = "[Shikaku Theme]";
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
                return;

            GameObject bridgeObject = new GameObject(objectName);
            bridgeObject.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(bridgeObject);
            bridgeObject.AddComponent<ThemeRuntimeBridge>();
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Shikaku_IsSystemDarkMode();
#endif
    }

    internal sealed class ThemeRuntimeBridge : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        private float _nextRefreshTime;
#endif

        private void OnEnable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidApplication.onConfigurationChanged +=
                OnAndroidConfigurationChanged;
#endif
            ThemeManager.RefreshSystemTheme();
        }

        private void OnDisable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidApplication.onConfigurationChanged -=
                OnAndroidConfigurationChanged;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ThemeManager.RefreshSystemTheme();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void OnAndroidConfigurationChanged(
            AndroidConfiguration configuration)
        {
            ThemeManager.SetSystemDarkMode(
                configuration != null &&
                configuration.uiModeNight == AndroidUIModeNight.Yes
            );
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + 1f;
            ThemeManager.RefreshSystemTheme();
        }
#endif
    }
}
