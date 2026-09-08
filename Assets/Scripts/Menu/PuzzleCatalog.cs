using System;
using System.Collections.Generic;
using UnityEngine;
using Shikaku.Logic;

namespace Shikaku.Menu
{
    /// <summary>
    /// Runtime catalog for PACKS (Flow Free style).
    ///
    /// Folder rules (matches your ask):
    /// - Free Play packs are ONLY loaded from Resources/Puzzles/FreePlay
    /// - Time Trial packs are ONLY loaded from Resources/Puzzles/TimeTrial
    /// - Daily packs are ONLY loaded from Resources/Puzzles/Daily
    ///
    /// Free Play collection folders are identified by each JSON file's
    /// packId. The folder name and packId must match so content paths remain
    /// stable even though Resources does not expose a TextAsset's folder.
    /// </summary>
    public static class PuzzleCatalog
    {
        public readonly struct PackInfo
        {
            public readonly string PackPath;   // e.g. "FreePlay/freeplay_3x3_pack01" (relative to Resources/Puzzles)
            public readonly string PackId;     // from JSON (optional)
            public readonly string Mode;       // from JSON (optional)
            public readonly int Size;          // square size (w==h) if known; else 0
            public readonly int Count;         // puzzles.Length

            public PackInfo(string packPath, string packId, string mode, int size, int count)
            {
                PackPath = packPath;
                PackId = packId;
                Mode = mode;
                Size = size;
                Count = count;
            }

            public string DisplayName
            {
                get
                {
                    // Prefer a friendly name like "3x3 Pack 01" from packId if possible.
                    // Fallback to file name.
                    if (!string.IsNullOrEmpty(PackId))
                    {
                        // common pattern: freeplay_3x3_pack01
                        var s = PackId;
                        s = s.Replace("freeplay_", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("timetrial_", "", StringComparison.OrdinalIgnoreCase);

                        // turn underscores into spaces
                        s = s.Replace('_', ' ');
                        return s;
                    }
                    return PackPath;
                }
            }
        }

        public sealed class FreePlayCollectionInfo
        {
            private readonly List<PackInfo> _sizePacks =
                new List<PackInfo>();

            internal FreePlayCollectionInfo(string packId)
            {
                PackId = packId;
                DisplayName = FormatFreePlayCollectionName(packId);
            }

            public string PackId { get; }
            public string DisplayName { get; }
            public IReadOnlyList<PackInfo> SizePacks => _sizePacks;

            public int TotalPuzzleCount
            {
                get
                {
                    int total = 0;
                    for (int i = 0; i < _sizePacks.Count; i++)
                        total += _sizePacks[i].Count;

                    return total;
                }
            }

            public bool TryGetSize(int size, out PackInfo sizePack)
            {
                for (int i = 0; i < _sizePacks.Count; i++)
                {
                    if (_sizePacks[i].Size != size)
                        continue;

                    sizePack = _sizePacks[i];
                    return true;
                }

                sizePack = default;
                return false;
            }

            internal bool ContainsSize(int size)
            {
                for (int i = 0; i < _sizePacks.Count; i++)
                {
                    if (_sizePacks[i].Size == size)
                        return true;
                }

                return false;
            }

            internal void AddSizePack(PackInfo sizePack)
            {
                _sizePacks.Add(sizePack);
            }

            internal void SortSizes()
            {
                _sizePacks.Sort((a, b) =>
                    a.Size.CompareTo(b.Size));
            }
        }

        private static bool _built;

        private static readonly List<PackInfo> _freePlayPacks = new ();
        private static readonly List<FreePlayCollectionInfo>
            _freePlayCollections = new ();
        private static readonly List<PackInfo> _timeTrialPacks = new ();
        private static readonly List<PackInfo> _dailyPacks = new ();
        private static readonly List<PackInfo> _storyPacks = new ();

        private static readonly Dictionary<string, List<string>> _packToPuzzleIds = new (StringComparer.Ordinal);
        private static readonly Dictionary<string, string>
            _progressPuzzleLocations = new (StringComparer.Ordinal);

        public static IReadOnlyList<PackInfo> FreePlayPacks { get { BuildIfNeeded(); return _freePlayPacks; } }
        public static IReadOnlyList<FreePlayCollectionInfo> FreePlayCollections { get { BuildIfNeeded(); return _freePlayCollections; } }
        public static IReadOnlyList<PackInfo> StoryPacks { get { BuildIfNeeded(); return _storyPacks; } }


        public static IReadOnlyList<string> GetPackPuzzleIds(string packPath)
        {
            BuildIfNeeded();
            return _packToPuzzleIds.TryGetValue(packPath, out var list) ? list : Array.Empty<string>();
        }


        public static bool TryResolveFreePlayPackPath(
            string packPath,
            out FreePlayCollectionInfo collection,
            out PackInfo sizePack)
        {
            BuildIfNeeded();

            for (int collectionIndex = 0;
                 collectionIndex < _freePlayCollections.Count;
                 collectionIndex++)
            {
                FreePlayCollectionInfo candidate =
                    _freePlayCollections[collectionIndex];

                for (int sizeIndex = 0;
                     sizeIndex < candidate.SizePacks.Count;
                     sizeIndex++)
                {
                    PackInfo candidatePack =
                        candidate.SizePacks[sizeIndex];

                    if (!string.Equals(
                            candidatePack.PackPath,
                            packPath,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    collection = candidate;
                    sizePack = candidatePack;
                    return true;
                }
            }

            collection = null;
            sizePack = default;
            return false;
        }


        private static void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            _freePlayPacks.Clear();
            _freePlayCollections.Clear();
            _timeTrialPacks.Clear();
            _dailyPacks.Clear();
            _packToPuzzleIds.Clear();
            _progressPuzzleLocations.Clear();
            _storyPacks.Clear();

            // IMPORTANT: Do NOT scan Resources/Puzzles root.
            // Only scan the mode subfolders.
            ScanFreePlayCollections();
            ScanFolder("TimeTrial", _timeTrialPacks);
            ScanFolder("Daily", _dailyPacks);
            ScanFolder("Story", _storyPacks);

            // stable order
            _freePlayPacks.Sort((a, b) =>
            {
                int collection = string.Compare(
                    a.PackId,
                    b.PackId,
                    StringComparison.OrdinalIgnoreCase);

                return collection != 0
                    ? collection
                    : a.Size.CompareTo(b.Size);
            });
            _freePlayCollections.Sort((a, b) =>
                string.Compare(
                    a.PackId,
                    b.PackId,
                    StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < _freePlayCollections.Count; i++)
                _freePlayCollections[i].SortSizes();

            _timeTrialPacks.Sort(PackSort);
            _dailyPacks.Sort(PackSort);

            // Story chapters should be ordered by filename/path, not puzzle size.
            // Otherwise a 4x5 Chapter 2 can sort before a 4x4 Chapter 1.
            _storyPacks.Sort((a, b) => string.CompareOrdinal(a.PackPath, b.PackPath));
        }

        private static string FormatFreePlayCollectionName(
            string packId)
        {
            if (string.IsNullOrWhiteSpace(packId))
                return "Puzzle Pack";

            var characters = new List<char>();
            bool capitalizeNext = true;
            char previous = ' ';

            for (int i = 0; i < packId.Length; i++)
            {
                char current = packId[i];

                if (current == '_' || current == '-')
                {
                    if (characters.Count > 0 &&
                        characters[characters.Count - 1] != ' ')
                    {
                        characters.Add(' ');
                    }

                    capitalizeNext = true;
                    previous = current;
                    continue;
                }

                if (char.IsDigit(current) &&
                    char.IsLetter(previous) &&
                    characters.Count > 0 &&
                    characters[characters.Count - 1] != ' ')
                {
                    characters.Add(' ');
                }

                characters.Add(
                    capitalizeNext
                        ? char.ToUpperInvariant(current)
                        : current);
                capitalizeNext = false;
                previous = current;
            }

            return new string(characters.ToArray()).Trim();
        }

        private static int PackSort(PackInfo a, PackInfo b)
        {
            int s = a.Size.CompareTo(b.Size);
            if (s != 0) return s;
            return string.CompareOrdinal(a.PackPath, b.PackPath);
        }

        private static void ScanFreePlayCollections()
        {
            TextAsset[] assets =
                Resources.LoadAll<TextAsset>("Puzzles/FreePlay");
            var collectionsById =
                new Dictionary<string, FreePlayCollectionInfo>(
                    StringComparer.Ordinal);

            foreach (TextAsset asset in assets)
            {
                if (asset == null ||
                    asset.text.IndexOf(
                        "\"puzzles\"",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                PuzzlePackData pack = null;
                try
                {
                    pack = JsonUtility.FromJson<PuzzlePackData>(
                        asset.text);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Could not parse Free Play file '{asset.name}': " +
                        exception.Message);
                }

                if (pack?.puzzles == null ||
                    pack.puzzles.Length == 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(pack.packId))
                {
                    Debug.LogError(
                        $"Free Play file '{asset.name}' has no packId.");
                    continue;
                }

                int width = pack.width;
                int height = pack.height;
                if ((width <= 0 || height <= 0) &&
                    pack.puzzles[0] != null)
                {
                    width = pack.puzzles[0].width;
                    height = pack.puzzles[0].height;
                }

                int size =
                    width > 0 && width == height
                        ? width
                        : 0;

                if (size <= 0)
                {
                    Debug.LogError(
                        $"Free Play file '{asset.name}' in " +
                        $"'{pack.packId}' must use a square board.");
                    continue;
                }

                if (!collectionsById.TryGetValue(
                        pack.packId,
                        out FreePlayCollectionInfo collection))
                {
                    collection =
                        new FreePlayCollectionInfo(pack.packId);
                    collectionsById.Add(pack.packId, collection);
                    _freePlayCollections.Add(collection);
                }

                if (collection.ContainsSize(size))
                {
                    Debug.LogError(
                        $"Free Play collection '{pack.packId}' has " +
                        $"more than one {size}x{size} file.");
                    continue;
                }

                // Convention: folder name matches packId. The TextAsset name
                // supplies the size filename because Resources does not expose
                // the containing folder at runtime.
                string packPath =
                    $"FreePlay/{pack.packId}/{asset.name}";

                if (_packToPuzzleIds.ContainsKey(packPath))
                {
                    Debug.LogError(
                        $"Duplicate Free Play path '{packPath}'.");
                    continue;
                }

                var puzzleIds =
                    new List<string>(pack.puzzles.Length);
                var idsInPack =
                    new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < pack.puzzles.Length; i++)
                {
                    var entry = pack.puzzles[i];
                    string entryId =
                        entry != null ? entry.id : null;

                    if (string.IsNullOrEmpty(entryId))
                    {
                        Debug.LogError(
                            $"Puzzle at index {i} in '{packPath}' has no id.");
                        entryId = $"missing_id_{i}";
                    }

                    if (!idsInPack.Add(entryId))
                    {
                        Debug.LogError(
                            $"Duplicate puzzle id '{entryId}' inside " +
                            $"'{packPath}'.");
                    }

                    if (_progressPuzzleLocations.TryGetValue(
                            entryId,
                            out string existingLocation))
                    {
                        Debug.LogError(
                            $"Duplicate progression puzzle id '{entryId}' " +
                            $"in '{existingLocation}' and '{packPath}'.");
                    }
                    else
                    {
                        _progressPuzzleLocations[entryId] = packPath;
                    }

                    puzzleIds.Add(entryId);
                }

                var info = new PackInfo(
                    packPath,
                    pack.packId,
                    pack.mode,
                    size,
                    pack.puzzles.Length);

                _packToPuzzleIds[packPath] = puzzleIds;
                _freePlayPacks.Add(info);
                collection.AddSizePack(info);
            }
        }

        private static void ScanFolder(string folder, List<PackInfo> dest)
        {
            var assets = Resources.LoadAll<TextAsset>($"Puzzles/{folder}");
            foreach (var ta in assets)
            {
                if (ta == null) continue;

                // Only treat PACK files as packs (must contain "puzzles").
                if (ta.text.IndexOf("\"puzzles\"", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                PuzzlePackData pack = null;
                try { pack = JsonUtility.FromJson<PuzzlePackData>(ta.text); }
                catch { /* ignore parse errors */ }

                if (pack?.puzzles == null || pack.puzzles.Length == 0)
                    continue;

                // Because Resources doesn't provide subfolder names at runtime,
                // this assumes packs are directly inside Resources/Puzzles/<folder>.
                var packPath = $"{folder}/{ta.name}";

                int w = pack.width;
                int h = pack.height;

                // If pack-level w/h missing, infer from first entry.
                if ((w <= 0 || h <= 0) && pack.puzzles[0] != null)
                {
                    w = pack.puzzles[0].width;
                    h = pack.puzzles[0].height;
                }

                int size = (w > 0 && h > 0 && w == h) ? w : 0;
                var info = new PackInfo(packPath, pack.packId, pack.mode, size, pack.puzzles.Length);
                dest.Add(info);

                // Store the immutable IDs from the puzzle JSON. PackPath is
                // retained separately and is used only to locate the content.
                var list = new List<string>(pack.puzzles.Length);
                var idsInPack = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < pack.puzzles.Length; i++)
                {
                    var entry = pack.puzzles[i];
                    var entryId = (entry != null) ? entry.id : null;

                    if (string.IsNullOrEmpty(entryId))
                    {
                        Debug.LogError(
                            $"Puzzle at index {i} in '{packPath}' has no id. " +
                            "Every shipped puzzle needs a permanent JSON id.");
                        entryId = $"missing_id_{i}";
                    }

                    if (!idsInPack.Add(entryId))
                    {
                        Debug.LogError(
                            $"Duplicate puzzle id '{entryId}' inside " +
                            $"the same pack '{packPath}'.");
                    }

                    // Time Trial intentionally may reuse a Free Play puzzle.
                    // It has aggregate scoring and no persistent per-puzzle
                    // completion, so only progression-bearing modes must be
                    // globally unique.
                    if (!folder.Equals(
                            "TimeTrial",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (_progressPuzzleLocations.TryGetValue(
                                entryId,
                                out string existingLocation))
                        {
                            Debug.LogError(
                                $"Duplicate progression puzzle id '{entryId}' " +
                                $"in '{existingLocation}' and '{packPath}'.");
                        }
                        else
                        {
                            _progressPuzzleLocations[entryId] = packPath;
                        }
                    }

                    list.Add(entryId);
                }

                _packToPuzzleIds[packPath] = list;
            }
        }
    }
}
