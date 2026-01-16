using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Turbot Fish technique (SE 4.0).
    /// 
    /// A Turbot Fish consists of exactly 2 strong links (conjugate pairs) that form a chain
    /// of 3 cells (A-B-C). If A and C "see" each other (same row/col/block), then any cell
    /// that sees BOTH A and C cannot contain the candidate.
    ///
    /// Special cases that are covered:
    /// - Skyscraper: Both strong links are in parallel rows/columns
    /// - Two-String Kite: One strong link in a row, one in a column with box connection
    /// - Generic Turbot: Any two strong links connected via a common cell
    /// 
    /// Logic: If A-B is a strong link and B-C is a strong link, and A sees C, then:
    /// - A and C have the same truth value (both true or both false)
    /// - But A and C share a house, so they can't both be true
    /// - Therefore both A and C are false, and B is true
    /// - Any cell seeing BOTH A and C (and not B) can have the candidate eliminated
    /// </summary>
    public class TurbotFish : ISudokuSolverStrategy
    {
        public string StrategyName => "Turbot Fish";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Try each candidate value 1-9
            for (int candidate = 1; candidate <= 9; candidate++)
            {
                SingleStepSolution solution = FindTurbotFishForCandidate(sudokuPuzzle, candidate);
                if (solution != null)
                {
                    return solution;
                }
            }

            return null;
        }

        private SingleStepSolution FindTurbotFishForCandidate(SudokuPuzzle puzzle, int candidate)
        {
            // Build the conjugate pair graph
            ColorGraph graph = ColorGraph.BuildForCandidate(puzzle, candidate);

            if (graph.Nodes.Count < 3)
            {
                return null; // Need at least 3 nodes for a Turbot Fish
            }

            // Find all nodes that have exactly 2 connections (potential endpoints of 2-link chain)
            // and iterate through looking for valid Turbot Fish patterns
            List<ColorNode> nodes = graph.Nodes.ToList();

            // For each potential "pivot" cell B that connects two strong links
            foreach (ColorNode pivotB in nodes.Where(n => n.Neighbors.Count >= 2))
            {
                List<ColorNode> neighbors = pivotB.Neighbors.ToList();

                // Try all pairs of neighbors
                for (int i = 0; i < neighbors.Count; i++)
                {
                    for (int j = i + 1; j < neighbors.Count; j++)
                    {
                        ColorNode endpointA = neighbors[i];
                        ColorNode endpointC = neighbors[j];

                        // A and C are the endpoints, B is the pivot
                        // CRITICAL: Only proceed if A and C "see" each other (share a house)
                        // This is required for valid Turbot Fish eliminations
                        if (endpointA.Sees(endpointC.Row, endpointC.Column))
                        {
                            // Find any cell that sees BOTH A and C and has the candidate
                            List<SingleStepSolution.Candidate> eliminations = FindEliminations(
                                puzzle, candidate, endpointA, endpointC, pivotB);

                            if (eliminations.Count > 0)
                            {
                                return CreateSolution(candidate, endpointA, pivotB, endpointC, eliminations);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, int candidate, ColorNode endA, ColorNode endC, ColorNode pivot)
        {
            List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

            // Find all cells that:
            // 1. Have the candidate
            // 2. See BOTH endpoints A and C
            // 3. Are not part of the chain (not A, not B, not C)
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Cell cell = puzzle.Cells[row, col];

                    // Skip if cell doesn't have the candidate
                    if (!cell.CanBe.Contains(candidate)) continue;

                    // Skip if cell is part of the chain
                    if (row == endA.Row && col == endA.Column) continue;
                    if (row == pivot.Row && col == pivot.Column) continue;
                    if (row == endC.Row && col == endC.Column) continue;

                    // Check if this cell sees BOTH endpoints
                    bool seesA = SolverUtility.CellsSeeEachOther(cell, puzzle.Cells[endA.Row, endA.Column]);
                    bool seesC = SolverUtility.CellsSeeEachOther(cell, puzzle.Cells[endC.Row, endC.Column]);

                    if (seesA && seesC)
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(row, col, candidate));
                    }
                }
            }

            return eliminations;
        }

        private SingleStepSolution CreateSolution(
            int candidate, ColorNode endA, ColorNode pivot, ColorNode endC,
            List<SingleStepSolution.Candidate> eliminations)
        {
            SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candidate;

            // Store the chain cells for visualization
            // FocusCells: the two endpoints (same parity = both true or both false together)
            solution.ContextData.FocusCells.Add(new int[] { endA.Row, endA.Column });
            solution.ContextData.FocusCells.Add(new int[] { endC.Row, endC.Column });

            // ReasoningCells: the pivot cell
            solution.ContextData.ReasoningCells.Add(new int[] { pivot.Row, pivot.Column });

            // Determine the pattern type for smart hint text
            // Skyscraper: both strong links are parallel (same row orientation or same col orientation)
            // Two-String Kite: strong links are perpendicular with box connection
            string patternType = DeterminePatternType(endA, pivot, endC);
            solution.ContextData.Notes = patternType;

            return solution;
        }

        private string DeterminePatternType(ColorNode endA, ColorNode pivot, ColorNode endC)
        {
            // Check the orientation of the two strong links:
            // Link 1: A to Pivot
            // Link 2: Pivot to C

            bool link1Row = endA.Row == pivot.Row;
            bool link1Col = endA.Column == pivot.Column;
            bool link2Row = pivot.Row == endC.Row;
            bool link2Col = pivot.Column == endC.Column;

            // Skyscraper: both links use same orientation (both row-based or both col-based)
            if (link1Row && link2Row) return "Skyscraper";
            if (link1Col && link2Col) return "Skyscraper";

            // Two-String Kite: one link is row-based, one is column-based
            if ((link1Row && link2Col) || (link1Col && link2Row)) return "Two-String Kite";

            // Box-based links or mixed: Generic Turbot Fish
            return "Turbot Fish";
        }
    }
}
