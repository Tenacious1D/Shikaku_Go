# Shikaku Go  iOS ATT and ad-consent sequence

## Production configuration

- iOS AdMob app ID:
  `REPLACE_WITH_SHIKAKU_IOS_ADMOB_APP_ID`
- ATT purpose:
  "Allowing tracking helps us show more relevant ads and measure ad
  performance. You can continue playing if you decline."
- Mediator: Unity LevelPlay
- Installed mediated networks: AdMob and Unity Ads
- Regulatory consent manager: Google UMP

## Runtime sequence

1. Google UMP refreshes the applicable regional privacy status.
2. UMP shows a required GDPR or U.S. state privacy form.
3. On a first installation, the existing Shikaku Go welcome/privacy screen
   is acknowledged before ATT can appear.
4. ATT waits until the app is active and other startup prompts have cleared.
5. Apple presents ATT once while its status is Not Determined.
6. LevelPlay initializes only after the status resolves.
7. Authorized users can provide IDFA for permitted personalized advertising
   and measurement.
8. Denied, restricted, and unavailable statuses continue without IDFA.
9. Gameplay, purchases, and ad-supported rewards are never gated on granting
   tracking permission.

## AdMob dashboard requirement

Use the iOS AdMob app in Privacy & messaging for regulatory UMP messages.
Do not enable a separate AdMob IDFA message: Shikaku Go owns the native ATT
request and its placement after onboarding. Enabling the AdMob IDFA message
could move Apple's prompt into the earlier UMP flow.

## Mac verification

1. Export an iOS development build.
2. Confirm `Info.plist` contains `NSUserTrackingUsageDescription`.
3. Confirm the UnityFramework target links
   `AppTrackingTransparency.framework`.
4. Install fresh on a physical iPhone with Settings > Privacy & Security >
   Tracking > Allow Apps to Request to Track enabled.
5. Complete any UMP form and the Shikaku Go welcome screen.
6. Confirm ATT appears afterward and only once.
7. Test Allow and Deny on separate clean installations.
8. In both cases confirm LevelPlay initializes and ads can load.
9. In the development ad-diagnostics screen, confirm the ATT status matches
   the choice.
10. Test with system-wide tracking requests disabled; the app should proceed
    as Restricted or Denied without displaying ATT.

ATT can only be reset reliably for testing by deleting the app, resetting
the simulator/device advertising privacy state, or using a separate test
device.
