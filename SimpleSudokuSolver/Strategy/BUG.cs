using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// BUG (Bivalue Universal Grave) Type 1 solver strategy.
    /// 
    /// Detects when the puzzle reaches a state where:
    /// - Every unsolved cell has exactly 2 candidates
    /// - EXCEPT one cell which has exactly 3 candidates (BUG+1 state)
    /// 
    /// In this state, the cell with 3 candidates must contain the "odd" candidate
    /// (the one that appears an odd number of times in at least one of its houses).
    /// This breaks the deadly pattern and ensures a unique solution.
    /// 
    /// Unlike most techniques, BUG places a value rather than eliminating candidates.
    /// </summary>
    public class BUG : ISudokuSolverStrategy
    {
        public string StrategyName => "BUG Type 1";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Get all unsolved cells
            var unsolvedCells = new List<Cell>();
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = sudokuPuzzle.Cells[r, c];
                    if (!cell.HasValue)
                        unsolvedCells.Add(cell);
                }
            }

            if (unsolvedCells.Count == 0)
                return null;

            // Check for BUG+1 pattern: all bivalue except one trivalue
            var triValueCells = unsolvedCells.Where(c => c.CanBe.Count == 3).ToList();

            if (triValueCells.Count != 1)
                return null; // Not BUG+1

            var otherCells = unsolvedCells.Where(c => c.CanBe.Count != 3).ToList();

            if (!otherCells.All(c => c.CanBe.Count == 2))
                return null; // Not all bivalue

            // Found BUG+1! Find the "odd" candidate
            var bugCell = triValueCells[0];

            foreach (var candidate in bugCell.CanBe)
            {
                if (IsOddCandidate(bugCell, candidate, sudokuPuzzle))
                {
                    // This is the value to place!
                    return new SingleStepSolution(
                        bugCell.RowIndex,
                        bugCell.ColumnIndex,
                        candidate,
                        StrategyName);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a candidate is the "odd" one in a BUG+1 pattern.
        /// The odd candidate appears an odd number of times in at least one of its houses.
        /// In a true BUG, each candidate appears exactly twice per house.
        /// </summary>
        private bool IsOddCandidate(Cell bugCell, int candidate, SudokuPuzzle puzzle)
        {
            // Count occurrences in the cell's row
            int rowCount = 0;
            for (int c = 0; c < 9; c++)
            {
                var cell = puzzle.Cells[bugCell.RowIndex, c];
                if (!cell.HasValue && cell.CanBe.Contains(candidate))
                    rowCount++;
            }

            // Count occurrences in the cell's column
            int colCount = 0;
            for (int r = 0; r < 9; r++)
            {
                var cell = puzzle.Cells[r, bugCell.ColumnIndex];
                if (!cell.HasValue && cell.CanBe.Contains(candidate))
                    colCount++;
            }

            // Count occurrences in the cell's block
            int blockStartRow = (bugCell.RowIndex / 3) * 3;
            int blockStartCol = (bugCell.ColumnIndex / 3) * 3;
            int blockCount = 0;
            for (int r = blockStartRow; r < blockStartRow + 3; r++)
            {
                for (int c = blockStartCol; c < blockStartCol + 3; c++)
                {
                    var cell = puzzle.Cells[r, c];
                    if (!cell.HasValue && cell.CanBe.Contains(candidate))
                        blockCount++;
                }
            }

            // The "odd" candidate breaks the BUG rule (appears an odd number of times in some house)
            return (rowCount % 2 == 1) || (colCount % 2 == 1) || (blockCount % 2 == 1);
        }
    }
}
