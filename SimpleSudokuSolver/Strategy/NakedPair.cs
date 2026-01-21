using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy looks for two cells in the same row / column / block that have exactly the same two candidate values.
    /// If such two cells are found, then the two candidate values cannot be in any other cell in 
    /// ANY house (row, column, or block) that both cells share.
    /// </summary>
    public class NakedPair : ISudokuSolverStrategy
    {
        public string StrategyName => "Naked Pair";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Check rows
            foreach (var row in sudokuPuzzle.Rows)
            {
                var result = GetNakedPairSolution(row.Cells, sudokuPuzzle);
                if (result != null) return result;
            }

            // Check columns
            foreach (var column in sudokuPuzzle.Columns)
            {
                var result = GetNakedPairSolution(column.Cells, sudokuPuzzle);
                if (result != null) return result;
            }

            // Check blocks
            foreach (var block in sudokuPuzzle.Blocks)
            {
                var result = GetNakedPairSolution(block.Cells.OfType<Cell>(), sudokuPuzzle);
                if (result != null) return result;
            }

            return null;
        }

        private SingleStepSolution GetNakedPairSolution(IEnumerable<Cell> cells, SudokuPuzzle puzzle)
        {
            var cellsWithNoValue = cells.Where(x => !x.HasValue).ToArray();
            var nakedPairCandidates = cellsWithNoValue.Where(x => x.CanBe.Count == 2).ToArray();

            for (int i = 0; i < nakedPairCandidates.Length - 1; i++)
            {
                Cell first = nakedPairCandidates[i];

                for (int j = i + 1; j < nakedPairCandidates.Length; j++)
                {
                    Cell second = nakedPairCandidates[j];

                    // Use set equality (order-independent) instead of SequenceEqual
                    if (new HashSet<int>(first.CanBe).SetEquals(second.CanBe))
                    {
                        // Found a valid Naked Pair - collect eliminations from ALL shared houses
                        var eliminations = GetAllEliminationsForNakedSet(
                          new[] { first, second }, first.CanBe.ToArray(), puzzle);

                        if (eliminations.Count > 0)
                        {
                            var solution = new SingleStepSolution(eliminations.Distinct().ToArray(), StrategyName);
                            solution.ContextData = new HintContextData
                            {
                                FocusCells = new List<int[]>
                                {
                                new int[] { first.RowIndex, first.ColumnIndex },
                                new int[] { second.RowIndex, second.ColumnIndex }
                                }
                            };
                            return solution;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Collects eliminations from ALL houses that the naked set cells share.
        /// </summary>
        private List<SingleStepSolution.Candidate> GetAllEliminationsForNakedSet(
          Cell[] nakedSetCells, int[] distinctValues, SudokuPuzzle puzzle)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();
            var nakedSetIndices = new HashSet<(int row, int col)>(
              nakedSetCells.Select(c => (c.RowIndex, c.ColumnIndex)));

            // Check if all cells share the same row
            if (nakedSetCells.All(c => c.RowIndex == nakedSetCells[0].RowIndex))
            {
                var row = puzzle.Rows[nakedSetCells[0].RowIndex];
                AddEliminationsFromHouse(row.Cells, nakedSetIndices, distinctValues, eliminations);
            }

            // Check if all cells share the same column
            if (nakedSetCells.All(c => c.ColumnIndex == nakedSetCells[0].ColumnIndex))
            {
                var column = puzzle.Columns[nakedSetCells[0].ColumnIndex];
                AddEliminationsFromHouse(column.Cells, nakedSetIndices, distinctValues, eliminations);
            }

            // Check if all cells share the same block (using 2D block indices)
            int firstBlockRow = nakedSetCells[0].RowIndex / 3;
            int firstBlockCol = nakedSetCells[0].ColumnIndex / 3;
            if (nakedSetCells.All(c => c.RowIndex / 3 == firstBlockRow && c.ColumnIndex / 3 == firstBlockCol))
            {
                var block = puzzle.Blocks[firstBlockRow, firstBlockCol];
                AddEliminationsFromHouse(block.Cells.OfType<Cell>(), nakedSetIndices, distinctValues, eliminations);
            }

            return eliminations;
        }

        private void AddEliminationsFromHouse(
          IEnumerable<Cell> houseCells,
          HashSet<(int row, int col)> nakedSetIndices,
          int[] distinctValues,
          List<SingleStepSolution.Candidate> eliminations)
        {
            foreach (var cell in houseCells)
            {
                if (cell.HasValue) continue;
                if (nakedSetIndices.Contains((cell.RowIndex, cell.ColumnIndex))) continue;

                var commonValues = cell.CanBe.Intersect(distinctValues).ToArray();
                foreach (var value in commonValues)
                {
                    eliminations.Add(new SingleStepSolution.Candidate(
                      cell.RowIndex, cell.ColumnIndex, value));
                }
            }
        }
    }
}
