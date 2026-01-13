using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Jellyfish
    /// 
    /// Definition (Row Jellyfish):
    /// - We look for a candidate digit X.
    /// - We find 4 rows where candidate X appears ONLY in the same 4 columns (or a subset thereof).
    /// - If found, candidate X can be eliminated from all other cells in those 4 columns.
    /// 
    /// Definition (Column Jellyfish):
    /// - Same logic, but swapping rows and columns.
    /// </summary>
    public class Jellyfish : ISudokuSolverStrategy
    {
        public string StrategyName => "Jellyfish";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // 1. Check Rows (Base) -> Eliminate in Columns (Cover)
            var solution = FindJellyfish(sudokuPuzzle, true);
            if (solution != null) return solution;

            // 2. Check Columns (Base) -> Eliminate in Rows (Cover)
            solution = FindJellyfish(sudokuPuzzle, false);
            if (solution != null) return solution;

            return null;
        }

        private SingleStepSolution FindJellyfish(SudokuPuzzle puzzle, bool baseIsRow)
        {
            int size = 4; // Jellyfish is size 4
            int numHouses = baseIsRow ? puzzle.Rows.Length : puzzle.Columns.Length;

            for (int digit = 1; digit <= 9; digit++)
            {
                // Map each house index to the set of indices in the other dimension where 'digit' exists.
                var potentialHouses = new List<int>();
                var houseMaps = new Dictionary<int, List<int>>();

                for (int i = 0; i < numHouses; i++)
                {
                    var cells = baseIsRow ? puzzle.Rows[i].Cells : puzzle.Columns[i].Cells;
                    var indices = new List<int>();

                    for (int j = 0; j < cells.Length; j++)
                    {
                        if (cells[j].CanBe.Contains(digit))
                        {
                            indices.Add(j);
                        }
                    }

                    if (indices.Count > 0 && indices.Count <= size)
                    {
                        potentialHouses.Add(i);
                        houseMaps[i] = indices;
                    }
                }

                if (potentialHouses.Count < size) continue;

                // Test combinations of 4 houses
                for (int i = 0; i < potentialHouses.Count - 3; i++)
                {
                    for (int j = i + 1; j < potentialHouses.Count - 2; j++)
                    {
                        for (int k = j + 1; k < potentialHouses.Count - 1; k++)
                        {
                            for (int l = k + 1; l < potentialHouses.Count; l++)
                            {
                                int h1 = potentialHouses[i];
                                int h2 = potentialHouses[j];
                                int h3 = potentialHouses[k];
                                int h4 = potentialHouses[l];

                                // Union of all columns (or rows if baseIsCol)
                                var coverIndices = new HashSet<int>();
                                foreach (var idx in houseMaps[h1]) coverIndices.Add(idx);
                                foreach (var idx in houseMaps[h2]) coverIndices.Add(idx);
                                foreach (var idx in houseMaps[h3]) coverIndices.Add(idx);
                                foreach (var idx in houseMaps[h4]) coverIndices.Add(idx);

                                if (coverIndices.Count == size) // The defining property of a basic Jellyfish
                                {
                                    // FOUND JELLYFISH!
                                    var eliminations = new List<SingleStepSolution.Candidate>();

                                    foreach (var coverIdx in coverIndices)
                                    {
                                        var coverHouseCells = baseIsRow ? puzzle.Columns[coverIdx].Cells : puzzle.Rows[coverIdx].Cells;

                                        foreach (var cell in coverHouseCells)
                                        {
                                            int cellBaseIdx = baseIsRow ? cell.RowIndex : cell.ColumnIndex;
                                            if (cellBaseIdx == h1 || cellBaseIdx == h2 || cellBaseIdx == h3 || cellBaseIdx == h4)
                                                continue;

                                            if (cell.CanBe.Contains(digit))
                                            {
                                                eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, digit));
                                            }
                                        }
                                    }

                                    if (eliminations.Count > 0)
                                    {
                                        var solution = new SingleStepSolution(eliminations.Distinct().ToArray(), StrategyName);

                                        // Populate ContextData for Visualization
                                        solution.ContextData = new HintContextData();
                                        solution.ContextData.PrimaryCandidate = digit;

                                        // HouseIndices: The Base Houses
                                        int offset = baseIsRow ? 0 : 9;
                                        solution.ContextData.HouseIndices.Add(h1 + offset);
                                        solution.ContextData.HouseIndices.Add(h2 + offset);
                                        solution.ContextData.HouseIndices.Add(h3 + offset);
                                        solution.ContextData.HouseIndices.Add(h4 + offset);

                                        // FocusCells: The cells in the Base Set that form the fish body
                                        AddFocusCell(solution, puzzle, h1, houseMaps[h1], baseIsRow);
                                        AddFocusCell(solution, puzzle, h2, houseMaps[h2], baseIsRow);
                                        AddFocusCell(solution, puzzle, h3, houseMaps[h3], baseIsRow);
                                        AddFocusCell(solution, puzzle, h4, houseMaps[h4], baseIsRow);

                                        return solution;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void AddFocusCell(SingleStepSolution solution, SudokuPuzzle puzzle, int baseIdx, List<int> coverIndices, bool baseIsRow)
        {
            foreach (var coverIdx in coverIndices)
            {
                int r = baseIsRow ? baseIdx : coverIdx;
                int c = baseIsRow ? coverIdx : baseIdx;
                solution.ContextData.FocusCells.Add(new int[] { r, c });
            }
        }
    }
}
