# Shikaku Go final Windows iOS release audit

Audit date: August 26, 2026

This audit is the last release gate that can be completed without Xcode,
Apple signing, App Store Connect, Sandbox/TestFlight, or physical Apple
devices.

## Windows audit result

The project passes the consolidated Windows audit and the complete Unity Edit
Mode test suite.

Verified in the project:

- Product name `Shikaku Go`, company `Smooth Brain Games`, bundle ID
  `com.smoothbraingames.shikakugo`, Apple Team ID `TCSF74TDNV`, version
  `1.0.0`, and positive iOS build number.
- iPhone + iPad, portrait only, Device SDK, iOS 15.0+, IL2CPP ARM64, and
  automatic signing.
- `HomeUI` is build scene 0 and `Gameplay` is build scene 1, with no extra
  enabled scenes.
- All 19 iPhone, iPad, notification, settings, Spotlight, and App Store icon
  slots use the approved opaque square `icon_legacy.png` source.
- The iOS target includes the Apple Game Center and LevelPlay dependency
  symbols and excludes the Google Play Games symbol.
- Ten unique Apple achievement IDs and the Game Center Xcode postprocessor.
- Local iOS notifications request permission only after the player opts in;
  no Push Notifications capability is required because version 1 schedules
  notifications locally.
- Production Firebase Apple plist identifiers and Crashlytics integration.
- UMP then ATT then LevelPlay startup sequencing, production AdMob/LevelPlay
  identifiers, mediation dependencies, SKAdNetwork source data, and ad-report
  support flow.
- App-owned and SDK-owned privacy manifests, required-reason API declarations,
  conditional tracking declarations, and purchase-history disclosure.
- Exact five-product IAP catalog, safe pending transaction fulfillment,
  Apple restore behavior, visible Restore Purchases control, and StoreKit
  post-export checks.
- Live production privacy, terms, and support URLs.
- Non-development release settings: Development Build, Script Debugging, and
  Autoconnect Profiler are disabled.

Run the audit again at any time from:

`Tools > Shikaku Go > iOS > Run Final Windows Release Audit`

Every future iOS export also runs the consolidated audit automatically in
addition to the specialized foundation, ads, privacy, and purchase validators.

## Required Mac environment

Apple currently requires App Store Connect uploads to be built with Xcode 26
or later and the iOS 26 SDK. Install:

1. The same Unity editor version used by the project (`6000.3.4f1`) with iOS
   Build Support.
2. Xcode 26 or later with the iOS 26 SDK.
3. CocoaPods if dependency resolution does not use Swift Package Manager.
4. The Apple Development and Apple Distribution signing access for team
   `TCSF74TDNV`.

Open or copy the Unity project without carrying `Library`, `Temp`, or `Logs`
from Windows. Let Unity regenerate them on the Mac.

## Mac-only verification sequence

1. Open the project in Unity, switch to iOS, and rerun the Final Windows
   Release Audit.
2. Make a clean Xcode export. Let External Dependency Manager resolve Apple
   dependencies, then open the generated `.xcworkspace` when CocoaPods is
   present.
3. In the app target, select team `TCSF74TDNV` and confirm the final bundle ID.
4. Confirm Game Center and In-App Purchase capabilities. Do not add Push
   Notifications for the local-reminder implementation.
5. Confirm the exported `Info.plist` contains the AdMob app ID, ATT purpose,
   SKAdNetwork items, and attribution-report endpoint.
6. Confirm `PrivacyInfo.xcprivacy` appears exactly once in the main app target,
   then generate and review Xcode's aggregated Privacy Report.
7. Confirm Firebase copied `GoogleService-Info.plist` and installed the
   Crashlytics dSYM upload build phase.
8. Confirm StoreKit, AppTrackingTransparency, GameKit, and UserNotifications
   frameworks/capabilities are present as expected.
9. Build a Development configuration on a physical iPhone and iPad. Test all
   dashboard integrations, consent branches, ATT Allow/Deny, ads, local
   reminders, Game Center, purchases, restoration, deep UI safe areas, and
   portrait rotation behavior.
10. Create a non-development Release archive, run Xcode validation, upload it
    to TestFlight, and repeat the release test matrix from the feature-specific
    Apple READMEs.

## External dashboard work that Windows cannot verify

Before App Review, confirm:

- Apple Developer/App Store Connect: Agreements, tax, banking, organization
  legal details, automatic-signing access, Game Center enablement, all ten
  achievements, all five IAPs, pricing/localization/review metadata, updated
  age-rating questions, App Privacy answers, export compliance, and EU DSA
  trader status if distributing in the EU.
- App Store version: attach the Game Center achievements and all new IAPs to
  the first version submission where required.
- LevelPlay: the iOS app uses the production app key and ad units; placements
  `hint`, `puzzle_break`, and `bottom_banner` exist; AdMob and Unity Ads are
  active in the relevant mediation groups; network credentials are valid.
- AdMob/UMP: the production iOS app and regulatory messages are published;
  do not enable a second AdMob-owned IDFA prompt because the app owns ATT.
- Firebase: a physical-device development crash appears in Crashlytics, and a
  Release archive uploads dSYMs without warnings.
- Sandbox/TestFlight: use clean Apple Sandbox accounts and test all five IAPs,
  cancellation, deferred approval, interrupted delivery, reinstall, and
  Restore Purchases.

## Submission stop conditions

Do not submit while Xcode or App Store Connect reports a missing privacy
manifest/signature, missing Game Center entitlement, unresolved CocoaPods,
missing dSYMs, invalid IAP metadata, placeholder screenshots, an incomplete
age rating/privacy questionnaire, or a failed archive validation.
