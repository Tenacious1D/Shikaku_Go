using System;
using System.Security.Cryptography;
using System.Text;
using Shikaku.SaveSystem;
using UnityEngine;

namespace Shikaku.Store
{
    public enum HintPurchaseDeliveryResult
    {
        Delivered,
        AlreadyDelivered,
        Invalid,
        Failed
    }

    public static class HintWallet
    {
        private const string DeliveredPurchasePrefix =
            "delivered_hint_purchase_";

        public static event Action BalanceChanged;

        public static int Balance => SaveManager.HintBalance;

        public static void AddHints(int amount)
        {
            if (amount <= 0)
                return;

            SaveManager.AddHints(amount);
            BalanceChanged?.Invoke();

            Debug.Log(
                $"HintWallet: Added {amount} hints. " +
                $"New balance: {Balance}");
        }

        /// <summary>
        /// Persists a purchased hint delivery before Unity IAP confirms the
        /// order. A transaction ID is mandatory so redelivery is idempotent.
        /// </summary>
        public static HintPurchaseDeliveryResult DeliverPurchasedHints(
            int amount,
            string transactionId)
        {
            if (amount <= 0 ||
                string.IsNullOrWhiteSpace(transactionId))
            {
                Debug.LogError(
                    "HintWallet: Purchased hints were not delivered " +
                    "because the order had no valid amount or transaction " +
                    "ID. The pending order must remain unconfirmed.");
                return HintPurchaseDeliveryResult.Invalid;
            }

            string deliveryId = StableHash(transactionId);
            string legacyDeliveredKey =
                DeliveredPurchasePrefix + deliveryId;

            if (SaveManager.HasDeliveredHintPurchase(deliveryId) ||
                PlayerPrefs.GetInt(legacyDeliveredKey, 0) == 1)
            {
                // Preserve idempotency for purchases delivered before the
                // SaveManager migration.
                SaveManager.RememberHintPurchaseDelivery(deliveryId);
                Debug.Log(
                    "HintWallet: Ignored a previously delivered hint " +
                    "purchase.");
                return HintPurchaseDeliveryResult.AlreadyDelivered;
            }

            if (!SaveManager.TryDeliverPurchasedHints(deliveryId, amount))
            {
                return SaveManager.HasDeliveredHintPurchase(deliveryId)
                    ? HintPurchaseDeliveryResult.AlreadyDelivered
                    : HintPurchaseDeliveryResult.Failed;
            }

            BalanceChanged?.Invoke();

            Debug.Log(
                $"HintWallet: Delivered {amount} purchased hints. " +
                $"New balance: {Balance}");

            return HintPurchaseDeliveryResult.Delivered;
        }

        /// <summary>
        /// Compatibility wrapper for callers that only need to know whether
        /// this invocation added hints.
        /// </summary>
        public static bool TryAddPurchasedHints(
            int amount,
            string transactionId)
        {
            return DeliverPurchasedHints(amount, transactionId) ==
                   HintPurchaseDeliveryResult.Delivered;
        }

        public static bool TrySpendHint()
        {
            if (Balance <= 0 || !SaveManager.TrySpendHint())
                return false;

            BalanceChanged?.Invoke();

            Debug.Log(
                $"HintWallet: Used one hint. New balance: {Balance}");
            return true;
        }

        public static bool TryClaimReward(int amount, string rewardId)
        {
            if (!SaveManager.TryClaimHintReward(rewardId, amount))
                return false;

            BalanceChanged?.Invoke();
            return true;
        }

        public static void ResetForTesting()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SaveManager.ResetHintBalanceForTesting();
            BalanceChanged?.Invoke();
            Debug.Log("HintWallet: Balance reset.");
#endif
        }

        private static string StableHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);

                foreach (byte currentByte in bytes)
                    builder.Append(currentByte.ToString("x2"));

                return builder.ToString();
            }
        }
    }
}
