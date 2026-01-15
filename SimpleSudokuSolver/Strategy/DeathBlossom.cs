using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Death Blossom
    /// 
    /// Pattern: Stem cell with N candidates, each linking to an ALS petal
    /// - Stem cell has N candidates {A, B, C, ...}
    /// - Each candidate is an RCC with a different ALS
    /// - All ALS petals share a common candidate Z
    /// 
    /// Eliminate Z from cells seeing all Z-cells in ALL petals.
    /// 
    /// SE Rating: 8.1
    /// </summary>
    public class DeathBlossom : ISudokuSolverStrategy
    {
        public string StrategyName => "Death Blossom";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            List<AlmostLockedSet> allAls = AlmostLockedSet.FindAll(sudokuPuzzle);

            // Find potential stem cells (cells with 2-4 candidates)
            List<Cell> stems = new List<Cell>();
            foreach (Row row in sudokuPuzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell.CanBe.Count >= 2 && cell.CanBe.Count <= 4)
                    {
                        stems.Add(cell);
                    }
                }
            }

            foreach (Cell stem in stems)
            {
                List<int> stemCandidates = stem.CanBe.ToList();

                // For each candidate in stem, find ALS that:
                // 1. Contains that candidate
                // 2. The stem sees all cells in ALS with that candidate (RCC)
                // 3. ALS doesn't contain the stem

                Dictionary<int, List<AlmostLockedSet>> alsForCandidate = new Dictionary<int, List<AlmostLockedSet>>();

                foreach (int cand in stemCandidates)
                {
                    alsForCandidate[cand] = new List<AlmostLockedSet>();

                    foreach (AlmostLockedSet als in allAls)
                    {
                        // ALS must not contain stem
                        if (als.Cells.Contains(stem)) continue;

                        // ALS must contain this candidate
                        if (!als.Candidates.Contains(cand)) continue;

                        // Stem must see all cells in ALS that have this candidate
                        List<Cell> candCells = als.Cells.Where(c => c.CanBe.Contains(cand)).ToList();
                        bool stemSeesAll = candCells.All(c => AlsUtility.AreCellsVisible(sudokuPuzzle, stem, c));

                        if (stemSeesAll)
                        {
                            alsForCandidate[cand].Add(als);
                        }
                    }
                }

                // Try to form Death Blossom: pick one ALS for each stem candidate
                SingleStepSolution result = TryFormBlossom(sudokuPuzzle, stem, stemCandidates, alsForCandidate, 0, 
                    new List<AlmostLockedSet>(), new HashSet<AlmostLockedSet>());
                
                if (result != null) return result;
            }

            return null;
        }

        private SingleStepSolution TryFormBlossom(
            SudokuPuzzle puzzle,
            Cell stem,
            List<int> stemCandidates,
            Dictionary<int, List<AlmostLockedSet>> alsForCandidate,
            int candIndex,
            List<AlmostLockedSet> petals,
            HashSet<AlmostLockedSet> usedAls)
        {
            if (candIndex == stemCandidates.Count)
            {
                // All stem candidates have a petal - check for common Z
                return CheckBlossomEliminations(puzzle, stem, petals, stemCandidates);
            }

            int cand = stemCandidates[candIndex];

            foreach (AlmostLockedSet als in alsForCandidate[cand])
            {
                if (usedAls.Contains(als)) continue;

                // Check disjoint with other petals
                bool disjoint = petals.All(p => AlsUtility.AreDisjoint(als, p));
                if (!disjoint) continue;

                petals.Add(als);
                usedAls.Add(als);

                SingleStepSolution result = TryFormBlossom(puzzle, stem, stemCandidates, alsForCandidate, 
                    candIndex + 1, petals, usedAls);
                if (result != null) return result;

                petals.RemoveAt(petals.Count - 1);
                usedAls.Remove(als);
            }

            return null;
        }

        private SingleStepSolution CheckBlossomEliminations(
            SudokuPuzzle puzzle,
            Cell stem,
            List<AlmostLockedSet> petals,
            List<int> stemCandidates)
        {
            if (petals.Count < 2) return null;

            // Find candidates present in ALL petals (but not stem candidates)
            HashSet<int> commonCands = new HashSet<int>(petals[0].Candidates);
            for (int i = 1; i < petals.Count; i++)
            {
                commonCands.IntersectWith(petals[i].Candidates);
            }

            // Remove stem candidates from consideration
            foreach (int sc in stemCandidates)
            {
                commonCands.Remove(sc);
            }

            foreach (int z in commonCands)
            {
                List<SingleStepSolution.Candidate> eliminations = 
                    AlsUtility.GetEliminations(puzzle, petals, z);

                // Also exclude stem from eliminations
                eliminations = eliminations.Where(e => 
                    !(e.IndexOfRow == stem.RowIndex && e.IndexOfColumn == stem.ColumnIndex)).ToList();

                if (eliminations.Count > 0)
                {
                    SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                    
                    List<List<int[]>> alsGroups = new List<List<int[]>>();
                    // First group is stem
                    alsGroups.Add(new List<int[]> { new int[] { stem.RowIndex, stem.ColumnIndex } });
                    // Add petals
                    foreach (AlmostLockedSet petal in petals)
                    {
                        alsGroups.Add(petal.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList());
                    }

                    solution.ContextData = new HintContextData
                    {
                        PrimaryCandidate = z,
                        EliminationCandidate = z,
                        RccCandidates = stemCandidates,
                        AlsGroups = alsGroups,
                        Notes = $"Stem: {petals.Count} petals"
                    };

                    return solution;
                }
            }

            return null;
        }
    }
}
