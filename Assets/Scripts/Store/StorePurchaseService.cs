using System;
using System.Collections.Generic;
using System.Linq;
using Shikaku.Ads;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Shikaku.Store
{
    /// <summary>
    /// Owns the single Unity IAP connection used by every store product.
    /// Local fulfillment is completed before a pending order is confirmed so
    /// Unity IAP can safely redeliver interrupted transactions.
    /// </summary>
    internal static class StorePurchaseService
    {
        public static event Action StateChanged;

        private static readonly Dictionary<string, Product> Products =
            new Dictionary<string, Product>();

        private static StoreController _store;
        private static bool _initializationStarted;
        private static bool _initializing;
        private static bool _purchaseFetchInProgress;
        private static bool _ownershipCheckCompleted;
        private static bool _purchaseInProgress;
        private static string _activeProductId;
        private static bool _restoreInProgress;
        private static PurchaseRestoreStatus _restoreStatus =
            PurchaseRestoreStatus.Idle;
        private static PurchaseOperationStatus _operationStatus =
            PurchaseOperationStatus.Idle;
        private static string _statusMessage = string.Empty;

        public static bool IsBusy =>
            _initializing || _purchaseFetchInProgress ||
            _purchaseInProgress || _restoreInProgress;

        public static PurchaseRestoreStatus RestoreStatus =>
            _restoreStatus;

        public static PurchaseOperationStatus OperationStatus =>
            _operationStatus;

        public static string StatusMessage => _statusMessage;

        public static bool CanRestorePurchases
        {
            get
            {
#if UNITY_EDITOR
                return !IsBusy;
#else
                return _store != null &&
                       !IsBusy &&
                       (Products.ContainsKey(
                            StoreProductCatalog.RemoveAdsId) ||
                        Products.ContainsKey(
                            StoreProductCatalog.RemoveAdsBundleId));
#endif
            }
        }

        public static void EnsureInitialized()
        {
#if UNITY_EDITOR
            if (_initializationStarted)
                return;

            _initializationStarted = true;
            SetOperationStatus(
                PurchaseOperationStatus.Idle,
                string.Empty);
#else
            if (_initializing || _purchaseFetchInProgress ||
                _purchaseInProgress || _restoreInProgress)
            {
                return;
            }

            if (Products.Count > 0 && _store != null)
            {
                if (!_ownershipCheckCompleted)
                    StartInitialPurchaseRefresh();

                return;
            }

            if (_initializationStarted)
                return;

            _initializationStarted = true;
            InitializeStore();
#endif
        }

        public static bool CanPurchase(string productId)
        {
            if (!StoreProductCatalog.TryGet(
                    productId,
                    out StoreProductSpec spec) ||
                IsBusy)
            {
                return false;
            }

            if (spec.RemovesAds && AdEntitlement.AdsRemoved)
                return false;

#if UNITY_EDITOR
            return true;
#else
            if (spec.Restorable && !_ownershipCheckCompleted)
                return false;

            return Products.ContainsKey(productId);
#endif
        }

        public static string GetPriceText(
            string productId,
            string editorFallbackPrice)
        {
            if (IsAdRemovalProduct(productId) &&
                AdEntitlement.AdsRemoved)
            {
                return "Purchased";
            }

#if UNITY_EDITOR
            return editorFallbackPrice;
#else
            if (Products.TryGetValue(productId, out Product product))
                return product.metadata.localizedPriceString;

            return _initializing ? "Loading..." : "Unavailable";
#endif
        }

        public static bool BeginPurchase(string productId)
        {
            EnsureInitialized();

            if (!CanPurchase(productId))
                return false;

            _purchaseInProgress = true;
            _activeProductId = productId;
            SetOperationStatus(
                PurchaseOperationStatus.Purchasing,
                "Waiting for the App Store...");

#if UNITY_EDITOR
            SimulateEditorPurchase(productId);
            CompleteActivePurchase(
                PurchaseOperationStatus.Succeeded,
                "Purchase completed.");
            return true;
#else
            try
            {
                _store.PurchaseProduct(Products[productId]);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"StorePurchaseService: Could not begin purchase: " +
                    exception);
                CompleteActivePurchase(
                    PurchaseOperationStatus.Failed,
                    "Could not start the purchase. Please try again.");
                return false;
            }
#endif
        }

        public static bool RestorePurchases()
        {
            EnsureInitialized();

#if UNITY_EDITOR
            Debug.Log(
                "StorePurchaseService: Restore is handled by the platform " +
                "store in device builds.");
            _restoreStatus = PurchaseRestoreStatus.Unavailable;
            NotifyStateChanged();
            return true;
#else
            if (!CanRestorePurchases)
            {
                _restoreStatus = PurchaseRestoreStatus.Unavailable;
                NotifyStateChanged();
                return false;
            }

            _restoreInProgress = true;
            _restoreStatus = PurchaseRestoreStatus.InProgress;
            NotifyStateChanged();

#if UNITY_ANDROID
            StartRestorePurchaseRefresh();
#else
            try
            {
                _store.RestoreTransactions((success, error) =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            $"StorePurchaseService: Restore failed: {error}");
                        CompleteRestore(PurchaseRestoreStatus.Failed);
                        return;
                    }

                    // Apple's callback only confirms that StoreKit finished
                    // resynchronizing. Query ownership before telling the
                    // player that anything was actually restored.
                    StartRestorePurchaseRefresh();
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"StorePurchaseService: Restore failed: {exception}");
                CompleteRestore(PurchaseRestoreStatus.Failed);
            }
#endif

            return true;
#endif
        }

#if !UNITY_EDITOR
        private static async void InitializeStore()
        {
            _initializing = true;
            SetOperationStatus(
                PurchaseOperationStatus.Connecting,
                "Connecting to the App Store...");

            if (_store == null)
            {
                _store = UnityIAPServices.StoreController();
                SubscribeToStoreEvents();
            }

            try
            {
                await _store.Connect();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "StorePurchaseService: Store connection failed: " +
                    exception);
                MarkInitializationUnavailable(
                    "The App Store is unavailable. Reopen the shop to retry.");
            }
        }

        private static void SubscribeToStoreEvents()
        {
            _store.OnStoreConnected += OnStoreConnected;
            _store.OnStoreDisconnected += OnStoreDisconnected;
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
        }

        private static void OnStoreConnected()
        {
            SetOperationStatus(
                PurchaseOperationStatus.LoadingProducts,
                "Loading App Store products...");
            _store.FetchProducts(
                StoreProductCatalog.BuildDefinitions());
        }

        private static void OnStoreDisconnected(
            StoreConnectionFailureDescription failure)
        {
            Debug.LogWarning(
                "StorePurchaseService: Store disconnected: " +
                failure.Message);
            Products.Clear();
            _initializationStarted = false;
            _initializing = false;
            _purchaseFetchInProgress = false;
            _ownershipCheckCompleted = false;
            _purchaseInProgress = false;
            _activeProductId = null;

            if (_restoreInProgress)
            {
                CompleteRestore(PurchaseRestoreStatus.Failed);
                return;
            }

            SetOperationStatus(
                PurchaseOperationStatus.Unavailable,
                "The App Store is unavailable. Reopen the shop to retry.");
        }

        private static void OnProductsFetched(List<Product> products)
        {
            Products.Clear();

            foreach (Product product in products)
            {
                if (StoreProductCatalog.TryGet(
                        product.definition.id,
                        out _))
                {
                    Products[product.definition.id] = product;
                }
            }

            _initializing = false;
            _ownershipCheckCompleted = false;
            _restoreStatus = PurchaseRestoreStatus.Idle;
            StartInitialPurchaseRefresh();
        }

        private static void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning(
                "StorePurchaseService: Product fetch failed: " +
                failure.FailureReason);
            Products.Clear();
            MarkInitializationUnavailable(
                "App Store products are unavailable. Reopen the shop to retry.");
        }

        private static void OnPurchasesFetched(Orders orders)
        {
            bool removeAdsPurchaseFound =
                orders.ConfirmedOrders.Any(ContainsAdRemovalProduct) ||
                orders.PendingOrders.Any(ContainsAdRemovalProduct);
            bool hasCompletePermanentProductMetadata =
                HasCompletePermanentProductMetadata();

            ReconcileRemoveAdsEntitlement(
                removeAdsPurchaseFound,
                hasCompletePermanentProductMetadata);

            _purchaseFetchInProgress = false;
            _ownershipCheckCompleted =
                hasCompletePermanentProductMetadata;

            if (_restoreInProgress)
            {
                CompleteRestore(
                    removeAdsPurchaseFound
                        ? PurchaseRestoreStatus.Succeeded
                        : hasCompletePermanentProductMetadata
                            ? PurchaseRestoreStatus.NothingFound
                            : PurchaseRestoreStatus.Failed);
                return;
            }

            SetOperationStatus(
                PurchaseOperationStatus.Idle,
                string.Empty);
        }

        private static void OnPurchasesFetchFailed(
            PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning(
                "StorePurchaseService: Purchase fetch failed: " +
                failure.Message);

            _purchaseFetchInProgress = false;

            if (_restoreInProgress)
            {
                CompleteRestore(PurchaseRestoreStatus.Failed);
                return;
            }

            SetOperationStatus(
                PurchaseOperationStatus.Failed,
                "Could not check previous purchases. Please try again.");
        }

        private static void OnPurchasePending(PendingOrder order)
        {
            Product product = order.CartOrdered.Items()
                .Select(item => item.Product)
                .FirstOrDefault(candidate =>
                    StoreProductCatalog.TryGet(
                        candidate.definition.id,
                        out _));

            if (product == null)
            {
                Debug.LogWarning(
                    "StorePurchaseService: Pending order contained no " +
                    "recognized product. It was left unconfirmed.");
                return;
            }

            string productId = product.definition.id;
            bool isPlayerInitiated =
                _purchaseInProgress &&
                string.Equals(
                    _activeProductId,
                    productId,
                    StringComparison.Ordinal);

            try
            {
                if (!FulfillPendingOrder(
                        order,
                        product,
                        isPlayerInitiated))
                {
                    if (isPlayerInitiated)
                    {
                        CompleteActivePurchase(
                            PurchaseOperationStatus.Failed,
                            "The purchase could not be safely delivered. " +
                            "It will retry automatically.");
                    }

                    return;
                }

                if (isPlayerInitiated)
                {
                    SetOperationStatus(
                        PurchaseOperationStatus.Purchasing,
                        "Finalizing purchase...");
                }

                _store.ConfirmPurchase(order);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "StorePurchaseService: Purchase fulfillment failed; " +
                    "the order was left pending for retry. " + exception);

                if (isPlayerInitiated)
                {
                    CompleteActivePurchase(
                        PurchaseOperationStatus.Failed,
                        "The purchase was not finalized. It will retry " +
                        "automatically.");
                }
            }
        }

        private static bool FulfillPendingOrder(
            PendingOrder order,
            Product product,
            bool isPlayerInitiated)
        {
            string productId = product.definition.id;

            if (!StoreProductCatalog.TryGet(
                    productId,
                    out StoreProductSpec spec))
            {
                return false;
            }

            if (spec.RemovesAds)
            {
                bool wasOwned = AdEntitlement.AdsRemoved;
                GrantRemoveAds(
                    recordAnalytics:
                        isPlayerInitiated && !wasOwned,
                    analyticsProductId: productId,
                    product: product);
            }

            if (spec.HintAmount <= 0)
                return true;

            // A restored non-consumable bundle restores Remove Ads, but its
            // one-time bonus consumables are not reissued by Restore Purchases.
            if (spec.GrantsBonusHints && _restoreInProgress)
                return true;

            HintPurchaseDeliveryResult delivery =
                HintWallet.DeliverPurchasedHints(
                    spec.HintAmount,
                    order.Info.TransactionID);

            if (delivery == HintPurchaseDeliveryResult.Invalid ||
                delivery == HintPurchaseDeliveryResult.Failed)
            {
                return false;
            }

            if (delivery == HintPurchaseDeliveryResult.AlreadyDelivered)
                return true;

            double price = Convert.ToDouble(
                product.metadata.localizedPrice);
            string currency = product.metadata.isoCurrencyCode;

            if (spec.GrantsBonusHints)
            {
                GameAnalytics.BundlePurchased(
                    productId,
                    spec.HintAmount,
                    price,
                    currency);
            }
            else
            {
                GameAnalytics.PurchaseCompleted(
                    productId,
                    "hints",
                    spec.HintAmount,
                    price,
                    currency);
            }

            return true;
        }

        private static void OnPurchaseConfirmed(Order order)
        {
            if (_purchaseInProgress)
            {
                CompleteActivePurchase(
                    PurchaseOperationStatus.Succeeded,
                    "Purchase completed.");
                return;
            }

            NotifyStateChanged();
        }

        private static void OnPurchaseFailed(FailedOrder order)
        {
            Debug.LogWarning(
                "StorePurchaseService: Purchase failed: " +
                $"{order.FailureReason} - {order.Details}");

            if (!_purchaseInProgress)
            {
                NotifyStateChanged();
                return;
            }

            if (order.FailureReason == PurchaseFailureReason.UserCancelled ||
                order.FailureReason == PurchaseFailureReason.OrderCancelled)
            {
                CompleteActivePurchase(
                    PurchaseOperationStatus.Cancelled,
                    "Purchase cancelled.");
                return;
            }

            string message =
                order.FailureReason ==
                PurchaseFailureReason.DuplicateTransaction
                    ? "This purchase is already owned. Use Restore " +
                      "Purchases in Settings."
                    : "Purchase failed. Please try again.";

            CompleteActivePurchase(
                PurchaseOperationStatus.Failed,
                message);
        }

        private static void OnPurchaseDeferred(DeferredOrder order)
        {
            Debug.Log(
                "StorePurchaseService: Purchase is awaiting approval.");
            CompleteActivePurchase(
                PurchaseOperationStatus.Deferred,
                "Purchase is awaiting approval.");
        }

        private static bool ContainsAdRemovalProduct(Order order)
        {
            return order.CartOrdered.Items().Any(
                item => IsAdRemovalProduct(
                    item.Product.definition.id));
        }

        private static void StartInitialPurchaseRefresh()
        {
            _purchaseFetchInProgress = true;
            SetOperationStatus(
                PurchaseOperationStatus.CheckingPurchases,
                "Checking previous purchases...");

            try
            {
                _store.FetchPurchases();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "StorePurchaseService: Purchase fetch failed: " +
                    exception);
                _purchaseFetchInProgress = false;
                SetOperationStatus(
                    PurchaseOperationStatus.Failed,
                    "Could not check previous purchases. Reopen the shop " +
                    "to retry.");
            }
        }

        private static void StartRestorePurchaseRefresh()
        {
            if (!_restoreInProgress)
                return;

            _purchaseFetchInProgress = true;
            NotifyStateChanged();

            try
            {
                _store.FetchPurchases();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "StorePurchaseService: Restore ownership refresh " +
                    "failed: " + exception);
                _purchaseFetchInProgress = false;
                CompleteRestore(PurchaseRestoreStatus.Failed);
            }
        }

        private static void MarkInitializationUnavailable(string message)
        {
            _initializationStarted = false;
            _initializing = false;
            _purchaseFetchInProgress = false;
            _ownershipCheckCompleted = false;
            _restoreStatus = PurchaseRestoreStatus.Unavailable;
            SetOperationStatus(
                PurchaseOperationStatus.Unavailable,
                message);
        }
#endif

        private static void CompleteRestore(
            PurchaseRestoreStatus status)
        {
            _purchaseFetchInProgress = false;
            _restoreInProgress = false;
            _restoreStatus = status;
            NotifyStateChanged();
        }

        private static bool IsAdRemovalProduct(string productId)
        {
            return StoreProductCatalog.TryGet(
                       productId,
                       out StoreProductSpec spec) &&
                   spec.RemovesAds;
        }

        private static void GrantRemoveAds(
            bool recordAnalytics,
            string analyticsProductId = StoreProductCatalog.RemoveAdsId,
            Product product = null)
        {
            bool wasOwned = AdEntitlement.AdsRemoved;

            if (AdsManager.Instance != null)
                AdsManager.Instance.ApplyRemoveAdsPurchase();
            else
                AdEntitlement.GrantRemoveAds();

            if (!recordAnalytics || wasOwned)
                return;

            double price = 0d;
            string currency = string.Empty;

            if (product == null)
                Products.TryGetValue(analyticsProductId, out product);

            if (product != null)
            {
                price = Convert.ToDouble(
                    product.metadata.localizedPrice);
                currency = product.metadata.isoCurrencyCode;
            }

            GameAnalytics.RemoveAdsPurchased(
                analyticsProductId,
                price,
                currency);
        }

        private static bool HasCompletePermanentProductMetadata()
        {
#if UNITY_EDITOR
            return true;
#else
            return Products.ContainsKey(
                       StoreProductCatalog.RemoveAdsId) &&
                   Products.ContainsKey(
                       StoreProductCatalog.RemoveAdsBundleId);
#endif
        }

        private static void ReconcileRemoveAdsEntitlement(
            bool isOwned,
            bool hasCompletePermanentProductMetadata)
        {
            if (isOwned)
            {
                GrantRemoveAds(recordAnalytics: false);
                return;
            }

            if (!hasCompletePermanentProductMetadata)
            {
                Debug.LogWarning(
                    "StorePurchaseService: Permanent-product metadata was " +
                    "incomplete, so the local Remove Ads entitlement was " +
                    "not revoked.");
                return;
            }

            if (!AdEntitlement.AdsRemoved)
                return;

            if (AdsManager.Instance != null)
                AdsManager.Instance.ApplyRemoveAdsRevocation();
            else
                AdEntitlement.RevokeRemoveAds();

            Debug.Log(
                "StorePurchaseService: The device store no longer reports " +
                "Remove Ads as owned; the local entitlement was revoked.");
        }

#if UNITY_EDITOR
        private static void SimulateEditorPurchase(string productId)
        {
            if (!StoreProductCatalog.TryGet(
                    productId,
                    out StoreProductSpec spec))
            {
                return;
            }

            if (spec.HintAmount > 0)
                HintWallet.AddHints(spec.HintAmount);

            if (spec.RemovesAds)
                GrantRemoveAds(recordAnalytics: true);

            if (spec.GrantsBonusHints)
            {
                GameAnalytics.BundlePurchased(
                    productId,
                    spec.HintAmount,
                    0d,
                    "Editor");
            }
            else if (spec.HintAmount > 0)
            {
                GameAnalytics.PurchaseCompleted(
                    productId,
                    "hints",
                    spec.HintAmount,
                    0d,
                    "Editor");
            }
        }
#endif

        private static void CompleteActivePurchase(
            PurchaseOperationStatus status,
            string message)
        {
            _purchaseInProgress = false;
            _activeProductId = null;
            SetOperationStatus(status, message);
        }

        private static void SetOperationStatus(
            PurchaseOperationStatus status,
            string message)
        {
            _operationStatus = status;
            _statusMessage = message ?? string.Empty;
            NotifyStateChanged();
        }

        private static void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
