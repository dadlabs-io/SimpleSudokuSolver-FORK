using System.Collections.Generic;

namespace SimpleSudokuSolver
{
    /// <summary>
    /// Detailed context about a hint to help UI visualization (e.g. pattern cells, pivot cells).
    /// This allows the UI to display complex strategies without re-calculating the pattern logic.
    /// 
    /// INDEXING CONVENTIONS:
    /// - Cell coordinates (row, col): ALWAYS 0-indexed (0-8)
    /// - Candidate values: 1-9 (standard Sudoku values)
    /// - HouseIndices encoding:
    ///   * Rows: 0-8 (row 0 = first row)
    ///   * Columns: 9-17 (column 9 = first column)
    ///   * Blocks: 18-26 (block 18 = top-left block)
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

        /// <summary>
        /// Candidates associated with the FocusCells (e.g. ALS Set A candidates).
        /// </summary>
        public List<int> FocusCandidates { get; set; } = new List<int>();

        /// <summary>
        /// Candidates associated with the ReasoningCells (e.g. ALS Set B candidates).
        /// </summary>
        public List<int> ReasoningCandidates { get; set; } = new List<int>();

        /// <summary>
        /// Fin cells for finned fish patterns.
        /// The fin is an extra candidate in one base house that restricts eliminations.
        /// Stored as [row, col] zero-indexed.
        /// </summary>
        public List<int[]> FinCells { get; set; } = new List<int[]>();
    }
}
