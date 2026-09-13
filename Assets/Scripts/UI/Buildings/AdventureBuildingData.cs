using System;
using System.Collections.Generic;
using Shikaku.Logic;
using Shikaku.Menu;
using UnityEngine;

namespace Shikaku.UI.Buildings
{
    // Cached content projection, never persisted as a second progression system.
    public sealed class AdventureBuildingData
    {
        public readonly struct Floor
        {
            public readonly string PuzzleId;
            public readonly int Width;
            public readonly int Depth;
            private readonly bool[] _occupied;
            public readonly int OccupiedCellCount;

            public Floor(string puzzleId, int width, int depth, string mask = null)
            {
                PuzzleId = puzzleId;
                Width = width;
                Depth = depth;
                if (!string.IsNullOrWhiteSpace(mask))
                {
                    int bits = 0;
                    foreach (char c in mask) if (c == '0' || c == '1') bits++;
                    if (bits != width * depth)
                        throw new ArgumentException($"Invalid building mask for '{puzzleId}': expected {width * depth} cells.");
                }
                _occupied = PuzzleModel.BuildExistsFromMask(width, depth, mask);
                int count = 0;
                foreach (bool cell in _occupied) if (cell) count++;
                OccupiedCellCount = count;
                if (count == 0) throw new ArgumentException($"Building floor '{puzzleId}' has an empty mask.");
            }

            public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Depth &&
                _occupied != null && _occupied[y * Width + x];

            public bool ContainsWorld(float x, float y)
                => Contains(Mathf.FloorToInt(x + Width * 0.5f), Mathf.FloorToInt(y + Depth * 0.5f));
        }

        private static readonly Dictionary<string, AdventureBuildingData> Cache = new();
        private static BuildingDefinition[] _definitions;
        public string PackPath { get; }
        public string StableId { get; }
        public BuildingDefinition Definition { get; }
        public IReadOnlyList<Floor> Floors { get; }
        public IReadOnlyList<BuildingGeometry.Patch> Patches { get; }
        public BuildingPalette Appearance { get; }

        private AdventureBuildingData(string path, string stableId, BuildingDefinition definition, Floor[] floors)
        {
            PackPath = path;
            StableId = stableId;
            Definition = definition;
            Floors = Array.AsReadOnly(floors);
            Appearance = BuildingAppearance.Resolve(stableId, definition);
            Patches = BuildingGeometry.BuildPatches(this).AsReadOnly();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            Cache.Clear();
            _definitions = null;
        }

        public static AdventureBuildingData Load(string packPath)
        {
            if (string.IsNullOrEmpty(packPath)) return null;
            if (Cache.TryGetValue(packPath, out var data)) return data;
            var pack = PuzzleLoader.LoadPackCached(packPath);
            if (pack == null) return null;
            _definitions ??= Resources.LoadAll<BuildingDefinition>("Buildings");
            BuildingDefinition definition = null;
            foreach (var candidate in _definitions)
            {
                if (candidate.chapterPackPath != packPath) continue;
                if (definition != null)
                {
                    Debug.LogError($"Duplicate BuildingDefinition for '{packPath}'. Use one definition per chapter.");
                    return null;
                }
                definition = candidate;
            }
            try { data = FromPack(packPath, pack, definition); }
            catch (ArgumentException exception) { Debug.LogError(exception.Message); return null; }
            Cache.Add(packPath, data);
            return data;
        }

        public static AdventureBuildingData FromPack(
            string path, PuzzlePackData pack, BuildingDefinition definition = null)
        {
            if (pack?.puzzles == null || pack.puzzles.Length == 0)
                throw new ArgumentException($"Building chapter '{path}' has no puzzles.");
            var floors = new Floor[pack.puzzles.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < floors.Length; i++)
            {
                var entry = pack.puzzles[i];
                int width = entry != null && entry.width > 0 ? entry.width : pack.width;
                int depth = entry != null && entry.height > 0 ? entry.height : pack.height;
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) ||
                    !ids.Add(entry.id) || width <= 0 || depth <= 0)
                    throw new ArgumentException($"Invalid building floor {i + 1} in '{path}'. Check puzzle ID and dimensions.");
                string mask = !string.IsNullOrEmpty(entry.mask) ? entry.mask : pack.mask;
                floors[i] = new Floor(entry.id, width, depth, mask);
            }
            string stableId = !string.IsNullOrWhiteSpace(definition?.buildingId) ? definition.buildingId :
                !string.IsNullOrWhiteSpace(pack.packId) ? pack.packId : path;
            return new AdventureBuildingData(path, stableId, definition, floors);
        }

        public bool[] ReadCompletion()
        {
            var completed = new bool[Floors.Count];
            for (int i = 0; i < completed.Length; i++)
                completed[i] = PuzzleProgressStore.IsCompleted(Floors[i].PuzzleId);
            return completed;
        }

        public int FindFloor(string puzzleId)
        {
            string id = PuzzleProgressStore.NormalizePuzzleId(puzzleId);
            for (int i = 0; i < Floors.Count; i++) if (Floors[i].PuzzleId == id) return i;
            return -1;
        }
    }
}
