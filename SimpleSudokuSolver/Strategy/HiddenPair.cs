using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
  /// <summary>
  /// Strategy looks for two cells in the same row / column / block that have two candidate values that cannot
  /// be in any other cell of the same row / column / block.
  /// If such two cells are found, all other candidate values from those two cells can be removed.
  /// </summary>
  /// <remarks>
  /// See also:
  /// - https://sudoku9x9.com/hidden_pair.html
  /// - http://www.sudokuwiki.org/Hidden_Candidates
  /// </remarks>
  public class HiddenPair : HiddenPairTripleQuadBase, ISudokuSolverStrategy
  {
    public string StrategyName => "Hidden Pair";

    public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
    {
      return GetSingleStepSolution(sudokuPuzzle, StrategyName);
    }

    protected override HiddenSetResult GetHiddenEliminations(
      IEnumerable<Cell> cells, SudokuPuzzle sudokuPuzzle)
    {
      HiddenSetResult result = HiddenSetResult.Empty;
      Cell[] cellsWithNoValue = cells.Where(x => !x.HasValue).ToArray();
      IDictionary<int, Cell[]> hiddenCandidates = GetHiddenCandidates(cellsWithNoValue, sudokuPuzzle, 2);

      for (int i = 1; i <= sudokuPuzzle.NumberOfRowsOrColumnsInPuzzle - 1; i++)
      {
        for (int j = i + 1; j <= sudokuPuzzle.NumberOfRowsOrColumnsInPuzzle; j++)
        {
          if (hiddenCandidates.ContainsKey(i) &&
            hiddenCandidates.ContainsKey(j) &&
            hiddenCandidates[i].SequenceEqual(hiddenCandidates[j]))
          {
            // Found a Hidden Pair! Capture context for visualization
            result.FocusCells = hiddenCandidates[i]; // The 2 cells forming the hidden pair
            result.HiddenValues = new int[] { i, j }; // The 2 hidden values

            // Add eliminations (remove OTHER candidates from these cells)
            result.Eliminations.AddRange(GetEliminations(hiddenCandidates[i][0], i, j));
            result.Eliminations.AddRange(GetEliminations(hiddenCandidates[i][1], i, j));

            return result; // Return first found
          }
        }
      }

      return result;
    }
  }
}
