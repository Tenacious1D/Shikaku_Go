#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Prevents an iOS export when production ad IDs, mediation dependencies,
    /// or release-mode switches are incomplete.
    /// </summary>
    public sealed class IOSAdsBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;

        [MenuItem(
            "Tools/Shikaku Go/iOS/Validate Production Ad Configuration")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> errors =
                IOSAdsReleaseConfiguration.CollectErrors();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Production iOS Ads",
                    "The production iOS ad configuration is valid.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Production iOS Ads Need Attention",
                "- " + string.Join("\n- ", errors),
                "OK");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors =
                IOSAdsReleaseConfiguration.CollectErrors();
            if (errors.Count == 0)
                return;

            throw new BuildFailedException(
                "Production iOS ad configuration failed:\n- " +
                string.Join("\n- ", errors));
        }
    }
}
#endif
