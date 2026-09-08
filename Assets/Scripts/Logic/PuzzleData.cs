using System;

namespace Shikaku.Logic
{
    [Serializable]
    public class SolutionData
    {
        // row-major: index = y * width + x
        public int[] values;

        // Canonical Shikaku ownership for each cell. Region ids preserve
        // boundaries when two rectangles with the same area touch.
        public int[] regionIds;
        public SolutionRegion[] regions;
    }

    [Serializable]
    public class SolutionRegion
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int area;
    }

    [Serializable]
    public class PuzzleData
    {
        public string id;
        public int width;
        public int height;

        // Optional board mask. 1 = cell exists, 0 = hole.
        // Supported formats:
        // "11111/11011/11111"
        // "11111\n11011\n11111"
        public string mask;

        public Given[] givens;
        public SolutionData solution;
    }

    [Serializable]
    public class Given
    {
        public int x;
        public int y;
        public int v;
    }

    // A "pack" file: one JSON containing many puzzles.
    // Example:
    // {
    //   "packId":"freeplay_4x4_pack01",
    //   "mode":"freeplay",
    //   "width":4, "height":4,
    //   "puzzles":[ { "id":"...", "givens":[...] }, ... ]
    // }
    [Serializable]
    public class PuzzlePackData
    {
        public string packId;
        public string mode;

        // Optional: if pack is single-size, you can put these at pack-level.
        public int width;
        public int height;

        // Optional pack-level mask default (entries can override)
        public string mask;

        public PuzzleEntry[] puzzles;
    }

    [Serializable]
    public class PuzzleEntry
    {
        public string id;

        // Optional per-entry size (for daily packs or mixed packs).
        public int width;
        public int height;

        // Optional per-entry mask (overrides pack.mask if provided)
        public string mask;

        public Given[] givens;
        public SolutionData solution;
    }
}

