using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    public class AlsXz : ISudokuSolverStrategy
    {
        public string StrategyName => "ALS-XZ";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // 1. Find all ALS in the puzzle
            var allAls = AlmostLockedSet.FindAll(sudokuPuzzle);
            System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Found {allAls.Count} total ALS in puzzle");

            // 2. Iterate all unique pairs
            for (int i = 0; i < allAls.Count; i++)
            {
                for (int j = i + 1; j < allAls.Count; j++)
                {
                    var als1 = allAls[i];
                    var als2 = allAls[j];

                    // Optimization: They must share candidates to interact
                    if (!als1.Candidates.Overlaps(als2.Candidates))
                        continue;

                    // CRITICAL: The two ALS must NOT share any cells (must be disjoint)
                    if (als1.Cells.Any(c => als2.Cells.Contains(c)))
                        continue;

                    // 3. Find Restricted Common Candidate (RCC - X)
                    // X is in both ALS.
                    // ALL cells in als1 with X must see ALL cells in als2 with X.
                    var commonCandidates = als1.Candidates.Intersect(als2.Candidates).ToList();

                    foreach (var x in commonCandidates)
                    {
                        if (IsRestrictedCommonCandidate(sudokuPuzzle, als1, als2, x, out int sharedHouseIndex))
                        {
                            // We found an RCC (X).
                            // Now look for Elimination Candidate (Z).
                            // Z is any other common candidate (or effectively any candidate Z in both? 
                            // Standard ALS-XZ: Z is another common candidate different from X.
                            // Extended: Z could be non-common if strong links exist, but let's stick to standard.)

                            foreach (var z in commonCandidates)
                            {
                                if (z == x) continue;

                                // 4. Find Valid Eliminations
                                // Eliminate Z from any cell that sees ALL Z-cells in als1 AND ALL Z-cells in als2.
                                var eliminations = GetEliminations(sudokuPuzzle, als1, als2, z);

                                if (eliminations.Count > 0)
                                {
                                    // === EXTENSIVE DEBUG LOGGING ===
                                    string als1Cells = string.Join(", ", als1.Cells.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"));
                                    string als2Cells = string.Join(", ", als2.Cells.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"));
                                    string als1Cands = string.Join(",", als1.Candidates);
                                    string als2Cands = string.Join(",", als2.Candidates);

                                    // Per-cell candidates
                                    string als1CellDetails = string.Join(", ", als1.Cells.Select(c =>
                                        $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}[{string.Join(",", c.CanBe)}]"));
                                    string als2CellDetails = string.Join(", ", als2.Cells.Select(c =>
                                        $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}[{string.Join(",", c.CanBe)}]"));

                                    // Z-cells in each set
                                    var zCells1 = als1.Cells.Where(c => c.CanBe.Contains(z)).ToList();
                                    var zCells2 = als2.Cells.Where(c => c.CanBe.Contains(z)).ToList();
                                    string zCells1Str = string.Join(", ", zCells1.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"));
                                    string zCells2Str = string.Join(", ", zCells2.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"));

                                    string elimStr = string.Join(", ", eliminations.Select(e => $"R{e.IndexOfRow + 1}C{e.IndexOfColumn + 1}"));

                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] ========== SOLUTION FOUND ==========");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Set A: Cells=[{als1Cells}], Candidates=[{als1Cands}]");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Set A Details: {als1CellDetails}");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Set B: Cells=[{als2Cells}], Candidates=[{als2Cands}]");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Set B Details: {als2CellDetails}");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] RCC (X) = {x}, SharedHouse = {sharedHouseIndex}");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Z = {z}");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Z-cells in Set A: [{zCells1Str}]");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Z-cells in Set B: [{zCells2Str}]");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] Eliminations: [{elimStr}] (removing {z})");
                                    System.Diagnostics.Debug.WriteLine($"[ALS-XZ] =====================================");

                                    // Found a solution!
                                    var solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                                    solution.ContextData = new HintContextData
                                    {
                                        FocusCells = als1.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                        ReasoningCells = als2.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                        FocusCandidates = als1.Candidates.ToList(),
                                        ReasoningCandidates = als2.Candidates.ToList(),
                                        PrimaryCandidate = x,
                                        HouseIndices = sharedHouseIndex >= 0 ? new List<int> { sharedHouseIndex } : new List<int>()
                                    };
                                    return solution;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private bool IsRestrictedCommonCandidate(SudokuPuzzle puzzle, AlmostLockedSet als1, AlmostLockedSet als2, int candidate, out int sharedHouseIndex)
        {
            sharedHouseIndex = -1;
            var cells1 = als1.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();
            var cells2 = als2.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();

            if (cells1.Count == 0 || cells2.Count == 0) return false;

            // All cells1 must see all cells2 via a SINGLE house
            // Check if all RCC cells share a row
            if (cells1.All(c => c.RowIndex == cells1[0].RowIndex) &&
                cells2.All(c => c.RowIndex == cells1[0].RowIndex))
            {
                sharedHouseIndex = cells1[0].RowIndex; // Row 0-8
                return true;
            }

            // Check if all RCC cells share a column
            if (cells1.All(c => c.ColumnIndex == cells1[0].ColumnIndex) &&
                cells2.All(c => c.ColumnIndex == cells1[0].ColumnIndex))
            {
                sharedHouseIndex = 9 + cells1[0].ColumnIndex; // Column = 9-17
                return true;
            }

            // Check if all RCC cells share a block
            var block1 = puzzle.GetBlockIndex(cells1[0]);
            bool allSameBlock = cells1.All(c =>
            {
                var b = puzzle.GetBlockIndex(c);
                return b.RowIndex == block1.RowIndex && b.ColumnIndex == block1.ColumnIndex;
            }) && cells2.All(c =>
            {
                var b = puzzle.GetBlockIndex(c);
                return b.RowIndex == block1.RowIndex && b.ColumnIndex == block1.ColumnIndex;
            });

            if (allSameBlock)
            {
                // Block index: 0-8 based on block position (row * 3 + col)
                sharedHouseIndex = 18 + block1.RowIndex * 3 + block1.ColumnIndex;
                return true;
            }

            // Fallback: check if they at least all see each other
            foreach (var c1 in cells1)
            {
                foreach (var c2 in cells2)
                {
                    if (!AreCellsVisible(puzzle, c1, c2))
                        return false;
                }
            }
            return true; // They see each other but not via a single identified house
        }

        private List<SingleStepSolution.Candidate> GetEliminations(SudokuPuzzle puzzle, AlmostLockedSet als1, AlmostLockedSet als2, int z)
        {
            var result = new List<SingleStepSolution.Candidate>();

            var zCells1 = als1.Cells.Where(c => c.CanBe.Contains(z)).ToList();
            var zCells2 = als2.Cells.Where(c => c.CanBe.Contains(z)).ToList();

            // Get all peers of ALL Z-cells from both sets
            var peers1 = GetCombinedPeers(puzzle, zCells1);
            var peers2 = GetCombinedPeers(puzzle, zCells2);

            // Intersection: cells that are peers of both sets
            var commonPeers = new HashSet<Cell>(peers1);
            commonPeers.IntersectWith(peers2);

            // Iterate common peers
            foreach (var peer in commonPeers)
            {
                // Must be a valid candidate cell
                if (peer.HasValue || !peer.CanBe.Contains(z)) continue;

                // Must NOT be inside the ALSs themselves
                if (als1.Cells.Contains(peer) || als2.Cells.Contains(peer)) continue;

                // Must see ALL zCells1 AND ALL zCells2
                bool seesAllSet1 = zCells1.All(c1 => AreCellsVisible(puzzle, peer, c1));
                bool seesAllSet2 = zCells2.All(c2 => AreCellsVisible(puzzle, peer, c2));

                if (seesAllSet1 && seesAllSet2)
                {
                    result.Add(new SingleStepSolution.Candidate(peer.RowIndex, peer.ColumnIndex, z));
                }
            }

            return result;
        }

        private HashSet<Cell> GetCombinedPeers(SudokuPuzzle puzzle, List<Cell> cells)
        {
            // Naive implementation: iterate all cells and check visibility
            // Better: Union of peers of each cell.
            var peers = new HashSet<Cell>();
            foreach (var c in cells)
            {
                AddPeers(puzzle, c, peers);
            }
            return peers;
        }

        private void AddPeers(SudokuPuzzle puzzle, Cell cell, HashSet<Cell> peers)
        {
            // Row
            for (int j = 0; j < 9; j++) peers.Add(puzzle.Cells[cell.RowIndex, j]);
            // Column
            for (int i = 0; i < 9; i++) peers.Add(puzzle.Cells[i, cell.ColumnIndex]);
            // Block
            var blockIdx = puzzle.GetBlockIndex(cell);
            if (blockIdx.RowIndex != -1)
            {
                var block = puzzle.Blocks[blockIdx.RowIndex, blockIdx.ColumnIndex];
                foreach (var c in block.Cells) peers.Add(c);
            }
        }

        private bool AreCellsVisible(SudokuPuzzle puzzle, Cell c1, Cell c2)
        {
            if (c1 == c2) return false; // Technically a cell sees itself in some definitions, but for Sudoku interaction usually strict.
            // But here: Two cells see each other if they share a house.
            if (c1.RowIndex == c2.RowIndex) return true;
            if (c1.ColumnIndex == c2.ColumnIndex) return true;

            var b1 = puzzle.GetBlockIndex(c1);
            var b2 = puzzle.GetBlockIndex(c2);
            if (b1.RowIndex != -1 && b1.RowIndex == b2.RowIndex && b1.ColumnIndex == b2.ColumnIndex) return true;

            return false;
        }
    }
}
