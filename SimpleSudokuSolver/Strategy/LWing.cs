using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: L-Wing (Local Wing)
    /// 
    /// Pattern: x=(x-y)=(y-z)=z
    /// Source: http://forum.enjoysudoku.com/local-wing-t34685.html
    /// 
    /// Structure (4 cells: a, b, c, d):
    /// - Cell a: Has candidate X with strong link to cell b on X
    /// - Cell b: Bivalue {X, Y} - converts X to Y
    /// - Cell c: Bivalue {Y, Z} - converts Y to Z (must see cell b)
    /// - Cell d: Has candidate Z with strong link from cell c on Z
    /// 
    /// Chain logic: If a ≠ X → b = X → b ≠ Y → c = Y → c ≠ Z → d = Z
    /// Conclusion: Either a = X OR d = Z (or both)
    /// 
    /// Elimination (cross-elimination at strong link endpoints):
    /// - Eliminate Z from cell a (if a contains Z)
    /// - Eliminate X from cell d (if d contains X)
    /// 
    /// SE Rating: 4.5
    /// </summary>
    public class LWing : ISudokuSolverStrategy
    {
        public string StrategyName => "L-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells - these are the middle cells (b and c)
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

            // For each pair of bivalue cells that share exactly one candidate
            for (int i = 0; i < bivalueCells.Count; i++)
            {
                for (int j = i + 1; j < bivalueCells.Count; j++)
                {
                    Cell cellB = bivalueCells[i];
                    Cell cellC = bivalueCells[j];

                    // Cells b and c must have a STRONG link on their shared candidate
                    // (not just see each other - the canonical L-Wing requires strong link on Y)

                    // Find shared candidate Y
                    List<int> candsB = cellB.CanBe.ToList();
                    List<int> candsC = cellC.CanBe.ToList();

                    int sharedY = -1;
                    int candX = -1; // unique to cell B
                    int candZ = -1; // unique to cell C

                    foreach (int c in candsB)
                    {
                        if (candsC.Contains(c))
                        {
                            sharedY = c;
                        }
                        else
                        {
                            candX = c;
                        }
                    }
                    foreach (int c in candsC)
                    {
                        if (!candsB.Contains(c))
                        {
                            candZ = c;
                        }
                    }

                    // L-Wing needs exactly one shared candidate and two different unique candidates
                    if (sharedY < 0 || candX < 0 || candZ < 0) continue;
                    if (candX == candZ) continue; // Would be W-Wing pattern, not L-Wing

                    // CRITICAL: Cells B and C must have STRONG link on sharedY
                    // The canonical L-Wing pattern x=(x-y)=(y-z)=z requires strong link on Y
                    if (!HasStrongLinkOnCandidate(sudokuPuzzle, cellB, cellC, sharedY)) continue;

                    // Try to find strong link endpoints
                    SingleStepSolution result = TryLWingPattern(sudokuPuzzle, cellB, cellC, candX, sharedY, candZ);
                    if (result != null) return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find strong link endpoints for the L-Wing pattern.
        /// cellB has {candX, sharedY}, cellC has {sharedY, candZ}
        /// Find cell a with strong link to cellB on candX
        /// Find cell d with strong link to cellC on candZ
        /// </summary>
        private SingleStepSolution TryLWingPattern(SudokuPuzzle puzzle, Cell cellB, Cell cellC,
            int candX, int sharedY, int candZ)
        {
            // Find all cells that form strong link with cellB on candX
            List<(Cell cellA, int houseIndex)> strongLinksFromB = FindStrongLinksFromCell(puzzle, cellB, candX);

            foreach ((Cell cellA, int houseIndexA) in strongLinksFromB)
            {
                if (cellA == cellC) continue; // Can't be the same cell

                // Find all cells that form strong link with cellC on candZ
                List<(Cell cellD, int houseIndex)> strongLinksFromC = FindStrongLinksFromCell(puzzle, cellC, candZ);

                foreach ((Cell cellD, int houseIndexD) in strongLinksFromC)
                {
                    if (cellD == cellA || cellD == cellB) continue; // Must be distinct

                    // Found L-Wing pattern: a--X==X--b{X,Y}--Y--c{Y,Z}--Z==Z--d
                    // Chain: a === b --- c === d (on candidates X, Y, Z)
                    // Conclusion: a = X OR d = Z
                    // Elimination: Z from a, X from d

                    List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

                    // Cross-eliminate: Z from cellA (if cellA has Z)
                    if (cellA.CanBe.Contains(candZ))
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(
                            cellA.RowIndex, cellA.ColumnIndex, candZ));
                    }
                    // Cross-eliminate: X from cellD (if cellD has X)
                    if (cellD.CanBe.Contains(candX))
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(
                            cellD.RowIndex, cellD.ColumnIndex, candX));
                    }

                    if (eliminations.Count > 0)
                    {
                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                        solution.ContextData = new HintContextData();
                        solution.ContextData.PrimaryCandidate = eliminations[0].Value;

                        // Focus cells in chain order: a, b, c, d
                        solution.ContextData.FocusCells.Add(new int[] { cellA.RowIndex, cellA.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cellB.RowIndex, cellB.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cellC.RowIndex, cellC.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cellD.RowIndex, cellD.ColumnIndex });

                        // All three candidates involved
                        solution.ContextData.FocusCandidates = new List<int> { candX, sharedY, candZ };
                        solution.ContextData.ReasoningCandidates = new List<int> { sharedY };

                        return solution;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find all cells that form a strong link with fromCell on the given candidate.
        /// A strong link exists when only 2 cells in a house have the candidate.
        /// </summary>
        private List<(Cell, int)> FindStrongLinksFromCell(SudokuPuzzle puzzle, Cell fromCell, int candidate)
        {
            List<(Cell, int)> strongLinks = new List<(Cell, int)>();

            // Check row
            List<Cell> rowCells = puzzle.Rows[fromCell.RowIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (rowCells.Count == 2)
            {
                Cell other = rowCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.RowIndex));
            }

            // Check column
            List<Cell> colCells = puzzle.Columns[fromCell.ColumnIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (colCells.Count == 2)
            {
                Cell other = colCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.ColumnIndex + 9));
            }

            // Check box
            int boxIndex = SolverUtility.GetBoxIndex(fromCell);
            List<Cell> boxCells = SolverUtility.GetBoxCells(puzzle, boxIndex)
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (boxCells.Count == 2)
            {
                Cell other = boxCells.First(c => c != fromCell);
                strongLinks.Add((other, boxIndex + 18));
            }

            return strongLinks;
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
    }
}
