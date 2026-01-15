using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: ALS-XY-Wing
    /// 
    /// Pattern: Three ALS connected by two RCCs (X and Y)
    /// ALS-A connects to ALS-B via RCC X
    /// ALS-A connects to ALS-C via RCC Y
    /// ALS-B and ALS-C both contain candidate Z
    /// 
    /// Eliminate Z from cells seeing all Z-cells in both ALS-B and ALS-C.
    /// 
    /// SE Rating: 7.8
    /// </summary>
    public class ALSXYWing : ISudokuSolverStrategy
    {
        public string StrategyName => "ALS-XY-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            List<AlmostLockedSet> allAls = AlmostLockedSet.FindAll(sudokuPuzzle);
            
            if (allAls.Count < 3) return null;

            // Try all combinations of 3 ALS
            for (int iA = 0; iA < allAls.Count; iA++)
            {
                AlmostLockedSet alsA = allAls[iA];

                for (int iB = 0; iB < allAls.Count; iB++)
                {
                    if (iB == iA) continue;
                    AlmostLockedSet alsB = allAls[iB];

                    // A and B must be disjoint
                    if (!AlsUtility.AreDisjoint(alsA, alsB)) continue;

                    // Find RCC X between A and B
                    List<int> commonAB = alsA.Candidates.Intersect(alsB.Candidates).ToList();

                    foreach (int x in commonAB)
                    {
                        if (!AlsUtility.IsRestrictedCommonCandidate(sudokuPuzzle, alsA, alsB, x, out _)) continue;

                        for (int iC = 0; iC < allAls.Count; iC++)
                        {
                            if (iC == iA || iC == iB) continue;
                            AlmostLockedSet alsC = allAls[iC];

                            // A, B, C must all be disjoint
                            if (!AlsUtility.AreDisjoint(alsA, alsC)) continue;
                            if (!AlsUtility.AreDisjoint(alsB, alsC)) continue;

                            // Find RCC Y between A and C (must be different from X)
                            List<int> commonAC = alsA.Candidates.Intersect(alsC.Candidates).ToList();

                            foreach (int y in commonAC)
                            {
                                if (y == x) continue;
                                if (!AlsUtility.IsRestrictedCommonCandidate(sudokuPuzzle, alsA, alsC, y, out _)) continue;

                                // Find Z in both B and C (not X or Y)
                                List<int> commonBC = alsB.Candidates.Intersect(alsC.Candidates).ToList();

                                foreach (int z in commonBC)
                                {
                                    if (z == x || z == y) continue;

                                    // Found ALS-XY-Wing! Get eliminations
                                    List<AlmostLockedSet> petals = new List<AlmostLockedSet> { alsB, alsC };
                                    List<SingleStepSolution.Candidate> eliminations = 
                                        AlsUtility.GetEliminations(sudokuPuzzle, petals, z);

                                    if (eliminations.Count > 0)
                                    {
                                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                                        
                                        solution.ContextData = new HintContextData
                                        {
                                            PrimaryCandidate = z,
                                            EliminationCandidate = z,
                                            RccCandidates = new List<int> { x, y },
                                            AlsGroups = new List<List<int[]>>
                                            {
                                                alsA.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                                alsB.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList(),
                                                alsC.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList()
                                            }
                                        };

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
    }
}
