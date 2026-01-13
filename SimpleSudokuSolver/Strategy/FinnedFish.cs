using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Finned Fish (X-Wing, Swordfish, Jellyfish)
    /// 
    /// A finned fish is a fish pattern where one base house has extra candidates (the "fin").
    /// The fin must be in a box that intersects the fish body.
    /// Eliminations are restricted to cells that see BOTH the cover house AND the fin's box.
    /// 
    /// Supports sizes 2 (Finned X-Wing), 3 (Finned Swordfish), 4 (Finned Jellyfish).
    /// </summary>
    public class FinnedFish : ISudokuSolverStrategy
    {
        public string StrategyName => _currentStrategyName;
        private string _currentStrategyName = "Finned Fish";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Try each size: 2 (X-Wing), 3 (Swordfish), 4 (Jellyfish)
            foreach (int size in new[] { 2, 3, 4 })
            {
                // Try rows as base
                var solution = FindFinnedFish(sudokuPuzzle, size, true);
                if (solution != null) return solution;

                // Try columns as base
                solution = FindFinnedFish(sudokuPuzzle, size, false);
                if (solution != null) return solution;
            }

            return null;
        }

        private SingleStepSolution FindFinnedFish(SudokuPuzzle puzzle, int size, bool baseIsRow)
        {
            int numHouses = baseIsRow ? puzzle.Rows.Length : puzzle.Columns.Length;

            for (int digit = 1; digit <= 9; digit++)
            {
                // Map each house to positions where digit appears
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

                    // For finned fish, we allow slightly more candidates (size + small fin)
                    // A valid base house needs 2 to size+2 candidates
                    if (indices.Count >= 2 && indices.Count <= size + 2)
                    {
                        houseMaps[i] = indices;
                    }
                }

                if (houseMaps.Count < size) continue;

                // Generate all combinations of 'size' houses
                var houseIndices = houseMaps.Keys.ToList();
                foreach (var combo in GetCombinations(houseIndices, size))
                {
                    var result = TryFinnedFishCombo(puzzle, digit, size, baseIsRow, combo, houseMaps);
                    if (result != null) return result;
                }
            }

            return null;
        }

        private SingleStepSolution TryFinnedFishCombo(
            SudokuPuzzle puzzle, int digit, int size, bool baseIsRow,
            List<int> baseHouses, Dictionary<int, List<int>> houseMaps)
        {
            // Union all cover indices
            var allCoverIndices = new HashSet<int>();
            foreach (var house in baseHouses)
            {
                foreach (var idx in houseMaps[house])
                {
                    allCoverIndices.Add(idx);
                }
            }

            // For a finned fish: cover count should be exactly 'size' OR size+1 with a fin
            // Basic fish: coverCount == size
            // Finned fish: coverCount == size, but one base house has extra candidates in a fin box

            if (allCoverIndices.Count != size) return null;

            // Check if this is a finned pattern (one house has candidates outside the cover set)
            List<int[]> finCells = new List<int[]>();
            List<int[]> fishCells = new List<int[]>();
            int? finBox = null;

            foreach (var house in baseHouses)
            {
                foreach (var coverIdx in houseMaps[house])
                {
                    int r = baseIsRow ? house : coverIdx;
                    int c = baseIsRow ? coverIdx : house;
                    
                    if (allCoverIndices.Contains(coverIdx))
                    {
                        fishCells.Add(new int[] { r, c });
                    }
                }
            }

            // Now look for fins - candidates in base houses that are outside the perfect fish pattern
            // For each base house, check if there are extra candidates
            foreach (var house in baseHouses)
            {
                var positions = houseMaps[house];
                
                // Check for candidates that would make this a fin
                // These are positions in the base house where the candidate exists
                // but that aren't part of the clean fish pattern
                foreach (var pos in positions)
                {
                    int r = baseIsRow ? house : pos;
                    int c = baseIsRow ? pos : house;
                    int box = (r / 3) * 3 + (c / 3);

                    // Check if this cell's position in the cover set is "extra"
                    // For a clean fish, each base house should have candidates only in the cover positions
                    // A fin is when a base house has an extra candidate
                    
                    // Count how many base houses use this cover position
                    int usageCount = 0;
                    foreach (var h in baseHouses)
                    {
                        if (houseMaps[h].Contains(pos)) usageCount++;
                    }

                    // If only one house uses this position and that house has more than minimum candidates,
                    // this could be a fin
                    if (usageCount == 1 && positions.Count > 2)
                    {
                        // This is a potential fin cell
                        if (finBox == null)
                        {
                            finBox = box;
                            finCells.Add(new int[] { r, c });
                        }
                        else if (finBox == box)
                        {
                            finCells.Add(new int[] { r, c });
                        }
                        else
                        {
                            // Fins must be in the same box - invalid
                            return null;
                        }
                    }
                }
            }

            // If no fins found, this is a basic fish (handled by other strategies)
            if (finCells.Count == 0) return null;

            // Find eliminations - cells that see BOTH the cover line AND the fin's box
            var eliminations = new List<SingleStepSolution.Candidate>();

            foreach (var coverIdx in allCoverIndices)
            {
                var coverHouseCells = baseIsRow ? puzzle.Columns[coverIdx].Cells : puzzle.Rows[coverIdx].Cells;

                foreach (var cell in coverHouseCells)
                {
                    // Skip if in a base house
                    int cellBaseIdx = baseIsRow ? cell.RowIndex : cell.ColumnIndex;
                    if (baseHouses.Contains(cellBaseIdx)) continue;

                    // Must have the candidate
                    if (!cell.CanBe.Contains(digit)) continue;

                    // Must see the fin's box
                    int cellBox = (cell.RowIndex / 3) * 3 + (cell.ColumnIndex / 3);
                    if (cellBox != finBox) continue;

                    eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, digit));
                }
            }

            if (eliminations.Count == 0) return null;

            // Set strategy name based on size
            _currentStrategyName = size switch
            {
                2 => "Finned X-Wing",
                3 => "Finned Swordfish",
                4 => "Finned Jellyfish",
                _ => "Finned Fish"
            };

            var solution = new SingleStepSolution(eliminations.Distinct().ToArray(), _currentStrategyName);

            // Populate ContextData
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = digit;

            // HouseIndices: Base houses
            int offset = baseIsRow ? 0 : 9;
            foreach (var house in baseHouses)
            {
                solution.ContextData.HouseIndices.Add(house + offset);
            }

            // FocusCells: The fish body cells
            foreach (var fc in fishCells)
            {
                solution.ContextData.FocusCells.Add(fc);
            }

            // FinCells: The fin cells
            foreach (var fin in finCells)
            {
                solution.ContextData.FinCells.Add(fin);
            }

            return solution;
        }

        private IEnumerable<List<int>> GetCombinations(List<int> source, int k)
        {
            if (k == 0)
            {
                yield return new List<int>();
                yield break;
            }

            for (int i = 0; i <= source.Count - k; i++)
            {
                foreach (var combo in GetCombinations(source.Skip(i + 1).ToList(), k - 1))
                {
                    combo.Insert(0, source[i]);
                    yield return combo;
                }
            }
        }
    }
}
