using System;
using UnityEngine;

namespace Shikaku.Settings
{
    public enum AppThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    public static class AppSettings
    {
        private const string KEY_COLOR_LABELS = "settings_color_labels";
        private const string KEY_MASTER_VOLUME = "settings_master_volume"; // 0..10
        private const string KEY_VIBRATION = "settings_vibration";
        private const string KEY_REDUCE_MOTION = "settings_reduce_motion";
        private const string KEY_DARK_MODE_PREFERENCE =
            "settings_dark_mode_preference";
        private const string KEY_DARK_MODE_USER_SELECTED =
            "settings_dark_mode_user_selected";
        private const string LEGACY_KEY_THEME_PREFERENCE =
            "settings_theme_preference";
        private const int LEGACY_THEME_DARK_VALUE = 2;

        public static event Action Changed;

        private static bool _loaded;
        private static bool _colorLabels;
        private static int _masterVolume;
        private static bool _vibrationEnabled;
        private static bool _reduceMotion;
        private static bool _darkModePreferred;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _colorLabels = PlayerPrefs.GetInt(KEY_COLOR_LABELS, 0) == 1;

            _masterVolume = Mathf.Clamp(
                PlayerPrefs.GetInt(KEY_MASTER_VOLUME, 10),
                0,
                10
            );
            ApplyMasterVolume(_masterVolume);

            _vibrationEnabled =
                PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
            _reduceMotion =
                PlayerPrefs.GetInt(KEY_REDUCE_MOTION, 0) == 1;

            if (PlayerPrefs.HasKey(KEY_DARK_MODE_PREFERENCE))
            {
                _darkModePreferred =
                    PlayerPrefs.GetInt(KEY_DARK_MODE_PREFERENCE, 0) == 1;
            }
            else
            {
                // Migrate the previous System / Light / Dark control.
                // Dark remains on; Light and System become the light-mode
                // preference when the phone is not forcing dark mode.
                int legacyTheme = PlayerPrefs.GetInt(
                    LEGACY_KEY_THEME_PREFERENCE,
                    0
                );
                _darkModePreferred =
                    legacyTheme == LEGACY_THEME_DARK_VALUE;
                PlayerPrefs.SetInt(
                    KEY_DARK_MODE_PREFERENCE,
                    _darkModePreferred ? 1 : 0
                );
                PlayerPrefs.Save();
            }
        }

        public static bool ColorLabels
        {
            get { EnsureLoaded(); return _colorLabels; }
        }

        public static void SetColorLabels(bool on)
        {
            EnsureLoaded();
            if (_colorLabels == on) return;

            _colorLabels = on;
            PlayerPrefs.SetInt(KEY_COLOR_LABELS, on ? 1 : 0);
            PlayerPrefs.Save();

            Changed?.Invoke();
        }

        public static int MasterVolume
        {
            get { EnsureLoaded(); return _masterVolume; }
        }

        public static void SetMasterVolume(int value)
        {
            EnsureLoaded();

            int v = Mathf.Clamp(value, 0, 10);
            if (_masterVolume == v) return;

            _masterVolume = v;
            PlayerPrefs.SetInt(KEY_MASTER_VOLUME, v);
            PlayerPrefs.Save();

            ApplyMasterVolume(v);
            Changed?.Invoke();
        }

        private static void ApplyMasterVolume(int v)
        {
            AudioListener.volume = v / 10f;
        }

        public static bool VibrationEnabled
        {
            get
            {
                EnsureLoaded();
                return _vibrationEnabled;
            }
        }

        public static void SetVibrationEnabled(bool on)
        {
            EnsureLoaded();

            if (_vibrationEnabled == on)
                return;

            _vibrationEnabled = on;

            PlayerPrefs.SetInt(KEY_VIBRATION, on ? 1 : 0);
            PlayerPrefs.Save();

            Changed?.Invoke();
        }

        public static bool ReduceMotion
        {
            get
            {
                EnsureLoaded();
                return _reduceMotion;
            }
        }

        public static void SetReduceMotion(bool on)
        {
            EnsureLoaded();
            if (_reduceMotion == on)
                return;

            _reduceMotion = on;
            PlayerPrefs.SetInt(KEY_REDUCE_MOTION, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool DarkModePreferred
        {
            get
            {
                EnsureLoaded();
                return _darkModePreferred;
            }
        }

        public static bool HasDarkModeUserSelection
        {
            get
            {
                EnsureLoaded();
                return PlayerPrefs.GetInt(
                    KEY_DARK_MODE_USER_SELECTED,
                    0
                ) == 1;
            }
        }

        public static AppThemeMode ThemeMode
        {
            get
            {
                EnsureLoaded();

                if (!HasDarkModeUserSelection)
                    return AppThemeMode.System;

                return _darkModePreferred
                    ? AppThemeMode.Dark
                    : AppThemeMode.Light;
            }
        }

        public static void SetThemeMode(AppThemeMode mode)
        {
            EnsureLoaded();

            if (mode != AppThemeMode.System &&
                mode != AppThemeMode.Light &&
                mode != AppThemeMode.Dark)
                mode = AppThemeMode.System;

            AppThemeMode previousMode = ThemeMode;

            if (mode == AppThemeMode.System)
            {
                PlayerPrefs.SetInt(KEY_DARK_MODE_USER_SELECTED, 0);
            }
            else
            {
                _darkModePreferred = mode == AppThemeMode.Dark;
                PlayerPrefs.SetInt(
                    KEY_DARK_MODE_PREFERENCE,
                    _darkModePreferred ? 1 : 0
                );
                PlayerPrefs.SetInt(KEY_DARK_MODE_USER_SELECTED, 1);
            }

            PlayerPrefs.Save();

            if (previousMode != mode)
                Changed?.Invoke();
        }

        public static void SetDarkModePreferred(bool on)
        {
            SetThemeMode(on ? AppThemeMode.Dark : AppThemeMode.Light);
        }
    }
}
