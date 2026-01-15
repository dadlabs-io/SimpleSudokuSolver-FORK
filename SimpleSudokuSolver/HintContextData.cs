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

        /// <summary>
        /// Bridge house index for W-Wing patterns.
        /// The bridge is the house containing the strong link connecting two bivalue cells.
        /// Uses standard house encoding: 0-8 rows, 9-17 columns, 18-26 blocks.
        /// Value of -1 means not set.
        /// </summary>
        public int BridgeHouseIndex { get; set; } = -1;

        /// <summary>
        /// Additional notes/metadata about the pattern (e.g. "Skyscraper", "Two-String Kite").
        /// </summary>
        public string Notes { get; set; } = "";

        // ===== F21: ALS Advanced Fields =====

        /// <summary>
        /// Groups of cells for each ALS in advanced ALS techniques.
        /// AlsGroups[0] = first ALS cells, AlsGroups[1] = second ALS cells, etc.
        /// Each inner list contains [row, col] pairs.
        /// </summary>
        public List<List<int[]>> AlsGroups { get; set; } = new List<List<int[]>>();

        /// <summary>
        /// Restricted Common Candidates between adjacent ALS pairs.
        /// RccCandidates[0] = RCC between ALS 0 and ALS 1, etc.
        /// </summary>
        public List<int> RccCandidates { get; set; } = new List<int>();

        /// <summary>
        /// The Z candidate for elimination (common to relevant ALS).
        /// </summary>
        public int EliminationCandidate { get; set; } = 0;

        // ===== F23: Forcing Chains Fields =====

        /// <summary>
        /// Cells in branch A path (first assumption: candidate A is true).
        /// Each entry is [row, col].
        /// </summary>
        public List<int[]> BranchACells { get; set; } = new List<int[]>();

        /// <summary>
        /// Cells in branch B path (second assumption: candidate B is true).
        /// Each entry is [row, col].
        /// </summary>
        public List<int[]> BranchBCells { get; set; } = new List<int[]>();
    }
}
