# Shikaku Go  Firebase iOS release

## Production registration

- Bundle ID: `com.smoothbraingames.shikakugo`
- Firebase project: `REPLACE_WITH_SHIKAKU_FIREBASE_PROJECT_ID`
- Firebase Apple app ID:
  `REPLACE_WITH_SHIKAKU_FIREBASE_IOS_APP_ID`
- Configuration asset: `Assets/GoogleService-Info.plist`
- Unity Firebase SDK: 13.15.0
- Firebase Apple SDK dependency: 12.17.0
- Product enabled in the Unity project: Crashlytics

The iOS release preflight parses the plist and stops the export when any
production identifier is missing or incorrect.

## Automatic behavior

- `CrashReportingService` initializes Firebase before the first scene on
  Android and iOS.
- Fatal Unity exceptions are reported as fatal Crashlytics events.
- Handled exceptions can be sent with
  `CrashReportingService.LogNonFatal(exception)`.
- The Firebase Unity Editor plugin copies `GoogleService-Info.plist` into
  the exported Xcode app target.
- The Firebase Crashlytics editor plugin adds the Xcode run-script build
  phase that uploads Apple dSYM files.
- A two-tap test-crash control is present only in development builds.

## Verification on the Mac

1. Export an iOS development build from Unity.
2. Complete Apple dependency resolution and open the generated Xcode
   workspace, not only the project file.
3. Confirm the main app target contains `GoogleService-Info.plist`.
4. Confirm Build Phases contains the Firebase Crashlytics run script.
5. Build and install on a physical iPhone.
6. Wait for the Unity log message `Firebase Crashlytics initialized.`
7. Tap `CRASHLYTICS TEST`, then tap the confirmation within five seconds.
8. Relaunch the app so the stored fatal report can be uploaded.
9. Verify the test issue appears under Firebase Console > Crashlytics.
10. Archive a non-development release build; the test control is excluded.

## App Store privacy reminder

Crashlytics collects crash stack traces, relevant application state, and
device/OS diagnostic information. Keep the App Store privacy answers and
the public privacy policy synchronized with the Firebase products included
in each release.
