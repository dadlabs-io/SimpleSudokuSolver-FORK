using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Model
{
    public class SudokuPuzzle
    {
        public Cell[,] Cells { get; }
        public Row[] Rows { get; }
        public Column[] Columns { get; }
        public Block[,] Blocks { get; }
        public int[] PossibleCellValues { get; }
        public SingleStepSolution[] Steps => _steps.Select(x => x.Item1).ToArray();
        public int NumberOfSteps => _steps.Count;

        public int NumberOfRowsOrColumnsInPuzzle { get; }
        public int NumberOfRowsOrColumnsInBlock { get; }

        /// <summary>
        /// The known solution for this puzzle (optional). 
        /// When set, eliminations are validated against this solution.
        /// </summary>
        public int[,] Solution { get; set; }

        /// <summary>
        /// If true, throw an exception when an elimination removes the correct solution value.
        /// Default is true when Solution is set.
        /// </summary>
        public bool ValidateEliminations { get; set; } = true;

        /// <summary>
        /// If set to a file path, logs each step with board state for debugging.
        /// </summary>
        public string StepLogPath { get; set; }

        /// <summary>
        /// If true, enables verbose file logging in strategies. Set to false for Beast Mode performance.
        /// </summary>
        public static bool EnableVerboseFileLogging { get; set; } = false;

        private readonly List<Tuple<SingleStepSolution, int[]>> _steps =
          new List<Tuple<SingleStepSolution, int[]>>();

        public SudokuPuzzle(int[,] sudoku)
        {
            Validation.ValidateSudoku(sudoku);

            NumberOfRowsOrColumnsInPuzzle = sudoku.GetLength(0);

            // We assume a square puzzle.
            NumberOfRowsOrColumnsInBlock = (int)Math.Sqrt(NumberOfRowsOrColumnsInPuzzle);

            Cells = new Cell[NumberOfRowsOrColumnsInPuzzle, NumberOfRowsOrColumnsInPuzzle];
            Rows = new Row[NumberOfRowsOrColumnsInPuzzle];
            Columns = new Column[NumberOfRowsOrColumnsInPuzzle];
            Blocks = new Block[NumberOfRowsOrColumnsInBlock, NumberOfRowsOrColumnsInBlock];
            PossibleCellValues = Enumerable.Range(1, NumberOfRowsOrColumnsInPuzzle).ToArray();

            for (int i = 0; i < NumberOfRowsOrColumnsInPuzzle; i++)
            {
                Rows[i] = new Row(i, NumberOfRowsOrColumnsInPuzzle);

                for (int j = 0; j < NumberOfRowsOrColumnsInPuzzle; j++)
                {
                    if (i == 0)
                    {
                        Columns[j] = new Column(j, NumberOfRowsOrColumnsInPuzzle);
                    }

                    var cell = new Cell(sudoku[i, j], i, j);
                    if (!cell.HasValue)
                    {
                        cell.CanBe.AddRange(PossibleCellValues);
                    }

                    Cells[i, j] = cell;
                    Columns[j].Cells[i] = Cells[i, j];
                    Rows[i].Cells[j] = Cells[i, j];

                    var blockRowIndex = i / NumberOfRowsOrColumnsInBlock;
                    var blockColumnIndex = j / NumberOfRowsOrColumnsInBlock;

                    if (Blocks[blockRowIndex, blockColumnIndex] == null)
                    {
                        Blocks[blockRowIndex, blockColumnIndex] = new Block(
                          blockRowIndex, blockColumnIndex, NumberOfRowsOrColumnsInBlock);
                    }

                    Blocks[blockRowIndex, blockColumnIndex].Cells[i % NumberOfRowsOrColumnsInBlock, j % NumberOfRowsOrColumnsInBlock] = Cells[i, j];
                }
            }
        }

        /// <summary>
        /// Applies <paramref name="singleStepSolution"/> to the puzzle.
        /// </summary>
        /// <param name="singleStepSolution">Solution which is applied to the puzzle.</param>
        public void ApplySingleStepSolution(SingleStepSolution singleStepSolution)
        {
            if (singleStepSolution == null)
                return;

            if (singleStepSolution.Result == null && singleStepSolution.Eliminations.Length == 0)
                return;

            int[] oldCanBe = new int[] { };

            if (singleStepSolution.Result != null)
            {
                var cell = Cells[singleStepSolution.Result.IndexOfRow, singleStepSolution.Result.IndexOfColumn];
                oldCanBe = cell.CanBe.ToArray();
                cell.Value = singleStepSolution.Result.Value;
            }
            if (singleStepSolution.Eliminations != null)
            {
                foreach (var elimination in singleStepSolution.Eliminations)
                {
                    // VALIDATION: Check if we're eliminating the correct solution value
                    if (ValidateEliminations && Solution != null)
                    {
                        int correctValue = Solution[elimination.IndexOfRow, elimination.IndexOfColumn];
                        if (elimination.Value == correctValue)
                        {
                            throw new InvalidOperationException(
                              $"INVALID ELIMINATION: Strategy '{singleStepSolution.Strategy}' tried to eliminate " +
                              $"{elimination.Value} from R{elimination.IndexOfRow + 1}C{elimination.IndexOfColumn + 1}, " +
                              $"but {elimination.Value} is the correct solution for that cell!");
                        }
                    }

                    Cells[elimination.IndexOfRow, elimination.IndexOfColumn].CanBe.Remove(elimination.Value);
                }
            }

            _steps.Add(new Tuple<SingleStepSolution, int[]>(singleStepSolution, oldCanBe.ToArray()));

            // STEP LOGGING: Log each step with board state for debugging
            if (EnableVerboseFileLogging && !string.IsNullOrEmpty(StepLogPath))
            {
                var logLines = new List<string>();
                logLines.Add($"=== STEP {_steps.Count}: {singleStepSolution.Strategy} ===");

                if (singleStepSolution.Result != null)
                {
                    var r = singleStepSolution.Result;
                    logLines.Add($"  PLACED: {r.Value} at R{r.IndexOfRow + 1}C{r.IndexOfColumn + 1}");
                }

                if (singleStepSolution.Eliminations != null && singleStepSolution.Eliminations.Length > 0)
                {
                    logLines.Add($"  ELIMINATIONS:");
                    foreach (var elim in singleStepSolution.Eliminations)
                    {
                        string marker = "";
                        if (Solution != null && elim.Value == Solution[elim.IndexOfRow, elim.IndexOfColumn])
                            marker = " *** INVALID - THIS IS THE SOLUTION! ***";
                        logLines.Add($"    - Remove {elim.Value} from R{elim.IndexOfRow + 1}C{elim.IndexOfColumn + 1}{marker}");
                    }
                }

                // Board state after this step
                logLines.Add($"  BOARD STATE:");
                for (int row = 0; row < NumberOfRowsOrColumnsInPuzzle; row++)
                {
                    var line = $"    R{row + 1}: ";
                    for (int col = 0; col < NumberOfRowsOrColumnsInPuzzle; col++)
                    {
                        var cell = Cells[row, col];
                        if (cell.HasValue)
                            line += $"[{cell.Value}] ";
                        else
                            line += $"({string.Join("", cell.CanBe)}) ";
                    }
                    logLines.Add(line);
                }
                logLines.Add("");
                System.IO.File.AppendAllLines(StepLogPath, logLines);
            }
        }

        /// <summary>
        /// Undoes the last applied <see cref="SingleStepSolution"/>.
        /// </summary>
        /// <returns><see cref="SingleStepSolution"/> which was undone, or null if nothing was undone.</returns>
        public SingleStepSolution UndoLastSingleStepSolution()
        {
            if (_steps.Count == 0)
                return null;

            var step = _steps.Last();
            var singleStepSolution = step.Item1;

            if (singleStepSolution.Result != null)
            {
                var cell = Cells[singleStepSolution.Result.IndexOfRow, singleStepSolution.Result.IndexOfColumn];
                cell.Value = 0;
                cell.CanBe.AddRange(step.Item2);
            }
            if (singleStepSolution.Eliminations != null)
            {
                foreach (var elimination in singleStepSolution.Eliminations)
                {
                    Cells[elimination.IndexOfRow, elimination.IndexOfColumn].CanBe.Add(elimination.Value);
                }

                // cell.CanBe is now no longer sorted
                foreach (var cell in Cells.OfType<Cell>())
                {
                    cell.CanBe.Sort();
                }
            }

            _steps.Remove(step);

            return singleStepSolution;
        }

        /// <summary>
        /// Converts puzzle into a 2D integer array.
        /// </summary>
        /// <returns>2D integer array where values represent values of cells in the puzzle.</returns>
        public int[,] ToIntArray()
        {
            var result = new int[NumberOfRowsOrColumnsInPuzzle, NumberOfRowsOrColumnsInPuzzle];

            for (int i = 0; i < NumberOfRowsOrColumnsInPuzzle; i++)
            {
                for (int j = 0; j < NumberOfRowsOrColumnsInPuzzle; j++)
                {
                    result[i, j] = Cells[i, j].Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns zero-based row and column index of the block which contains the <paramref name="cell"/>.
        /// Returns -1 for both row and column index if <paramref name="cell"/> is null or not part of the puzzle.
        /// </summary>
        public (int RowIndex, int ColumnIndex) GetBlockIndex(Cell cell)
        {
            if (cell == null)
                return (-1, -1);

            for (int i = 0; i < NumberOfRowsOrColumnsInPuzzle; i++)
            {
                for (int j = 0; j < NumberOfRowsOrColumnsInPuzzle; j++)
                {
                    if (Cells[i, j] == cell)
                        return (i / NumberOfRowsOrColumnsInBlock, j / NumberOfRowsOrColumnsInBlock);
                }
            }

            return (-1, -1);
        }

        /// <summary>
        /// Returns whether the puzzle is solved or not.
        /// </summary>
        public bool IsSolved()
        {
            return Validation.IsPuzzleSolved(this);
        }

        public override string ToString() => Formatter.PuzzleToString(this);
    }
}
