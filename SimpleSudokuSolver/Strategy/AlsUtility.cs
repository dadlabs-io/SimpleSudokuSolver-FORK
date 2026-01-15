using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Utility class for ALS (Almost Locked Set) operations.
    /// Extracted from AlsXz for reuse across ALS-XY-Wing, ALS-Chain, Death Blossom.
    /// </summary>
    internal static class AlsUtility
    {
        /// <summary>
        /// Checks if a candidate is a Restricted Common Candidate (RCC) between two ALS.
        /// RCC means all cells with that candidate in als1 see all cells with that candidate in als2.
        /// </summary>
        public static bool IsRestrictedCommonCandidate(
            SudokuPuzzle puzzle,
            AlmostLockedSet als1,
            AlmostLockedSet als2,
            int candidate,
            out int sharedHouseIndex)
        {
            sharedHouseIndex = -1;
            List<Cell> cells1 = als1.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();
            List<Cell> cells2 = als2.Cells.Where(c => c.CanBe.Contains(candidate)).ToList();

            if (cells1.Count == 0 || cells2.Count == 0) return false;

            // Check if all RCC cells share a row
            if (cells1.All(c => c.RowIndex == cells1[0].RowIndex) &&
                cells2.All(c => c.RowIndex == cells1[0].RowIndex))
            {
                sharedHouseIndex = cells1[0].RowIndex; // Row 0-8
                return true;
            }

            // Check if all RCC cells share a column
            if (cells1.All(c => c.ColumnIndex == cells1[0].ColumnIndex) &&
                cells2.All(c => c.ColumnIndex == cells1[0].ColumnIndex))
            {
                sharedHouseIndex = 9 + cells1[0].ColumnIndex; // Column = 9-17
                return true;
            }

            // Check if all RCC cells share a block
            var block1 = puzzle.GetBlockIndex(cells1[0]);
            bool allSameBlock = cells1.All(c =>
            {
                var b = puzzle.GetBlockIndex(c);
                return b.RowIndex == block1.RowIndex && b.ColumnIndex == block1.ColumnIndex;
            }) && cells2.All(c =>
            {
                var b = puzzle.GetBlockIndex(c);
                return b.RowIndex == block1.RowIndex && b.ColumnIndex == block1.ColumnIndex;
            });

            if (allSameBlock)
            {
                sharedHouseIndex = 18 + block1.RowIndex * 3 + block1.ColumnIndex;
                return true;
            }

            // Fallback: check if they at least all see each other
            foreach (Cell c1 in cells1)
            {
                foreach (Cell c2 in cells2)
                {
                    if (!AreCellsVisible(puzzle, c1, c2))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Gets eliminations for candidate Z from cells that see all Z-cells in both ALS.
        /// </summary>
        public static List<SingleStepSolution.Candidate> GetEliminations(
            SudokuPuzzle puzzle,
            AlmostLockedSet als1,
            AlmostLockedSet als2,
            int z)
        {
            List<SingleStepSolution.Candidate> result = new List<SingleStepSolution.Candidate>();

            List<Cell> zCells1 = als1.Cells.Where(c => c.CanBe.Contains(z)).ToList();
            List<Cell> zCells2 = als2.Cells.Where(c => c.CanBe.Contains(z)).ToList();

            HashSet<Cell> peers1 = GetCombinedPeers(puzzle, zCells1);
            HashSet<Cell> peers2 = GetCombinedPeers(puzzle, zCells2);

            HashSet<Cell> commonPeers = new HashSet<Cell>(peers1);
            commonPeers.IntersectWith(peers2);

            foreach (Cell peer in commonPeers)
            {
                if (peer.HasValue || !peer.CanBe.Contains(z)) continue;
                if (als1.Cells.Contains(peer) || als2.Cells.Contains(peer)) continue;

                bool seesAllSet1 = zCells1.All(c1 => AreCellsVisible(puzzle, peer, c1));
                bool seesAllSet2 = zCells2.All(c2 => AreCellsVisible(puzzle, peer, c2));

                if (seesAllSet1 && seesAllSet2)
                {
                    result.Add(new SingleStepSolution.Candidate(peer.RowIndex, peer.ColumnIndex, z));
                }
            }

            return result;
        }

        /// <summary>
        /// Gets eliminations for candidate Z from cells that see all Z-cells in multiple ALS.
        /// </summary>
        public static List<SingleStepSolution.Candidate> GetEliminations(
            SudokuPuzzle puzzle,
            List<AlmostLockedSet> alsList,
            int z)
        {
            List<SingleStepSolution.Candidate> result = new List<SingleStepSolution.Candidate>();

            // Get Z-cells and peers from each ALS
            List<HashSet<Cell>> peerSets = new List<HashSet<Cell>>();
            List<List<Cell>> zCellSets = new List<List<Cell>>();

            foreach (AlmostLockedSet als in alsList)
            {
                List<Cell> zCells = als.Cells.Where(c => c.CanBe.Contains(z)).ToList();
                zCellSets.Add(zCells);
                peerSets.Add(GetCombinedPeers(puzzle, zCells));
            }

            // Find intersection of all peer sets
            HashSet<Cell> commonPeers = new HashSet<Cell>(peerSets[0]);
            for (int i = 1; i < peerSets.Count; i++)
            {
                commonPeers.IntersectWith(peerSets[i]);
            }

            foreach (Cell peer in commonPeers)
            {
                if (peer.HasValue || !peer.CanBe.Contains(z)) continue;
                if (alsList.Any(als => als.Cells.Contains(peer))) continue;

                // Must see all Z-cells in all ALS
                bool seesAll = zCellSets.All(zCells =>
                    zCells.All(c => AreCellsVisible(puzzle, peer, c)));

                if (seesAll)
                {
                    result.Add(new SingleStepSolution.Candidate(peer.RowIndex, peer.ColumnIndex, z));
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if two cells see each other (share a house).
        /// </summary>
        public static bool AreCellsVisible(SudokuPuzzle puzzle, Cell c1, Cell c2)
        {
            if (c1 == c2) return false;
            if (c1.RowIndex == c2.RowIndex) return true;
            if (c1.ColumnIndex == c2.ColumnIndex) return true;

            var b1 = puzzle.GetBlockIndex(c1);
            var b2 = puzzle.GetBlockIndex(c2);
            if (b1.RowIndex != -1 && b1.RowIndex == b2.RowIndex && b1.ColumnIndex == b2.ColumnIndex)
                return true;

            return false;
        }

        /// <summary>
        /// Gets combined peers (cells that see any of the given cells).
        /// </summary>
        public static HashSet<Cell> GetCombinedPeers(SudokuPuzzle puzzle, List<Cell> cells)
        {
            HashSet<Cell> peers = new HashSet<Cell>();
            foreach (Cell c in cells)
            {
                AddPeers(puzzle, c, peers);
            }
            return peers;
        }

        /// <summary>
        /// Adds all peers of a cell to the set.
        /// </summary>
        public static void AddPeers(SudokuPuzzle puzzle, Cell cell, HashSet<Cell> peers)
        {
            // Row
            for (int j = 0; j < 9; j++) peers.Add(puzzle.Cells[cell.RowIndex, j]);
            // Column
            for (int i = 0; i < 9; i++) peers.Add(puzzle.Cells[i, cell.ColumnIndex]);
            // Block
            var blockIdx = puzzle.GetBlockIndex(cell);
            if (blockIdx.RowIndex != -1)
            {
                var block = puzzle.Blocks[blockIdx.RowIndex, blockIdx.ColumnIndex];
                foreach (Cell c in block.Cells) peers.Add(c);
            }
        }

        /// <summary>
        /// Checks if two ALS are disjoint (share no cells).
        /// </summary>
        public static bool AreDisjoint(AlmostLockedSet als1, AlmostLockedSet als2)
        {
            return !als1.Cells.Any(c => als2.Cells.Contains(c));
        }

        /// <summary>
        /// Checks if all ALS in the list are mutually disjoint.
        /// </summary>
        public static bool AreAllDisjoint(List<AlmostLockedSet> alsList)
        {
            for (int i = 0; i < alsList.Count; i++)
            {
                for (int j = i + 1; j < alsList.Count; j++)
                {
                    if (!AreDisjoint(alsList[i], alsList[j]))
                        return false;
                }
            }
            return true;
        }
    }
}
