# Production iOS ad configuration

## Configuration owned by the Unity project

- LevelPlay iOS app key: `27cbd222d`
- Rewarded ad unit: `nim37ahd2q6zh5nx`
- Banner ad unit: `fexc8v06pu3xv7n7`
- AdMob iOS app ID: `REPLACE_WITH_SHIKAKU_IOS_ADMOB_APP_ID`
- Runtime owner: `AdsManager` in `HomeUI.unity`
- Mediation adapters: Google AdMob and Unity Ads

The iOS app key and ad units are intentionally stored separately from the
Android values on the shared `AdsManager`. The Unity build target selects the
iOS values for an iPhone/iPad build and leaves Android behavior unchanged.

LevelPlay automatic initialization must remain disabled. `AdsManager` starts
LevelPlay manually only after Google UMP consent handling and Apple's ATT
status have resolved.

Every iOS build now fails before export if a production identifier changes or
is removed, the test suite is enabled, ads are disabled for testing, the
`HomeUI` scene is missing, automatic initialization is enabled, an adapter is
missing, or the LevelPlay SKAdNetwork identifier is absent.

The Xcode postprocessor adds:

- `su67r6k2v3.skadnetwork` to `SKAdNetworkItems` if needed.
- `NSAdvertisingAttributionReportEndpoint` with
  `https://postbacks-is.com/` for universal SKAN postback reporting.

Google Mobile Ads adds the complete mediated-network SKAdNetwork list and the
AdMob application identifier during the same Xcode export. Apple's default
App Transport Security policy remains intact.

## LevelPlay dashboard checks

Before device testing, confirm that the iOS app in the LevelPlay dashboard:

1. Uses bundle ID `com.smoothbraingames.shikakugo`.
2. Has rewarded and banner ad units corresponding to the IDs
   above.
3. Uses placement names `hint` and `bottom_banner`.
4. Has AdMob and Unity Ads enabled for the relevant iOS auction/mediation
   groups.
5. Has the required credentials entered for each mediated network.

## Mac and physical-device verification

1. Export the iOS project from Unity.
2. Open the generated `.xcworkspace`, not only the `.xcodeproj`, because the
   LevelPlay, AdMob, Unity Ads, UMP, and Firebase dependencies use CocoaPods.
3. Confirm the pods install successfully.
4. Make a Development build on a physical iPhone.
5. After consent and ATT resolve, open the in-game ad diagnostics and launch
   the LevelPlay integration test suite.
6. Verify the LevelPlay SDK, AdMob adapter, and Unity Ads adapter are detected.
7. Load and display one rewarded and one banner test ad.
8. Create a non-Development archive and confirm the integration test suite is
   unavailable and production ads still initialize.
## Inappropriate-ad reporting

Players can open **Settings > Help > Report an Ad** and choose either:

- **Email Report** to compose an editable email to
  `support@smoothbraingames.com`.
- **Open Support Page** to visit
  `https://smoothbraingames.com/#support`.

The email asks the player to describe the problem and attach a screenshot. It
also includes metadata for at most the five most recently displayed ads:
display time, format, placement, network, network instance, creative ID, ad
ID, auction ID, and ad unit ID. It adds the app version and basic device/OS
information. It does not include IDFA, GAID, ad revenue, or another persistent
advertising identifier, and the game never sends a report automatically.

When a report arrives:

1. Save the screenshot and note the reported display time.
2. In the LevelPlay dashboard, open **Ad Quality > Creatives**.
3. Search using the creative ID first. If it is unavailable, narrow the search
   with the network, display time, placement, auction ID, ad ID, and ad unit.
4. Review the creative, then block/report it to the ad source from Ad Quality
   when appropriate.
5. Reply to the player and retain the report long enough to confirm that the
   creative no longer serves.

Before release, display a rewarded ad and banner on a physical iPhone.
Immediately open the reporting flow after each format and confirm the
draft email identifies the most recent ad, opens the correct support address,
and contains no advertising identifier. Also confirm that the support-page
button opens the `#support` section and that the modal is readable in both
light and dark appearance modes.
