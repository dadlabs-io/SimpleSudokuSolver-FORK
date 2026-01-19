using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Digit Forcing Chain
    /// 
    /// Explores both possibilities of a bivalue cell:
    /// - If candidate A is true → trace implications → reach conclusion C
    /// - If candidate B is true → trace implications → reach conclusion C
    /// - If both paths reach same conclusion C → C must be true
    /// 
    /// Conclusions can be:
    /// - Placement: "Cell X must be 5" (both paths place candidate in same cell)
    /// - Elimination: "Cell X can't be 5" (both paths eliminate same candidate)
    /// 
    /// SE Rating: 7.5
    /// </summary>
    public class DigitForcingChain : ISudokuSolverStrategy
    {
        public string StrategyName => "Digit Forcing Chain";

        private const int MaxBranchDepth = 12;

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells (cells with exactly 2 candidates)
            List<Cell> bivalueCells = new List<Cell>();
            foreach (Row row in sudokuPuzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (!cell.HasValue && cell.CanBe.Count == 2)
                    {
                        bivalueCells.Add(cell);
                    }
                }
            }

            // Try each bivalue cell as the forcing cell
            foreach (Cell forcingCell in bivalueCells)
            {
                SingleStepSolution result = TryForcingChain(sudokuPuzzle, forcingCell);
                if (result != null) return result;
            }

            return null;
        }

        private SingleStepSolution TryForcingChain(SudokuPuzzle puzzle, Cell forcingCell)
        {
            List<int> candidates = forcingCell.CanBe.ToList();
            int candA = candidates[0];
            int candB = candidates[1];

            // Trace implications for branch A (assume candA is true)
            List<Implication> implA = TraceImplications(puzzle, forcingCell, candA);

            // Trace implications for branch B (assume candB is true)
            List<Implication> implB = TraceImplications(puzzle, forcingCell, candB);

            // Find common conclusions - placements
            foreach (Implication iA in implA.Where(i => i.IsPlacement))
            {
                foreach (Implication iB in implB.Where(i => i.IsPlacement))
                {
                    if (iA.Row == iB.Row && iA.Col == iB.Col && iA.Candidate == iB.Candidate)
                    {
                        // Both paths place same candidate in same cell!
                        // This is a placement conclusion (not yet handled by our solution type)
                        // For now, log and skip - we focus on eliminations
                    }
                }
            }

            // Find common conclusions - eliminations
            foreach (Implication iA in implA.Where(i => !i.IsPlacement))
            {
                foreach (Implication iB in implB.Where(i => !i.IsPlacement))
                {
                    if (iA.Row == iB.Row && iA.Col == iB.Col && iA.Candidate == iB.Candidate)
                    {
                        // Both paths eliminate same candidate from same cell!
                        Cell targetCell = puzzle.Cells[iA.Row, iA.Col];
                        if (!targetCell.HasValue && targetCell.CanBe.Contains(iA.Candidate))
                        {
                            // Skip if it's the forcing cell itself
                            if (iA.Row == forcingCell.RowIndex && iA.Col == forcingCell.ColumnIndex)
                                continue;

                            SingleStepSolution solution = new SingleStepSolution(
                                new[] { new SingleStepSolution.Candidate(iA.Row, iA.Col, iA.Candidate) },
                                StrategyName);

                            solution.ContextData = new HintContextData
                            {
                                FocusCells = new List<int[]> { new[] { forcingCell.RowIndex, forcingCell.ColumnIndex } },
                                BranchACells = implA.Select(i => new int[] { i.Row, i.Col }).ToList(),
                                BranchBCells = implB.Select(i => new int[] { i.Row, i.Col }).ToList(),
                                // Full chain data: [row, col, candidate, isPlacement (1=placed, 0=eliminated)]
                                BranchAChain = implA.Select(i => new int[] { i.Row, i.Col, i.Candidate, i.IsPlacement ? 1 : 0 }).ToList(),
                                BranchBChain = implB.Select(i => new int[] { i.Row, i.Col, i.Candidate, i.IsPlacement ? 1 : 0 }).ToList(),
                                PrimaryCandidate = iA.Candidate,
                                ReasoningCandidates = new List<int> { candA, candB },
                                Notes = $"Both paths eliminate {iA.Candidate} from R{iA.Row + 1}C{iA.Col + 1}"
                            };

                            // DEBUG: Log forcing cell details
                            string logPath = @"C:\github.com\sudoku-app\memory-bank\short-term\logs\dfc_debug.log";
                            var logLines = new[]
                            {
                                $"[{System.DateTime.Now:HH:mm:ss}] DigitForcingChain Found:",
                                $"  ForcingCell: R{forcingCell.RowIndex + 1}C{forcingCell.ColumnIndex + 1} (0-based: {forcingCell.RowIndex},{forcingCell.ColumnIndex})",
                                $"  Candidates: [{candA}, {candB}]",
                                $"  Elimination: R{iA.Row + 1}C{iA.Col + 1} = {iA.Candidate}",
                                ""
                            };
                            System.IO.File.AppendAllLines(logPath, logLines);

                            return solution;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Trace implications starting from assuming a candidate is true in a cell.
        /// Uses simple implication following (not full AIC).
        /// </summary>
        private List<Implication> TraceImplications(SudokuPuzzle puzzle, Cell startCell, int assumedTrue)
        {
            List<Implication> implications = new List<Implication>();
            HashSet<(int row, int col, int cand, bool isPlacement)> visited =
                new HashSet<(int, int, int, bool)>();

            Queue<Implication> queue = new Queue<Implication>();

            // Starting assumption: this candidate is TRUE in this cell
            Implication start = new Implication(startCell.RowIndex, startCell.ColumnIndex, assumedTrue, true);
            queue.Enqueue(start);
            visited.Add((start.Row, start.Col, start.Candidate, start.IsPlacement));

            while (queue.Count > 0 && implications.Count < MaxBranchDepth * 2)
            {
                Implication current = queue.Dequeue();
                implications.Add(current);

                if (current.IsPlacement)
                {
                    // If candidate C is TRUE in cell X:
                    // 1. All other candidates in X are FALSE (eliminated)
                    // 2. Candidate C is FALSE in all cells that see X

                    Cell cell = puzzle.Cells[current.Row, current.Col];

                    // Other candidates in same cell are eliminated
                    foreach (int otherCand in cell.CanBe)
                    {
                        if (otherCand == current.Candidate) continue;
                        var key = (current.Row, current.Col, otherCand, false);
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            queue.Enqueue(new Implication(current.Row, current.Col, otherCand, false));
                        }
                    }

                    // Same candidate in visible cells is eliminated
                    foreach (Cell peer in GetVisibleCells(puzzle, cell))
                    {
                        if (!peer.CanBe.Contains(current.Candidate)) continue;
                        var key = (peer.RowIndex, peer.ColumnIndex, current.Candidate, false);
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            queue.Enqueue(new Implication(peer.RowIndex, peer.ColumnIndex, current.Candidate, false));
                        }
                    }
                }
                else
                {
                    // If candidate C is FALSE in cell X:
                    // 1. If X had only 2 candidates, the other must be TRUE
                    // 2. If C only had 2 positions in a house, the other position is TRUE

                    Cell cell = puzzle.Cells[current.Row, current.Col];

                    // Naked single implication
                    List<int> remaining = cell.CanBe.Where(c =>
                        !visited.Contains((current.Row, current.Col, c, false)) &&
                        c != current.Candidate).ToList();

                    if (remaining.Count == 1)
                    {
                        var key = (current.Row, current.Col, remaining[0], true);
                        if (!visited.Contains(key))
                        {
                            visited.Add(key);
                            queue.Enqueue(new Implication(current.Row, current.Col, remaining[0], true));
                        }
                    }

                    // Hidden single implication (check each house)
                    CheckHiddenSingleImplication(puzzle, cell, current.Candidate, visited, queue,
                        puzzle.Rows[current.Row].Cells);
                    CheckHiddenSingleImplication(puzzle, cell, current.Candidate, visited, queue,
                        puzzle.Columns[current.Col].Cells);

                    int boxIdx = SolverUtility.GetBoxIndex(cell);
                    CheckHiddenSingleImplication(puzzle, cell, current.Candidate, visited, queue,
                        SolverUtility.GetBoxCells(puzzle, boxIdx));
                }
            }

            return implications;
        }

        private void CheckHiddenSingleImplication(
            SudokuPuzzle puzzle, Cell eliminatedCell, int candidate,
            HashSet<(int, int, int, bool)> visited, Queue<Implication> queue,
            IEnumerable<Cell> houseCells)
        {
            // Find cells in the house that can have this candidate (excluding eliminated ones)
            List<Cell> remaining = houseCells
                .Where(c => c != eliminatedCell && c.CanBe.Contains(candidate))
                .Where(c => !visited.Contains((c.RowIndex, c.ColumnIndex, candidate, false)))
                .ToList();

            if (remaining.Count == 1)
            {
                Cell onlyCell = remaining[0];
                var key = (onlyCell.RowIndex, onlyCell.ColumnIndex, candidate, true);
                if (!visited.Contains(key))
                {
                    visited.Add(key);
                    queue.Enqueue(new Implication(onlyCell.RowIndex, onlyCell.ColumnIndex, candidate, true));
                }
            }
        }

        private List<Cell> GetVisibleCells(SudokuPuzzle puzzle, Cell cell)
        {
            HashSet<Cell> visible = new HashSet<Cell>();

            // Row
            foreach (Cell c in puzzle.Rows[cell.RowIndex].Cells)
            {
                if (c != cell && !c.HasValue) visible.Add(c);
            }

            // Column
            foreach (Cell c in puzzle.Columns[cell.ColumnIndex].Cells)
            {
                if (c != cell && !c.HasValue) visible.Add(c);
            }

            // Box
            int boxIdx = SolverUtility.GetBoxIndex(cell);
            foreach (Cell c in SolverUtility.GetBoxCells(puzzle, boxIdx))
            {
                if (c != cell && !c.HasValue) visible.Add(c);
            }

            return visible.ToList();
        }

        private class Implication
        {
            public int Row { get; }
            public int Col { get; }
            public int Candidate { get; }
            public bool IsPlacement { get; } // True = candidate is TRUE, False = candidate is FALSE

            public Implication(int row, int col, int candidate, bool isPlacement)
            {
                Row = row;
                Col = col;
                Candidate = candidate;
                IsPlacement = isPlacement;
            }
        }
    }
}
