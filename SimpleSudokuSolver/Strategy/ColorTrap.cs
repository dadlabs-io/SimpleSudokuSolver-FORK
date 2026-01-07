using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Color Trap technique (part of Simple Coloring).
    /// 
    /// Logic: Build a chain of conjugate pairs for a candidate. Color the chain alternating 0/1.
    /// Any cell that "sees" both colors cannot contain the candidate (Color Trap elimination).
    /// 
    /// See also: http://sudopedia.enjoysudoku.com/Simple_Colors.html
    /// </summary>
    public class ColorTrap : ISudokuSolverStrategy
    {
        public string StrategyName => "Color Trap";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Try each candidate value 1-9
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindColorTrapForCandidate(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }

            return null;
        }

        private SingleStepSolution FindColorTrapForCandidate(SudokuPuzzle puzzle, int candidate)
        {
            // Build the conjugate pair graph for this candidate
            ColorGraph graph = ColorGraph.BuildForCandidate(puzzle, candidate);

            if (graph.Nodes.Count == 0)
            {
                return null; // No conjugate pairs for this candidate
            }

            // Process each unvisited chain
            while (!graph.AllNodesVisited())
            {
                graph.ClearColors();

                // Find an unvisited node to start a new chain
                ColorNode startNode = graph.Nodes.FirstOrDefault(n => !n.Visited);
                if (startNode == null) break;

                // Color the chain alternating 0/1
                ColorGraph.ColorChain(startNode, 0);

                // Get all colored nodes in this chain
                List<ColorNode> coloredNodes = graph.Nodes.Where(n => n.ColorValue.HasValue).ToList();
                List<ColorNode> color0Nodes = coloredNodes.Where(n => n.ColorValue == 0).ToList();
                List<ColorNode> color1Nodes = coloredNodes.Where(n => n.ColorValue == 1).ToList();

                if (color0Nodes.Count == 0 || color1Nodes.Count == 0)
                {
                    continue; // Need at least one of each color for a trap
                }

                // Find cells that can see BOTH colors = Color Trap victims
                List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

                for (int row = 0; row < 9; row++)
                {
                    for (int col = 0; col < 9; col++)
                    {
                        Cell cell = puzzle.Cells[row, col];

                        // Skip cells that don't have this candidate
                        if (!cell.CanBe.Contains(candidate)) continue;

                        // Skip cells that are part of the chain (they have a defined color)
                        if (coloredNodes.Any(n => n.Row == row && n.Column == col)) continue;

                        // Check if this cell sees at least one Color 0 node AND one Color 1 node
                        bool seesColor0 = color0Nodes.Any(n => n.Sees(row, col));
                        bool seesColor1 = color1Nodes.Any(n => n.Sees(row, col));

                        if (seesColor0 && seesColor1)
                        {
                            // Color Trap! This cell sees both colors, so candidate is eliminated
                            eliminations.Add(new SingleStepSolution.Candidate(row, col, candidate));
                        }
                    }
                }

                if (eliminations.Count > 0)
                {
                    SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                    // Populate ContextData for Unity visualization
                    solution.ContextData = new HintContextData();
                    solution.ContextData.PrimaryCandidate = candidate;

                    // FocusCells = Color 0 nodes (Color A)
                    foreach (ColorNode node in color0Nodes)
                    {
                        solution.ContextData.FocusCells.Add(new int[] { node.Row, node.Column });
                    }

                    // ReasoningCells = Color 1 nodes (Color B)
                    foreach (ColorNode node in color1Nodes)
                    {
                        solution.ContextData.ReasoningCells.Add(new int[] { node.Row, node.Column });
                    }

                    return solution;
                }
            }

            return null;
        }
    }
}
