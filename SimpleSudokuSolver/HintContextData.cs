using System.Collections.Generic;

namespace SimpleSudokuSolver
{
    /// <summary>
    /// detailed context about a hint to help UI visualization (e.g. pattern cells, pivot cells).
    /// This allows the UI to display complex strategies without re-calculating the pattern logic.
    /// </summary>
    public class HintContextData
    {
        public HintContextData()
        {
            FocusCells = new List<int[]>();
            ReasoningCells = new List<int[]>();
            HouseIndices = new List<int>();
        }

        /// <summary>
        /// The defining cells of the pattern.
        /// E.g. for X-Wing: The 4 corner cells forming the rectangle.
        /// E.g. for XY-Wing: The 2 "Wing" cells.
        /// stored as [row, col] zero-indexed.
        /// </summary>
        public List<int[]> FocusCells { get; set; }

        /// <summary>
        /// Supporting cells that enable the logic.
        /// E.g. for XY-Wing: The "Pivot" cell.
        /// stored as [row, col] zero-indexed.
        /// </summary>
        public List<int[]> ReasoningCells { get; set; }

        /// <summary>
        /// Indices of houses (rows, columns, or blocks) involved in the pattern.
        /// Rows: 0-8, Columns: 9-17, Blocks: 18-26.
        /// </summary>
        public List<int> HouseIndices { get; set; }

        /// <summary>
        /// The primary candidate value being acted upon (if applicable).
        /// </summary>
        public int? PrimaryCandidate { get; set; }
    }
}
