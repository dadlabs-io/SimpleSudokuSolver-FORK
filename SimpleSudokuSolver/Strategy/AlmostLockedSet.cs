using System;
using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    internal enum HouseType
    {
        Row,
        Column,
        Block
    }

    /// <summary>
    /// Represents an Almost Locked Set (ALS): A set of N cells in a house containing exactly N+1 candidates.
    /// </summary>
    internal class AlmostLockedSet
    {
        public List<Cell> Cells { get; }
        public HashSet<int> Candidates { get; }
        public HouseType House { get; }
        public int HouseIndex { get; }

        public AlmostLockedSet(List<Cell> cells, HashSet<int> candidates, HouseType house, int houseIndex)
        {
            Cells = cells;
            Candidates = candidates;
            House = house;
            HouseIndex = houseIndex;
        }

        public override string ToString()
        {
            return $"ALS in {House} {HouseIndex}: Cells[{string.Join(",", Cells.Select(c => $"{c.RowIndex},{c.ColumnIndex}"))}], Cand[{string.Join(",", Candidates)}]";
        }

        /// <summary>
        /// Finds all Almost Locked Sets in the puzzle.
        /// </summary>
        public static List<AlmostLockedSet> FindAll(SudokuPuzzle puzzle)
        {
            var result = new List<AlmostLockedSet>();

            // Rows
            foreach (var row in puzzle.Rows)
            {
                var cells = row.Cells.Where(c => !c.HasValue).ToList();
                result.AddRange(FindInGroup(cells, HouseType.Row, row.RowIndex));
            }

            // Columns
            foreach (var col in puzzle.Columns)
            {
                var cells = col.Cells.Where(c => !c.HasValue).ToList();
                result.AddRange(FindInGroup(cells, HouseType.Column, col.ColumnIndex));
            }

            // Blocks
            for (int r = 0; r < puzzle.NumberOfRowsOrColumnsInBlock; r++)
            {
                for (int c = 0; c < puzzle.NumberOfRowsOrColumnsInBlock; c++)
                {
                    var block = puzzle.Blocks[r, c];
                    if (block == null) continue;

                    var cells = new List<Cell>();
                    foreach (var cell in block.Cells)
                    {
                        if (!cell.HasValue) cells.Add(cell);
                    }

                    int blockIndex = r * puzzle.NumberOfRowsOrColumnsInBlock + c;
                    result.AddRange(FindInGroup(cells, HouseType.Block, blockIndex));
                }
            }

            return result;
        }

        private static IEnumerable<AlmostLockedSet> FindInGroup(List<Cell> cells, HouseType houseType, int houseIndex)
        {
            int n = cells.Count;
            // ALS must have at least 1 cell.
            // If n=1, 2 candidates. (Bi-value cell).
            // Iterating all subsets. 2^n.
            // Max cells in house is 9. If 9 cells empty, 2^9 = 512. Cheap.
            
            // Limit subset size? ALS size is typically small, but theoretically can be up to 8.
            // We iterate mask 1 to 2^n - 1.
            int powerSet = 1 << n; // 2^n

            for (int i = 1; i < powerSet; i++)
            {
                var subset = new List<Cell>();
                var unionCandidates = new HashSet<int>();

                int subsetSize = 0;
                for (int j = 0; j < n; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        var cell = cells[j];
                        subset.Add(cell);
                        subsetSize++;
                        foreach (var cand in cell.CanBe)
                        {
                            unionCandidates.Add(cand);
                        }
                    }
                }

                // Definition: N cells with N+1 candidates
                // subsetSize = N
                // unionCandidates.Count = N+1
                if (subsetSize >= 1 && unionCandidates.Count == subsetSize + 1)
                {
                    yield return new AlmostLockedSet(subset, unionCandidates, houseType, houseIndex);
                }
            }
        }
    }
}
