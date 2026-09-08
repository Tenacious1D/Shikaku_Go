using System.Collections.Generic;
using UnityEngine;

namespace Shikaku.Logic
{
    public static class PuzzleLoader
    {
        // -------------------------
        // Pack cache
        // -------------------------
        // Key: packId/path passed to LoadPackCached (relative to Resources/Puzzles, no .json)
        private static readonly Dictionary<string, PuzzlePackData> _packCache = new ();

        /// <summary>
        /// Loads and parses a pack once, then reuses it from memory.
        /// Pass packId like "TimeTrial/Easy_pack01" (relative to Resources/Puzzles).
        /// </summary>
        public static PuzzlePackData LoadPackCached(string packId)
        {
            if (string.IsNullOrEmpty(packId))
            {
                Debug.LogError("PuzzleLoader.LoadPackCached: packId is null/empty");
                return null;
            }

            if (_packCache.TryGetValue(packId, out var cached) && cached != null)
                return cached;

            var ta = Resources.Load<TextAsset>($"Puzzles/{packId}");
            if (ta == null)
            {
                Debug.LogError($"PuzzleLoader: Missing Resources/Puzzles/{packId}.json");
                return null;
            }

            // Make sure it's actually a pack (friendly error)
            if (ta.text.IndexOf("\"puzzles\"", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError($"PuzzleLoader: '{packId}' is not a pack file (missing 'puzzles').");
                return null;
            }

            var pack = JsonUtility.FromJson<PuzzlePackData>(ta.text);
            if (pack == null || pack.puzzles == null || pack.puzzles.Length == 0)
            {
                Debug.LogError($"PuzzleLoader: Failed to parse pack JSON or empty puzzles[] in {packId}");
                return null;
            }

            _packCache[packId] = pack;
            return pack;
        }

        public static PuzzleData LoadFromResourcesPackKey(string packKey)
        {
            if (string.IsNullOrEmpty(packKey))
            {
                Debug.LogError("PuzzleLoader.LoadFromResourcesPackKey: packKey null/empty");
                return null;
            }

            // Expected: "<packId>#<index>"
            int hash = packKey.LastIndexOf('#');
            if (hash < 0 || hash == packKey.Length - 1)
            {
                // Not a pack key, fall back to the old behavior
                return LoadFromResources(packKey);
            }

            string packId = packKey.Substring(0, hash);
            string idxStr = packKey.Substring(hash + 1);

            if (!int.TryParse(idxStr, out int index))
            {
                Debug.LogError($"PuzzleLoader.LoadFromResourcesPackKey: invalid index in '{packKey}'");
                return null;
            }

            // Fast cached pack load
            var pack = LoadPackCached(packId);
            if (pack == null) return null;

            if (index < 0 || index >= pack.puzzles.Length)
            {
                Debug.LogError($"PuzzleLoader: Pack index {index} out of range (0..{pack.puzzles.Length - 1}) for '{packId}'");
                return null;
            }

            var entry = pack.puzzles[index];
            if (entry == null)
            {
                Debug.LogError($"PuzzleLoader: Null entry at index {index} in pack '{packId}'");
                return null;
            }

            // Convert pack entry -> PuzzleData
            var dataOut = new PuzzleData();
            dataOut.id = string.IsNullOrEmpty(entry.id) ? packKey : entry.id;

            int w = (entry.width > 0) ? entry.width : pack.width;
            int h = (entry.height > 0) ? entry.height : pack.height;

            if (w <= 0 || h <= 0)
            {
                Debug.LogError($"PuzzleLoader: Missing width/height in pack '{packId}' (entry or pack).");
                return null;
            }

            dataOut.width = w;
            dataOut.height = h;
            dataOut.mask = !string.IsNullOrEmpty(entry.mask) ? entry.mask : pack.mask; // ✅ NEW
            dataOut.givens = entry.givens ?? new Given[0];
            dataOut.solution = entry.solution;

            return ValidateOrNull(dataOut, packKey);
        }


        // Put json files at: Assets/Resources/Puzzles/<name>.json
        // For a single puzzle file: name == puzzleId
        // For a pack file: name == packId (and you select a puzzle inside it)

        /// <summary>
        /// Backward compatible:
        /// - If JSON is a single puzzle, returns it.
        /// - If JSON is a pack:
        ///    - returns the only puzzle if puzzles.Length == 1
        ///    - otherwise logs error (use LoadFromResourcesPackIndex / PackPuzzleId)
        /// </summary>
        public static PuzzleData LoadFromResources(string puzzleIdOrPackId)
        {
            if (!string.IsNullOrEmpty(puzzleIdOrPackId) && puzzleIdOrPackId.Contains("#"))
                return LoadFromResourcesPackKey(puzzleIdOrPackId);

            var ta = Resources.Load<TextAsset>($"Puzzles/{puzzleIdOrPackId}");
            if (ta == null)
            {
                Debug.LogError($"PuzzleLoader: Missing Resources/Puzzles/{puzzleIdOrPackId}.json");
                return null;
            }

            return ParseSingleOrPack(ta.text, puzzleIdOrPackId, defaultPackIndex: null, defaultPuzzleId: null);
        }

        /// <summary>
        /// Load a puzzle from a pack by index (0-based).
        /// </summary>
        public static PuzzleData LoadFromResourcesPackIndex(string packId, int index)
        {
            var ta = Resources.Load<TextAsset>($"Puzzles/{packId}");
            if (ta == null)
            {
                Debug.LogError($"PuzzleLoader: Missing Resources/Puzzles/{packId}.json");
                return null;
            }

            return ParseSingleOrPack(ta.text, packId, defaultPackIndex: index, defaultPuzzleId: null);
        }

        /// <summary>
        /// Load a puzzle from a pack by puzzleId.
        /// </summary>
        public static PuzzleData LoadFromResourcesPackPuzzleId(string packId, string puzzleId)
        {
            var ta = Resources.Load<TextAsset>($"Puzzles/{packId}");
            if (ta == null)
            {
                Debug.LogError($"PuzzleLoader: Missing Resources/Puzzles/{packId}.json");
                return null;
            }

            return ParseSingleOrPack(ta.text, packId, defaultPackIndex: null, defaultPuzzleId: puzzleId);
        }

        // -------------------------
        // Internal helpers
        // -------------------------

        private static PuzzleData ParseSingleOrPack(string json, string nameForErrors, int? defaultPackIndex, string defaultPuzzleId)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"PuzzleLoader: Empty JSON in {nameForErrors}");
                return null;
            }

            // Heuristic: pack files contain a "puzzles" field.
            bool looksLikePack = json.IndexOf("\"puzzles\"", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikePack)
            {
                // Single puzzle
                var data = JsonUtility.FromJson<PuzzleData>(json);
                if (data == null)
                {
                    Debug.LogError($"PuzzleLoader: Failed to parse single puzzle JSON for {nameForErrors}");
                    return null;
                }

                if (string.IsNullOrEmpty(data.id)) data.id = nameForErrors;
                return ValidateOrNull(data, nameForErrors);
            }

            // Pack
            var pack = JsonUtility.FromJson<PuzzlePackData>(json);
            if (pack == null || pack.puzzles == null || pack.puzzles.Length == 0)
            {
                Debug.LogError($"PuzzleLoader: Failed to parse pack JSON or empty puzzles[] in {nameForErrors}");
                return null;
            }

            PuzzleEntry entry = null;

            if (!string.IsNullOrEmpty(defaultPuzzleId))
            {
                for (int i = 0; i < pack.puzzles.Length; i++)
                {
                    if (pack.puzzles[i] != null && pack.puzzles[i].id == defaultPuzzleId)
                    {
                        entry = pack.puzzles[i];
                        break;
                    }
                }

                if (entry == null)
                {
                    Debug.LogError($"PuzzleLoader: Puzzle id '{defaultPuzzleId}' not found in pack '{nameForErrors}'");
                    return null;
                }
            }
            else if (defaultPackIndex.HasValue)
            {
                int idx = defaultPackIndex.Value;
                if (idx < 0 || idx >= pack.puzzles.Length)
                {
                    Debug.LogError($"PuzzleLoader: Pack index {idx} out of range (0..{pack.puzzles.Length - 1}) for '{nameForErrors}'");
                    return null;
                }
                entry = pack.puzzles[idx];
            }
            else
            {
                // For backward compatibility, allow pack files that contain exactly one puzzle.
                if (pack.puzzles.Length == 1)
                {
                    entry = pack.puzzles[0];
                }
                else
                {
                    Debug.LogError(
                        $"PuzzleLoader: '{nameForErrors}' is a pack with {pack.puzzles.Length} puzzles. " +
                        $"Use LoadFromResourcesPackIndex(packId, index) or LoadFromResourcesPackPuzzleId(packId, puzzleId).");
                    return null;
                }
            }

            if (entry == null)
            {
                Debug.LogError($"PuzzleLoader: Null entry in pack '{nameForErrors}'");
                return null;
            }

            // Build a PuzzleData from pack entry + pack defaults
            var dataOut = new PuzzleData();
            dataOut.id = string.IsNullOrEmpty(entry.id) ? nameForErrors : entry.id;

            // Width/height can be on entry or pack; pick entry if set, else pack.
            int w = (entry.width > 0) ? entry.width : pack.width;
            int h = (entry.height > 0) ? entry.height : pack.height;

            if (w <= 0 || h <= 0)
            {
                Debug.LogError($"PuzzleLoader: Missing width/height in pack '{nameForErrors}' (entry or pack).");
                return null;
            }

            dataOut.width = w;
            dataOut.height = h;
            dataOut.mask = !string.IsNullOrEmpty(entry.mask) ? entry.mask : pack.mask; // ✅ NEW
            dataOut.givens = entry.givens ?? new Given[0];
            dataOut.solution = entry.solution;

            return ValidateOrNull(dataOut, nameForErrors);
        }

        public static bool ValidatePuzzleData(PuzzleData data, out string error)
        {
            error = null;
            if (data == null) { error = "Puzzle data is null."; return false; }
            if (data.width <= 0 || data.height <= 0)
            {
                error = "Puzzle width and height must be positive.";
                return false;
            }

            int total = data.width * data.height;
            bool[] exists = PuzzleModel.BuildExistsFromMask(data.width, data.height, data.mask);
            if (!string.IsNullOrWhiteSpace(data.mask))
            {
                int bitCount = 0;
                foreach (char character in data.mask)
                    if (character == '0' || character == '1') bitCount++;
                if (bitCount != total)
                {
                    error = $"Mask has {bitCount} cells; expected {total}.";
                    return false;
                }
            }

            var cluesByCell = new Dictionary<int, int>();
            if (data.givens == null || data.givens.Length == 0)
            {
                error = "Puzzle has no clues.";
                return false;
            }
            for (int i = 0; i < data.givens.Length; i++)
            {
                Given clue = data.givens[i];
                if (clue == null || clue.v <= 0 || clue.x < 0 || clue.x >= data.width ||
                    clue.y < 0 || clue.y >= data.height)
                {
                    error = $"Clue {i} is invalid.";
                    return false;
                }
                int index = clue.y * data.width + clue.x;
                if (!exists[index] || cluesByCell.ContainsKey(index))
                {
                    error = $"Clue {i} is masked or duplicates another clue.";
                    return false;
                }
                cluesByCell.Add(index, clue.v);
            }

            if (data.solution == null) return true;
            if (data.solution.values != null && data.solution.values.Length != total)
            {
                error = $"Solution values has {data.solution.values.Length} cells; expected {total}.";
                return false;
            }

            SolutionRegion[] regions = data.solution.regions;
            int[] regionIds = data.solution.regionIds;
            if (regions == null || regions.Length == 0 || regionIds == null || regionIds.Length != total)
            {
                error = "Solution must contain regions and a full regionIds array.";
                return false;
            }

            int[] ownedCounts = new int[regions.Length];
            for (int i = 0; i < total; i++)
            {
                if (!exists[i])
                {
                    if (regionIds[i] >= 0)
                    {
                        error = $"Masked cell {i} belongs to a solution region.";
                        return false;
                    }
                    continue;
                }
                if (regionIds[i] < 0 || regionIds[i] >= regions.Length)
                {
                    error = $"Cell {i} has invalid solution region id {regionIds[i]}.";
                    return false;
                }
                ownedCounts[regionIds[i]]++;
            }

            for (int id = 0; id < regions.Length; id++)
            {
                SolutionRegion region = regions[id];
                if (region == null || region.width <= 0 || region.height <= 0 ||
                    region.x < 0 || region.y < 0 || region.x + region.width > data.width ||
                    region.y + region.height > data.height || region.area != region.width * region.height ||
                    ownedCounts[id] != region.area)
                {
                    error = $"Solution region {id} has invalid bounds or area.";
                    return false;
                }

                int clueCount = 0;
                int clueValue = 0;
                for (int y = region.y; y < region.y + region.height; y++)
                {
                    for (int x = region.x; x < region.x + region.width; x++)
                    {
                        int index = y * data.width + x;
                        if (!exists[index] || regionIds[index] != id)
                        {
                            error = $"Solution region {id} ownership is inconsistent.";
                            return false;
                        }
                        if (cluesByCell.TryGetValue(index, out int clue))
                        {
                            clueCount++;
                            clueValue = clue;
                        }
                    }
                }
                if (clueCount != 1 || clueValue != region.area)
                {
                    error = $"Solution region {id} must contain exactly one matching clue.";
                    return false;
                }
            }
            return true;
        }

        private static PuzzleData ValidateOrNull(PuzzleData data, string nameForErrors)
        {
            if (ValidatePuzzleData(data, out string error)) return data;
            Debug.LogError($"PuzzleLoader: Invalid Shikaku puzzle '{nameForErrors}': {error}");
            return null;
        }
    }
}