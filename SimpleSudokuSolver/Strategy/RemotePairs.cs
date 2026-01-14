using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Implements the Remote Pairs technique (SE 5.0).
    /// 
    /// Remote Pairs occur when a chain of bivalue cells (all with the same two candidates)
    /// are connected through shared houses. When the chain has an even number of cells,
    /// the endpoints can see each other through the chain, allowing eliminations.
    /// 
    /// Key properties:
    /// - All cells in the chain have exactly 2 candidates (the same pair)
    /// - Each consecutive pair shares a house (row, column, or box)
    /// - Eliminations occur in cells that see both endpoints of any odd-length subchain
    /// </summary>
    public class RemotePairs : ISudokuSolverStrategy
    {
        public string StrategyName => "Remote Pairs";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells and group by candidate pair
            var bivValueCells = new Dictionary<(int, int), List<Cell>>();

            foreach (Cell cell in sudokuPuzzle.Cells)
            {
                if (cell.CanBe.Count == 2)
                {
                    var candidates = cell.CanBe.OrderBy(x => x).ToList();
                    var key = (candidates[0], candidates[1]);
                    
                    if (!bivValueCells.ContainsKey(key))
                        bivValueCells[key] = new List<Cell>();
                    
                    bivValueCells[key].Add(cell);
                }
            }

            // For each pair of candidates with multiple bivalue cells
            foreach (var kvp in bivValueCells)
            {
                if (kvp.Value.Count < 4) continue; // Need at least 4 cells for remote pairs

                var candidates = kvp.Key;
                var cells = kvp.Value;

                // Try to build chains starting from each cell
                SingleStepSolution solution = FindRemotePairChain(sudokuPuzzle, cells, candidates.Item1, candidates.Item2);
                if (solution != null) return solution;
            }

            return null;
        }

        private SingleStepSolution FindRemotePairChain(SudokuPuzzle puzzle, List<Cell> bivValueCells, int candA, int candB)
        {
            // Build adjacency - cells that share a house
            var adjacency = new Dictionary<Cell, List<Cell>>();
            foreach (Cell cell in bivValueCells)
            {
                adjacency[cell] = bivValueCells
                    .Where(other => other != cell && SolverUtility.CellsSeeEachOther(cell, other))
                    .ToList();
            }

            // BFS to find all chains of length 4+
            foreach (Cell start in bivValueCells)
            {
                var visited = new HashSet<Cell> { start };
                var queue = new Queue<(Cell current, List<Cell> path)>();
                queue.Enqueue((start, new List<Cell> { start }));

                while (queue.Count > 0)
                {
                    var (current, path) = queue.Dequeue();

                    // Check if we have a chain of 4+ cells (even length means opposite parities at ends)
                    if (path.Count >= 4 && path.Count % 2 == 0)
                    {
                        // First and last cells have the same parity
                        // Find eliminations: cells that see BOTH endpoints
                        Cell first = path[0];
                        Cell last = path[path.Count - 1];

                        var eliminations = FindEliminations(puzzle, first, last, candA, candB, path);
                        if (eliminations.Count > 0)
                        {
                            return CreateSolution(candA, candB, path, eliminations);
                        }
                    }

                    // Continue building the chain
                    foreach (Cell neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            var newPath = new List<Cell>(path) { neighbor };
                            queue.Enqueue((neighbor, newPath));
                        }
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(SudokuPuzzle puzzle, Cell first, Cell last, int candA, int candB, List<Cell> chain)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            // Find cells that see both endpoints and have either candidate
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Cell cell = puzzle.Cells[row, col];
                    if (chain.Contains(cell)) continue;

                    bool seesFirst = SolverUtility.CellsSeeEachOther(cell, first);
                    bool seesLast = SolverUtility.CellsSeeEachOther(cell, last);

                    if (seesFirst && seesLast)
                    {
                        if (cell.CanBe.Contains(candA))
                            eliminations.Add(new SingleStepSolution.Candidate(row, col, candA));
                        if (cell.CanBe.Contains(candB))
                            eliminations.Add(new SingleStepSolution.Candidate(row, col, candB));
                    }
                }
            }

            return eliminations;
        }

        private SingleStepSolution CreateSolution(int candA, int candB, List<Cell> chain, List<SingleStepSolution.Candidate> eliminations)
        {
            SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
            solution.ContextData = new HintContextData();
            solution.ContextData.PrimaryCandidate = candA;
            solution.ContextData.FocusCandidates.Add(candA);
            solution.ContextData.FocusCandidates.Add(candB);

            // Alternate coloring for chain visualization
            for (int i = 0; i < chain.Count; i++)
            {
                Cell cell = chain[i];
                if (i % 2 == 0)
                    solution.ContextData.FocusCells.Add(new int[] { cell.RowIndex, cell.ColumnIndex });
                else
                    solution.ContextData.ReasoningCells.Add(new int[] { cell.RowIndex, cell.ColumnIndex });
            }

            return solution;
        }
    }
}
