using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: M-Wing (Medusa Wing)
    /// 
    /// Pattern: (x=y)-y=(y-x)=x
    /// - A bivalue cell {X, Y}
    /// - A strong link on Y connecting to another cell
    /// - That cell sees a third cell with a strong link on X
    /// 
    /// Chain structure:
    /// {X,Y} ─── Y ═══ Y ─── {Y,X} ═══ X
    /// 
    /// Logic:
    /// - Either the bivalue cell is X or Y
    /// - If it's Y, then the strong link forces X elsewhere
    /// - If it's X, then X is true here
    /// - Either way, cells seeing both endpoints cannot have X
    /// 
    /// SE Rating: 4.5
    /// </summary>
    public class MWing : ISudokuSolverStrategy
    {
        public string StrategyName => "M-Wing";

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

            // For each bivalue cell, try to build M-Wing pattern
            foreach (Cell pivotCell in bivalueCells)
            {
                List<int> candidates = pivotCell.CanBe.ToList();
                int candX = candidates[0];
                int candY = candidates[1];

                // Try both orientations: X as elimination candidate, Y as link candidate, and vice versa
                SingleStepSolution result = TryMWingPattern(sudokuPuzzle, pivotCell, candX, candY);
                if (result != null) return result;

                result = TryMWingPattern(sudokuPuzzle, pivotCell, candY, candX);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Try to find M-Wing pattern with given elimination candidate (elimCand) and link candidate (linkCand).
        /// Pattern: {elimCand,linkCand} ─── linkCand ═══ linkCand ─── {linkCand,elimCand} ═══ elimCand
        /// </summary>
        private SingleStepSolution TryMWingPattern(SudokuPuzzle puzzle, Cell pivotCell, int elimCand, int linkCand)
        {
            // Find cells that see the pivot and have linkCand (potential weak link targets)
            List<Cell> linkTargets = FindCellsWithCandidate(puzzle, linkCand)
                .Where(c => c != pivotCell && SolverUtility.CellsSeeEachOther(c, pivotCell))
                .ToList();

            foreach (Cell linkCell1 in linkTargets)
            {
                // Look for a strong link on linkCand from linkCell1
                List<(Cell, int)> strongLinks = FindStrongLinksFromCell(puzzle, linkCell1, linkCand);

                foreach ((Cell linkCell2, int houseIndex) in strongLinks)
                {
                    // linkCell2 should see another bivalue cell with {linkCand, elimCand}
                    // that is connected to the original pivot via elimCand strong link
                    
                    // Find cells with elimCand that see linkCell2 and have a strong link back
                    List<Cell> elimTargets = FindCellsWithCandidate(puzzle, elimCand)
                        .Where(c => c != pivotCell && c != linkCell1 && c != linkCell2)
                        .Where(c => SolverUtility.CellsSeeEachOther(c, linkCell2))
                        .ToList();

                    foreach (Cell endCell in elimTargets)
                    {
                        // Check if there's a strong link on elimCand from endCell back to a cell that sees pivot
                        // OR if endCell is a bivalue {linkCand, elimCand} that completes the pattern
                        if (endCell.CanBe.Count == 2 && 
                            endCell.CanBe.Contains(linkCand) && 
                            endCell.CanBe.Contains(elimCand))
                        {
                            // This is a valid M-Wing pattern!
                            // Chain: pivotCell{X,Y} -Y- linkCell1 =Y= linkCell2 -Y- endCell{Y,X}
                            // Eliminate X from cells seeing both pivotCell and endCell
                            
                            List<SingleStepSolution.Candidate> eliminations = FindEliminations(puzzle, pivotCell, endCell, elimCand);
                            
                            if (eliminations.Count > 0)
                            {
                                SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                                
                                solution.ContextData = new HintContextData();
                                solution.ContextData.PrimaryCandidate = elimCand;
                                
                                // FocusCells: chain cells
                                solution.ContextData.FocusCells.Add(new int[] { pivotCell.RowIndex, pivotCell.ColumnIndex });
                                solution.ContextData.FocusCells.Add(new int[] { linkCell1.RowIndex, linkCell1.ColumnIndex });
                                solution.ContextData.FocusCells.Add(new int[] { linkCell2.RowIndex, linkCell2.ColumnIndex });
                                solution.ContextData.FocusCells.Add(new int[] { endCell.RowIndex, endCell.ColumnIndex });
                                
                                // FocusCandidates: both candidates involved
                                solution.ContextData.FocusCandidates = new List<int> { elimCand, linkCand };
                                
                                // ReasoningCandidates: [linkCand, elimCand]
                                solution.ContextData.ReasoningCandidates = new List<int> { linkCand, elimCand };
                                
                                return solution;
                            }
                        }
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

        /// <summary>
        /// Find strong links (conjugate pairs) from a given cell on a given candidate.
        /// Returns list of (other cell in strong link, house index).
        /// </summary>
        private List<(Cell, int)> FindStrongLinksFromCell(SudokuPuzzle puzzle, Cell fromCell, int candidate)
        {
            List<(Cell, int)> strongLinks = new List<(Cell, int)>();

            // Check row
            List<Cell> rowCells = puzzle.Rows[fromCell.RowIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (rowCells.Count == 2)
            {
                Cell other = rowCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.RowIndex));
            }

            // Check column
            List<Cell> colCells = puzzle.Columns[fromCell.ColumnIndex].Cells
                .Where(c => c.CanBe.Contains(candidate))
                .ToList();
            if (colCells.Count == 2)
            {
                Cell other = colCells.First(c => c != fromCell);
                strongLinks.Add((other, fromCell.ColumnIndex + 9));
            }

            // Check box
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
