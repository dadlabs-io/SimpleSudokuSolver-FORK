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

                    // Find shared candidates - valid link requires EXACTLY ONE shared
                    List<int> otherCands = other.CanBe.ToList();
                    List<int> sharedCands = cands.Where(c => otherCands.Contains(c)).ToList();

                    // Reject if cells have identical candidates (both shared) or no shared
                    if (sharedCands.Count != 1) continue;

                    int sharedCand = sharedCands[0];
                    adjacency[cell].Add((other, sharedCand));
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

                    // startCand is our entry candidate, so we pass it as currentCand
                    // The DFS will compute exitCand = other candidate = the linking candidate
                    SingleStepSolution result = DFSFindChain(
                        sudokuPuzzle, adjacency, chain, visited,
                        startCell, startCand, startCand, bivalueCells);

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

                // Standard XY-Chain: eliminate from cells seeing both endpoints
                // (Nice Loop logic was too complex and error-prone, disabled)
                List<SingleStepSolution.Candidate> eliminations = FindEliminations(puzzle, chain, startCand);

                if (eliminations.Count > 0)
                {
                    // DEBUG: Log the complete XY-Chain pattern for verification
                    if (Model.SudokuPuzzle.EnableVerboseFileLogging)
                    {
                        string logPath = @"c:\github.com\sudoku-app\memory-bank\short-term\logs\xychain-debug.log";
                        var logLines = new List<string>();
                        logLines.Add($"[{System.DateTime.Now:HH:mm:ss}] [XY-Chain] Found pattern:");
                        logLines.Add($"  Start candidate: {startCand}");
                        logLines.Add($"  Chain length: {chain.Count}");
                        logLines.Add($"  Chain:");
                        for (int i = 0; i < chain.Count; i++)
                        {
                            var c = chain[i];
                            logLines.Add($"    [{i}] R{c.RowIndex + 1}C{c.ColumnIndex + 1} candidates=[{string.Join(",", c.CanBe)}]");
                        }
                        logLines.Add($"  Conclusion: Standard XY-Chain - eliminate {startCand} from cells seeing both endpoints");
                        foreach (var elim in eliminations)
                        {
                            logLines.Add($"  ELIMINATION: {elim.Value} from R{elim.IndexOfRow + 1}C{elim.IndexOfColumn + 1}");
                        }

                        // Dump full board state
                        logLines.Add($"[XY-Chain] BOARD STATE:");
                        for (int row = 0; row < 9; row++)
                        {
                            var rowCells = puzzle.Rows[row].Cells;
                            var line = $"  R{row + 1}: ";
                            for (int col = 0; col < 9; col++)
                            {
                                var cell = rowCells[col];
                                if (cell.HasValue)
                                    line += $"[{cell.Value}] ";
                                else
                                    line += $"({string.Join("", cell.CanBe)}) ";
                            }
                            logLines.Add(line);
                        }
                        logLines.Add("");
                        System.IO.File.AppendAllLines(logPath, logLines);
                    }

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

                    // CRITICAL: We can only link via the exit candidate
                    // The current cell "exits" on exitCand, so neighbor must share exitCand
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
            SudokuPuzzle puzzle, List<Cell> chain, int elimCandidate)
        {
            List<SingleStepSolution.Candidate> eliminations = new List<SingleStepSolution.Candidate>();

            // Get start and end cells for visibility checks
            Cell startCell = chain[0];
            Cell endCell = chain[chain.Count - 1];

            // Create HashSet for O(1) chain membership lookup
            HashSet<Cell> chainCells = new HashSet<Cell>(chain);

            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    // Skip ANY cell that is part of the chain (not just start/end)
                    if (chainCells.Contains(cell)) continue;
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

        /// <summary>
        /// Validates the reverse direction of the chain.
        /// If end=startCand, trace backwards through the chain.
        /// Returns true if this leads to start=startCand (consistent, standard XY-Chain).
        /// Returns false if it leads to start≠startCand (contradiction, Nice Loop).
        /// </summary>
        private bool ValidateReverseChain(List<Cell> chain, int startCand)
        {
            // Trace backwards: assume end cell = startCand
            // For each step, entering on one candidate means exiting on the other
            // The weak link between cells means if one cell IS candidate X, neighboring cell is NOT X

            // Starting from end cell, we're "entering" on startCand (assumption)
            int currentCand = startCand;

            // Go backwards through the chain
            for (int i = chain.Count - 1; i > 0; i--)
            {
                Cell currentCell = chain[i];
                Cell prevCell = chain[i - 1];

                // Current cell is "active" on currentCand (either true or we're assuming it)
                // We need to find what candidate links to prevCell

                List<int> currCands = currentCell.CanBe.ToList();
                int otherCandInCell = currCands.First(c => c != currentCand);

                // The link to prevCell is via some shared candidate
                // In reverse, if currentCell = currentCand, then via weak link on otherCandInCell,
                // prevCell ≠ otherCandInCell. Since prevCell is bivalue, prevCell = the other candidate.

                // Find the shared candidate between currentCell and prevCell
                List<int> prevCands = prevCell.CanBe.ToList();
                List<int> shared = currCands.Where(c => prevCands.Contains(c)).ToList();

                if (shared.Count != 1)
                {
                    // This shouldn't happen if chain was built correctly
                    return false;
                }

                int sharedCand = shared[0];

                // The chain link is on sharedCand
                // XY-Chain: we ENTER on currentCand, EXIT on otherCandInCell
                // The link to prevCell should be on the EXIT candidate (otherCandInCell)

                if (sharedCand == otherCandInCell)
                {
                    // Link is on the exit candidate - this is correct
                    // Weak link: currentCell exits otherCandInCell → prevCell ≠ otherCandInCell
                    // prevCell is bivalue, so prevCell = its OTHER candidate
                    int prevOther = prevCands.First(c => c != sharedCand);
                    currentCand = prevOther;
                }
                else
                {
                    // Link is on the entry candidate (currentCand), not the exit
                    // This means the chain was built differently - can't validate in reverse
                    return false;
                }
            }

            // After tracing backwards, currentCand is what the start cell should be
            // If currentCand == startCand, reverse is consistent with forward
            // If currentCand != startCand, reverse contradicts forward (Nice Loop)
            return currentCand == startCand;
        }
    }
}
