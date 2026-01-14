using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: L-Wing (Local Wing)
    /// 
    /// Pattern: x=(x-y)=(y-z)=z
    /// - Three different candidates involved
    /// - Forms an L-shaped chain pattern
    /// 
    /// Chain structure:
    /// {X,Y} ─── Y ═══ Y ─── {Y,Z} ─── Z ═══ Z
    /// 
    /// Logic:
    /// - If first bivalue is X, chain is satisfied
    /// - If first bivalue is Y, it forces through the chain
    /// - Either way, eliminations at endpoints are valid
    /// 
    /// SE Rating: 4.5
    /// </summary>
    public class LWing : ISudokuSolverStrategy
    {
        public string StrategyName => "L-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find all bivalue cells - these are potential chain starting points
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

            // For each pair of bivalue cells that share one candidate, try to build L-Wing
            for (int i = 0; i < bivalueCells.Count; i++)
            {
                for (int j = i + 1; j < bivalueCells.Count; j++)
                {
                    Cell cell1 = bivalueCells[i];
                    Cell cell2 = bivalueCells[j];

                    // Find shared candidate
                    int sharedCand = -1;
                    int cand1Only = -1;
                    int cand2Only = -1;

                    List<int> cands1 = cell1.CanBe.ToList();
                    List<int> cands2 = cell2.CanBe.ToList();

                    foreach (int c in cands1)
                    {
                        if (cands2.Contains(c))
                        {
                            sharedCand = c;
                        }
                        else
                        {
                            cand1Only = c;
                        }
                    }
                    foreach (int c in cands2)
                    {
                        if (!cands1.Contains(c))
                        {
                            cand2Only = c;
                        }
                    }

                    // L-Wing needs exactly one shared candidate and two different unique candidates
                    if (sharedCand < 0 || cand1Only < 0 || cand2Only < 0) continue;
                    if (cand1Only == cand2Only) continue; // This would be W-Wing, not L-Wing

                    // Try to connect them through strong links
                    SingleStepSolution result = TryLWingPattern(sudokuPuzzle, cell1, cell2, sharedCand, cand1Only, cand2Only);
                    if (result != null) return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to connect two bivalue cells through the shared candidate's strong links.
        /// cell1 has {sharedCand, cand1Only}, cell2 has {sharedCand, cand2Only}
        /// </summary>
        private SingleStepSolution TryLWingPattern(SudokuPuzzle puzzle, Cell cell1, Cell cell2,
            int sharedCand, int cand1Only, int cand2Only)
        {
            // Find cells with sharedCand that see cell1
            List<Cell> linkTargets1 = FindCellsWithCandidate(puzzle, sharedCand)
                .Where(c => c != cell1 && c != cell2 && SolverUtility.CellsSeeEachOther(c, cell1))
                .ToList();

            foreach (Cell linkCell1 in linkTargets1)
            {
                // Find strong link on sharedCand from linkCell1
                List<(Cell, int)> strongLinks = FindStrongLinksFromCell(puzzle, linkCell1, sharedCand);

                foreach ((Cell linkCell2, int houseIndex) in strongLinks)
                {
                    // Check if linkCell2 sees cell2
                    if (linkCell2 == cell1 || linkCell2 == cell2) continue;
                    if (!SolverUtility.CellsSeeEachOther(linkCell2, cell2)) continue;

                    // Found L-Wing pattern!
                    // Eliminate cand1Only from cells seeing both cell1 and... 
                    // Actually L-Wing eliminates the unique candidates based on chain logic

                    // The pattern creates: if cell1=cand1Only, fine
                    // If cell1=sharedCand, then linkCell1!=sharedCand (weak), 
                    // so linkCell2=sharedCand (strong), so cell2!=sharedCand, so cell2=cand2Only

                    // So either cell1=cand1Only OR cell2=cand2Only (or both)
                    // This means we can eliminate cand1Only from cells seeing both endpoints
                    // where the endpoint for cand1Only is cell1

                    // Try eliminations
                    List<SingleStepSolution.Candidate> eliminations =
                        FindEliminations(puzzle, cell1, cell2, cand1Only);

                    // Also try cand2Only
                    if (eliminations.Count == 0)
                    {
                        eliminations = FindEliminations(puzzle, cell1, cell2, cand2Only);
                    }

                    if (eliminations.Count > 0)
                    {
                        int elimCand = eliminations[0].Value;
                        SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                        solution.ContextData = new HintContextData();
                        solution.ContextData.PrimaryCandidate = elimCand;

                        solution.ContextData.FocusCells.Add(new int[] { cell1.RowIndex, cell1.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { linkCell1.RowIndex, linkCell1.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { linkCell2.RowIndex, linkCell2.ColumnIndex });
                        solution.ContextData.FocusCells.Add(new int[] { cell2.RowIndex, cell2.ColumnIndex });

                        solution.ContextData.FocusCandidates = new List<int> { sharedCand, cand1Only, cand2Only };
                        solution.ContextData.ReasoningCandidates = new List<int> { sharedCand };

                        return solution;
                    }
                }
            }

            return null;
        }

        private List<Cell> FindCellsWithCandidate(SudokuPuzzle puzzle, int candidate)
        {
            List<Cell> cells = new List<Cell>();
            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell.CanBe.Contains(candidate))
                    {
                        cells.Add(cell);
                    }
                }
            }
            return cells;
        }

        private List<(Cell, int)> FindStrongLinksFromCell(SudokuPuzzle puzzle, Cell fromCell, int candidate)
        {
            List<(Cell, int)> strongLinks = new List<(Cell, int)>();

            List<Cell> rowCells = puzzle.Rows[fromCell.RowIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (rowCells.Count == 2)
            {
                Cell other = rowCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.RowIndex));
            }

            List<Cell> colCells = puzzle.Columns[fromCell.ColumnIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (colCells.Count == 2)
            {
                Cell other = colCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.ColumnIndex + 9));
            }

            int boxIndex = SolverUtility.GetBoxIndex(fromCell);
            List<Cell> boxCells = SolverUtility.GetBoxCells(puzzle, boxIndex)
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (boxCells.Count == 2)
            {
                Cell other = boxCells.First(c => c != fromCell);
                strongLinks.Add((other, boxIndex + 18));
            }

            return strongLinks;
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
