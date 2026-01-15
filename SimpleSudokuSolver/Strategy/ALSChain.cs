using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: ALS-Chain
    /// 
    /// Pattern: Chain of N ALS connected by RCCs
    /// ALS1 --X1-- ALS2 --X2-- ALS3 ... --Xn-- ALSn
    /// 
    /// Eliminate common candidate from cells seeing all occurrences
    /// in first and last ALS.
    /// 
    /// SE Rating: 8.0
    /// </summary>
    public class ALSChain : ISudokuSolverStrategy
    {
        public string StrategyName => "ALS-Chain";

        private const int MaxChainLength = 6;

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            List<AlmostLockedSet> allAls = AlmostLockedSet.FindAll(sudokuPuzzle);
            
            if (allAls.Count < 2) return null;

            // Build adjacency for ALS that can connect via RCC
            Dictionary<AlmostLockedSet, List<(AlmostLockedSet other, int rcc)>> adjacency = 
                new Dictionary<AlmostLockedSet, List<(AlmostLockedSet, int)>>();

            foreach (AlmostLockedSet als in allAls)
            {
                adjacency[als] = new List<(AlmostLockedSet, int)>();
            }

            for (int i = 0; i < allAls.Count; i++)
            {
                for (int j = i + 1; j < allAls.Count; j++)
                {
                    AlmostLockedSet als1 = allAls[i];
                    AlmostLockedSet als2 = allAls[j];

                    if (!AlsUtility.AreDisjoint(als1, als2)) continue;

                    List<int> common = als1.Candidates.Intersect(als2.Candidates).ToList();
                    foreach (int cand in common)
                    {
                        if (AlsUtility.IsRestrictedCommonCandidate(sudokuPuzzle, als1, als2, cand, out _))
                        {
                            adjacency[als1].Add((als2, cand));
                            adjacency[als2].Add((als1, cand));
                        }
                    }
                }
            }

            // DFS to find chains
            foreach (AlmostLockedSet startAls in allAls)
            {
                List<AlmostLockedSet> chain = new List<AlmostLockedSet> { startAls };
                List<int> rccs = new List<int>();
                HashSet<AlmostLockedSet> visited = new HashSet<AlmostLockedSet> { startAls };

                SingleStepSolution result = DFSFindChain(sudokuPuzzle, adjacency, chain, rccs, visited, startAls);
                if (result != null) return result;
            }

            return null;
        }

        private SingleStepSolution DFSFindChain(
            SudokuPuzzle puzzle,
            Dictionary<AlmostLockedSet, List<(AlmostLockedSet, int)>> adjacency,
            List<AlmostLockedSet> chain,
            List<int> rccs,
            HashSet<AlmostLockedSet> visited,
            AlmostLockedSet current)
        {
            if (chain.Count >= MaxChainLength) return null;

            // Check for eliminations with current chain (need at least 3 ALS)
            if (chain.Count >= 3)
            {
                AlmostLockedSet first = chain[0];
                AlmostLockedSet last = chain[chain.Count - 1];

                // Find common candidate Z in first and last (not an RCC in the chain)
                List<int> commonEnds = first.Candidates.Intersect(last.Candidates).ToList();
                foreach (int z in commonEnds)
                {
                    if (rccs.Contains(z)) continue;

                    List<AlmostLockedSet> endpoints = new List<AlmostLockedSet> { first, last };
                    List<SingleStepSolution.Candidate> eliminations = 
                        AlsUtility.GetEliminations(puzzle, endpoints, z);

                    if (eliminations.Count > 0)
                    {
                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                        
                        solution.ContextData = new HintContextData
                        {
                            PrimaryCandidate = z,
                            EliminationCandidate = z,
                            RccCandidates = new List<int>(rccs),
                            AlsGroups = chain.Select(als => 
                                als.Cells.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList()
                            ).ToList(),
                            Notes = $"Chain length: {chain.Count}"
                        };

                        return solution;
                    }
                }
            }

            // Extend chain
            foreach ((AlmostLockedSet next, int rcc) in adjacency[current])
            {
                if (visited.Contains(next)) continue;

                // RCC must be different from previous (alternating)
                if (rccs.Count > 0 && rccs[rccs.Count - 1] == rcc) continue;

                chain.Add(next);
                rccs.Add(rcc);
                visited.Add(next);

                SingleStepSolution result = DFSFindChain(puzzle, adjacency, chain, rccs, visited, next);
                if (result != null) return result;

                chain.RemoveAt(chain.Count - 1);
                rccs.RemoveAt(rccs.Count - 1);
                visited.Remove(next);
            }

            return null;
        }
    }
}
