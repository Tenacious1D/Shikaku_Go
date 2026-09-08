using UnityEngine;
using Shikaku.SaveSystem;

namespace Shikaku.Menu
{
    /// <summary>
    /// SaveManager-backed store for durable per-puzzle outcomes.
    /// Puzzle IDs are the immutable IDs from the puzzle JSON; pack paths are
    /// deliberately excluded so moving a puzzle does not lose its progress.
    /// </summary>
    public static class PuzzleProgressStore
    {
        public static string NormalizePuzzleId(string puzzleId)
        {
            if (string.IsNullOrWhiteSpace(puzzleId))
                return string.Empty;

            string normalized = puzzleId.Trim();

            // Accept the previous "<packPath>|<entryId>" value while scenes
            // and developer overrides transition to raw JSON IDs.
            int pipe = normalized.LastIndexOf('|');
            if (pipe >= 0 && pipe < normalized.Length - 1)
                normalized = normalized.Substring(pipe + 1);

            return normalized;
        }

        public static bool IsCompleted(string puzzleId)
        {
            return SaveManager.IsPuzzleCompleted(
                NormalizePuzzleId(puzzleId));
        }

        public static void MarkCompleted(string puzzleId)
        {
            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return;

            SaveManager.MarkPuzzleCompleted(id);
        }

        public static bool TryGetBestTime(
    string puzzleId,
    out float seconds)
        {
            seconds = 0f;

            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return false;

            return SaveManager.TryGetBestTime(
                id,
                out seconds);
        }

        public static bool TryGetBestTime(
            string puzzleId,
            out float seconds,
            out bool usedHint)
        {
            seconds = 0f;
            usedHint = false;

            string id = NormalizePuzzleId(puzzleId);
            if (string.IsNullOrEmpty(id))
                return false;

            return SaveManager.TryGetBestTime(
                id,
                out seconds,
                out usedHint);
        }

        public static bool SaveBestTimeIfBetter(
            string puzzleId,
            float seconds)
        {
            string id = NormalizePuzzleId(puzzleId);

            if (string.IsNullOrEmpty(id) ||
                seconds < 0f)
            {
                return false;
            }

            return SaveManager.SaveBestTimeIfBetter(
                id,
                seconds);
        }

        public static bool SaveBestTimeIfBetter(
            string puzzleId,
            float seconds,
            bool usedHint)
        {
            string id = NormalizePuzzleId(puzzleId);

            if (string.IsNullOrEmpty(id) ||
                seconds < 0f)
            {
                return false;
            }

            return SaveManager.SaveBestTimeIfBetter(
                id,
                seconds,
                usedHint);
        }
    }
}
