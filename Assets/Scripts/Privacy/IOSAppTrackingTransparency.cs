using System;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Shikaku.Privacy
{
    public enum IOSATTAuthorizationStatus
    {
        Unavailable = -1,
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3
    }

    /// <summary>
    /// Thin Unity bridge for Apple's AppTrackingTransparency framework.
    /// Call only after regulatory consent and before initializing ad SDKs.
    /// </summary>
    public sealed class IOSAppTrackingTransparency : MonoBehaviour
    {
        private const string BridgeObjectName =
            "ShikakuATTBridge";

        private static IOSAppTrackingTransparency _instance;
        private static Action<IOSATTAuthorizationStatus>
            _pendingCallbacks;
        private static bool _requestInFlight;

        public static IOSATTAuthorizationStatus CurrentStatus
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return NormalizeStatus(
                    ShikakuATTGetAuthorizationStatus());
#else
                return IOSATTAuthorizationStatus.Unavailable;
#endif
            }
        }

        public static bool RequiresSystemPrompt(
            IOSATTAuthorizationStatus status) =>
            status == IOSATTAuthorizationStatus.NotDetermined;

        public static bool AllowsTracking(
            IOSATTAuthorizationStatus status) =>
            status == IOSATTAuthorizationStatus.Authorized;

        public static void RequestIfNeeded(
            Action<IOSATTAuthorizationStatus> completed)
        {
            IOSATTAuthorizationStatus status = CurrentStatus;
            if (!RequiresSystemPrompt(status))
            {
                completed?.Invoke(status);
                return;
            }

            _pendingCallbacks += completed;
            if (_requestInFlight)
                return;

            _requestInFlight = true;
            EnsureInstance();

#if UNITY_IOS && !UNITY_EDITOR
            ShikakuATTRequestAuthorization(
                BridgeObjectName,
                nameof(OnNativeAuthorizationCompleted));
#else
            FinishRequest(IOSATTAuthorizationStatus.Unavailable);
#endif
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var bridgeObject = new GameObject(BridgeObjectName);
            _instance =
                bridgeObject.AddComponent<IOSAppTrackingTransparency>();
            DontDestroyOnLoad(bridgeObject);
        }

        private void OnNativeAuthorizationCompleted(string rawStatus)
        {
            if (!int.TryParse(rawStatus, out int status))
                status = (int)IOSATTAuthorizationStatus.Unavailable;

            FinishRequest(NormalizeStatus(status));
        }

        private static void FinishRequest(
            IOSATTAuthorizationStatus status)
        {
            _requestInFlight = false;

            Action<IOSATTAuthorizationStatus> callbacks =
                _pendingCallbacks;
            _pendingCallbacks = null;

            try
            {
                callbacks?.Invoke(status);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static IOSATTAuthorizationStatus NormalizeStatus(
            int status)
        {
            return status >=
                       (int)IOSATTAuthorizationStatus.NotDetermined &&
                   status <=
                       (int)IOSATTAuthorizationStatus.Authorized
                ? (IOSATTAuthorizationStatus)status
                : IOSATTAuthorizationStatus.Unavailable;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int
            ShikakuATTGetAuthorizationStatus();

        [DllImport("__Internal")]
        private static extern void
            ShikakuATTRequestAuthorization(
                string gameObjectName,
                string callbackMethodName);
#endif
    }
}
