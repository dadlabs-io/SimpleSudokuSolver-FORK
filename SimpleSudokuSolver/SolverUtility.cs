using SimpleSudokuSolver.Model;
using System.Collections.Generic;

namespace SimpleSudokuSolver
{
    /// <summary>
    /// Shared utility methods for solver strategies.
    /// Eliminates duplication of common helper methods across strategy classes.
    /// </summary>
    public static class SolverUtility
    {
        /// <summary>
        /// Checks if two cells can "see" each other (share row, column, or box).
        /// Note: Returns true even if a == b (same cell sees itself via shared row)
        /// </summary>
        public static bool CellsSeeEachOther(Cell a, Cell b)
        {
            if (a.RowIndex == b.RowIndex) return true;
            if (a.ColumnIndex == b.ColumnIndex) return true;
            return GetBoxIndex(a) == GetBoxIndex(b);
        }

        /// <summary>
        /// Checks if two cells see each other BUT are NOT the same cell.
        /// Used for W-Wing validation where link cells must be distinct from endpoints.
        /// </summary>
        public static bool CellsSeeEachOtherButNotSame(Cell a, Cell b)
        {
            // First check: must be different cells
            if (a.RowIndex == b.RowIndex && a.ColumnIndex == b.ColumnIndex)
                return false; // Same cell - not valid
            
            // Then check if they see each other
            return CellsSeeEachOther(a, b);
        }

        /// <summary>
        /// Checks if two cells are the same OR see each other.
        /// </summary>
        public static bool CellsSeeEachOtherOrSame(Cell a, Cell b)
        {
            if (a.RowIndex == b.RowIndex && a.ColumnIndex == b.ColumnIndex) return true;
            return CellsSeeEachOther(a, b);
        }

        /// <summary>
        /// Gets the 0-based box index (0-8) for a cell.
        /// </summary>
        public static int GetBoxIndex(Cell cell)
        {
            return GetBoxIndex(cell.RowIndex, cell.ColumnIndex);
        }

        /// <summary>
        /// Gets the 0-based box index (0-8) from row and column.
        /// </summary>
        public static int GetBoxIndex(int row, int col)
        {
            return (row / 3) * 3 + (col / 3);
        }

        /// <summary>
        /// Checks if two candidate lists contain the same elements (order-independent).
        /// </summary>
        public static bool HaveSameCandidates(List<int> list1, List<int> list2)
        {
            if (list1.Count != list2.Count) return false;
            foreach (var item in list1)
            {
                if (!list2.Contains(item)) return false;
            }
            return true;
        }

        /// <summary>
        /// Gets all cells in a box by 0-based box index.
        /// </summary>
        public static List<Cell> GetBoxCells(SudokuPuzzle puzzle, int boxIndex)
        {
            var cells = new List<Cell>();
            int boxRow = (boxIndex / 3) * 3;
            int boxCol = (boxIndex % 3) * 3;

            for (int r = boxRow; r < boxRow + 3; r++)
            {
                for (int c = boxCol; c < boxCol + 3; c++)
                {
                    cells.Add(puzzle.Rows[r].Cells[c]);
                }
            }
            return cells;
        }
    }
}
