using System;
using Firebase;
using Firebase.Crashlytics;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Initializes Firebase Crashlytics once for the lifetime of the app.
/// This is intentionally independent of scenes and analytics consent:
/// Crashlytics records technical failures, not gameplay analytics.
/// </summary>
public sealed class CrashReportingService : MonoBehaviour
{
    private static CrashReportingService instance;
    private static FirebaseApp firebaseApp;
    private static bool initializationStarted;

    public static bool IsReady { get; private set; }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
    private bool testCrashArmed;
    private bool testCrashRequested;
    private float testCrashArmExpiresAt;
    private GUIStyle testCrashButtonStyle;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAutomatically()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (instance != null)
            return;

        GameObject serviceObject =
            new GameObject(nameof(CrashReportingService));

        instance = serviceObject.AddComponent<CrashReportingService>();
        DontDestroyOnLoad(serviceObject);
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private static void Initialize()
    {
        if (initializationStarted)
            return;

        initializationStarted = true;

#if DEVELOPMENT_BUILD
        FirebaseApp.LogLevel = LogLevel.Debug;
#endif

        FirebaseApp.CheckAndFixDependenciesAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled)
                {
                    Debug.LogWarning(
                        "Crashlytics initialization was canceled.");
                    return;
                }

                if (task.IsFaulted)
                {
                    Debug.LogException(
                        task.Exception?.GetBaseException() ??
                        new Exception(
                            "Crashlytics initialization failed."));
                    return;
                }

                DependencyStatus status = task.Result;
                if (status != DependencyStatus.Available)
                {
                    Debug.LogWarning(
                        "Crashlytics dependencies are unavailable: " +
                        status);
                    return;
                }

                firebaseApp = FirebaseApp.DefaultInstance;
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;

                Crashlytics.SetCustomKey(
                    "app_version",
                    Application.version);
                Crashlytics.SetCustomKey(
                    "unity_version",
                    Application.unityVersion);
                Crashlytics.SetCustomKey(
                    "platform",
                    Application.platform.ToString());
                Crashlytics.SetCustomKey(
                    "build_type",
                    Debug.isDebugBuild ? "development" : "release");

                Crashlytics.Log("Crashlytics initialized.");
                IsReady = true;
                Debug.Log("Firebase Crashlytics initialized.");
            });
    }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
    private void Update()
    {
        if (testCrashArmed && Time.unscaledTime > testCrashArmExpiresAt)
            testCrashArmed = false;

        if (!testCrashRequested)
            return;

        testCrashRequested = false;
        Crashlytics.Log("A development-only test crash was requested.");

        // Firebase's Unity verification guide uses an unhandled managed
        // exception. ReportUncaughtExceptionsAsFatal records this as fatal.
        throw new Exception(
            "Shikaku Go Crashlytics test exception - please ignore");
    }

    private void OnGUI()
    {
        if (!IsReady)
            return;

        if (testCrashButtonStyle == null)
        {
            testCrashButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(16, Screen.width / 28)
            };
        }

        Rect safeArea = Screen.safeArea;
        float margin = Mathf.Max(16f, Screen.width * 0.025f);
        float height = Mathf.Max(58f, Screen.height * 0.055f);
        float width = Mathf.Min(
            safeArea.width - margin * 2f,
            Screen.width * 0.72f);
        float guiSafeTop = Screen.height - safeArea.yMax;

        Rect buttonRect = new Rect(
            safeArea.center.x - width * 0.5f,
            guiSafeTop + margin,
            width,
            height);

        string label = testCrashArmed
            ? "TAP AGAIN: SEND TEST CRASH"
            : "CRASHLYTICS TEST";

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = testCrashArmed
            ? new Color(0.88f, 0.22f, 0.18f)
            : new Color(0.95f, 0.72f, 0.18f);

        bool pressed = GUI.Button(
            buttonRect,
            label,
            testCrashButtonStyle);

        GUI.backgroundColor = previousColor;

        if (!pressed)
            return;

        if (testCrashArmed)
        {
            testCrashArmed = false;
            testCrashRequested = true;
        }
        else
        {
            testCrashArmed = true;
            testCrashArmExpiresAt = Time.unscaledTime + 5f;
        }
    }
#endif

    /// <summary>
    /// Records a handled exception without terminating the app.
    /// </summary>
    public static void LogNonFatal(Exception exception)
    {
        if (exception == null)
            return;

        if (IsReady)
            Crashlytics.LogException(exception);
        else
            Debug.LogException(exception);
    }
}
