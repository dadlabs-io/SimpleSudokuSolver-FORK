using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: AIC (Alternating Inference Chain)
    /// 
    /// General chain technique that alternates between strong and weak links.
    /// This is the fallback for chain patterns not caught by named techniques.
    /// 
    /// Strong Link: If one end is FALSE, the other MUST be TRUE (conjugate pair)
    /// Weak Link: If one end is TRUE, the other MUST be FALSE (same house visibility)
    /// 
    /// If a chain starts and ends on the same candidate with opposite polarity,
    /// cells seeing both endpoints can have that candidate eliminated.
    /// 
    /// SE Rating: 7.0+
    /// </summary>
    public class AIC : ISudokuSolverStrategy
    {
        public string StrategyName => "AIC";

        private const int MAX_CHAIN_LENGTH = 12;

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Build graph of all strong links for all candidates
            Dictionary<int, ColorGraph> graphs = new Dictionary<int, ColorGraph>();
            for (int digit = 1; digit <= 9; digit++)
            {
                graphs[digit] = ColorGraph.BuildForCandidate(sudokuPuzzle, digit);
            }

            // Try to find AIC starting from each cell-candidate pair
            foreach (Row row in sudokuPuzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    foreach (int candidate in cell.CanBe)
                    {
                        SingleStepSolution result = TryBuildAIC(sudokuPuzzle, graphs, cell, candidate);
                        if (result != null) return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Try to build an AIC starting from a given cell-candidate.
        /// Uses DFS alternating between strong and weak links.
        /// </summary>
        private SingleStepSolution TryBuildAIC(
            SudokuPuzzle puzzle,
            Dictionary<int, ColorGraph> graphs,
            Cell startCell,
            int startCandidate)
        {
            // Chain nodes: (cell, candidate, isStrong - whether link TO this node was strong)
            List<(Cell cell, int candidate, bool isStrongLinkIn)> chain = 
                new List<(Cell, int, bool)>();
            
            HashSet<(int row, int col, int cand)> visited = 
                new HashSet<(int, int, int)>();

            // Start with a strong link out (we'll treat the start as if we arrived via weak link)
            chain.Add((startCell, startCandidate, false));
            visited.Add((startCell.RowIndex, startCell.ColumnIndex, startCandidate));

            return DFSBuildAIC(puzzle, graphs, chain, visited, startCell, startCandidate, true);
        }

        private SingleStepSolution DFSBuildAIC(
            SudokuPuzzle puzzle,
            Dictionary<int, ColorGraph> graphs,
            List<(Cell cell, int candidate, bool isStrongLinkIn)> chain,
            HashSet<(int row, int col, int cand)> visited,
            Cell currentCell,
            int currentCandidate,
            bool needStrongLink) // Whether next link must be strong
        {
            if (chain.Count >= MAX_CHAIN_LENGTH) return null;

            // Check for closure - can we link back to start with eliminations?
            if (chain.Count >= 4)
            {
                Cell startCell = chain[0].cell;
                int startCand = chain[0].candidate;

                // For elimination: we need the chain to create a "pincer" effect
                // If endpoints both have the same candidate and chain has odd number of strong links,
                // cells seeing both endpoints can eliminate that candidate

                if (currentCandidate == startCand)
                {
                    List<SingleStepSolution.Candidate> eliminations = 
                        FindEliminations(puzzle, startCell, currentCell, startCand);

                    if (eliminations.Count > 0)
                    {
                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                        solution.ContextData = new HintContextData();
                        solution.ContextData.PrimaryCandidate = startCand;

                        foreach ((Cell c, int cand, bool _) in chain)
                        {
                            solution.ContextData.FocusCells.Add(new int[] { c.RowIndex, c.ColumnIndex });
                        }

                        solution.ContextData.Notes = $"AIC length: {chain.Count}";

                        return solution;
                    }
                }
            }

            // Extend chain
            if (needStrongLink)
            {
                // Find strong links from current cell-candidate
                List<Cell> strongNeighbors = FindStrongLinkTargets(puzzle, currentCell, currentCandidate);

                foreach (Cell neighbor in strongNeighbors)
                {
                    var key = (neighbor.RowIndex, neighbor.ColumnIndex, currentCandidate);
                    if (visited.Contains(key)) continue;

                    chain.Add((neighbor, currentCandidate, true));
                    visited.Add(key);

                    // After strong link, next must be weak (can be same cell different candidate,
                    // or different cell same candidate via visibility)
                    SingleStepSolution result = DFSBuildAIC(
                        puzzle, graphs, chain, visited, neighbor, currentCandidate, false);

                    if (result != null) return result;

                    chain.RemoveAt(chain.Count - 1);
                    visited.Remove(key);
                }

                // Also try: within same cell, switch to different candidate (bivalue link)
                if (currentCell.CanBe.Count == 2)
                {
                    int otherCand = currentCell.CanBe.First(c => c != currentCandidate);
                    var key = (currentCell.RowIndex, currentCell.ColumnIndex, otherCand);
                    if (!visited.Contains(key))
                    {
                        chain.Add((currentCell, otherCand, true)); // Bivalue is considered strong
                        visited.Add(key);

                        SingleStepSolution result = DFSBuildAIC(
                            puzzle, graphs, chain, visited, currentCell, otherCand, false);

                        if (result != null) return result;

                        chain.RemoveAt(chain.Count - 1);
                        visited.Remove(key);
                    }
                }
            }
            else
            {
                // Find weak links - same candidate in visible cells
                List<Cell> visibleCells = FindWeakLinkTargets(puzzle, currentCell, currentCandidate);

                foreach (Cell neighbor in visibleCells)
                {
                    var key = (neighbor.RowIndex, neighbor.ColumnIndex, currentCandidate);
                    if (visited.Contains(key)) continue;

                    chain.Add((neighbor, currentCandidate, false));
                    visited.Add(key);

                    // After weak link, next must be strong
                    SingleStepSolution result = DFSBuildAIC(
                        puzzle, graphs, chain, visited, neighbor, currentCandidate, true);

                    if (result != null) return result;

                    chain.RemoveAt(chain.Count - 1);
                    visited.Remove(key);
                }

                // Also: within same cell, switch to different candidate (weak within cell for non-bivalue)
                foreach (int otherCand in currentCell.CanBe)
                {
                    if (otherCand == currentCandidate) continue;
                    
                    var key = (currentCell.RowIndex, currentCell.ColumnIndex, otherCand);
                    if (visited.Contains(key)) continue;

                    chain.Add((currentCell, otherCand, false));
                    visited.Add(key);

                    SingleStepSolution result = DFSBuildAIC(
                        puzzle, graphs, chain, visited, currentCell, otherCand, true);

                    if (result != null) return result;

                    chain.RemoveAt(chain.Count - 1);
                    visited.Remove(key);
                }
            }

            return null;
        }

        private List<Cell> FindStrongLinkTargets(SudokuPuzzle puzzle, Cell fromCell, int candidate)
        {
            List<Cell> targets = new List<Cell>();

            // Row conjugate
            List<Cell> rowCells = puzzle.Rows[fromCell.RowIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (rowCells.Count == 2)
            {
                Cell other = rowCells.First(c => c != fromCell);
                if (!targets.Contains(other)) targets.Add(other);
            }

            // Column conjugate
            List<Cell> colCells = puzzle.Columns[fromCell.ColumnIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (colCells.Count == 2)
            {
                Cell other = colCells.First(c => c != fromCell);
                if (!targets.Contains(other)) targets.Add(other);
            }

            // Box conjugate
            int boxIndex = SolverUtility.GetBoxIndex(fromCell);
            List<Cell> boxCells = SolverUtility.GetBoxCells(puzzle, boxIndex)
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (boxCells.Count == 2)
            {
                Cell other = boxCells.First(c => c != fromCell);
                if (!targets.Contains(other)) targets.Add(other);
            }

            return targets;
        }

        private List<Cell> FindWeakLinkTargets(SudokuPuzzle puzzle, Cell fromCell, int candidate)
        {
            List<Cell> targets = new List<Cell>();

            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell == fromCell) continue;
                    if (!cell.CanBe.Contains(candidate)) continue;
                    if (!SolverUtility.CellsSeeEachOther(cell, fromCell)) continue;

                    targets.Add(cell);
                }
            }

            return targets;
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
