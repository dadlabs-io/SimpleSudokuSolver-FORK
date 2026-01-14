using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the X-Chain technique (SE 5.5).
    /// 
    /// An X-Chain is a chain of strong links on a single candidate that alternates
    /// between cells. When the chain has an odd number of links (even number of cells),
    /// the endpoints have opposite parity - one must be true, one must be false.
    /// 
    /// Any cell that sees BOTH endpoints cannot contain the candidate
    /// (since at least one endpoint will be true).
    /// 
    /// X-Chains generalize Turbot Fish to chains of any length.
    /// </summary>
    public class XChain : ISudokuSolverStrategy
    {
        public string StrategyName => "X-Chain";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindXChain(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }
            return null;
        }

        private SingleStepSolution FindXChain(SudokuPuzzle puzzle, int candidate)
        {
            // Build the conjugate pair graph
            ColorGraph graph = ColorGraph.BuildForCandidate(puzzle, candidate);

            if (graph.Nodes.Count < 4)
            {
                return null; // Need at least 4 nodes for a meaningful X-Chain
            }

            // For each node, try to find chains of length 4+ 
            // (which means 3+ strong links, longer than Turbot Fish)
            List<ColorNode> allNodes = graph.Nodes.ToList();

            foreach (ColorNode start in allNodes)
            {
                // BFS to find all reachable nodes with their distances
                var distances = new Dictionary<ColorNode, int>();
                var paths = new Dictionary<ColorNode, List<ColorNode>>();
                var queue = new Queue<ColorNode>();

                distances[start] = 0;
                paths[start] = new List<ColorNode> { start };
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    ColorNode current = queue.Dequeue();
                    int currentDist = distances[current];

                    foreach (ColorNode neighbor in current.Neighbors)
                    {
                        if (!distances.ContainsKey(neighbor))
                        {
                            distances[neighbor] = currentDist + 1;
                            paths[neighbor] = new List<ColorNode>(paths[current]) { neighbor };
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Look for chains with even number of cells (odd distance = even cells)
                // and chain length > 2 (i.e., distance >= 3)
                foreach (var kvp in distances.Where(d => d.Value >= 3 && d.Value % 2 == 1))
                {
                    ColorNode end = kvp.Key;
                    List<ColorNode> chain = paths[end];

                    // Find eliminations
                    var eliminations = FindEliminations(puzzle, candidate, start, end, chain);
                    if (eliminations.Count > 0)
                    {
                        return CreateSolution(candidate, chain, eliminations);
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, int candidate, ColorNode start, ColorNode end, List<ColorNode> chain)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (!cell.CanBe.Contains(candidate)) continue;

                    // Skip if cell is part of the chain
                    if (chain.Any(n => n.Row == row && n.Column == col)) continue;

                    // Check if cell sees both endpoints
                    bool seesStart = start.Sees(row, col) || (start.Row == row && start.Column == col);
                    bool seesEnd = end.Sees(row, col) || (end.Row == row && end.Column == col);

                    // Use actual visibility check
                    seesStart = (row == start.Row || col == start.Column || 
                                (row / 3 == start.Row / 3 && col / 3 == start.Column / 3));
                    seesEnd = (row == end.Row || col == end.Column || 
                              (row / 3 == end.Row / 3 && col / 3 == end.Column / 3));

                    if (seesStart && seesEnd)
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(row, col, candidate));
                    }
                }
            }

            return eliminations;
        }

        private SingleStepSolution CreateSolution(int candidate, List<ColorNode> chain, List<SingleStepSolution.Candidate> eliminations)
        {
            SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candidate;

            // Alternate colors along the chain
            for (int i = 0; i < chain.Count; i++)
            {
                ColorNode node = chain[i];
                if (i % 2 == 0)
                    solution.ContextData.FocusCells.Add(new int[] { node.Row, node.Column });
                else
                    solution.ContextData.ReasoningCells.Add(new int[] { node.Row, node.Column });
            }

            solution.ContextData.Notes = $"Chain length: {chain.Count}";

            return solution;
        }
    }
}
