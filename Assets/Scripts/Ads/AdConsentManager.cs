using System;
using System.Collections.Generic;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Shikaku.Ads
{
    /// <summary>
    /// Handles GDPR / privacy consent before ads initialize.
    /// Android first, but also works for iOS once the iOS ad setup exists.
    /// </summary>
    public static class AdConsentManager
    {
        private static bool _hasStartedConsentFlow;
        private static bool _hasCompletedConsentFlow;
        private static Action _adsReadyCallbacks;
        private static Action _consentResolvedCallbacks;

        public static bool CanRequestAds =>
            _hasCompletedConsentFlow &&
            ConsentInformation.CanRequestAds();
        public static bool HasCompletedConsentFlow =>
            _hasCompletedConsentFlow;

        public static bool IsPrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus ==
            PrivacyOptionsRequirementStatus.Required;

        /// <summary>
        /// Call this before initializing LevelPlay.
        /// If consent is gathered or not required, this runs onReadyToInitializeAds.
        /// </summary>
        public static void EnsureConsentThenRun(Action onReadyToInitializeAds)
        {
            if (_hasCompletedConsentFlow)
            {
                if (ConsentInformation.CanRequestAds())
                    InvokeSafely(onReadyToInitializeAds);

                return;
            }

            _adsReadyCallbacks += onReadyToInitializeAds;
            StartConsentFlowIfNeeded();
        }

        /// <summary>
        /// Runs after the consent form has closed or consent was determined
        /// unnecessary. Unlike the ads-ready callback, this also runs when the
        /// player declines consent so normal app startup can continue.
        /// </summary>
        public static void EnsureConsentResolvedThenRun(
            Action onConsentResolved)
        {
            if (_hasCompletedConsentFlow)
            {
                InvokeSafely(onConsentResolved);
                return;
            }

            _consentResolvedCallbacks += onConsentResolved;
            StartConsentFlowIfNeeded();
        }

        private static void StartConsentFlowIfNeeded()
        {
            if (_hasStartedConsentFlow)
                return;

            _hasStartedConsentFlow = true;

            var request = BuildConsentRequestParameters();

            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning(
                        "AdConsentManager: Consent info update failed: " +
                        updateError.Message
                    );

                    // Consent from a previous session may still be usable.
                    TryFinishConsentFlow();
                    return;
                }

                Debug.Log(
                    $"AdConsentManager: UMP status after update � " +
                    $"ConsentStatus={ConsentInformation.ConsentStatus}, " +
                    $"CanRequestAds={ConsentInformation.CanRequestAds()}, " +
                    $"PrivacyOptions={ConsentInformation.PrivacyOptionsRequirementStatus}"
                );

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                    {
                        Debug.LogWarning(
                            $"AdConsentManager: Consent form error. " +
                            $"Code={formError.ErrorCode}, Message={formError.Message}"
                        );
                    }
                    else
                    {
                        Debug.Log(
                            $"AdConsentManager: LoadAndShow completed. " +
                            $"ConsentStatus={ConsentInformation.ConsentStatus}, " +
                            $"CanRequestAds={ConsentInformation.CanRequestAds()}, " +
                            $"PrivacyOptions={ConsentInformation.PrivacyOptionsRequirementStatus}"
                        );
                    }

                    TryFinishConsentFlow();
                });
            });
        }

        private static ConsentRequestParameters BuildConsentRequestParameters()
        {
            var request = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = false
            };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,

                TestDeviceHashedIds = new List<string>
        {
            "71C01950AA07BC251F6827177A8A6284"
        }
            };

            request.ConsentDebugSettings = debugSettings;

            Debug.Log(
                "AdConsentManager: Forced EEA debug settings attached. " +
                $"DebugBuild={Debug.isDebugBuild}"
            );
#else
    Debug.Log("AdConsentManager: Production consent settings attached.");
#endif

            return request;
        }

        private static void TryFinishConsentFlow()
        {
            _hasCompletedConsentFlow = true;

            Action consentResolvedCallbacks =
                _consentResolvedCallbacks;
            _consentResolvedCallbacks = null;
            InvokeSafely(consentResolvedCallbacks);

            if (!ConsentInformation.CanRequestAds())
            {
                _adsReadyCallbacks = null;
                Debug.LogWarning(
                    "AdConsentManager: Ads cannot currently be requested. " +
                    "LevelPlay will not initialize this session."
                );
                return;
            }

            Debug.Log("AdConsentManager: Consent complete. Ads may initialize.");

            Action adsReadyCallbacks = _adsReadyCallbacks;
            _adsReadyCallbacks = null;
            InvokeSafely(adsReadyCallbacks);
        }

        private static void InvokeSafely(Action callback)
        {
            if (callback == null)
                return;

            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Call this from a Settings button if privacy options are required.
        /// </summary>
        public static void ShowPrivacyOptions(
            Action<bool> onClosed = null)
        {
            ConsentForm.ShowPrivacyOptionsForm(showError =>
            {
                bool succeeded = showError == null;
                if (showError != null)
                {
                    Debug.LogWarning(
                        "AdConsentManager: Privacy options form failed: " +
                        showError.Message
                    );
                }

                InvokeSafely(() => onClosed?.Invoke(succeeded));
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Testing only. Do not call this in production.
        /// </summary>
        public static void ResetConsentForTesting()
        {
            ConsentInformation.Reset();

            _hasStartedConsentFlow = false;
            _hasCompletedConsentFlow = false;
            _adsReadyCallbacks = null;
            _consentResolvedCallbacks = null;

            Debug.Log("AdConsentManager: Consent reset for testing.");
        }
#endif
    }
}