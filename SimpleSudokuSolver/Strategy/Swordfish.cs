using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Swordfish
    /// 
    /// Definition (Row Swordfish):
    /// - We look for a candidate digit X.
    /// - We find 3 rows where candidate X appears ONLY in the same 3 columns (or a subset thereof).
    /// - If found, candidate X can be eliminated from all other cells in those 3 columns.
    /// 
    /// Definition (Column Swordfish):
    /// - Same logic, but swapping rows and columns.
    /// </summary>
    public class Swordfish : ISudokuSolverStrategy
    {
        public string StrategyName => "Swordfish";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // 1. Check Rows (Base) -> Eliminate in Columns (Cover)
            var solution = FindSwordfish(sudokuPuzzle, true);
            if (solution != null) return solution;

            // 2. Check Columns (Base) -> Eliminate in Rows (Cover)
            solution = FindSwordfish(sudokuPuzzle, false);
            if (solution != null) return solution;

            return null;
        }

        private SingleStepSolution FindSwordfish(SudokuPuzzle puzzle, bool baseIsRow)
        {
            int size = 3; // Swordfish is size 3
            int numHouses = baseIsRow ? puzzle.Rows.Length : puzzle.Columns.Length;

            for (int digit = 1; digit <= 9; digit++)
            {
                // Step 1: Gather potential base houses.
                // A valid base house must have the candidate at least twice (usually) and at most 'size' times?
                // Actually, for a Fish:
                // The candidates in the base set must be contained within the cover set.
                // So if we pick 3 rows, all occurrences of 'digit' in those 3 rows must fall into just 3 columns.

                // Map each house index to the set of indices in the other dimension where 'digit' exists.
                // e.g. if baseIsRow, map RowIndex -> List<ColIndex>
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
                            indices.Add(j); // j is ColIndex if baseIsRow, else RowIndex
                        }
                    }

                    // Optimization: A base house must have at least 2 candidates to be part of a useful fish?
                    // (Strictly speaking, it could have 1, but that would likely be a simpler technique or degenerate)
                    // We'll require >= 2 to avoid waste, though 1 is theoretically possible in "Finned" logic.
                    // For basic Swordfish, sticking to >= 2 is safe and standard for solvers.
                    // Actually, let's allow > 0 to be safe, but typically it's 2 or 3.
                    if (indices.Count > 0 && indices.Count <= size)
                    {
                        potentialHouses.Add(i);
                        houseMaps[i] = indices;
                    }
                }

                if (potentialHouses.Count < size) continue;

                // Step 2: Test combinations of 3 houses
                // We need to pick 3 distinct houses from potentialHouses
                for (int i = 0; i < potentialHouses.Count - 2; i++)
                {
                    for (int j = i + 1; j < potentialHouses.Count - 1; j++)
                    {
                        for (int k = j + 1; k < potentialHouses.Count; k++)
                        {
                            int h1 = potentialHouses[i];
                            int h2 = potentialHouses[j];
                            int h3 = potentialHouses[k];

                            // Union of all columns (or rows if baseIsCol)
                            var coverIndices = new HashSet<int>();
                            foreach (var idx in houseMaps[h1]) coverIndices.Add(idx);
                            foreach (var idx in houseMaps[h2]) coverIndices.Add(idx);
                            foreach (var idx in houseMaps[h3]) coverIndices.Add(idx);

                            if (coverIndices.Count == size) // The defining property of a basic Swordfish
                            {
                                // FOUND SWORDFISH!
                                // Base Set: {h1, h2, h3}
                                // Cover Set: {coverIndices...}

                                // Now check for eliminations
                                // We eliminate 'digit' from the Cover Set (columns), EXCLUDING the Base Set (rows)
                                var eliminations = new List<SingleStepSolution.Candidate>();

                                foreach (var coverIdx in coverIndices)
                                {
                                    var coverHouseCells = baseIsRow ? puzzle.Columns[coverIdx].Cells : puzzle.Rows[coverIdx].Cells;

                                    foreach (var cell in coverHouseCells)
                                    {
                                        // "cell" is in a Cover House.
                                        // We ensure it is NOT in one of the Base Houses.
                                        int cellBaseIdx = baseIsRow ? cell.RowIndex : cell.ColumnIndex;
                                        if (cellBaseIdx == h1 || cellBaseIdx == h2 || cellBaseIdx == h3)
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
                                    // If baseIsRow=true (Rows), Indices 0-8
                                    // If baseIsRow=false (Cols), Indices 9-17
                                    int offset = baseIsRow ? 0 : 9;
                                    solution.ContextData.HouseIndices.Add(h1 + offset);
                                    solution.ContextData.HouseIndices.Add(h2 + offset);
                                    solution.ContextData.HouseIndices.Add(h3 + offset);

                                    // ReasoningHouses: The Cover Houses
                                    // Removed as per architectural alignment with XWing.cs
                                    // The UI Hint logic will derive the cover set from the focus cells if needed.

                                    // FocusCells: The cells in the Base Set that form the fish body (intersections)
                                    // These are the cells at (h1, cX), (h2, cY), etc.
                                    AddFocusCell(solution, puzzle, h1, houseMaps[h1], baseIsRow);
                                    AddFocusCell(solution, puzzle, h2, houseMaps[h2], baseIsRow);
                                    AddFocusCell(solution, puzzle, h3, houseMaps[h3], baseIsRow);

                                    return solution;
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
