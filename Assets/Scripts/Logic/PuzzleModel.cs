using System;
using System.Collections.Generic;

namespace Shikaku.Logic
{
    public sealed class ShikakuRegion
    {
        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Area => Width * Height;
        public int ClueCount { get; internal set; }
        public int ClueValue { get; internal set; }
        public bool IsValid => ClueCount == 1 && ClueValue == Area;
        public bool IsHintLocked { get; internal set; }

        internal ShikakuRegion(int id, int x, int y, int width, int height, bool isHintLocked)
        {
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsHintLocked = isHintLocked;
        }

        public bool Contains(int x, int y)
        {
            return x >= X && x < X + Width && y >= Y && y < Y + Height;
        }

        public bool HasSameBounds(int x, int y, int width, int height)
        {
            return X == x && Y == y && Width == width && Height == height;
        }
    }

    public readonly struct ShikakuRegionEvaluation
    {
        public bool GeometryAllowed { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Area => Width * Height;
        public int ClueCount { get; }
        public int ClueValue { get; }
        public bool IsRuleValid =>
            GeometryAllowed && ClueCount == 1 && ClueValue == Area;

        internal ShikakuRegionEvaluation(
            bool geometryAllowed,
            int x,
            int y,
            int width,
            int height,
            int clueCount,
            int clueValue)
        {
            GeometryAllowed = geometryAllowed;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ClueCount = clueCount;
            ClueValue = clueValue;
        }
    }

    /// <summary>
    /// Region-based Shikaku board state. Clues and the mask are immutable;
    /// player moves are complete axis-aligned rectangles committed atomically.
    /// </summary>
    public class PuzzleModel
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] GivenNumber { get; private set; } = Array.Empty<int>();
        public bool[] CellExists { get; private set; } = Array.Empty<bool>();

        // Temporary compatibility for HUD code that still asks whether a cell
        // is filled. RegionId is the authoritative Shikaku state.
        public int[] Value { get; private set; } = Array.Empty<int>();
        public int[] ComponentId => RegionId;
        public int[] RegionId { get; private set; } = Array.Empty<int>();

        public int SelectedCellIndex { get; private set; } = -1;
        public int SelectedRegionId { get; private set; } = -1;
        public int SelectedComponentId => SelectedRegionId;
        public int SelectedValue => SelectedCellIndex >= 0 && SelectedCellIndex < Value.Length
            ? Value[SelectedCellIndex]
            : 0;

        public int RegionCount => _regions.Count;
        public int PlayableCellCount { get; private set; }
        public int AssignedCellCount { get; private set; }

        public event Action<int> CellChanged;
        public event Action SelectionChanged;
        public event Action SelectionCleared;
        public event Action BoardReset;
        public event Action BoardChanged;

        private readonly Dictionary<int, ShikakuRegion> _regions = new Dictionary<int, ShikakuRegion>();
        private int _nextRegionId;

        public PuzzleModel(int width, int height, int[] givenNumber)
        {
            LoadPuzzle(width, height, givenNumber, null);
        }

        public void LoadPuzzle(PuzzleData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            bool[] exists = BuildExistsFromMask(data.width, data.height, data.mask);
            int[] given = new int[data.width * data.height];

            if (data.givens != null)
            {
                for (int i = 0; i < data.givens.Length; i++)
                {
                    Given clue = data.givens[i];
                    if (clue == null || clue.x < 0 || clue.x >= data.width || clue.y < 0 || clue.y >= data.height)
                        continue;

                    int index = clue.y * data.width + clue.x;
                    if (exists[index])
                        given[index] = clue.v;
                }
            }

            LoadPuzzle(data.width, data.height, given, exists);
        }

        public void LoadPuzzle(int width, int height, int[] givenNumber, bool[] mask)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            Width = width;
            Height = height;
            int total = width * height;
            GivenNumber = new int[total];
            CellExists = new bool[total];
            Value = new int[total];
            RegionId = new int[total];
            PlayableCellCount = 0;

            for (int i = 0; i < total; i++)
            {
                bool exists = mask == null || mask.Length != total || mask[i];
                CellExists[i] = exists;
                RegionId[i] = exists ? -1 : -2;
                if (!exists)
                    continue;

                PlayableCellCount++;
                int clue = givenNumber != null && i < givenNumber.Length ? givenNumber[i] : 0;
                GivenNumber[i] = Math.Max(0, clue);
            }

            _regions.Clear();
            _nextRegionId = 0;
            AssignedCellCount = 0;
            SelectedCellIndex = -1;
            SelectedRegionId = -1;
            CreateAutomaticSingletonRegions();
            BoardReset?.Invoke();
            SelectionChanged?.Invoke();
        }

        private void CreateAutomaticSingletonRegions()
        {
            for (int index = 0; index < GivenNumber.Length; index++)
            {
                if (!CellExistsAt(index) || GivenNumber[index] != 1)
                    continue;

                var region = new ShikakuRegion(
                    _nextRegionId++,
                    index % Width,
                    index / Width,
                    1,
                    1,
                    true);
                EvaluateRegion(region);
                _regions.Add(region.Id, region);
            }

            if (_regions.Count > 0)
                RebuildCellOwnership();
        }

        public static bool[] BuildExistsFromMask(int width, int height, string mask)
        {
            int total = Math.Max(0, width * height);
            var exists = new bool[total];
            for (int i = 0; i < total; i++)
                exists[i] = true;

            if (string.IsNullOrWhiteSpace(mask))
                return exists;

            var bits = new List<char>(total);
            foreach (char character in mask)
            {
                if (character == '0' || character == '1')
                    bits.Add(character);
            }

            if (bits.Count != total)
                return exists;

            for (int i = 0; i < total; i++)
                exists[i] = bits[i] == '1';

            return exists;
        }

        public bool CellExistsAt(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < CellExists.Length && CellExists[cellIndex];
        }

        public bool IsAnchorCell(int cellIndex)
        {
            return CellExistsAt(cellIndex) && GivenNumber[cellIndex] > 0;
        }

        public int GetRegionIdAt(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < RegionId.Length ? RegionId[cellIndex] : -2;
        }

        public ShikakuRegion GetRegionAt(int cellIndex)
        {
            int id = GetRegionIdAt(cellIndex);
            return _regions.TryGetValue(id, out ShikakuRegion region) ? region : null;
        }

        public bool TryGetRegion(int regionId, out ShikakuRegion region)
        {
            return _regions.TryGetValue(regionId, out region);
        }

        public bool IsRegionValidAt(int cellIndex)
        {
            ShikakuRegion region = GetRegionAt(cellIndex);
            return region != null && region.IsValid;
        }

        public bool IsHintLockedCell(int cellIndex)
        {
            ShikakuRegion region = GetRegionAt(cellIndex);
            return region != null && region.IsHintLocked;
        }

        public int GetRegionSizeAt(int cellIndex)
        {
            ShikakuRegion region = GetRegionAt(cellIndex);
            return region != null ? region.Area : 0;
        }

        public int GetRegionClueCountAt(int cellIndex)
        {
            ShikakuRegion region = GetRegionAt(cellIndex);
            return region != null ? region.ClueCount : 0;
        }

        public bool IsRegionSelectedAt(int cellIndex)
        {
            return SelectedRegionId >= 0 && GetRegionIdAt(cellIndex) == SelectedRegionId;
        }

        public bool SelectRegionAt(int cellIndex)
        {
            int id = GetRegionIdAt(cellIndex);
            if (id < 0)
            {
                ClearSelection();
                return false;
            }

            SelectedCellIndex = cellIndex;
            SelectedRegionId = id;
            SelectionChanged?.Invoke();
            return true;
        }

        public void ClearSelection()
        {
            if (SelectedCellIndex == -1 && SelectedRegionId == -1)
                return;

            SelectedCellIndex = -1;
            SelectedRegionId = -1;
            SelectionChanged?.Invoke();
            SelectionCleared?.Invoke();
        }

        public bool CanCommitRegion(int startIndex, int endIndex)
        {
            return TryNormalizeBounds(startIndex, endIndex, out int x, out int y, out int w, out int h) &&
                   CanCommitRectangle(x, y, w, h);
        }

        public ShikakuRegionEvaluation EvaluateRegionCandidate(
            int startIndex,
            int endIndex)
        {
            if (!TryNormalizeBounds(
                    startIndex,
                    endIndex,
                    out int x,
                    out int y,
                    out int regionWidth,
                    out int regionHeight))
            {
                return default;
            }

            bool geometryAllowed =
                CanCommitRectangle(x, y, regionWidth, regionHeight);
            int clueCount = 0;
            int clueValue = 0;

            if (geometryAllowed)
            {
                for (int row = y; row < y + regionHeight; row++)
                {
                    for (int column = x;
                         column < x + regionWidth;
                         column++)
                    {
                        int clue = GivenNumber[row * Width + column];
                        if (clue <= 0)
                            continue;

                        clueCount++;
                        clueValue = clue;
                    }
                }
            }

            return new ShikakuRegionEvaluation(
                geometryAllowed,
                x,
                y,
                regionWidth,
                regionHeight,
                clueCount,
                clueCount == 1 ? clueValue : 0);
        }

        internal void CopyRegionsTo(List<ShikakuRegion> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            foreach (ShikakuRegion region in _regions.Values)
                destination.Add(region);
        }

        public bool CanCommitRectangle(int x, int y, int regionWidth, int regionHeight)
        {
            if (x < 0 || y < 0 || regionWidth <= 0 || regionHeight <= 0 ||
                x + regionWidth > Width || y + regionHeight > Height)
                return false;

            for (int row = y; row < y + regionHeight; row++)
            {
                for (int column = x; column < x + regionWidth; column++)
                {
                    int index = row * Width + column;
                    if (!CellExistsAt(index))
                        return false;

                    ShikakuRegion existing = GetRegionAt(index);
                    if (existing != null && existing.IsHintLocked)
                        return false;
                }
            }

            return true;
        }

        public int CountOverlappedRegions(int startIndex, int endIndex)
        {
            if (!TryNormalizeBounds(startIndex, endIndex, out int x, out int y, out int w, out int h))
                return 0;

            var ids = new HashSet<int>();
            for (int row = y; row < y + h; row++)
            {
                for (int column = x; column < x + w; column++)
                {
                    int id = GetRegionIdAt(row * Width + column);
                    if (id >= 0)
                        ids.Add(id);
                }
            }
            return ids.Count;
        }

        public bool TryCommitRegion(int startIndex, int endIndex, bool hintLocked = false)
        {
            if (!TryNormalizeBounds(startIndex, endIndex, out int x, out int y, out int w, out int h))
                return false;

            return TryCommitRectangle(x, y, w, h, hintLocked);
        }

        public bool TryCommitRectangle(int x, int y, int regionWidth, int regionHeight, bool hintLocked = false)
        {
            // Validate the entire draft before removing any existing region.
            if (!CanCommitRectangle(x, y, regionWidth, regionHeight))
                return false;

            var overlappedIds = new HashSet<int>();
            for (int row = y; row < y + regionHeight; row++)
            {
                for (int column = x; column < x + regionWidth; column++)
                {
                    int id = GetRegionIdAt(row * Width + column);
                    if (id >= 0)
                        overlappedIds.Add(id);
                }
            }

            foreach (int id in overlappedIds)
                _regions.Remove(id);

            var region = new ShikakuRegion(_nextRegionId++, x, y, regionWidth, regionHeight, hintLocked);
            EvaluateRegion(region);
            _regions.Add(region.Id, region);
            SelectedRegionId = region.Id;
            SelectedCellIndex = y * Width + x;
            RebuildCellOwnership();
            BoardChanged?.Invoke();
            SelectionChanged?.Invoke();
            return true;
        }

        public bool TryCommitSolutionRegion(SolutionRegion solutionRegion)
        {
            return solutionRegion != null && TryCommitRectangle(
                solutionRegion.x,
                solutionRegion.y,
                solutionRegion.width,
                solutionRegion.height,
                true);
        }

        public bool IsSolutionRegionAlreadyCorrect(SolutionRegion solutionRegion)
        {
            if (solutionRegion == null || solutionRegion.width <= 0 || solutionRegion.height <= 0)
                return false;

            int firstIndex = solutionRegion.y * Width + solutionRegion.x;
            ShikakuRegion current = GetRegionAt(firstIndex);
            return current != null && current.IsValid && current.HasSameBounds(
                solutionRegion.x,
                solutionRegion.y,
                solutionRegion.width,
                solutionRegion.height);
        }

        public bool TryRemoveRegionAt(int cellIndex)
        {
            ShikakuRegion region = GetRegionAt(cellIndex);
            if (region == null || region.IsHintLocked)
                return false;

            _regions.Remove(region.Id);
            bool removedSelection = SelectedRegionId == region.Id;
            if (removedSelection)
            {
                SelectedCellIndex = -1;
                SelectedRegionId = -1;
            }

            RebuildCellOwnership();
            BoardChanged?.Invoke();
            if (removedSelection)
            {
                SelectionChanged?.Invoke();
                SelectionCleared?.Invoke();
            }
            return true;
        }

        public void ClearRegions()
        {
            if (_regions.Count == 0)
                return;

            _regions.Clear();
            SelectedCellIndex = -1;
            SelectedRegionId = -1;
            RebuildCellOwnership();
            BoardChanged?.Invoke();
            SelectionChanged?.Invoke();
            SelectionCleared?.Invoke();
        }

        public int CountCorrectAssignedCells()
        {
            int count = 0;
            foreach (ShikakuRegion region in _regions.Values)
            {
                if (region.IsValid)
                    count += region.Area;
            }
            return count;
        }

        public bool IsSolved()
        {
            if (PlayableCellCount <= 0 || AssignedCellCount != PlayableCellCount)
                return false;

            int clueCount = 0;
            for (int i = 0; i < GivenNumber.Length; i++)
            {
                if (GivenNumber[i] > 0)
                    clueCount++;
            }

            if (_regions.Count != clueCount)
                return false;

            foreach (ShikakuRegion region in _regions.Values)
            {
                if (!region.IsValid)
                    return false;
            }
            return true;
        }

        // Compatibility names retained while the existing HUD tutorial is retired.
        public int GetComponentIdAt(int cellIndex) => GetRegionIdAt(cellIndex);
        public int GetComponentSizeAt(int cellIndex) => GetRegionSizeAt(cellIndex);
        public bool IsComponentCompleteAt(int cellIndex) => IsRegionValidAt(cellIndex);
        public bool ComponentHasAnchorAt(int cellIndex) => GetRegionClueCountAt(cellIndex) == 1;

        private bool TryNormalizeBounds(int startIndex, int endIndex, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            if (!CellExistsAt(startIndex) || !CellExistsAt(endIndex))
                return false;

            int startX = startIndex % Width;
            int startY = startIndex / Width;
            int endX = endIndex % Width;
            int endY = endIndex / Width;
            x = Math.Min(startX, endX);
            y = Math.Min(startY, endY);
            w = Math.Abs(endX - startX) + 1;
            h = Math.Abs(endY - startY) + 1;
            return true;
        }

        private void EvaluateRegion(ShikakuRegion region)
        {
            int clueCount = 0;
            int clueValue = 0;
            for (int row = region.Y; row < region.Y + region.Height; row++)
            {
                for (int column = region.X; column < region.X + region.Width; column++)
                {
                    int clue = GivenNumber[row * Width + column];
                    if (clue <= 0)
                        continue;
                    clueCount++;
                    clueValue = clue;
                }
            }
            region.ClueCount = clueCount;
            region.ClueValue = clueCount == 1 ? clueValue : 0;
        }

        private void RebuildCellOwnership()
        {
            AssignedCellCount = 0;
            for (int i = 0; i < RegionId.Length; i++)
            {
                RegionId[i] = CellExistsAt(i) ? -1 : -2;
                Value[i] = 0;
            }

            foreach (ShikakuRegion region in _regions.Values)
            {
                int displayValue = region.ClueValue > 0 ? region.ClueValue : 1;
                for (int row = region.Y; row < region.Y + region.Height; row++)
                {
                    for (int column = region.X; column < region.X + region.Width; column++)
                    {
                        int index = row * Width + column;
                        RegionId[index] = region.Id;
                        Value[index] = displayValue;
                        AssignedCellCount++;
                    }
                }
            }
        }
    }
}
