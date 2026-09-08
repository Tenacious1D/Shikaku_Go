# Shikaku Go iOS In-App Purchases

This is the release checklist for the iOS purchase implementation. Runtime
product IDs come from `StoreProductCatalog`; changing an ID in App Store
Connect without changing and retesting the catalog will block the Unity iOS
build.

## Production catalog

| Product ID | App Store type | Fulfillment | Restore behavior |
| --- | --- | --- | --- |
| `remove_ads` | Non-Consumable | Permanently removes banner ads | Restores Remove Ads |
| `remove_ads_hints_30` | Non-Consumable | Permanently removes banner ads and grants 30 bonus hints on the original local delivery | Restores Remove Ads; Restore Purchases does not reissue the bonus hints |
| `hints_10` | Consumable | Adds 10 hints | Does not restore |
| `hints_25` | Consumable | Adds 25 hints | Does not restore |
| `hints_60` | Consumable | Adds 60 hints | Does not restore |

Family Sharing should remain disabled for both non-consumables for version 1.
There are no subscriptions.

## App Store Connect checks

Before testing a signed build:

1. Make sure the Paid Applications agreement is active and banking/tax setup
   is complete for Smooth Brain Games LLC.
2. Confirm all five products belong to bundle ID
   `com.smoothbraingames.shikakugo` and use the exact IDs and types above.
3. Complete each product's availability, price, localization, and App Review
   screenshot/notes. Products needed by the build must not be in a missing
   metadata state.
4. Keep Family Sharing off for `remove_ads` and
   `remove_ads_hints_30` unless the runtime and product policy are redesigned.
5. Add the five in-app purchases to the first app-version submission. New
   in-app purchases submitted with a new app must be selected on that version
   before sending it to App Review.
6. Create at least two Sandbox Apple Accounts: one clean account and one used
   for purchase/restore tests.

## Unity checks before export

Run:

`Tools > Shikaku Go > iOS > Validate In-App Purchases`

The iOS build also runs this automatically. It checks:

- the exact five-product catalog and types;
- Unity IAP 5.4.0 or newer;
- iOS 15.0+ and the production bundle ID;
- the visible Restore Purchases control and purchase status UI;
- the purchase-history privacy declaration.

After export, a second build check confirms Unity IAP added
`StoreKit.framework` and the In-App Purchase Xcode capability.

## Xcode local StoreKit testing

1. Create a StoreKit Configuration file in Xcode containing the exact five
   IDs and matching consumable/non-consumable types.
2. Select it in the Run scheme's StoreKit Configuration setting for fast local
   tests.
3. Test successful purchase, user cancellation, interrupted purchase,
   Ask to Buy/deferred purchase, and transaction redelivery.
4. Use Xcode's StoreKit transaction manager to refund/revoke each permanent
   product and verify ads return when neither permanent product is owned.
5. Set the scheme's StoreKit Configuration back to `None` before Sandbox or
   TestFlight testing. A local configuration does not contact App Store
   Connect.

## Sandbox and TestFlight matrix

Test on both iPhone and iPad in portrait:

- buy each hint pack and verify the exact balance increase;
- terminate the app during a purchase, relaunch, and verify the pending
  transaction is delivered once rather than duplicated;
- cancel each purchase and verify the shop reports cancellation and remains
  usable;
- buy `remove_ads` and verify banner ads disappear immediately;
- buy the bundle and verify Remove Ads plus exactly 30 hints;
- use Restore Purchases on the purchasing Apple Account and verify Remove Ads
  is restored;
- use Restore Purchases on a clean Apple Account and verify "No purchases were
  found";
- reinstall and verify permanent Remove Ads ownership returns after the store
  check;
- confirm consumable hint packs do not restore;
- test offline launch, store connection failure, reopening the shop to retry,
  and returning online;
- verify prices/currency are localized by the App Store rather than hardcoded.

## App Review notes

Tell App Review:

- the shop is opened from the home screen;
- Restore Purchases is in Settings;
- `remove_ads_hints_30` is a non-consumable bundle whose durable entitlement is
  Remove Ads and whose 30 hints are a one-time consumable bonus;
- no account sign-in is required inside the game.

## Version 1 data boundary

Unity IAP/StoreKit performs Apple transaction verification and the game stores
fulfilled transaction IDs locally before confirmation. That protects against
duplicate redelivery on the same installation. There is no game account or
server ledger in version 1, so consumable balances and the bundle's bonus-hint
delivery history do not sync across devices and are not recoverable after app
data is erased. Adding cross-device consumable recovery later requires a
server/account system using Apple's signed transaction data.
