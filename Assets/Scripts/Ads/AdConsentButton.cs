using UnityEngine;

namespace Shikaku.Ads
{
    public class AdConsentButton : MonoBehaviour
    {
        public void ShowPrivacyOptions()
        {
            AdConsentManager.ShowPrivacyOptions();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ResetConsentForTesting()
        {
            AdConsentManager.ResetConsentForTesting();
        }
#endif
    }
}