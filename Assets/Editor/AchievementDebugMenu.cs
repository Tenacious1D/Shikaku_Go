#if UNITY_EDITOR
using Shikaku.Achievements;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class AchievementDebugMenu
    {
        [MenuItem("Shikaku Go/Achievements/Print Current State")]
        private static void PrintCurrentState()
        {
            Debug.Log(AchievementService.BuildDebugSummary());
        }

        [MenuItem("Shikaku Go/Achievements/Re-evaluate Current Progress")]
        private static void ReevaluateCurrentProgress()
        {
            AchievementService.ReconcileLocalProgress();
            Debug.Log(AchievementService.BuildDebugSummary());
        }

        [MenuItem("Shikaku Go/Achievements/Reset Achievement Data")]
        private static void ResetAchievementData()
        {
            AchievementService.ResetLocalAchievementDataForTesting();
            Debug.Log(
                "Local achievement unlocks and achievement metrics were reset. " +
                "Puzzle completion data was not changed.");
        }
    }
}
#endif
