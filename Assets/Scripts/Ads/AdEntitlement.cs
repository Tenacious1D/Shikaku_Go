using System;
using UnityEngine;

namespace Shikaku.Store
{
    public static class AdEntitlement
    {
        private const string AdsRemovedKey = "purchase_remove_ads";


        public static bool AdsRemoved =>
            PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;

        public static void GrantRemoveAds()
        {
            if (AdsRemoved)
                return;

            PlayerPrefs.SetInt(AdsRemovedKey, 1);
            PlayerPrefs.Save();


            Debug.Log("AdEntitlement: Remove Ads granted.");
        }

        /// <summary>
        /// Clears the locally cached entitlement after Google Play
        /// successfully reports that Remove Ads is no longer owned.
        /// </summary>
        public static void RevokeRemoveAds()
        {
            if (!AdsRemoved)
                return;

            PlayerPrefs.DeleteKey(AdsRemovedKey);
            PlayerPrefs.Save();

            Debug.Log("AdEntitlement: Remove Ads revoked.");
        }

        public static void RevokeRemoveAdsForTesting()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RevokeRemoveAds();
            Debug.Log("AdEntitlement: Remove Ads reset for testing.");
#endif
        }
    }
}