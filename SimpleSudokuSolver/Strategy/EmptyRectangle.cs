using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Empty Rectangle technique (SE 4.4).
    /// 
    /// An Empty Rectangle occurs when:
    /// 1. A candidate forms an "L-shape" or corner pattern within a box
    ///    (all instances confined to one row and one column within that box)
    /// 2. There's a conjugate pair in a line outside the box
    /// 3. The box's corner cell sees one end of the conjugate pair
    /// 
    /// This creates an elimination at the intersection of the other line
    /// and the conjugate pair's opposite endpoint.
    /// </summary>
    public class EmptyRectangle : ISudokuSolverStrategy
    {
        public string StrategyName => "Empty Rectangle";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindEmptyRectangle(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }
            return null;
        }

        private SingleStepSolution FindEmptyRectangle(SudokuPuzzle puzzle, int candidate)
        {
            // Check each block for an "Empty Rectangle" pattern
            for (int blockRow = 0; blockRow < 3; blockRow++)
            {
                for (int blockCol = 0; blockCol < 3; blockCol++)
                {
                    Block block = puzzle.Blocks[blockRow, blockCol];
                    List<Cell> cellsWithCandidate = GetCellsWithCandidate(block, candidate);

                    if (cellsWithCandidate.Count < 2 || cellsWithCandidate.Count > 4)
                        continue;

                    // Check if cells form an ER pattern (all in one row or all in one column,
                    // or L-shaped in one row + one column)
                    var rows = cellsWithCandidate.Select(c => c.RowIndex).Distinct().ToList();
                    var cols = cellsWithCandidate.Select(c => c.ColumnIndex).Distinct().ToList();

                    // For ER, we need candidates confined to exactly 2 rows and 2 columns
                    // forming an "L" or corner shape (not filling the full 2x2)
                    if (rows.Count != 2 || cols.Count != 2) continue;

                    // Check that it's not a full 2x2 block (that would be 4 cells)
                    // ER needs 2-3 cells in an L-shape
                    if (cellsWithCandidate.Count >= 4)
                    {
                        bool hasAllFour = true;
                        foreach (int r in rows)
                        {
                            foreach (int c in cols)
                            {
                                if (!cellsWithCandidate.Any(cell => cell.RowIndex == r && cell.ColumnIndex == c))
                                {
                                    hasAllFour = false;
                                    break;
                                }
                            }
                        }
                        if (hasAllFour) continue; // Full 2x2 is not an ER
                    }

                    // Find the "hinge" - the corner where both lines meet
                    foreach (int hingeRow in rows)
                    {
                        foreach (int hingeCol in cols)
                        {
                            // The hinge must have the candidate
                            Cell hingeCell = puzzle.Cells[hingeRow, hingeCol];
                            if (!hingeCell.CanBe.Contains(candidate)) continue;

                            // Try to find a conjugate pair in the row or column extending from the hinge
                            SingleStepSolution solution = TryRowConjugatePair(puzzle, candidate, hingeRow, hingeCol, blockRow, blockCol, cellsWithCandidate);
                            if (solution != null) return solution;

                            solution = TryColumnConjugatePair(puzzle, candidate, hingeRow, hingeCol, blockRow, blockCol, cellsWithCandidate);
                            if (solution != null) return solution;
                        }
                    }
                }
            }
            return null;
        }

        private SingleStepSolution TryRowConjugatePair(SudokuPuzzle puzzle, int candidate, int hingeRow, int hingeCol, int blockRow, int blockCol, List<Cell> erCells)
        {
            // Find a conjugate pair in the same row, outside this block
            List<Cell> rowCellsWithCandidate = new List<Cell>();
            for (int col = 0; col < 9; col++)
            {
                Cell cell = puzzle.Cells[hingeRow, col];
                if (cell.CanBe.Contains(candidate) && col / 3 != blockCol)
                {
                    rowCellsWithCandidate.Add(cell);
                }
            }

            // We need exactly 1 cell outside the block (the other end of a strong link)
            if (rowCellsWithCandidate.Count != 1) return null;

            Cell pairEnd = rowCellsWithCandidate[0];

            // Now look for the elimination: find a cell that sees both
            // the pairEnd (via column) and the ER column cells (via row)
            int erOtherRow = erCells.Where(c => c.RowIndex != hingeRow).Select(c => c.RowIndex).FirstOrDefault();
            if (erOtherRow == 0 && !erCells.Any(c => c.RowIndex != hingeRow)) return null;

            Cell elimCell = puzzle.Cells[erOtherRow, pairEnd.ColumnIndex];
            if (!elimCell.CanBe.Contains(candidate)) return null;
            if (erCells.Contains(elimCell)) return null; // Can't eliminate from ER itself

            return CreateSolution(candidate, erCells, puzzle.Cells[hingeRow, hingeCol], pairEnd, elimCell);
        }

        private SingleStepSolution TryColumnConjugatePair(SudokuPuzzle puzzle, int candidate, int hingeRow, int hingeCol, int blockRow, int blockCol, List<Cell> erCells)
        {
            // Find a conjugate pair in the same column, outside this block
            List<Cell> colCellsWithCandidate = new List<Cell>();
            for (int row = 0; row < 9; row++)
            {
                Cell cell = puzzle.Cells[row, hingeCol];
                if (cell.CanBe.Contains(candidate) && row / 3 != blockRow)
                {
                    colCellsWithCandidate.Add(cell);
                }
            }

            if (colCellsWithCandidate.Count != 1) return null;

            Cell pairEnd = colCellsWithCandidate[0];

            int erOtherCol = erCells.Where(c => c.ColumnIndex != hingeCol).Select(c => c.ColumnIndex).FirstOrDefault();
            if (erOtherCol == 0 && !erCells.Any(c => c.ColumnIndex != hingeCol)) return null;

            Cell elimCell = puzzle.Cells[pairEnd.RowIndex, erOtherCol];
            if (!elimCell.CanBe.Contains(candidate)) return null;
            if (erCells.Contains(elimCell)) return null;

            return CreateSolution(candidate, erCells, puzzle.Cells[hingeRow, hingeCol], pairEnd, elimCell);
        }

        private List<Cell> GetCellsWithCandidate(Block block, int candidate)
        {
            List<Cell> cells = new List<Cell>();
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    if (block.Cells[r, c].CanBe.Contains(candidate))
                    {
                        cells.Add(block.Cells[r, c]);
                    }
                }
            }
            return cells;
        }

        private SingleStepSolution CreateSolution(int candidate, List<Cell> erCells, Cell hinge, Cell pairEnd, Cell elimCell)
        {
            var eliminations = new[] { new SingleStepSolution.Candidate(elimCell.RowIndex, elimCell.ColumnIndex, candidate) };
            SingleStepSolution solution = new SingleStepSolution(eliminations, StrategyName);
            
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candidate;

            // ER cells as focus
            foreach (Cell cell in erCells)
            {
                solution.ContextData.FocusCells.Add(new int[] { cell.RowIndex, cell.ColumnIndex });
            }

            // Hinge and pair end as reasoning
            solution.ContextData.ReasoningCells.Add(new int[] { hinge.RowIndex, hinge.ColumnIndex });
            solution.ContextData.ReasoningCells.Add(new int[] { pairEnd.RowIndex, pairEnd.ColumnIndex });

            return solution;
        }
    }
}
