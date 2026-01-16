using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Empty Rectangle technique (SE 4.4).
    /// 
    /// An Empty Rectangle occurs when:
    /// 1. A candidate forms an "L-shape" within a box (2-3 cells in 2 rows and 2 columns)
    /// 2. A conjugate pair exists in a row or column ENTIRELY OUTSIDE the box
    /// 3. One endpoint of the conjugate pair sees into the ER box
    /// 
    /// The elimination occurs at a cell that sees both:
    /// - The OTHER endpoint of the conjugate pair
    /// - The ER's perpendicular arm (via the virtual strong link)
    /// 
    /// CRITICAL: The conjugate pair must be entirely outside the ER box.
    /// If the ER hinge is part of the "pair," it is NOT a valid ER pattern.
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

                    // ER requires 2-3 cells in L-shape (not 1, not 4+)
                    if (cellsWithCandidate.Count < 2 || cellsWithCandidate.Count > 3)
                        continue;

                    // Check if cells form an ER pattern (confined to 2 rows and 2 columns)
                    var rows = cellsWithCandidate.Select(c => c.RowIndex).Distinct().ToList();
                    var cols = cellsWithCandidate.Select(c => c.ColumnIndex).Distinct().ToList();

                    if (rows.Count != 2 || cols.Count != 2) continue;

                    // Now look for external conjugate pairs that connect to this ER
                    SingleStepSolution solution = TryExternalRowPairs(puzzle, candidate, blockRow, blockCol, cellsWithCandidate, rows, cols);
                    if (solution != null) return solution;

                    solution = TryExternalColumnPairs(puzzle, candidate, blockRow, blockCol, cellsWithCandidate, rows, cols);
                    if (solution != null) return solution;
                }
            }
            return null;
        }

        /// <summary>
        /// Look for conjugate pairs in rows OUTSIDE the ER box where one endpoint sees into the ER.
        /// </summary>
        private SingleStepSolution TryExternalRowPairs(SudokuPuzzle puzzle, int candidate,
            int erBlockRow, int erBlockCol, List<Cell> erCells, List<int> erRows, List<int> erCols)
        {
            // Check each row outside the ER block
            for (int row = 0; row < 9; row++)
            {
                // Skip rows inside the ER block
                if (row / 3 == erBlockRow) continue;

                // Find cells with this candidate in this row
                List<Cell> rowCells = new List<Cell>();
                for (int col = 0; col < 9; col++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (cell.CanBe.Contains(candidate))
                    {
                        rowCells.Add(cell);
                    }
                }

                // Must be exactly 2 cells for a conjugate pair
                if (rowCells.Count != 2) continue;

                Cell endpointA = rowCells[0];
                Cell endpointB = rowCells[1];

                // Check if one endpoint sees into the ER box (shares column with an ER cell)
                Cell seeingEndpoint = null;
                Cell otherEndpoint = null;

                if (erCols.Contains(endpointA.ColumnIndex))
                {
                    seeingEndpoint = endpointA;
                    otherEndpoint = endpointB;
                }
                else if (erCols.Contains(endpointB.ColumnIndex))
                {
                    seeingEndpoint = endpointB;
                    otherEndpoint = endpointA;
                }
                else
                {
                    continue; // Neither endpoint sees the ER
                }

                // Find the ER's "perpendicular arm" - the row within the ER that is NOT seen by seeingEndpoint
                int seeingCol = seeingEndpoint.ColumnIndex;
                int erOtherRow = erRows.FirstOrDefault(r => erCells.Any(c => c.RowIndex == r && c.ColumnIndex != seeingCol));
                
                // If no perpendicular row found, skip
                if (erOtherRow == 0 && !erCells.Any(c => c.RowIndex == 0 && c.ColumnIndex != seeingCol))
                {
                    // Try the other row
                    erOtherRow = erRows.FirstOrDefault(r => r != erRows.First());
                }

                // The elimination is at the intersection of:
                // - otherEndpoint's column
                // - erOtherRow (the perpendicular arm's row)
                Cell elimCell = puzzle.Cells[erOtherRow, otherEndpoint.ColumnIndex];

                if (!elimCell.CanBe.Contains(candidate)) continue;
                if (erCells.Contains(elimCell)) continue;
                if (elimCell == endpointA || elimCell == endpointB) continue;

                return CreateSolution(candidate, erCells, seeingEndpoint, otherEndpoint, elimCell);
            }

            return null;
        }

        /// <summary>
        /// Look for conjugate pairs in columns OUTSIDE the ER box where one endpoint sees into the ER.
        /// </summary>
        private SingleStepSolution TryExternalColumnPairs(SudokuPuzzle puzzle, int candidate,
            int erBlockRow, int erBlockCol, List<Cell> erCells, List<int> erRows, List<int> erCols)
        {
            // Check each column outside the ER block
            for (int col = 0; col < 9; col++)
            {
                // Skip columns inside the ER block
                if (col / 3 == erBlockCol) continue;

                // Find cells with this candidate in this column
                List<Cell> colCells = new List<Cell>();
                for (int row = 0; row < 9; row++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (cell.CanBe.Contains(candidate))
                    {
                        colCells.Add(cell);
                    }
                }

                // Must be exactly 2 cells for a conjugate pair
                if (colCells.Count != 2) continue;

                Cell endpointA = colCells[0];
                Cell endpointB = colCells[1];

                // Check if one endpoint sees into the ER box (shares row with an ER cell)
                Cell seeingEndpoint = null;
                Cell otherEndpoint = null;

                if (erRows.Contains(endpointA.RowIndex))
                {
                    seeingEndpoint = endpointA;
                    otherEndpoint = endpointB;
                }
                else if (erRows.Contains(endpointB.RowIndex))
                {
                    seeingEndpoint = endpointB;
                    otherEndpoint = endpointA;
                }
                else
                {
                    continue; // Neither endpoint sees the ER
                }

                // Find the ER's "perpendicular arm" - the column within the ER that is NOT seen by seeingEndpoint
                int seeingRow = seeingEndpoint.RowIndex;
                int erOtherCol = erCols.FirstOrDefault(c => erCells.Any(cell => cell.ColumnIndex == c && cell.RowIndex != seeingRow));

                // If no perpendicular column found, try the other one
                if (erOtherCol == 0 && !erCells.Any(c => c.ColumnIndex == 0 && c.RowIndex != seeingRow))
                {
                    erOtherCol = erCols.FirstOrDefault(c => c != erCols.First());
                }

                // The elimination is at the intersection of:
                // - otherEndpoint's row
                // - erOtherCol (the perpendicular arm's column)
                Cell elimCell = puzzle.Cells[otherEndpoint.RowIndex, erOtherCol];

                if (!elimCell.CanBe.Contains(candidate)) continue;
                if (erCells.Contains(elimCell)) continue;
                if (elimCell == endpointA || elimCell == endpointB) continue;

                return CreateSolution(candidate, erCells, seeingEndpoint, otherEndpoint, elimCell);
            }

            return null;
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

        private SingleStepSolution CreateSolution(int candidate, List<Cell> erCells, 
            Cell seeingEndpoint, Cell otherEndpoint, Cell elimCell)
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

            // Conjugate pair endpoints as reasoning
            solution.ContextData.ReasoningCells.Add(new int[] { seeingEndpoint.RowIndex, seeingEndpoint.ColumnIndex });
            solution.ContextData.ReasoningCells.Add(new int[] { otherEndpoint.RowIndex, otherEndpoint.ColumnIndex });

            return solution;
        }
    }
}
