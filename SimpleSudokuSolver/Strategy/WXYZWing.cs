using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: WXYZ-Wing
    /// 
    /// Pattern:
    /// - 4 cells containing exactly 4 candidates total {W, X, Y, Z}
    /// - One cell (hinge) sees all other cells
    /// - Z is the common candidate appearing in all cells that can see each other
    /// 
    /// Logic:
    /// - If the 4 cells contain only 4 candidates, one cell must be each candidate
    /// - The candidate Z that appears in multiple cells can be eliminated from cells
    ///   that see ALL cells containing Z
    /// 
    /// Simplified approach: Find 4 cells where candidates union to exactly 4 values,
    /// and one candidate (Z) can be eliminated from cells seeing all Z-containing cells.
    /// </summary>
    public class WXYZWing : ISudokuSolverStrategy
    {
        public string StrategyName => "WXYZ-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find cells with 2-4 candidates (potential wing cells)
            var candidateCells = new List<Cell>();
            foreach (var row in sudokuPuzzle.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.CanBe.Count >= 2 && cell.CanBe.Count <= 4)
                    {
                        candidateCells.Add(cell);
                    }
                }
            }

            // Try to find WXYZ-Wing patterns
            // Look for a hinge cell with 3-4 candidates that sees 3 other cells
            foreach (var hinge in candidateCells.Where(c => c.CanBe.Count >= 3))
            {
                var hingeCandidates = hinge.CanBe.ToList();
                
                // Find cells that see the hinge
                var cellsSeeingHinge = candidateCells
                    .Where(c => c != hinge && SolverUtility.CellsSeeEachOther(c, hinge) && c.CanBe.Count == 2)
                    .ToList();

                if (cellsSeeingHinge.Count < 3) continue;

                // Try combinations of 3 wing cells
                for (int i = 0; i < cellsSeeingHinge.Count - 2; i++)
                {
                    for (int j = i + 1; j < cellsSeeingHinge.Count - 1; j++)
                    {
                        for (int k = j + 1; k < cellsSeeingHinge.Count; k++)
                        {
                            var wing1 = cellsSeeingHinge[i];
                            var wing2 = cellsSeeingHinge[j];
                            var wing3 = cellsSeeingHinge[k];

                            // Check if all 4 cells together have exactly 4 candidates
                            var allCandidates = new HashSet<int>();
                            allCandidates.UnionWith(hinge.CanBe);
                            allCandidates.UnionWith(wing1.CanBe);
                            allCandidates.UnionWith(wing2.CanBe);
                            allCandidates.UnionWith(wing3.CanBe);

                            if (allCandidates.Count != 4) continue;

                            // Find Z - the candidate that appears in cells that can see each other
                            // For WXYZ-Wing, Z is the restricted common candidate
                            foreach (int zCandidate in allCandidates)
                            {
                                var cellsWithZ = new List<Cell>();
                                if (hinge.CanBe.Contains(zCandidate)) cellsWithZ.Add(hinge);
                                if (wing1.CanBe.Contains(zCandidate)) cellsWithZ.Add(wing1);
                                if (wing2.CanBe.Contains(zCandidate)) cellsWithZ.Add(wing2);
                                if (wing3.CanBe.Contains(zCandidate)) cellsWithZ.Add(wing3);

                                if (cellsWithZ.Count < 2) continue;

                                // All cells with Z must see each other for elimination to work
                                bool allZCellsSeeEachOther = true;
                                for (int a = 0; a < cellsWithZ.Count && allZCellsSeeEachOther; a++)
                                {
                                    for (int b = a + 1; b < cellsWithZ.Count && allZCellsSeeEachOther; b++)
                                    {
                                        if (!SolverUtility.CellsSeeEachOther(cellsWithZ[a], cellsWithZ[b]))
                                        {
                                            allZCellsSeeEachOther = false;
                                        }
                                    }
                                }

                                if (!allZCellsSeeEachOther) continue;

                                // Find eliminations - cells that see ALL cells containing Z
                                var eliminations = FindEliminations(sudokuPuzzle, cellsWithZ, zCandidate);

                                if (eliminations.Count > 0)
                                {
                                    var solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                                    // Populate ContextData
                                    solution.ContextData = new HintContextData();
                                    solution.ContextData.PrimaryCandidate = zCandidate;

                                    // FocusCells: [hinge, wing1, wing2, wing3]
                                    solution.ContextData.FocusCells.Add(new int[] { hinge.RowIndex, hinge.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { wing1.RowIndex, wing1.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { wing2.RowIndex, wing2.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { wing3.RowIndex, wing3.ColumnIndex });

                                    // FocusCandidates: all 4 candidates
                                    solution.ContextData.FocusCandidates = allCandidates.ToList();

                                    return solution;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, List<Cell> cellsWithZ, int zCandidate)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            foreach (var row in puzzle.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    // Skip the wing cells
                    if (cellsWithZ.Contains(cell)) continue;

                    // Must have the Z candidate
                    if (!cell.CanBe.Contains(zCandidate)) continue;

                    // Must see ALL cells containing Z
                    bool seesAll = cellsWithZ.All(zCell => SolverUtility.CellsSeeEachOther(cell, zCell));
                    if (seesAll)
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, zCandidate));
                    }
                }
            }

            return eliminations;
        }
    }
}
