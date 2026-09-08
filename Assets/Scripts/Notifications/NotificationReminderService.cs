using System;
using System.Collections;
using System.Collections.Generic;
using Shikaku.Menu;
using Shikaku.Services;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Notifications.iOS;
#endif

namespace Shikaku.Notifications
{
    public enum ReminderPermissionState
    {
        Unsupported,
        NotRequested,
        Requesting,
        Allowed,
        Denied,
        DeniedPermanently
    }

    /// <summary>
    /// Owns Shikaku Go's optional, locally scheduled mobile reminders.
    /// Reminders are deliberately quiet, inexact, and limited to one per day.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NotificationReminderService : MonoBehaviour
    {
        private const int ReminderMinutes = 18 * 60;
        private const int MaxScheduledReminderCount = 2;
        private const int NotificationSlotCount = 7;
        private const int NotificationIdBase = 74100;

        private static readonly TimeSpan InactivityLimit =
            TimeSpan.FromHours(66);

        private const string EnabledKey =
            "settings_notifications_enabled";

        private const string LastHandledIntentKey =
            "notifications_last_handled_intent";

        private const string ChannelId = "puzzle_reminders_v1";
        private const string SmallIconId = "shikaku_notification";
        private const string ReminderIntentData =
            "shikaku://daily-reminder";

        private const string IOSNotificationIdentifierPrefix =
            "shikaku_daily_reminder_";

        private static readonly string[] ReminderMessages =
        {
            "Keep your streak glowing - solve one puzzle today.",
            "Give your brain a satisfying little puzzle.",
            "A fresh Shikaku challenge is waiting.",
            "Take a quiet puzzle break.",
            "Your Shikaku streak is waiting."
        };

        private static NotificationReminderService _instance;

        private Coroutine _permissionRoutine;
        private Action<bool> _permissionCompletion;
        private DateTime _lastOpenedUtc;

        public static event Action StateChanged;
        public static event Action ReminderOpened;

        private static void RaiseReminderOpened()
        {
            ReminderOpened?.Invoke();
        }

        public static bool IsSupported
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsEnabled =>
            PlayerPrefs.GetInt(EnabledKey, 0) == 1;

        public static ReminderPermissionState PermissionState
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_instance != null &&
                    _instance._permissionRoutine != null)
                {
                    return ReminderPermissionState.Requesting;
                }

                switch (AndroidNotificationCenter.UserPermissionToPost)
                {
                    case PermissionStatus.Allowed:
                        return ReminderPermissionState.Allowed;
                    case PermissionStatus.Denied:
                        return ReminderPermissionState.Denied;
                    case PermissionStatus.DeniedDontAskAgain:
                        return ReminderPermissionState.DeniedPermanently;
                    case PermissionStatus.RequestPending:
                        return ReminderPermissionState.Requesting;
                    default:
                        return ReminderPermissionState.NotRequested;
                }
#elif UNITY_IOS && !UNITY_EDITOR
                if (_instance != null &&
                    _instance._permissionRoutine != null)
                {
                    return ReminderPermissionState.Requesting;
                }

                return GetIOSPermissionState();
#else
                return ReminderPermissionState.Unsupported;
#endif
            }
        }

        public static bool IsSystemAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (PermissionState != ReminderPermissionState.Allowed)
                    return false;

                EnsureInstance();
                return _instance != null &&
                    _instance.IsReminderChannelEnabled();
#elif UNITY_IOS && !UNITY_EDITOR
                return PermissionState == ReminderPermissionState.Allowed &&
                    IsIOSNotificationDeliveryEnabled();
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void RequestEnable(Action<bool> completed)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            EnsureInstance();

            if (_instance == null)
            {
                completed?.Invoke(false);
                return;
            }

            if (PermissionState == ReminderPermissionState.Allowed)
            {
                SetEnabledPreference(true);
                _instance.RebuildSchedule();
                StateChanged?.Invoke();
                completed?.Invoke(true);
                return;
            }

            if (PermissionState ==
                ReminderPermissionState.DeniedPermanently)
            {
                SetEnabledPreference(false);
                StateChanged?.Invoke();
                completed?.Invoke(false);
                return;
            }

            _instance.BeginPermissionRequest(completed);
#else
            SetEnabledPreference(false);
            StateChanged?.Invoke();
            completed?.Invoke(false);
#endif
        }

        public static void Disable()
        {
            SetEnabledPreference(false);

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            EnsureInstance();
            _instance?.CancelReminderNotifications();
#endif

            StateChanged?.Invoke();
        }

        public static void OpenSystemNotificationSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureInstance();
            AndroidNotificationCenter.OpenNotificationSettings(ChannelId);
#elif UNITY_IOS && !UNITY_EDITOR
            OpenIOSNotificationSettings();
#endif
        }

        private static void EnsureInstance()
        {
            if (_instance != null || !Application.isPlaying)
                return;

            _instance = FindFirstObjectByType<NotificationReminderService>();
            if (_instance != null)
                return;

            var serviceObject =
                new GameObject("NotificationReminderService");
            DontDestroyOnLoad(serviceObject);
            _instance =
                serviceObject.AddComponent<NotificationReminderService>();
        }

        private static void SetEnabledPreference(bool enabled)
        {
            PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            DailyStreakService.PuzzleCompleted +=
                OnAnyPuzzleCompleted;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            MarkOpenedNow();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidNotifications();
            HandleNotificationIntent();
            RebuildSchedule();
#elif UNITY_IOS && !UNITY_EDITOR
            HandleNotificationIntent();
            RebuildSchedule();
#endif
        }

        private void OnDestroy()
        {
            DailyStreakService.PuzzleCompleted -=
                OnAnyPuzzleCompleted;

            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (paused)
            {
                RebuildSchedule();
            }
            else
            {
                MarkOpenedNow();
                HandleNotificationIntent();
                RebuildSchedule();
                StateChanged?.Invoke();
            }
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!focused)
                return;

            MarkOpenedNow();
            HandleNotificationIntent();
            RebuildSchedule();
            StateChanged?.Invoke();
#endif
        }

        private void OnAnyPuzzleCompleted()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            RebuildSchedule();
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void MarkOpenedNow()
        {
            _lastOpenedUtc = DateTime.UtcNow;
        }
#endif

        private static DateTime[] BuildReminderFireTimes(
            DateTime now,
            DateTime cutoffUtc,
            bool completedToday)
        {
            var fireTimes = new List<DateTime>(
                MaxScheduledReminderCount);

            for (int dayOffset = 0;
                 fireTimes.Count < MaxScheduledReminderCount &&
                 dayOffset <= NotificationSlotCount;
                 dayOffset++)
            {
                DateTime date = now.Date.AddDays(dayOffset);

                if (dayOffset == 0 && completedToday)
                    continue;

                DateTime fireTime =
                    date.AddMinutes(ReminderMinutes);

                if (fireTime <= now)
                    continue;

                if (fireTime.ToUniversalTime() > cutoffUtc)
                    break;

                fireTimes.Add(fireTime);
            }

            return fireTimes.ToArray();
        }

        private static string GetReminderMessage(DateTime date)
        {
            return ReminderMessages[
                Math.Abs(date.DayOfYear + date.Year * 397) %
                ReminderMessages.Length];
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeAndroidNotifications()
        {
            AndroidNotificationCenter.Initialize();

            var channel = new AndroidNotificationChannel(
                ChannelId,
                "Notifications",
                "Optional daily reminders to play a Shikaku puzzle.",
                Importance.Low)
            {
                CanBypassDnd = false,
                CanShowBadge = false,
                EnableLights = false,
                EnableVibration = false,
                LockScreenVisibility = LockScreenVisibility.Public
            };

            AndroidNotificationCenter.RegisterNotificationChannel(channel);
        }

        private bool IsReminderChannelEnabled()
        {
            try
            {
                AndroidNotificationChannel channel =
                    AndroidNotificationCenter.GetNotificationChannel(
                        ChannelId);

                return string.IsNullOrEmpty(channel.Id) || channel.Enabled;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "NotificationReminderService: Could not read " +
                    $"notification channel state. {exception.Message}");
                return true;
            }
        }

        private void BeginPermissionRequest(Action<bool> completed)
        {
            if (_permissionRoutine != null)
            {
                _permissionCompletion += completed;
                return;
            }

            _permissionCompletion = completed;
            _permissionRoutine =
                StartCoroutine(RequestPermissionRoutine());
            StateChanged?.Invoke();
        }

        private IEnumerator RequestPermissionRoutine()
        {
            var request = new PermissionRequest();

            while (request.Status == PermissionStatus.RequestPending)
                yield return null;

            bool allowed =
                request.Status == PermissionStatus.Allowed;

            SetEnabledPreference(allowed);
            _permissionRoutine = null;

            if (allowed)
                RebuildSchedule();
            else
                CancelReminderNotifications();

            StateChanged?.Invoke();

            Action<bool> completion = _permissionCompletion;
            _permissionCompletion = null;
            completion?.Invoke(allowed);
        }

        private void RebuildSchedule()
        {
            InitializeAndroidNotifications();
            CancelReminderNotifications();

            if (!IsEnabled ||
                PermissionState != ReminderPermissionState.Allowed ||
                !IsReminderChannelEnabled())
            {
                return;
            }

            DateTime now = DateTime.Now;
            DateTime cutoffUtc =
                (_lastOpenedUtc == default
                    ? DateTime.UtcNow
                    : _lastOpenedUtc) + InactivityLimit;
            DateTime[] fireTimes = BuildReminderFireTimes(
                now,
                cutoffUtc,
                DailyStreakService.HasCompletedToday);

            for (int scheduled = 0;
                 scheduled < fireTimes.Length;
                 scheduled++)
            {
                DateTime fireTime = fireTimes[scheduled];
                string message = GetReminderMessage(fireTime.Date);

                var notification = new AndroidNotification(
                    "Shikaku Go",
                    message,
                    fireTime)
                {
                    SmallIcon = SmallIconId,
                    Color = new Color32(58, 139, 69, 255),
                    IntentData = ReminderIntentData,
                    ShouldAutoCancel = true,
                    ShowInForeground = false,
                    ShowTimestamp = true
                };

                AndroidNotificationCenter
                    .SendNotificationWithExplicitID(
                        notification,
                        ChannelId,
                        NotificationIdBase + scheduled);

            }

            Debug.Log(
                "NotificationReminderService: Scheduled " +
                $"{fireTimes.Length} reminder(s) at " +
                DateTime.Today
                    .AddMinutes(ReminderMinutes)
                    .ToString("h:mm tt") +
                " within 48 hours of the last app open.");
        }

        private void CancelReminderNotifications()
        {
            for (int i = 0; i < NotificationSlotCount; i++)
            {
                AndroidNotificationCenter.CancelNotification(
                    NotificationIdBase + i);
            }
        }

        private void HandleNotificationIntent()
        {
            AndroidNotificationIntentData intent;

            try
            {
                intent =
                    AndroidNotificationCenter
                        .GetLastNotificationIntent();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "NotificationReminderService: Could not read " +
                    $"notification launch data. {exception.Message}");
                return;
            }

            if (intent == null ||
                intent.Notification.IntentData != ReminderIntentData)
            {
                return;
            }

            string signature =
                $"{intent.Id}:{intent.Notification.FireTime.Ticks}";

            if (PlayerPrefs.GetString(
                    LastHandledIntentKey,
                    string.Empty) == signature)
            {
                return;
            }

            PlayerPrefs.SetString(
                LastHandledIntentKey,
                signature);
            PlayerPrefs.SetString(
                HomeScreenController.StartScreenKey,
                HomeScreenController.DailyScreenId);
            PlayerPrefs.Save();

            RaiseReminderOpened();
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void
            Shikaku_OpenNotificationSettings();

        private static ReminderPermissionState
            GetIOSPermissionState()
        {
            AuthorizationStatus status =
                iOSNotificationCenter
                    .GetNotificationSettings()
                    .AuthorizationStatus;

            switch (status)
            {
                case AuthorizationStatus.Authorized:
                case AuthorizationStatus.Provisional:
                case AuthorizationStatus.Ephemeral:
                    return ReminderPermissionState.Allowed;
                case AuthorizationStatus.Denied:
                    return ReminderPermissionState.DeniedPermanently;
                default:
                    return ReminderPermissionState.NotRequested;
            }
        }

        private static bool IsIOSNotificationDeliveryEnabled()
        {
            AuthorizationStatus status =
                iOSNotificationCenter
                    .GetNotificationSettings()
                    .AuthorizationStatus;

            return status == AuthorizationStatus.Authorized ||
                   status == AuthorizationStatus.Provisional ||
                   status == AuthorizationStatus.Ephemeral;
        }

        private static void OpenIOSNotificationSettings()
        {
            Shikaku_OpenNotificationSettings();
        }

        private void BeginPermissionRequest(Action<bool> completed)
        {
            if (_permissionRoutine != null)
            {
                _permissionCompletion += completed;
                return;
            }

            _permissionCompletion = completed;
            _permissionRoutine =
                StartCoroutine(RequestPermissionRoutine());
            StateChanged?.Invoke();
        }

        private IEnumerator RequestPermissionRoutine()
        {
            using (var request = new AuthorizationRequest(
                       AuthorizationOption.Alert,
                       registerForRemoteNotifications: false))
            {
                while (!request.IsFinished)
                    yield return null;

                bool allowed = request.Granted;

                if (!string.IsNullOrEmpty(request.Error))
                {
                    Debug.LogWarning(
                        "NotificationReminderService: iOS " +
                        "authorization request failed. " +
                        request.Error);
                }

                SetEnabledPreference(allowed);
                _permissionRoutine = null;

                if (allowed)
                    RebuildSchedule();
                else
                    CancelReminderNotifications();

                StateChanged?.Invoke();

                Action<bool> completion = _permissionCompletion;
                _permissionCompletion = null;
                completion?.Invoke(allowed);
            }
        }

        private void RebuildSchedule()
        {
            CancelReminderNotifications();

            if (!IsEnabled ||
                PermissionState != ReminderPermissionState.Allowed ||
                !IsIOSNotificationDeliveryEnabled())
            {
                return;
            }

            DateTime now = DateTime.Now;
            DateTime cutoffUtc =
                (_lastOpenedUtc == default
                    ? DateTime.UtcNow
                    : _lastOpenedUtc) + InactivityLimit;
            DateTime[] fireTimes = BuildReminderFireTimes(
                now,
                cutoffUtc,
                DailyStreakService.HasCompletedToday);

            for (int scheduled = 0;
                 scheduled < fireTimes.Length;
                 scheduled++)
            {
                DateTime fireTime = fireTimes[scheduled];
                var notification = new iOSNotification(
                    IOSNotificationIdentifierPrefix + scheduled)
                {
                    Title = "Shikaku Go",
                    Body = GetReminderMessage(fireTime.Date),
                    Data = ReminderIntentData + "|" +
                        fireTime.ToUniversalTime().Ticks,
                    ShowInForeground = false,
                    ForegroundPresentationOption =
                        PresentationOption.None,
                    Trigger = new iOSNotificationCalendarTrigger
                    {
                        Year = fireTime.Year,
                        Month = fireTime.Month,
                        Day = fireTime.Day,
                        Hour = fireTime.Hour,
                        Minute = fireTime.Minute,
                        Second = 0,
                        UtcTime = false,
                        Repeats = false
                    }
                };

                iOSNotificationCenter.ScheduleNotification(
                    notification);
            }

            Debug.Log(
                "NotificationReminderService: Scheduled " +
                $"{fireTimes.Length} quiet iOS reminder(s) at " +
                DateTime.Today
                    .AddMinutes(ReminderMinutes)
                    .ToString("h:mm tt") +
                " within 48 hours of the last app open.");
        }

        private void CancelReminderNotifications()
        {
            for (int i = 0; i < NotificationSlotCount; i++)
            {
                string identifier =
                    IOSNotificationIdentifierPrefix + i;
                iOSNotificationCenter.RemoveScheduledNotification(
                    identifier);
                iOSNotificationCenter.RemoveDeliveredNotification(
                    identifier);
            }
        }

        private void HandleNotificationIntent()
        {
            StartCoroutine(QueryIOSNotificationIntent());
        }

        private IEnumerator QueryIOSNotificationIntent()
        {
            var query =
                iOSNotificationCenter.QueryLastRespondedNotification();
            yield return query;

            if (query.State !=
                QueryLastRespondedNotificationState
                    .HaveRespondedNotification)
            {
                yield break;
            }

            iOSNotification notification = query.Notification;
            if (notification == null ||
                string.IsNullOrEmpty(notification.Data) ||
                !notification.Data.StartsWith(
                    ReminderIntentData + "|",
                    StringComparison.Ordinal))
            {
                yield break;
            }

            string signature =
                notification.Identifier + ":" + notification.Data;

            if (PlayerPrefs.GetString(
                    LastHandledIntentKey,
                    string.Empty) == signature)
            {
                yield break;
            }

            PlayerPrefs.SetString(
                LastHandledIntentKey,
                signature);
            PlayerPrefs.SetString(
                HomeScreenController.StartScreenKey,
                HomeScreenController.DailyScreenId);
            PlayerPrefs.Save();

            RaiseReminderOpened();
        }
#endif
    }
}
