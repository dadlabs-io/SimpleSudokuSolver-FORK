using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: S-Wing (Split Wing)
    /// 
    /// Correct Pattern:
    /// {X,Y} ─── Y ═══ {Y,Z} ─── Z ═══ {Z,X}
    ///   A               B               C
    /// 
    /// Requirements:
    /// - A is bivalue cell {X, Y}
    /// - B is bivalue cell {Y, Z} with strong link on Y with A
    /// - C is bivalue cell {Z, X} with strong link on Z with B
    /// 
    /// Logic:
    /// - If A = X → X is at A
    /// - If A = Y → B can't be Y → B = Z → C can't be Z → C = X
    /// - Either way, X is at A or C
    /// 
    /// Elimination: Remove X from cells seeing both A and C
    /// 
    /// SE Rating: 4.5
    /// </summary>
    public class SWing : ISudokuSolverStrategy
    {
        public string StrategyName => "S-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells
            List<Cell> bivalueCells = new List<Cell>();
            foreach (Row row in sudokuPuzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell.CanBe.Count == 2)
                    {
                        bivalueCells.Add(cell);
                    }
                }
            }

            // Try each bivalue cell as A
            foreach (Cell cellA in bivalueCells)
            {
                List<int> candsA = cellA.CanBe.ToList();
                int candX = candsA[0];
                int candY = candsA[1];

                // Try both orientations (X,Y) and (Y,X)
                SingleStepSolution result = TrySWingFromA(sudokuPuzzle, bivalueCells, cellA, candX, candY);
                if (result != null) return result;

                result = TrySWingFromA(sudokuPuzzle, bivalueCells, cellA, candY, candX);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Find S-Wing pattern starting from cell A with candidates {X, Y}
        /// Looking for: A{X,Y} ─── Y ═══ B{Y,Z} ─── Z ═══ C{Z,X}
        /// </summary>
        private SingleStepSolution TrySWingFromA(SudokuPuzzle puzzle, List<Cell> bivalueCells,
            Cell cellA, int candX, int candY)
        {
            // Find bivalue cells B that:
            // 1. Have Y as one candidate
            // 2. Form a strong link on Y with some house containing A
            // 3. Are bivalue {Y, Z} where Z != X
            foreach (Cell cellB in bivalueCells)
            {
                if (cellB == cellA) continue;
                if (!cellB.CanBe.Contains(candY)) continue;

                // Get B's other candidate (Z)
                int candZ = cellB.CanBe.First(c => c != candY);
                if (candZ == candX) continue; // Z must be different from X

                // Check if A and B form a strong link on Y (only 2 Ys in their shared house)
                if (!HasStrongLinkOnCandidate(puzzle, cellA, cellB, candY)) continue;

                // Now find bivalue cell C that:
                // 1. Is bivalue {Z, X}
                // 2. Forms a strong link on Z with B
                foreach (Cell cellC in bivalueCells)
                {
                    if (cellC == cellA || cellC == cellB) continue;
                    if (!cellC.CanBe.Contains(candZ) || !cellC.CanBe.Contains(candX)) continue;
                    if (cellC.CanBe.Count != 2) continue; // Must be exactly {Z, X}

                    // Check if B and C form a strong link on Z
                    if (!HasStrongLinkOnCandidate(puzzle, cellB, cellC, candZ)) continue;

                    // Found S-Wing! A{X,Y} - B{Y,Z} - C{Z,X}
                    // Eliminate X from cells seeing both A and C
                    List<SingleStepSolution.Candidate> eliminations =
                        FindEliminations(puzzle, cellA, cellC, candX);

                    if (eliminations.Count > 0)
                    {
                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                        solution.ContextData = new HintContextData();
                        solution.ContextData.PrimaryCandidate = candX;

                        // Pass all 3 bivalue cells
                        solution.ContextData.FocusCells.Add(new int[] { cellA.RowIndex, cellA.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cellB.RowIndex, cellB.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cellC.RowIndex, cellC.ColumnIndex });

                        // Pass all 3 candidates involved
                        solution.ContextData.FocusCandidates = new List<int> { candX, candY, candZ };

                        return solution;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Check if two cells form a strong link on a candidate
        /// (i.e., they are the only two cells with that candidate in a shared house)
        /// </summary>
        private bool HasStrongLinkOnCandidate(SudokuPuzzle puzzle, Cell cell1, Cell cell2, int candidate)
        {
            // Check row
            if (cell1.RowIndex == cell2.RowIndex)
            {
                int count = puzzle.Rows[cell1.RowIndex].Cells.Count(c => c.CanBe.Contains(candidate));
                if (count == 2) return true;
            }

            // Check column
            if (cell1.ColumnIndex == cell2.ColumnIndex)
            {
                int count = puzzle.Columns[cell1.ColumnIndex].Cells.Count(c => c.CanBe.Contains(candidate));
                if (count == 2) return true;
            }

            // Check box
            int box1 = SolverUtility.GetBoxIndex(cell1);
            int box2 = SolverUtility.GetBoxIndex(cell2);
            if (box1 == box2)
            {
                int count = SolverUtility.GetBoxCells(puzzle, box1).Count(c => c.CanBe.Contains(candidate));
                if (count == 2) return true;
            }

            return false;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, Cell cellA, Cell cellC, int elimCandidate)
        {
            List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell == cellA || cell == cellC) continue;
                    if (!cell.CanBe.Contains(elimCandidate)) continue;

                    if (SolverUtility.CellsSeeEachOther(cell, cellA) &&
                        SolverUtility.CellsSeeEachOther(cell, cellC))
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(
                            cell.RowIndex, cell.ColumnIndex, elimCandidate));
                    }
                }
            }

            return eliminations;
        }
    }
}
