using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: W-Wing
    /// 
    /// Pattern:
    /// - Two identical bivalue cells {X, Z} (StartCell and EndCell)
    /// - These cells don't see each other directly
    /// - A strong link on X in a bridge house connects them:
    ///   - Link1 SEES StartCell (distinct cells, not the same!)
    ///   - Link2 SEES EndCell (distinct cells, not the same!)
    ///   - Link1 and Link2 form a conjugate pair on X in the bridge house
    /// 
    /// Logic:
    /// - If Link1 = X → StartCell ≠ X → StartCell = Z (bivalue)
    /// - If Link2 = X → EndCell ≠ X → EndCell = Z (bivalue)
    /// - Strong link guarantees one is X, so one endpoint = Z
    /// - Therefore, cells that see BOTH StartCell and EndCell cannot be Z
    /// 
    /// Ported and adapted from Sudoku-FORK WWingStep.cs (C# 13 → C# 9)
    /// BUG FIX Jan 2026: Link cells must SEE endpoints, not BE endpoints
    /// </summary>
    public class WWing : ISudokuSolverStrategy
    {
        public string StrategyName => "W-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells
            var bivalueCells = new List<Cell>();
            foreach (var row in sudokuPuzzle.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.CanBe.Count == 2)
                    {
                        bivalueCells.Add(cell);
                    }
                }
            }

            // Find pairs of identical bivalue cells
            for (int i = 0; i < bivalueCells.Count; i++)
            {
                for (int j = i + 1; j < bivalueCells.Count; j++)
                {
                    var startCell = bivalueCells[i];
                    var endCell = bivalueCells[j];

                    // Must have same candidates
                    if (!SolverUtility.HaveSameCandidates(startCell.CanBe, endCell.CanBe)) continue;

                    // Must NOT see each other directly
                    if (SolverUtility.CellsSeeEachOther(startCell, endCell)) continue;

                    var candidates = startCell.CanBe.ToList();
                    int candX = candidates[0];
                    int candZ = candidates[1];

                    // Try each candidate as the strong link candidate (X)
                    foreach (var linkCandidate in candidates)
                    {
                        int zCandidate = candidates.First(c => c != linkCandidate);

                        // Find a strong link on linkCandidate connecting the cells
                        var bridgeResult = FindStrongLinkBridge(sudokuPuzzle, startCell, endCell, linkCandidate);
                        if (bridgeResult.HasValue)
                        {
                            // Found W-Wing! Calculate eliminations
                            var eliminations = FindEliminations(sudokuPuzzle, startCell, endCell, zCandidate);

                            if (eliminations.Count > 0)
                            {
                                var solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                                // Populate ContextData
                                solution.ContextData = new HintContextData();
                                solution.ContextData.PrimaryCandidate = zCandidate;

                                // FocusCells: [StartCell, EndCell]
                                solution.ContextData.FocusCells.Add(new int[] { startCell.RowIndex, startCell.ColumnIndex });
                                solution.ContextData.FocusCells.Add(new int[] { endCell.RowIndex, endCell.ColumnIndex });

                                // FocusCandidates: both candidates
                                solution.ContextData.FocusCandidates = candidates;

                                // ReasoningCandidates: [X (link candidate), Z (elimination candidate)]
                                solution.ContextData.ReasoningCandidates = new List<int> { linkCandidate, zCandidate };

                                // BridgeHouseIndex
                                solution.ContextData.BridgeHouseIndex = bridgeResult.Value;

                                return solution;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a house with a strong link on the given candidate that connects startCell and endCell.
        /// A strong link means exactly 2 cells in the house have the candidate.
        /// Returns the house index (0-8 rows, 9-17 cols, 18-26 boxes) or null if not found.
        /// </summary>
        private int? FindStrongLinkBridge(SudokuPuzzle puzzle, Cell startCell, Cell endCell, int candidate)
        {
            // Check all rows for a strong link
            for (int row = 0; row < 9; row++)
            {
                var cellsWithCandidate = puzzle.Rows[row].Cells
                    .Where(c => c.CanBe.Contains(candidate))
                    .ToList();

                if (cellsWithCandidate.Count == 2)
                {
                    // Strong link! Check if it connects to both cells
                    var linkCell1 = cellsWithCandidate[0];
                    var linkCell2 = cellsWithCandidate[1];

                    // CRITICAL: Link cells must SEE endpoints AND be DISTINCT from endpoints
                    if ((SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, startCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, endCell)) ||
                        (SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, endCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, startCell)))
                    {
                        return row; // Row index 0-8
                    }
                }
            }

            // Check all columns for a strong link
            for (int col = 0; col < 9; col++)
            {
                var cellsWithCandidate = puzzle.Columns[col].Cells
                    .Where(c => c.CanBe.Contains(candidate))
                    .ToList();

                if (cellsWithCandidate.Count == 2)
                {
                    var linkCell1 = cellsWithCandidate[0];
                    var linkCell2 = cellsWithCandidate[1];

                    // CRITICAL: Link cells must SEE endpoints AND be DISTINCT from endpoints
                    if ((SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, startCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, endCell)) ||
                        (SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, endCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, startCell)))
                    {
                        return col + 9; // Column index 9-17
                    }
                }
            }

            // Check all boxes for a strong link
            for (int box = 0; box < 9; box++)
            {
                int boxRow = (box / 3) * 3;
                int boxCol = (box % 3) * 3;
                var cellsWithCandidate = new List<Cell>();

                for (int r = boxRow; r < boxRow + 3; r++)
                {
                    for (int c = boxCol; c < boxCol + 3; c++)
                    {
                        var cell = puzzle.Rows[r].Cells[c];
                        if (cell.CanBe.Contains(candidate))
                        {
                            cellsWithCandidate.Add(cell);
                        }
                    }
                }

                if (cellsWithCandidate.Count == 2)
                {
                    var linkCell1 = cellsWithCandidate[0];
                    var linkCell2 = cellsWithCandidate[1];

                    // CRITICAL: Link cells must SEE endpoints AND be DISTINCT from endpoints
                    if ((SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, startCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, endCell)) ||
                        (SolverUtility.CellsSeeEachOtherButNotSame(linkCell1, endCell) && SolverUtility.CellsSeeEachOtherButNotSame(linkCell2, startCell)))
                    {
                        return box + 18; // Box index 18-26
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, Cell startCell, Cell endCell, int zCandidate)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            // Find cells that see BOTH startCell and endCell
            foreach (var row in puzzle.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    // Skip the W-Wing cells themselves
                    if (cell == startCell || cell == endCell) continue;

                    // Must have the Z candidate
                    if (!cell.CanBe.Contains(zCandidate)) continue;

                    // Must see both endpoints
                    if (SolverUtility.CellsSeeEachOther(cell, startCell) && SolverUtility.CellsSeeEachOther(cell, endCell))
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, zCandidate));
                    }
                }
            }

            return eliminations;
        }
    }
}
