using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Empty Rectangle technique (SE 4.4).
    /// </summary>
    public class EmptyRectangle : ISudokuSolverStrategy
    {
        public string StrategyName => "Empty Rectangle";

        // Debug flag - set to true to enable logging
        private const bool DEBUG = true;
        private const string LOG_FILE = @"C:\temp\er_debug.log";

        private void Log(string message)
        {
            if (DEBUG)
            {
                try
                {
                    File.AppendAllText(LOG_FILE, $"{DateTime.Now:HH:mm:ss} [ER] {message}\n");
                }
                catch { /* ignore file errors */ }
            }
        }

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindEmptyRectangle(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }
            return null;
        }

        private SingleStepSolution FindEmptyRectangle(SudokuPuzzle puzzle, int candidate)
        {
            for (int blockRow = 0; blockRow < 3; blockRow++)
            {
                for (int blockCol = 0; blockCol < 3; blockCol++)
                {
                    Block block = puzzle.Blocks[blockRow, blockCol];
                    List<Cell> cellsWithCandidate = GetCellsWithCandidate(block, candidate);

                    if (cellsWithCandidate.Count < 2 || cellsWithCandidate.Count > 3)
                        continue;

                    var rows = cellsWithCandidate.Select(c => c.RowIndex).Distinct().ToList();
                    var cols = cellsWithCandidate.Select(c => c.ColumnIndex).Distinct().ToList();

                    if (rows.Count != 2 || cols.Count != 2) continue;

                    Log($"Found ER pattern for candidate {candidate} in Block({blockRow},{blockCol})");
                    Log($"  ER cells: {string.Join(", ", cellsWithCandidate.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"))}");
                    Log($"  erRows: [{string.Join(", ", rows)}], erCols: [{string.Join(", ", cols)}]");

                    SingleStepSolution solution = TryExternalRowPairs(puzzle, candidate, blockRow, blockCol, cellsWithCandidate, rows, cols);
                    if (solution != null) return solution;

                    solution = TryExternalColumnPairs(puzzle, candidate, blockRow, blockCol, cellsWithCandidate, rows, cols);
                    if (solution != null) return solution;
                }
            }
            return null;
        }

        private SingleStepSolution TryExternalRowPairs(SudokuPuzzle puzzle, int candidate,
            int erBlockRow, int erBlockCol, List<Cell> erCells, List<int> erRows, List<int> erCols)
        {
            for (int row = 0; row < 9; row++)
            {
                if (row / 3 == erBlockRow) continue;

                List<Cell> rowCells = new List<Cell>();
                for (int col = 0; col < 9; col++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (cell.CanBe.Contains(candidate))
                    {
                        rowCells.Add(cell);
                    }
                }

                if (rowCells.Count != 2) continue;

                Cell endpointA = rowCells[0];
                Cell endpointB = rowCells[1];

                Log($"  Checking Row {row + 1} conjugate pair: R{endpointA.RowIndex + 1}C{endpointA.ColumnIndex + 1} - R{endpointB.RowIndex + 1}C{endpointB.ColumnIndex + 1}");

                bool aSees = erCols.Contains(endpointA.ColumnIndex);
                bool bSees = erCols.Contains(endpointB.ColumnIndex);

                Log($"    aSees={aSees} (col {endpointA.ColumnIndex} in erCols?), bSees={bSees} (col {endpointB.ColumnIndex} in erCols?)");

                if (aSees == bSees)
                {
                    Log($"    SKIP: Both see or neither see");
                    continue;
                }

                Cell seeingEndpoint = aSees ? endpointA : endpointB;
                Cell otherEndpoint = aSees ? endpointB : endpointA;

                int seeingCol = seeingEndpoint.ColumnIndex;
                var perpCells = erCells.Where(c => c.ColumnIndex != seeingCol).ToList();

                Log($"    seeingEndpoint: R{seeingEndpoint.RowIndex + 1}C{seeingEndpoint.ColumnIndex + 1}");
                Log($"    otherEndpoint: R{otherEndpoint.RowIndex + 1}C{otherEndpoint.ColumnIndex + 1}");
                Log($"    perpCells (col != {seeingCol}): {string.Join(", ", perpCells.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"))}");

                if (perpCells.Count == 0)
                {
                    Log($"    SKIP: No perpendicular cells");
                    continue;
                }

                int perpRow = perpCells[0].RowIndex;

                // CRITICAL: All perpendicular cells must be in the same row
                // Otherwise the elimination is ambiguous (7 could go in different rows)
                if (perpCells.Any(c => c.RowIndex != perpRow))
                {
                    Log($"    SKIP: perpCells in multiple rows (ambiguous)");
                    continue;
                }

                Cell elimCell = puzzle.Cells[perpRow, otherEndpoint.ColumnIndex];

                Log($"    perpRow={perpRow + 1}, elimCell=R{elimCell.RowIndex + 1}C{elimCell.ColumnIndex + 1}");

                if (!elimCell.CanBe.Contains(candidate))
                {
                    Log($"    SKIP: elimCell doesn't have candidate");
                    continue;
                }
                if (erCells.Contains(elimCell))
                {
                    Log($"    SKIP: elimCell is an ER cell");
                    continue;
                }
                if (elimCell == endpointA || elimCell == endpointB)
                {
                    Log($"    SKIP: elimCell is a pair endpoint");
                    continue;
                }
                if (elimCell.RowIndex / 3 == erBlockRow && elimCell.ColumnIndex / 3 == erBlockCol)
                {
                    Log($"    SKIP: elimCell is inside ER block");
                    continue;
                }

                Log($"    SUCCESS! Eliminating {candidate} from R{elimCell.RowIndex + 1}C{elimCell.ColumnIndex + 1}");
                return CreateSolution(candidate, erCells, seeingEndpoint, otherEndpoint, elimCell);
            }

            return null;
        }

        private SingleStepSolution TryExternalColumnPairs(SudokuPuzzle puzzle, int candidate,
            int erBlockRow, int erBlockCol, List<Cell> erCells, List<int> erRows, List<int> erCols)
        {
            for (int col = 0; col < 9; col++)
            {
                if (col / 3 == erBlockCol) continue;

                List<Cell> colCells = new List<Cell>();
                for (int row = 0; row < 9; row++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (cell.CanBe.Contains(candidate))
                    {
                        colCells.Add(cell);
                    }
                }

                if (colCells.Count != 2) continue;

                Cell endpointA = colCells[0];
                Cell endpointB = colCells[1];

                Log($"  Checking Col {col + 1} conjugate pair: R{endpointA.RowIndex + 1}C{endpointA.ColumnIndex + 1} - R{endpointB.RowIndex + 1}C{endpointB.ColumnIndex + 1}");

                bool aSees = erRows.Contains(endpointA.RowIndex);
                bool bSees = erRows.Contains(endpointB.RowIndex);

                Log($"    aSees={aSees} (row {endpointA.RowIndex} in erRows?), bSees={bSees} (row {endpointB.RowIndex} in erRows?)");

                if (aSees == bSees)
                {
                    Log($"    SKIP: Both see or neither see");
                    continue;
                }

                Cell seeingEndpoint = aSees ? endpointA : endpointB;
                Cell otherEndpoint = aSees ? endpointB : endpointA;

                int seeingRow = seeingEndpoint.RowIndex;
                var perpCells = erCells.Where(c => c.RowIndex != seeingRow).ToList();

                Log($"    seeingEndpoint: R{seeingEndpoint.RowIndex + 1}C{seeingEndpoint.ColumnIndex + 1}");
                Log($"    otherEndpoint: R{otherEndpoint.RowIndex + 1}C{otherEndpoint.ColumnIndex + 1}");
                Log($"    perpCells (row != {seeingRow}): {string.Join(", ", perpCells.Select(c => $"R{c.RowIndex + 1}C{c.ColumnIndex + 1}"))}");

                if (perpCells.Count == 0)
                {
                    Log($"    SKIP: No perpendicular cells");
                    continue;
                }


                int perpCol = perpCells[0].ColumnIndex;

                // CRITICAL: All perpendicular cells must be in the same column
                // Otherwise the elimination is ambiguous (7 could go in different columns)
                if (perpCells.Any(c => c.ColumnIndex != perpCol))
                {
                    Log($"    SKIP: perpCells in multiple columns (ambiguous)");
                    continue;
                }

                Cell elimCell = puzzle.Cells[otherEndpoint.RowIndex, perpCol];

                Log($"    perpCol={perpCol + 1}, elimCell=R{elimCell.RowIndex + 1}C{elimCell.ColumnIndex + 1}");

                if (!elimCell.CanBe.Contains(candidate))
                {
                    Log($"    SKIP: elimCell doesn't have candidate");
                    continue;
                }
                if (erCells.Contains(elimCell))
                {
                    Log($"    SKIP: elimCell is an ER cell");
                    continue;
                }
                if (elimCell == endpointA || elimCell == endpointB)
                {
                    Log($"    SKIP: elimCell is a pair endpoint");
                    continue;
                }
                if (elimCell.RowIndex / 3 == erBlockRow && elimCell.ColumnIndex / 3 == erBlockCol)
                {
                    Log($"    SKIP: elimCell is inside ER block");
                    continue;
                }

                Log($"    SUCCESS! Eliminating {candidate} from R{elimCell.RowIndex + 1}C{elimCell.ColumnIndex + 1}");
                return CreateSolution(candidate, erCells, seeingEndpoint, otherEndpoint, elimCell);
            }

            return null;
        }

        private List<Cell> GetCellsWithCandidate(Block block, int candidate)
        {
            List<Cell> cells = new List<Cell>();
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    if (block.Cells[r, c].CanBe.Contains(candidate))
                    {
                        cells.Add(block.Cells[r, c]);
                    }
                }
            }
            return cells;
        }

        private SingleStepSolution CreateSolution(int candidate, List<Cell> erCells,
            Cell seeingEndpoint, Cell otherEndpoint, Cell elimCell)
        {
            var eliminations = new[] { new SingleStepSolution.Candidate(elimCell.RowIndex, elimCell.ColumnIndex, candidate) };
            SingleStepSolution solution = new SingleStepSolution(eliminations, StrategyName);

            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candidate;

            foreach (Cell cell in erCells)
            {
                solution.ContextData.FocusCells.Add(new int[] { cell.RowIndex, cell.ColumnIndex });
            }

            solution.ContextData.ReasoningCells.Add(new int[] { seeingEndpoint.RowIndex, seeingEndpoint.ColumnIndex });
            solution.ContextData.ReasoningCells.Add(new int[] { otherEndpoint.RowIndex, otherEndpoint.ColumnIndex });

            return solution;
        }
    }
}
