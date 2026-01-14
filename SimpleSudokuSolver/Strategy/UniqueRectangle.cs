using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Unique Rectangle Type 1 solver strategy.
    /// 
    /// Finds 4-cell rectangles spanning 2 rows, 2 columns, and 2 blocks where:
    /// - 3 cells are bivalue (contain exactly the same 2 candidates)
    /// - 1 cell has those 2 candidates plus extra(s)
    /// 
    /// Since valid Sudokus have a unique solution, the "deadly pattern" (all 4 bivalue)
    /// cannot occur. Therefore, the cell with extras MUST be one of its extra candidates,
    /// and we can eliminate the deadly pair candidates from it.
    /// </summary>
    public class UniqueRectangle : ISudokuSolverStrategy
    {
        public string StrategyName => "Unique Rectangle Type 1";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all potential rectangles
            foreach (var rect in FindRectangles(sudokuPuzzle))
            {
                var result = CheckType1(rect, sudokuPuzzle);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Finds all 4-cell rectangles that span exactly 2 rows, 2 columns, and 2 blocks.
        /// </summary>
        private IEnumerable<Cell[]> FindRectangles(SudokuPuzzle puzzle)
        {
            // Get all unsolved cells
            var unsolvedCells = new List<Cell>();
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = puzzle.Cells[r, c];
                    if (!cell.HasValue && cell.CanBe.Count >= 2)
                        unsolvedCells.Add(cell);
                }
            }

            // For each pair of rows
            for (int r1 = 0; r1 < 8; r1++)
            {
                for (int r2 = r1 + 1; r2 < 9; r2++)
                {
                    // For each pair of columns
                    for (int c1 = 0; c1 < 8; c1++)
                    {
                        for (int c2 = c1 + 1; c2 < 9; c2++)
                        {
                            var cell1 = puzzle.Cells[r1, c1];
                            var cell2 = puzzle.Cells[r1, c2];
                            var cell3 = puzzle.Cells[r2, c1];
                            var cell4 = puzzle.Cells[r2, c2];

                            // All 4 cells must be unsolved
                            if (cell1.HasValue || cell2.HasValue || cell3.HasValue || cell4.HasValue)
                                continue;

                            // Must span exactly 2 blocks
                            int block1 = GetBlockIndex(r1, c1);
                            int block2 = GetBlockIndex(r1, c2);
                            int block3 = GetBlockIndex(r2, c1);
                            int block4 = GetBlockIndex(r2, c2);

                            var distinctBlocks = new HashSet<int> { block1, block2, block3, block4 };
                            if (distinctBlocks.Count != 2)
                                continue;

                            yield return new Cell[] { cell1, cell2, cell3, cell4 };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check for Unique Rectangle Type 1:
        /// - Exactly 3 cells are bivalue with the same 2 candidates
        /// - 1 cell has those 2 candidates plus extra(s)
        /// </summary>
        private SingleStepSolution CheckType1(Cell[] rect, SudokuPuzzle puzzle)
        {
            // Find cells with exactly 2 candidates
            var bivalueCells = rect.Where(c => c.CanBe.Count == 2).ToList();

            // Type 1: exactly 3 bivalue cells
            if (bivalueCells.Count != 3)
                return null;

            // Check if all 3 bivalue cells have the same 2 candidates
            var firstCandidates = bivalueCells[0].CanBe.OrderBy(x => x).ToList();
            foreach (var cell in bivalueCells.Skip(1))
            {
                var candidates = cell.CanBe.OrderBy(x => x).ToList();
                if (!candidates.SequenceEqual(firstCandidates))
                    return null;
            }

            // The "deadly pair" is the two candidates in the bivalue cells
            int digitA = firstCandidates[0];
            int digitB = firstCandidates[1];

            // Find the target cell (the one with more than 2 candidates)
            var targetCell = rect.FirstOrDefault(c => c.CanBe.Count > 2);
            if (targetCell == null)
                return null;

            // Target must contain both deadly digits
            if (!targetCell.CanBe.Contains(digitA) || !targetCell.CanBe.Contains(digitB))
                return null;

            // Create eliminations: remove digitA and digitB from target cell
            var eliminations = new List<SingleStepSolution.Candidate>();
            eliminations.Add(new SingleStepSolution.Candidate(targetCell.RowIndex, targetCell.ColumnIndex, digitA));
            eliminations.Add(new SingleStepSolution.Candidate(targetCell.RowIndex, targetCell.ColumnIndex, digitB));

            return new SingleStepSolution(eliminations.ToArray(), StrategyName);
        }

        /// <summary>
        /// Get the 0-based block index for a cell at (row, col).
        /// </summary>
        private int GetBlockIndex(int row, int col)
        {
            return (row / 3) * 3 + (col / 3);
        }
    }
}
