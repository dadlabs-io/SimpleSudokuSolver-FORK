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

                    // 3. Find Restricted Common Candidate (RCC - X)
                    // X is in both ALS.
                    // ALL cells in als1 with X must see ALL cells in als2 with X.
                    var commonCandidates = als1.Candidates.Intersect(als2.Candidates).ToList();

                    foreach (var x in commonCandidates)
                    {
                        if (IsRestrictedCommonCandidate(sudokuPuzzle, als1, als2, x))
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
                                    // Found a solution!
                                    // Found a solution!
                                    var solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                                    solution.ContextData = new HintContextData
                                    {
                                        FocusCells = als1.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                        ReasoningCells = als2.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                        PrimaryCandidate = x
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

        private bool IsRestrictedCommonCandidate(SudokuPuzzle puzzle, AlmostLockedSet als1, AlmostLockedSet als2, int candidate)
        {
            var cells1 = als1.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();
            var cells2 = als2.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();

            // All cells1 must see all cells2
            foreach (var c1 in cells1)
            {
                foreach (var c2 in cells2)
                {
                    if (!AreCellsVisible(puzzle, c1, c2))
                        return false;
                }
            }
            return true;
        }

        private List<SingleStepSolution.Candidate> GetEliminations(SudokuPuzzle puzzle, AlmostLockedSet als1, AlmostLockedSet als2, int z)
        {
            var result = new List<SingleStepSolution.Candidate>();

            var zCells1 = als1.Cells.Where(c => c.CanBe.Contains(z)).ToList();
            var zCells2 = als2.Cells.Where(c => c.CanBe.Contains(z)).ToList();

            // We need to check all other cells in the grid (or just peers of the ALS sets)
            // Optimization: Only check peers of zCells1 that are also peers of zCells2

            // Get all peers of zCells1
            var peers1 = GetCombinedPeers(puzzle, zCells1);

            // Iterate peers
            foreach (var peer in peers1)
            {
                // Must be a valid candidate cell
                if (peer.HasValue || !peer.CanBe.Contains(z)) continue;

                // Must NOT be inside the ALSs themselves
                if (als1.Cells.Contains(peer) || als2.Cells.Contains(peer)) continue;

                // Must see all zCells2
                bool seesAllSet2 = true;
                foreach (var c2 in zCells2)
                {
                    if (!AreCellsVisible(puzzle, peer, c2))
                    {
                        seesAllSet2 = false;
                        break;
                    }
                }

                if (seesAllSet2)
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
