using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
  /// <summary>
  /// Strategy looks for three cells in the same row / column / block that have IN TOTAL three candidate values 
  /// that cannot be in any other cell of the same row / column / block.
  /// If such three cells are found, all other candidate values from those three cells can be removed.
  /// </summary>
  /// <remarks>
  /// See also:
  /// - https://sudoku9x9.com/hidden_pair.html
  /// - http://www.sudokuwiki.org/Hidden_Candidates
  /// </remarks>
  public class HiddenTriple : HiddenPairTripleQuadBase, ISudokuSolverStrategy
  {
    public string StrategyName => "Hidden Triple";

    public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
    {
      return GetSingleStepSolution(sudokuPuzzle, StrategyName);
    }

    protected override HiddenSetResult GetHiddenEliminations(
      IEnumerable<Cell> cells, SudokuPuzzle sudokuPuzzle)
    {
      HiddenSetResult result = HiddenSetResult.Empty;
      Cell[] cellsWithNoValue = cells.Where(x => !x.HasValue).ToArray();
      IDictionary<int, Cell[]> hiddenCandidates = GetHiddenCandidates(cellsWithNoValue, sudokuPuzzle, 2, 3);

      for (int i = 1; i <= sudokuPuzzle.NumberOfRowsOrColumnsInPuzzle - 2; i++)
      {
        for (int j = i + 1; j <= sudokuPuzzle.NumberOfRowsOrColumnsInPuzzle - 1; j++)
        {
          for (int k = j + 1; k <= sudokuPuzzle.NumberOfRowsOrColumnsInPuzzle; k++)
          {
            if (hiddenCandidates.ContainsKey(i) &&
              hiddenCandidates.ContainsKey(j) &&
              hiddenCandidates.ContainsKey(k))
            {
              Cell[] union = hiddenCandidates[i].Union(hiddenCandidates[j]).
                Union(hiddenCandidates[k]).Distinct().ToArray();
              if (union.Length == 3)
              {
                // Found a Hidden Triple! Capture context for visualization
                result.FocusCells = union; // The 3 cells forming the hidden triple
                result.HiddenValues = new int[] { i, j, k }; // The 3 hidden values

                // Add eliminations (remove OTHER candidates from these cells)
                foreach (Cell cell in union)
                {
                  result.Eliminations.AddRange(GetEliminations(cell, i, j, k));
                }

                return result; // Return first found
              }
            }
          }
        }
      }

      return result;
    }
  }
}
