using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
  /// <summary>
  /// Result from GetHiddenEliminations containing both eliminations and context for visualization.
  /// </summary>
  public struct HiddenSetResult
  {
    /// <summary>
    /// Candidates to eliminate from the hidden set cells.
    /// </summary>
    public List<SingleStepSolution.Candidate> Eliminations;

    /// <summary>
    /// The N cells forming the hidden set (e.g., 2 cells for Hidden Pair).
    /// </summary>
    public Cell[] FocusCells;

    /// <summary>
    /// The N hidden candidate values that define the set (e.g., {3, 7} for a Hidden Pair).
    /// </summary>
    public int[] HiddenValues;

    /// <summary>
    /// Creates an empty result with initialized Eliminations list.
    /// </summary>
    public static HiddenSetResult Empty => new HiddenSetResult
    {
      Eliminations = new List<SingleStepSolution.Candidate>(),
      FocusCells = null,
      HiddenValues = null
    };
  }

  public abstract class HiddenPairTripleQuadBase
  {
    /// <summary>
    /// Finds hidden set eliminations within a house (row, column, or block).
    /// Returns both eliminations AND context about which cells/values form the hidden set.
    /// </summary>
    protected abstract HiddenSetResult GetHiddenEliminations(
      IEnumerable<Cell> cells, SudokuPuzzle sudokuPuzzle);

    protected SingleStepSolution GetSingleStepSolution(SudokuPuzzle sudokuPuzzle, string strategyName)
    {
      // Try rows first (house indices 0-8)
      for (int i = 0; i < sudokuPuzzle.Rows.Length; i++)
      {
        HiddenSetResult result = GetHiddenEliminations(sudokuPuzzle.Rows[i].Cells, sudokuPuzzle);
        if (result.Eliminations != null && result.Eliminations.Count > 0)
        {
          SingleStepSolution solution = new SingleStepSolution(result.Eliminations.Distinct().ToArray(), strategyName);
          solution.ContextData = BuildContextData(result, i); // Row index 0-8
          return solution;
        }
      }

      // Try columns (house indices 9-17)
      for (int i = 0; i < sudokuPuzzle.Columns.Length; i++)
      {
        HiddenSetResult result = GetHiddenEliminations(sudokuPuzzle.Columns[i].Cells, sudokuPuzzle);
        if (result.Eliminations != null && result.Eliminations.Count > 0)
        {
          SingleStepSolution solution = new SingleStepSolution(result.Eliminations.Distinct().ToArray(), strategyName);
          solution.ContextData = BuildContextData(result, 9 + i); // Column index 9-17
          return solution;
        }
      }

      // Try blocks (house indices 18-26)
      // Blocks is a 2D array [row, col], iterate and track flat index
      int blockIndex = 0;
      for (int r = 0; r < sudokuPuzzle.Blocks.GetLength(0); r++)
      {
        for (int c = 0; c < sudokuPuzzle.Blocks.GetLength(1); c++)
        {
          HiddenSetResult result = GetHiddenEliminations(sudokuPuzzle.Blocks[r, c].Cells.OfType<Cell>(), sudokuPuzzle);
          if (result.Eliminations != null && result.Eliminations.Count > 0)
          {
            SingleStepSolution solution = new SingleStepSolution(result.Eliminations.Distinct().ToArray(), strategyName);
            solution.ContextData = BuildContextData(result, 18 + blockIndex); // Block index 18-26
            return solution;
          }
          blockIndex++;
        }
      }

      return null;
    }

    /// <summary>
    /// Builds HintContextData from the HiddenSetResult for UI visualization.
    /// </summary>
    private HintContextData BuildContextData(HiddenSetResult result, int houseIndex)
    {
      HintContextData context = new HintContextData();

      // FocusCells: The cells forming the hidden set (0-indexed [row, col])
      if (result.FocusCells != null)
      {
        foreach (Cell cell in result.FocusCells)
        {
          context.FocusCells.Add(new int[] { cell.RowIndex, cell.ColumnIndex });
        }
      }

      // FocusCandidates: The hidden values that define the set
      if (result.HiddenValues != null)
      {
        context.FocusCandidates.AddRange(result.HiddenValues);
      }

      // HouseIndices: Which house contains this pattern
      context.HouseIndices.Add(houseIndex);

      return context;
    }

    /// <summary>
    /// Returns a dictionary where key is one of <see cref="SudokuPuzzle.PossibleCellValues"/> 
    /// and value is the collection of cells containing that value, but only if the <paramref name="cellsWithNoValue"/>
    /// contains a certain number of such cells (<paramref name="numberOfCellsContainingValue"/>).
    /// </summary>
    /// <param name="cellsWithNoValue">Empty cells of a single row/column/block.</param>
    /// <param name="sudokuPuzzle">Sudoku puzzle.</param>
    /// <param name="numberOfCellsContainingValue">Tells how many cells containing a value we are looking for.</param>
    /// <returns>See summary.</returns>
    protected IDictionary<int, Cell[]> GetHiddenCandidates(Cell[] cellsWithNoValue, SudokuPuzzle sudokuPuzzle,
      params int[] numberOfCellsContainingValue)
    {
      var candidates = new Dictionary<int, Cell[]>();

      foreach (var cellValue in sudokuPuzzle.PossibleCellValues)
      {
        var valueInCells = cellsWithNoValue.Where(x => x.CanBe.Contains(cellValue)).ToArray();
        if (numberOfCellsContainingValue.Contains(valueInCells.Length))
          candidates.Add(cellValue, valueInCells);
      }

      return candidates;
    }

    /// <summary>
    /// For each member of<paramref name="cell"/>'s <see cref="Cell.CanBe"/>:
    /// - if member is part of <paramref name="valuesToExclude"/>, ignore it
    /// - if member is not part of <paramref name="valuesToExclude"/> return it as an elimination
    /// </summary>
    /// <param name="cell">Cell which is analyzed for eliminations.</param>
    /// <param name="valuesToExclude">Values which are not elimination candidates.</param>
    /// <returns>See summary.</returns>
    protected IEnumerable<SingleStepSolution.Candidate> GetEliminations(Cell cell, params int[] valuesToExclude)
    {
      var eliminations = new List<SingleStepSolution.Candidate>();
      var eliminatedValues = cell.CanBe.Except(valuesToExclude);
      foreach (var eliminatedValue in eliminatedValues)
      {
        eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, eliminatedValue));
      }

      return eliminations;
    }
  }
}
