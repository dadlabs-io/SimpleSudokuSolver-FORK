using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Color Wrap technique (part of Simple Coloring).
    /// 
    /// Logic: If two nodes of the SAME color in a conjugate chain appear in the SAME house 
    /// (Row, Column, or Block), then that color is FALSE.
    /// All cells of that color in the chain can have the candidate eliminated.
    /// 
    /// See also: http://sudopedia.enjoysudoku.com/Simple_Colors.html
    /// </summary>
    public class ColorWrap : ISudokuSolverStrategy
    {
        public string StrategyName => "Color Wrap";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Try each candidate value 1-9
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindColorWrapForCandidate(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }

            return null;
        }

        private SingleStepSolution FindColorWrapForCandidate(SudokuPuzzle puzzle, int candidate)
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

                // Check for wrap contradictions in color 0
                SingleStepSolution solution = CheckForWrap(puzzle, candidate, coloredNodes, color0Nodes);
                if (solution != null) return solution;

                // Check for wrap contradictions in color 1
                solution = CheckForWrap(puzzle, candidate, coloredNodes, color1Nodes);
                if (solution != null) return solution;
            }

            return null;
        }

        private SingleStepSolution CheckForWrap(SudokuPuzzle puzzle, int candidate, List<ColorNode> allChainNodes, List<ColorNode> parityNodes)
        {
            if (parityNodes.Count < 2) return null;

            // Check Rows
            for (int row = 0; row < 9; row++)
            {
                var nodesInRow = parityNodes.Where(n => n.Row == row).ToList();
                if (nodesInRow.Count >= 2)
                {
                    return CreateSolution(candidate, allChainNodes, parityNodes, nodesInRow);
                }
            }

            // Check Columns
            for (int col = 0; col < 9; col++)
            {
                var nodesInCol = parityNodes.Where(n => n.Column == col).ToList();
                if (nodesInCol.Count >= 2)
                {
                    return CreateSolution(candidate, allChainNodes, parityNodes, nodesInCol);
                }
            }

            // Check Blocks
            for (int bRow = 0; bRow < 3; bRow++)
            {
                for (int bCol = 0; bCol < 3; bCol++)
                {
                    var nodesInBlock = parityNodes.Where(n => n.Row / 3 == bRow && n.Column / 3 == bCol).ToList();
                    if (nodesInBlock.Count >= 2)
                    {
                        return CreateSolution(candidate, allChainNodes, parityNodes, nodesInBlock);
                    }
                }
            }

            return null;
        }

        private SingleStepSolution CreateSolution(int candidate, List<ColorNode> allChainNodes, List<ColorNode> falseParityNodes, List<ColorNode> offendingNodes)
        {
            // All cells of the false parity are eliminations
            List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();
            foreach (var node in falseParityNodes)
            {
                eliminations.Add(new SingleStepSolution.Candidate(node.Row, node.Column, candidate));
            }

            SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candidate;

            // Follow Color Trap convention:
            // Color 0 nodes (Color A) -> FocusCells
            // Color 1 nodes (Color B) -> ReasoningCells
            var color0Nodes = allChainNodes.Where(n => n.ColorValue == 0).ToList();
            var color1Nodes = allChainNodes.Where(n => n.ColorValue == 1).ToList();

            foreach (var node in color0Nodes)
                solution.ContextData.FocusCells.Add(new int[] { node.Row, node.Column });

            foreach (var node in color1Nodes)
                solution.ContextData.ReasoningCells.Add(new int[] { node.Row, node.Column });

            // Detect clashing house for hint context
            if (offendingNodes.Count >= 2)
            {
                int r1 = offendingNodes[0].Row;
                int c1 = offendingNodes[0].Column;
                int r2 = offendingNodes[1].Row;
                int c2 = offendingNodes[1].Column;

                int houseIdx = -1;
                if (r1 == r2) houseIdx = r1; // Row 0-8
                else if (c1 == c2) houseIdx = c1 + 9; // Column 9-17
                else houseIdx = (r1 / 3 * 3 + c1 / 3) + 18; // Block 18-26

                solution.ContextData.HouseIndices.Add(houseIdx);

                // Store WHICH parity clashed (0 or 1) in FocusCandidates[0]
                int clashingParity = offendingNodes[0].ColorValue ?? 0;
                solution.ContextData.FocusCandidates.Add(clashingParity);
            }

            return solution;
        }
    }
}
