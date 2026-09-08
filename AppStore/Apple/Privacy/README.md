# Apple privacy manifest and Xcode export validation

## Unity project ownership

Shikaku Go's authoritative app manifest is stored at:

`Assets/Editor/IOSPrivacy/PrivacyInfo.xcprivacy`

The file remains in an Editor-only folder so Unity does not copy it as an
unmanaged plug-in. `IOSPrivacyManifestBuildProcessor` validates it before an
iOS build, copies it to the root of the generated Xcode project, and adds it
to the main application's Copy Bundle Resources phase. The operation is safe
for clean and appended Unity exports.

The app manifest declares:

- Tracking because personalized advertising can occur only after ATT
  authorization.
- Optional analytics data, advertising interactions, purchase history, crash
  diagnostics, and performance/diagnostic information used by the installed
  services.
- UserDefaults access with approved reason `CA92.1`, covering the app's use of
  Unity `PlayerPrefs` for preferences, progress flags, and local settings.- File timestamp reason `C617.1` and disk-space reason `E174.1`, aggregating
  the declarations supplied by Unity IAP and LevelPlay as an additional
  safeguard while retaining those SDK-owned manifests.
- An empty app-owned tracking-domain array. Ad-network domains remain owned by
  the privacy manifests embedded in each advertising SDK rather than being
  guessed or duplicated here.

The processor also checks that the installed Unity IAP, Analytics, Services
Core, and LevelPlay packages still contain their SDK-owned privacy manifests.
It never deletes or replaces SDK manifests.

## Mac export and archive verification

1. In Unity, run **Tools > Shikaku Go > iOS > Validate Privacy Manifest**.
2. Export the iOS project.
3. Run CocoaPods and open the generated `.xcworkspace`.
4. In Xcode, confirm `PrivacyInfo.xcprivacy` belongs to the main app target and
   appears in **Build Phases > Copy Bundle Resources** exactly once.
5. In Terminal, run `plutil -lint PrivacyInfo.xcprivacy` from the export root.
6. Confirm the Pods project contains privacy manifests for Google Mobile Ads,
   Google UMP, Firebase Core, Firebase Crashlytics, ironSource/LevelPlay, and
   Unity Ads.
7. Archive the app. In Xcode Organizer, generate the archive's Privacy Report
   and review the aggregated app and SDK declarations.
8. Inspect the archived application and confirm the final app bundle contains
   `PrivacyInfo.xcprivacy` at its root beside `Info.plist`.
9. Resolve every Xcode or App Store Connect warning about an invalid manifest,
   missing SDK signature, or undeclared required-reason API before submission.

## App Store Connect boundary

The privacy manifest does not replace App Store Connect's App Privacy
questionnaire. The questionnaire and public privacy policy must match the
actual production behavior shown in the final Xcode Privacy Report, including
conditional tracking, analytics consent, advertising, purchases, crash
diagnostics, and user-requested data deletion.
