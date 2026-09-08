using System;
using System.Collections.Generic;

namespace Shikaku.Logic
{
    public class PuzzleModel
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public int[] GivenNumber { get; private set; }   // fixed anchors
        public int[] Value { get; private set; }         // 0 empty else number

        // NEW: cell existence mask (true = normal cell, false = hole)
        public bool[] CellExists { get; private set; }

        // Components (recomputed after each change)
        public int[] ComponentId { get; private set; }   // -1 empty else component

        // Cells belonging to regions that were completed by a hint.
        // These cells are treated as locked: they cannot be selected or erased.
        private readonly HashSet<int> _hintLockedCells = new HashSet<int>();

        private readonly List<int> _compSize = new List<int>();
        private readonly List<int> _compValue = new List<int>();
        private readonly List<bool> _compHasAnchor = new List<bool>();

        // Selection is now "a component" (we store a seed cell index)
        public int SelectedCellIndex { get; private set; } = -1;

        public int SelectedComponentId =>
            (SelectedCellIndex >= 0 && SelectedCellIndex < ComponentId.Length) ? ComponentId[SelectedCellIndex] : -1;

        public int SelectedValue =>
            (SelectedCellIndex >= 0 && SelectedCellIndex < Value.Length) ? Value[SelectedCellIndex] : 0;

        public event Action<int> CellChanged;
        public event Action SelectionChanged;
        public event Action SelectionCleared;

        public event Action BoardReset;

        private void TryQueueSameValueNeighbor(
    int idx,
    bool inside,
    int targetValue,
    bool[] visited,
    Queue<int> q)
        {
            if (!inside) return;
            if (idx < 0 || idx >= Value.Length) return;
            if (visited[idx]) return;
            if (!CellExistsAt(idx)) return;
            if (Value[idx] != targetValue) return;

            visited[idx] = true;
            q.Enqueue(idx);
        }

        public PuzzleModel(int width, int height, int[] givenNumber)
        {
            LoadPuzzle(width, height, givenNumber, mask: null);
        }

        public void LoadPuzzle(PuzzleData data)
        {
            if (data == null) return;

            // Build cell existence from optional mask
            bool[] exists = BuildExistsFromMask(data.width, data.height, data.mask);

            int[] given = new int[data.width * data.height];

            if (data.givens != null)
            {
                for (int i = 0; i < data.givens.Length; i++)
                {
                    var g = data.givens[i];
                    if (g == null) continue;
                    if (g.x < 0 || g.x >= data.width || g.y < 0 || g.y >= data.height) continue;

                    int idx = g.y * data.width + g.x;
                    if (exists != null && !exists[idx]) continue; // ignore givens on holes

                    given[idx] = g.v;
                }
            }

            LoadPuzzle(data.width, data.height, given, exists);
        }


        // NEW overload that supports mask
        public void LoadPuzzle(int width, int height, int[] givenNumber, bool[] mask)
        {
            Width = width;
            Height = height;

            int total = width * height;

            GivenNumber = new int[total];
            Value = new int[total];
            ComponentId = new int[total];

            // If mask is null, all cells exist
            CellExists = (mask != null && mask.Length == total) ? mask : null;

            // copy givens into GivenNumber and Value
            for (int i = 0; i < total; i++)
            {
                bool exists = CellExistsAt(i);

                int g = (givenNumber != null && i < givenNumber.Length) ? givenNumber[i] : 0;

                if (!exists)
                {
                    // Hole: forced empty, never a given
                    GivenNumber[i] = 0;
                    Value[i] = 0;
                }
                else
                {
                    GivenNumber[i] = g;
                    Value[i] = g; // anchors start filled as their number
                }
            }

            SelectedCellIndex = -1;

            // A newly loaded/restarted puzzle has no hint-locked regions.
            _hintLockedCells.Clear();

            RebuildComponents();

            // One signal that the whole board should redraw
            BoardReset?.Invoke();
            SelectionChanged?.Invoke();
        }

        private bool CellExistsAt(int idx)
        {
            if (idx < 0 || idx >= Width * Height) return false;
            if (CellExists == null) return true;
            return CellExists[idx];
        }

        private static bool[] BuildExistsFromMask(int width, int height, string maskStr)
        {
            int total = width * height;

            if (string.IsNullOrWhiteSpace(maskStr))
                return null; // null means all cells exist

            // Pull only 0/1 chars
            var bits = new List<char>(total);
            foreach (char c in maskStr)
            {
                if (c == '0' || c == '1') bits.Add(c);
            }

            if (bits.Count != total)
            {
                // If mismatch, ignore mask (treat as rectangle)
                return null;
            }

            var exists = new bool[total];
            for (int i = 0; i < total; i++)
                exists[i] = (bits[i] == '1');

            return exists;
        }

        public bool IsAnchorCell(int cellIndex)
        {
            if (!CellExistsAt(cellIndex)) return false;
            return GivenNumber[cellIndex] > 0;
        }

        public void ClearSelection()
        {
            if (SelectedCellIndex == -1) return;
            SelectedCellIndex = -1;
            SelectionChanged?.Invoke();
            SelectionCleared?.Invoke();
        }

        // True if this cell belongs to a region that was completed by a hint.
        public bool IsHintLockedCell(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Value.Length)
                return false;

            return _hintLockedCells.Contains(cellIndex);
        }

        public bool IsGivenOnlyCompleteComponentAt(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Value.Length)
                return false;

            if (!CellExistsAt(cellIndex))
                return false;

            int componentId = ComponentId[cellIndex];

            if (componentId < 0)
                return false;

            // It must actually be complete.
            if (_compSize[componentId] != _compValue[componentId])
                return false;

            // Every cell in the component must be an original given.
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i))
                    continue;

                if (ComponentId[i] != componentId)
                    continue;

                if (!IsAnchorCell(i))
                    return false;
            }

            return true;
        }

        // Returns whether tapping this filled cell should be allowed to select it.
        public bool CanSelectCell(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Value.Length)
                return false;

            if (!CellExistsAt(cellIndex))
                return false;

            if (Value[cellIndex] == 0)
                return false;

            // Hint-completed regions are locked.
            if (IsHintLockedCell(cellIndex))
                return false;

            // Regions already complete entirely from givens are pointless to select.
            if (IsGivenOnlyCompleteComponentAt(cellIndex))
                return false;

            return true;
        }

        public void SelectCell(int cellIndex)
        {
            if (!CanSelectCell(cellIndex))
                return;

            SelectedCellIndex = cellIndex;
            SelectionChanged?.Invoke();
        }

        // Can paint ONLY empty non-anchor, adjacent to the selected COMPONENT, and not exceeding size
        public bool CanPaintCell(int cellIndex)
        {
            if (!IsPlacementCandidate(cellIndex, requireFilled: false))
                return false;

            return GetProjectedSelectedComponentSize(cellIndex) <= SelectedValue;
        }

        /// <summary>
        /// Returns true when painting this empty cell would connect same-number
        /// components into a region larger than the selected number allows.
        /// </summary>
        public bool WouldPaintOverflow(int cellIndex)
        {
            return IsPlacementCandidate(cellIndex, requireFilled: false) &&
                   GetProjectedSelectedComponentSize(cellIndex) > SelectedValue;
        }

        /// <summary>
        /// True when the filled cell is a player-editable neighbor of the
        /// selected component. The candidate can still be rejected for overflow.
        /// </summary>
        public bool IsOverwriteCandidate(int cellIndex)
        {
            return IsPlacementCandidate(cellIndex, requireFilled: true);
        }

        public bool CanOverwriteCell(int cellIndex)
        {
            return IsOverwriteCandidate(cellIndex) &&
                   GetProjectedSelectedComponentSize(cellIndex) <= SelectedValue;
        }

        public bool WouldOverwriteOverflow(int cellIndex)
        {
            return IsOverwriteCandidate(cellIndex) &&
                   GetProjectedSelectedComponentSize(cellIndex) > SelectedValue;
        }

        private bool IsPlacementCandidate(int cellIndex, bool requireFilled)
        {
            if (SelectedCellIndex == -1)
                return false;

            if (cellIndex < 0 || cellIndex >= Value.Length)
                return false;

            if (!CellExistsAt(cellIndex) || IsAnchorCell(cellIndex))
                return false;

            bool isFilled = Value[cellIndex] != 0;
            if (isFilled != requireFilled)
                return false;

            if (requireFilled && IsHintLockedCell(cellIndex))
                return false;

            int selectedComp = SelectedComponentId;
            if (selectedComp < 0)
                return false;

            if (requireFilled && ComponentId[cellIndex] == selectedComp)
                return false;

            int number = _compValue[selectedComp];
            int currentSize = _compSize[selectedComp];

            // A complete region has no meaningful placement attempt.
            if (currentSize >= number)
                return false;

            return HasOrthogonalNeighborInComponent(cellIndex, selectedComp);
        }

        private int GetProjectedSelectedComponentSize(int cellIndex)
        {
            int selectedComp = SelectedComponentId;
            if (selectedComp < 0)
                return int.MaxValue;

            int number = _compValue[selectedComp];
            int mergedSize = _compSize[selectedComp] + 1;

            int cL = GetNeighborComponentId(cellIndex, -1, 0);
            int cR = GetNeighborComponentId(cellIndex, 1, 0);
            int cU = GetNeighborComponentId(cellIndex, 0, -1);
            int cD = GetNeighborComponentId(cellIndex, 0, 1);

            mergedSize += AddOtherIfSameNumber(
                cL, selectedComp, number, -1);
            mergedSize += AddOtherIfSameNumber(
                cR, selectedComp, number, cL);
            mergedSize += AddOtherIfSameNumber(
                cU, selectedComp, number, cL, cR);
            mergedSize += AddOtherIfSameNumber(
                cD, selectedComp, number, cL, cR, cU);

            return mergedSize;
        }

        private int AddOtherIfSameNumber(int other, int selectedComp, int number, int ignoreA)
        {
            if (other == -1 || other == selectedComp || other == ignoreA) return 0;
            if (_compValue[other] != number) return 0;
            return _compSize[other];
        }

        private int AddOtherIfSameNumber(int other, int selectedComp, int number, int ignoreA, int ignoreB)
        {
            if (other == -1 || other == selectedComp || other == ignoreA || other == ignoreB) return 0;
            if (_compValue[other] != number) return 0;
            return _compSize[other];
        }

        private int AddOtherIfSameNumber(int other, int selectedComp, int number, int ignoreA, int ignoreB, int ignoreC)
        {
            if (other == -1 || other == selectedComp || other == ignoreA || other == ignoreB || other == ignoreC) return 0;
            if (_compValue[other] != number) return 0;
            return _compSize[other];
        }

        private bool HasOrthogonalNeighborInComponent(int cellIndex, int comp)
        {
            int x = cellIndex % Width;
            int y = cellIndex / Width;

            if (x > 0)
            {
                int n = cellIndex - 1;
                if (CellExistsAt(n) && ComponentId[n] == comp) return true;
            }
            if (x < Width - 1)
            {
                int n = cellIndex + 1;
                if (CellExistsAt(n) && ComponentId[n] == comp) return true;
            }
            if (y > 0)
            {
                int n = cellIndex - Width;
                if (CellExistsAt(n) && ComponentId[n] == comp) return true;
            }
            if (y < Height - 1)
            {
                int n = cellIndex + Width;
                if (CellExistsAt(n) && ComponentId[n] == comp) return true;
            }

            return false;
        }

        public void TryApplyToCell(int cellIndex)
        {
            if (!CanPaintCell(cellIndex)) return;

            int n = SelectedValue;
            Value[cellIndex] = n;
            CellChanged?.Invoke(cellIndex);

            RebuildComponents();

            // If the selected component (after rebuild) is now complete, clear selection
            int comp = SelectedComponentId;
            if (comp != -1 && _compSize[comp] >= _compValue[comp])
                ClearSelection();
            else
                SelectionChanged?.Invoke(); // refresh counts/labels immediately
        }

        public bool TryOverwriteCell(int cellIndex)
        {
            if (!CanOverwriteCell(cellIndex))
                return false;

            int selectedValue = SelectedValue;
            int displacedValue = Value[cellIndex];

            Value[cellIndex] = selectedValue;
            CellChanged?.Invoke(cellIndex);

            RebuildComponents();

            // Replacing one cell can split its old region. Keep every piece
            // that still reaches an anchor and remove only detached pieces.
            List<int> removedCells =
                PruneOrphanComponentsOfValue(displacedValue);

            for (int i = 0; i < removedCells.Count; i++)
                CellChanged?.Invoke(removedCells[i]);

            RebuildComponents();

            int selectedComp = SelectedComponentId;
            if (selectedComp != -1 &&
                _compSize[selectedComp] >= _compValue[selectedComp])
            {
                ClearSelection();
            }
            else
            {
                SelectionChanged?.Invoke();
            }

            return true;
        }

        public void ForceSolveCellsFromSolution(
    IReadOnlyList<int> targetCells,
    int[] solutionValues)
        {
            if (targetCells == null || targetCells.Count == 0)
                return;

            if (solutionValues == null ||
                solutionValues.Length != Value.Length)
                return;

            int firstTarget = targetCells[0];

            if (firstTarget < 0 || firstTarget >= Value.Length)
                return;

            int targetValue = solutionValues[firstTarget];

            if (targetValue <= 0)
                return;

            bool[] isTarget = new bool[Value.Length];

            // Remember every value that the hint overwrites.
            HashSet<int> displacedValues = new HashSet<int>();

            for (int i = 0; i < targetCells.Count; i++)
            {
                int idx = targetCells[i];

                if (idx < 0 || idx >= Value.Length)
                    continue;

                if (!CellExistsAt(idx))
                    continue;

                isTarget[idx] = true;
            }

            // Overwrite the target solution region.
            for (int i = 0; i < targetCells.Count; i++)
            {
                int idx = targetCells[i];

                if (idx < 0 || idx >= Value.Length)
                    continue;

                if (!CellExistsAt(idx))
                    continue;

                int previousValue = Value[idx];

                if (previousValue == targetValue)
                    continue;

                // Track the old color so its detached pieces can be cleaned up.
                if (previousValue > 0)
                    displacedValues.Add(previousValue);

                Value[idx] = targetValue;
                CellChanged?.Invoke(idx);
            }

            // Remove wrong same-number cells connected to the solved region.
            // This prevents a solved region from becoming part of a larger
            // incorrectly drawn region with the same number.
            bool[] visited = new bool[Value.Length];
            Queue<int> q = new Queue<int>();

            for (int i = 0; i < targetCells.Count; i++)
            {
                int idx = targetCells[i];

                if (idx < 0 || idx >= Value.Length)
                    continue;

                if (!CellExistsAt(idx))
                    continue;

                if (visited[idx])
                    continue;

                visited[idx] = true;
                q.Enqueue(idx);
            }

            while (q.Count > 0)
            {
                int cur = q.Dequeue();

                int x = cur % Width;
                int y = cur / Width;

                TryQueueSameValueNeighbor(
                    cur - 1,
                    x > 0,
                    targetValue,
                    visited,
                    q);

                TryQueueSameValueNeighbor(
                    cur + 1,
                    x < Width - 1,
                    targetValue,
                    visited,
                    q);

                TryQueueSameValueNeighbor(
                    cur - Width,
                    y > 0,
                    targetValue,
                    visited,
                    q);

                TryQueueSameValueNeighbor(
                    cur + Width,
                    y < Height - 1,
                    targetValue,
                    visited,
                    q);
            }

            for (int i = 0; i < Value.Length; i++)
            {
                if (!visited[i])
                    continue;

                if (isTarget[i])
                    continue;

                if (!CellExistsAt(i))
                    continue;

                if (IsAnchorCell(i))
                    continue;

                if (Value[i] == 0)
                    continue;

                Value[i] = 0;
                CellChanged?.Invoke(i);
            }

            // Recalculate components after the hint changed the board.
            RebuildComponents();

            // Delete pieces of overwritten regions that are now detached
            // from their fixed anchors.
            foreach (int displacedValue in displacedValues)
            {
                List<int> removedCells =
                    PruneOrphanComponentsOfValue(displacedValue);

                for (int i = 0; i < removedCells.Count; i++)
                    CellChanged?.Invoke(removedCells[i]);
            }

            // Recalculate again after deleting the detached pieces.
            RebuildComponents();

            // Every cell belonging to the solution region completed by this hint
            // is now permanently locked until the puzzle is restarted.
            for (int i = 0; i < targetCells.Count; i++)
            {
                int idx = targetCells[i];

                if (idx < 0 || idx >= Value.Length)
                    continue;

                if (!CellExistsAt(idx))
                    continue;

                _hintLockedCells.Add(idx);
            }

            SelectedCellIndex = -1;
            SelectionChanged?.Invoke();
        }

        public bool TryEraseRegionFromAnchor(int anchorIndex)
        {
            if (anchorIndex < 0 || anchorIndex >= Value.Length)
                return false;

            if (!CellExistsAt(anchorIndex))
                return false;

            if (!IsAnchorCell(anchorIndex))
                return false;

            if (Value[anchorIndex] == 0)
                return false;

            int componentId = ComponentId[anchorIndex];

            if (componentId < 0)
                return false;

            // If any cell in this component was completed by a hint,
            // the entire region is locked and cannot be erased.
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i))
                    continue;

                if (ComponentId[i] != componentId)
                    continue;

                if (IsHintLockedCell(i))
                    return false;
            }

            var cellsToErase = new List<int>();

            // Collect every non-anchor cell currently connected
            // to this anchor as part of the same-number component.
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i))
                    continue;

                if (ComponentId[i] != componentId)
                    continue;

                // Never erase fixed puzzle anchors.
                if (IsAnchorCell(i))
                    continue;

                if (Value[i] != 0)
                    cellsToErase.Add(i);
            }

            // The anchor currently has no drawn cells attached.
            if (cellsToErase.Count == 0)
                return false;

            for (int i = 0; i < cellsToErase.Count; i++)
            {
                int cellIndex = cellsToErase[i];

                Value[cellIndex] = 0;
                CellChanged?.Invoke(cellIndex);
            }

            RebuildComponents();

            // Keep the tapped anchor selected after clearing its region.
            SelectCell(anchorIndex);

            return true;
        }

        public void TryRemoveFromCell(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Value.Length) return;
            if (!CellExistsAt(cellIndex)) return;
            if (IsAnchorCell(cellIndex)) return;
            if (Value[cellIndex] == 0) return;

            // Hint-completed cells cannot be erased.
            if (IsHintLockedCell(cellIndex)) return;

            // --- SNAPSHOT selection BEFORE mutation/rebuild ---
            int prevSelectedCell = SelectedCellIndex;

            int prevCompId = -1;
            int prevAnchorIndex = -1;
            var prevCompCells = new System.Collections.Generic.List<int>(32);

            if (prevSelectedCell >= 0 && prevSelectedCell < Value.Length && Value[prevSelectedCell] != 0)
            {
                prevCompId = ComponentId[prevSelectedCell];

                if (prevCompId != -1)
                {
                    for (int i = 0; i < Value.Length; i++)
                    {
                        if (ComponentId[i] == prevCompId)
                        {
                            prevCompCells.Add(i);
                            if (prevAnchorIndex == -1 && IsAnchorCell(i))
                                prevAnchorIndex = i;
                        }
                    }
                }
            }

            int removedValue = Value[cellIndex];

            // --- ERASE ---
            Value[cellIndex] = 0;
            CellChanged?.Invoke(cellIndex);

            RebuildComponents();

            // prune orphan components of that number (no anchors)
            var removedCells = PruneOrphanComponentsOfValue(removedValue);
            for (int i = 0; i < removedCells.Count; i++)
                CellChanged?.Invoke(removedCells[i]);

            RebuildComponents();

            // --- RESTORE selection to the PREVIOUSLY selected blob ---
            // Prefer the anchor of the previously selected blob (stable identity)
            if (prevAnchorIndex != -1 && Value[prevAnchorIndex] != 0)
            {
                SelectCell(prevAnchorIndex);
            }
            // Otherwise, keep previous selected cell if it still exists
            else if (prevSelectedCell != -1 && Value[prevSelectedCell] != 0)
            {
                SelectCell(prevSelectedCell);
            }
            // Otherwise, select any remaining cell that used to be in that blob
            else
            {
                bool reselected = false;
                for (int k = 0; k < prevCompCells.Count; k++)
                {
                    int i = prevCompCells[k];
                    if (i >= 0 && i < Value.Length && Value[i] != 0)
                    {
                        SelectCell(i);
                        reselected = true;
                        break;
                    }
                }

                if (!reselected)
                    ClearSelection();
            }

            SelectionChanged?.Invoke();
        }

        // Remove any components of a specific number that have zero anchors inside them.
        private List<int> PruneOrphanComponentsOfValue(int number)
        {
            var removed = new List<int>();

            // mark kill-components
            bool[] kill = new bool[_compSize.Count];
            for (int c = 0; c < _compSize.Count; c++)
            {
                if (_compValue[c] == number && !_compHasAnchor[c])
                    kill[c] = true;
            }

            // clear cells belonging to killed components
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i)) continue;

                int c = ComponentId[i];
                if (c == -1) continue;
                if (kill[c])
                {
                    if (Value[i] != 0)
                    {
                        Value[i] = 0;
                        removed.Add(i);
                    }
                }
            }

            return removed;
        }

        // --- Component queries (used by UI/validation) ---

        public int GetComponentIdAt(int cellIndex) => ComponentId[cellIndex];

        public int GetComponentSizeAt(int cellIndex)
        {
            int c = ComponentId[cellIndex];
            return (c >= 0 && c < _compSize.Count) ? _compSize[c] : 0;
        }


        public bool IsComponentCompleteAt(int cellIndex)
        {
            int c = ComponentId[cellIndex];
            if (c < 0) return false;
            return _compSize[c] == _compValue[c];
        }

        public bool ComponentHasAnchorAt(int cellIndex)
        {
            int c = ComponentId[cellIndex];
            if (c < 0) return false;
            return _compHasAnchor[c];
        }

        public bool IsSolved()
        {
            // Must be full (no zeros) for EXISTING cells only
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i)) continue;
                if (Value[i] == 0) return false;
            }

            // Givens must be satisfied (existing cells only)
            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i)) continue;
                if (GivenNumber[i] > 0 && Value[i] != GivenNumber[i]) return false;
            }

            // Every component must be exactly its number AND must contain at least one given
            for (int c = 0; c < _compSize.Count; c++)
            {
                if (_compSize[c] != _compValue[c]) return false;
                if (!_compHasAnchor[c]) return false;
            }

            return true;
        }

        // Rebuild connected components for current Value[] (fast for small boards)
        private void RebuildComponents()
        {
            Array.Fill(ComponentId, -1);
            _compSize.Clear();
            _compValue.Clear();
            _compHasAnchor.Clear();

            int nextId = 0;
            var q = new Queue<int>();

            for (int i = 0; i < Value.Length; i++)
            {
                if (!CellExistsAt(i)) continue;
                if (Value[i] == 0) continue;
                if (ComponentId[i] != -1) continue;

                int val = Value[i];
                int count = 0;
                bool hasAnchor = false;

                ComponentId[i] = nextId;
                q.Enqueue(i);

                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    count++;

                    if (GivenNumber[cur] > 0) hasAnchor = true;

                    int x = cur % Width;
                    int y = cur / Width;

                    TryFlood(cur - 1, x > 0, val, nextId, q);
                    TryFlood(cur + 1, x < Width - 1, val, nextId, q);
                    TryFlood(cur - Width, y > 0, val, nextId, q);
                    TryFlood(cur + Width, y < Height - 1, val, nextId, q);
                }

                _compSize.Add(count);
                _compValue.Add(val);
                _compHasAnchor.Add(hasAnchor);

                nextId++;
            }
        }

        private void TryFlood(int ni, bool inside, int val, int compId, Queue<int> q)
        {
            if (!inside) return;
            if (ni < 0 || ni >= Value.Length) return;
            if (!CellExistsAt(ni)) return;
            if (ComponentId[ni] != -1) return;
            if (Value[ni] != val) return;

            ComponentId[ni] = compId;
            q.Enqueue(ni);
        }

        private int GetNeighborComponentId(int index, int dx, int dy)
        {
            int x = index % Width;
            int y = index / Width;
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) return -1;

            int ni = ny * Width + nx;
            if (!CellExistsAt(ni)) return -1;

            return ComponentId[ni];
        }
    }
}