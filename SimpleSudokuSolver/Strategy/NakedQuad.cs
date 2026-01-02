using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy looks for four cells in the same row / column / block that contain IN TOTAL four candidates.
    /// Each of the four cells can contain two, three or four candidates.
    /// If such four cells are found, then the four candidate values cannot be in any other cell in 
    /// ANY house (row, column, or block) that all four cells share.
    /// </summary>
    public class NakedQuad : ISudokuSolverStrategy
    {
        public string StrategyName => "Naked Quad";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Check rows
            foreach (var row in sudokuPuzzle.Rows)
            {
                var result = GetNakedQuadSolution(row.Cells, sudokuPuzzle);
                if (result != null) return result;
            }

            // Check columns
            foreach (var column in sudokuPuzzle.Columns)
            {
                var result = GetNakedQuadSolution(column.Cells, sudokuPuzzle);
                if (result != null) return result;
            }

            // Check blocks
            foreach (var block in sudokuPuzzle.Blocks)
            {
                var result = GetNakedQuadSolution(block.Cells.OfType<Cell>(), sudokuPuzzle);
                if (result != null) return result;
            }

            return null;
        }

        private SingleStepSolution GetNakedQuadSolution(IEnumerable<Cell> cells, SudokuPuzzle puzzle)
        {
            var cellsWithNoValue = cells.Where(x => !x.HasValue).ToArray();

            // We need at least 4 cells which have 2, 3 or 4 possible potential values
            var nakedQuadCandidates = cellsWithNoValue.Where(x => x.CanBe.Count >= 2 && x.CanBe.Count <= 4).ToArray();
            if (nakedQuadCandidates.Length < 4)
                return null;

            for (int i = 0; i < nakedQuadCandidates.Length - 3; i++)
            {
                Cell first = nakedQuadCandidates[i];

                for (int j = i + 1; j < nakedQuadCandidates.Length - 2; j++)
                {
                    Cell second = nakedQuadCandidates[j];

                    for (int k = j + 1; k < nakedQuadCandidates.Length - 1; k++)
                    {
                        Cell third = nakedQuadCandidates[k];

                        for (int m = k + 1; m < nakedQuadCandidates.Length; m++)
                        {
                            Cell fourth = nakedQuadCandidates[m];

                            var distinctValues = GetDistinctPotentialCellValuesInCandidates(
                              first.CanBe, second.CanBe, third.CanBe, fourth.CanBe);

                            if (distinctValues.Length == 4)
                            {
                                // Found a valid Naked Quad - collect eliminations from ALL shared houses
                                var eliminations = GetAllEliminationsForNakedSet(
                                  new[] { first, second, third, fourth }, distinctValues, puzzle);

                                if (eliminations.Count > 0)
                                {
                                    var solution = new SingleStepSolution(eliminations.Distinct().ToArray(), StrategyName);
                                    solution.ContextData = new HintContextData
                                    {
                                        FocusCells = new List<int[]>
                    {
                      new int[] { first.RowIndex, first.ColumnIndex },
                      new int[] { second.RowIndex, second.ColumnIndex },
                      new int[] { third.RowIndex, third.ColumnIndex },
                      new int[] { fourth.RowIndex, fourth.ColumnIndex }
                    }
                                    };
                                    return solution;
                                }
                            }
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

        private int[] GetDistinctPotentialCellValuesInCandidates(params IEnumerable<int>[] items)
        {
            return items.SelectMany(x => x).Distinct().ToArray();
        }
    }
}
