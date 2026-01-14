using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: XY-Chain
    /// 
    /// Pattern: Chain through bivalue cells, alternating candidates
    /// {A,B} → {B,C} → {C,D} → {D,E}
    /// 
    /// Logic:
    /// - Each cell in chain is bivalue
    /// - Adjacent cells share exactly one candidate
    /// - Chain endpoints determine what can be eliminated
    /// - If endpoints share a candidate, cells seeing both can eliminate it
    /// 
    /// SE Rating: 6.5
    /// </summary>
    public class XYChain : ISudokuSolverStrategy
    {
        public string StrategyName => "XY-Chain";

        private const int MAX_CHAIN_LENGTH = 12;

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells
            List<Cell> bivalueCells = new List<Cell>();
            foreach (Row row in sudokuPuzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell.CanBe.Count == 2)
                    {
                        bivalueCells.Add(cell);
                    }
                }
            }

            if (bivalueCells.Count < 2) return null;

            // Build adjacency map: which bivalue cells can link to which
            Dictionary<Cell, List<(Cell neighbor, int sharedCand)>> adjacency = 
                new Dictionary<Cell, List<(Cell, int)>>();

            foreach (Cell cell in bivalueCells)
            {
                adjacency[cell] = new List<(Cell, int)>();
                List<int> cands = cell.CanBe.ToList();

                foreach (Cell other in bivalueCells)
                {
                    if (other == cell) continue;
                    if (!SolverUtility.CellsSeeEachOther(cell, other)) continue;

                    // Find shared candidate
                    List<int> otherCands = other.CanBe.ToList();
                    foreach (int c in cands)
                    {
                        if (otherCands.Contains(c))
                        {
                            adjacency[cell].Add((other, c));
                            break; // Only one shared candidate makes a valid link
                        }
                    }
                }
            }

            // DFS to find chains with eliminations
            foreach (Cell startCell in bivalueCells)
            {
                List<int> startCands = startCell.CanBe.ToList();

                // Try chains starting with each candidate
                foreach (int startCand in startCands)
                {
                    int otherCand = startCands.First(c => c != startCand);
                    
                    List<Cell> chain = new List<Cell> { startCell };
                    HashSet<Cell> visited = new HashSet<Cell> { startCell };

                    SingleStepSolution result = DFSFindChain(
                        sudokuPuzzle, adjacency, chain, visited, 
                        startCell, startCand, otherCand, bivalueCells);

                    if (result != null) return result;
                }
            }

            return null;
        }

        private SingleStepSolution DFSFindChain(
            SudokuPuzzle puzzle,
            Dictionary<Cell, List<(Cell, int)>> adjacency,
            List<Cell> chain,
            HashSet<Cell> visited,
            Cell currentCell,
            int startCand,  // The candidate at the START of the chain
            int currentCand, // The "active" candidate at current position
            List<Cell> allBivalueCells)
        {
            if (chain.Count >= MAX_CHAIN_LENGTH) return null;

            // The current cell's "exit" candidate is the one we're NOT using to link backward
            List<int> currCands = currentCell.CanBe.ToList();
            int exitCand = currCands.First(c => c != currentCand);

            // Check if we can make eliminations with current chain
            // Eliminations possible if exit candidate matches start candidate
            if (chain.Count >= 3 && exitCand == startCand)
            {
                Cell startCell = chain[0];
                Cell endCell = currentCell;

                List<SingleStepSolution.Candidate> eliminations = 
                    FindEliminations(puzzle, startCell, endCell, startCand);

                if (eliminations.Count > 0)
                {
                    SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                    solution.ContextData = new HintContextData();
                    solution.ContextData.PrimaryCandidate = startCand;

                    foreach (Cell c in chain)
                    {
                        solution.ContextData.FocusCells.Add(new int[] { c.RowIndex, c.ColumnIndex });
                    }

                    solution.ContextData.Notes = $"Chain length: {chain.Count}";

                    return solution;
                }
            }

            // Continue building chain
            if (adjacency.TryGetValue(currentCell, out List<(Cell, int)> neighbors))
            {
                foreach ((Cell neighbor, int sharedCand) in neighbors)
                {
                    if (visited.Contains(neighbor)) continue;
                    
                    // We link via the exit candidate
                    if (sharedCand != exitCand) continue;

                    chain.Add(neighbor);
                    visited.Add(neighbor);

                    // The neighbor's "entry" candidate is sharedCand
                    // So neighbor's active candidate (for next link) is its OTHER candidate
                    SingleStepSolution result = DFSFindChain(
                        puzzle, adjacency, chain, visited,
                        neighbor, startCand, sharedCand, allBivalueCells);

                    if (result != null) return result;

                    chain.RemoveAt(chain.Count - 1);
                    visited.Remove(neighbor);
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, Cell startCell, Cell endCell, int elimCandidate)
        {
            List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell == startCell || cell == endCell) continue;
                    if (!cell.CanBe.Contains(elimCandidate)) continue;

                    if (SolverUtility.CellsSeeEachOther(cell, startCell) &&
                        SolverUtility.CellsSeeEachOther(cell, endCell))
                    {
                        eliminations.Add(new SingleStepSolution.Candidate(
                            cell.RowIndex, cell.ColumnIndex, elimCandidate));
                    }
                }
            }

            return eliminations;
        }
    }
}
