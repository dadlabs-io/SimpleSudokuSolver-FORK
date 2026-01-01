using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy is looking for a "Y-Wing" or "XY-Wing" pattern:
    /// - Pivot cell: Candidates AB
    /// - Wing 1: Candidates AC (connected to Pivot)
    /// - Wing 2: Candidates BC (connected to Pivot)
    /// 
    /// If these 3 cells exist, then any cell that sees BOTH Wing 1 and Wing 2 cannot contain candidate C.
    /// Because:
    /// - If Pivot is A -> Wing 1 is C.
    /// - If Pivot is B -> Wing 2 is C.
    /// So either way, one of the wings must be C.
    /// </summary>
    public class XYWing : ISudokuSolverStrategy
    {
        public string StrategyName => "XY-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            // 1. Find all bi-value cells (cells with exactly 2 candidates)
            var bivalueCells = new List<Cell>();
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = sudokuPuzzle.Cells[r, c];
                    if (!cell.HasValue && cell.CanBe.Count == 2)
                    {
                        bivalueCells.Add(cell);
                    }
                }
            }

            // 2. Iterate each cell as a potential Pivot
            foreach (var pivot in bivalueCells)
            {
                // Pivot has candidates {X, Y}
                int X = pivot.CanBe[0];
                int Y = pivot.CanBe[1];

                // Find potential wings connected to Pivot
                // Wing must be bi-value and share exactly one candidate with Pivot
                var potentialWings = bivalueCells.Where(w => w != pivot && IsConnected(pivot, w)).ToList();

                // Check for Wing 1 (XZ) and Wing 2 (YZ)
                // We need two wings: one sharing X (and having Z), one sharing Y (and having Z)
                // where Z is the same 'common' candidate

                // Let's iterate all pairs of wings
                for (int i = 0; i < potentialWings.Count; i++)
                {
                    var wing1 = potentialWings[i];
                    for (int j = i + 1; j < potentialWings.Count; j++)
                    {
                        var wing2 = potentialWings[j];

                        // Determine the Z candidate
                        // Wing1 must share one candidate with Pivot, Wing2 must share the OTHER candidate with Pivot

                        Cell wingX = null; // The wing that shares X
                        Cell wingY = null; // The wing that shares Y

                        if (wing1.CanBe.Contains(X) && !wing1.CanBe.Contains(Y)) wingX = wing1;
                        else if (wing1.CanBe.Contains(Y) && !wing1.CanBe.Contains(X)) wingY = wing1;

                        if (wing2.CanBe.Contains(X) && !wing2.CanBe.Contains(Y))
                        {
                            if (wingX != null) continue; // We already have a wingX
                            wingX = wing2;
                        }
                        else if (wing2.CanBe.Contains(Y) && !wing2.CanBe.Contains(X))
                        {
                            if (wingY != null) continue; // We already have a wingY
                            wingY = wing2;
                        }

                        if (wingX == null || wingY == null) continue;

                        // Now check if they share a common candidate Z (which is NOT X or Y)
                        var zCandidates = wingX.CanBe.Intersect(wingY.CanBe).Where(c => c != X && c != Y).ToList();

                        if (zCandidates.Count != 1) continue; // Must share exactly one Z

                        int Z = zCandidates[0];

                        // Great! We have a valid XY-Wing pattern:
                        // Pivot: AB (here XY)
                        // WingX: AZ (here XZ)
                        // WingY: BZ (here YZ)
                        // Common Z is the target for elimination.

                        // Now find cells that see BOTH wings
                        var commonCells = GetCommonVisibleCells(sudokuPuzzle, wingX, wingY);

                        foreach (var target in commonCells)
                        {
                            if (target.CanBe.Contains(Z))
                            {
                                // Found elimination!
                                eliminations.Add(new SingleStepSolution.Candidate(target.RowIndex, target.ColumnIndex, Z));
                            }
                        }

                        if (eliminations.Count > 0)
                        {
                            var solution = new SingleStepSolution(eliminations.Distinct().ToArray(), StrategyName);
                            solution.ContextData = new HintContextData
                            {
                                PrimaryCandidate = Z,
                                FocusCells = new List<int[]>
                                {
                                    new int[] { pivot.RowIndex, pivot.ColumnIndex }, // Pivot
                                    new int[] { wingX.RowIndex, wingX.ColumnIndex }, // Wing A
                                    new int[] { wingY.RowIndex, wingY.ColumnIndex }  // Wing B
                                }
                            };
                            return solution;
                        }
                    }
                }
            }

            return null;
        }

        private bool IsConnected(Cell a, Cell b)
        {
            return a.RowIndex == b.RowIndex ||
                   a.ColumnIndex == b.ColumnIndex ||
                   GetBlockIndex(a.RowIndex, a.ColumnIndex) == GetBlockIndex(b.RowIndex, b.ColumnIndex);
        }

        private List<Cell> GetCommonVisibleCells(SudokuPuzzle puzzle, Cell a, Cell b)
        {
            var result = new List<Cell>();
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = puzzle.Cells[r, c];
                    if (cell.HasValue) continue;
                    if (cell == a || cell == b) continue;

                    if (IsConnected(cell, a) && IsConnected(cell, b))
                    {
                        result.Add(cell);
                    }
                }
            }
            return result;
        }

        private int GetBlockIndex(int row, int col)
        {
            return (row / 3) * 3 + (col / 3);
        }
    }
}
