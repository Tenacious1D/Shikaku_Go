#if UNITY_EDITOR
using Shikaku.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class PuzzleProgressDebugMenu
    {
        private const string MenuPath = "Shikaku City/Progress/Reset Puzzle Progress...";

        [MenuItem(MenuPath)]
        private static void ResetPuzzleProgress()
        {
            if (!CanResetPuzzleProgress()) return;
            if (!EditorUtility.DisplayDialog(
                "Reset puzzle progress?",
                "Reset puzzle completion and best times, Free Play and Adventure unlocks " +
                "and continue positions, Daily completion and streak, and Time Trial scores?\n\n" +
                "Hints, purchases, claimed rewards, achievements, tutorial completion, and " +
                "settings are kept. A separate recovery copy of the current save will be created.",
                "Reset Puzzle Progress", "Cancel")) return;

            if (SaveManager.ResetPuzzleProgressForTesting(out string recoveryPath))
                Debug.Log("Puzzle progress reset. Enter Play Mode to see the fresh progression. " +
                    "Recovery copy: " + recoveryPath);
            else
                Debug.LogError("Puzzle progress reset did not complete. Check the save errors above. " +
                    (string.IsNullOrEmpty(recoveryPath) ? "" : "Recovery copy: " + recoveryPath));
        }

        [MenuItem(MenuPath, true)]
        private static bool CanResetPuzzleProgress()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }
    }
}
#endif