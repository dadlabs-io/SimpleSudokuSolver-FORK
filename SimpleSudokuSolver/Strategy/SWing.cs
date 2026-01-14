using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: S-Wing (Split Wing)
    /// 
    /// Pattern: x=x-(x=y)-y=y
    /// - Two bivalue cells {X, Y} at endpoints
    /// - Connected through strong links on both X and Y
    /// 
    /// Chain structure:
    /// X ═══ X ─── {X,Y} ─── Y ═══ Y
    /// 
    /// Logic:
    /// - Symmetric pattern where endpoints both contain X and Y
    /// - If left X is true, right Y is false, so right X is true
    /// - If left X is false, chain forces consistent state
    /// - Cells seeing both endpoints can have eliminations
    /// 
    /// SE Rating: 4.5
    /// </summary>
    public class SWing : ISudokuSolverStrategy
    {
        public string StrategyName => "S-Wing";

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

            // For each bivalue cell as the pivot, try to build S-Wing
            foreach (Cell pivotCell in bivalueCells)
            {
                List<int> candidates = pivotCell.CanBe.ToList();
                int candX = candidates[0];
                int candY = candidates[1];

                SingleStepSolution result = TrySWingPattern(sudokuPuzzle, pivotCell, candX, candY);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Pattern: startCell(X) ═══ linkCell1(X) ─── pivotCell{X,Y} ─── linkCell2(Y) ═══ endCell(Y)
        /// </summary>
        private SingleStepSolution TrySWingPattern(SudokuPuzzle puzzle, Cell pivotCell, int candX, int candY)
        {
            // Find cells with candX that see pivot and have a strong link on X
            List<Cell> xCellsSeeingPivot = FindCellsWithCandidate(puzzle, candX)
                .Where(c => c != pivotCell && SolverUtility.CellsSeeEachOther(c, pivotCell))
                .ToList();

            // Find cells with candY that see pivot and have a strong link on Y
            List<Cell> yCellsSeeingPivot = FindCellsWithCandidate(puzzle, candY)
                .Where(c => c != pivotCell && SolverUtility.CellsSeeEachOther(c, pivotCell))
                .ToList();

            foreach (Cell linkCell1 in xCellsSeeingPivot)
            {
                // Find strong link on X from linkCell1
                List<(Cell, int)> xStrongLinks = FindStrongLinksFromCell(puzzle, linkCell1, candX);

                foreach ((Cell startCell, int houseIdx1) in xStrongLinks)
                {
                    if (startCell == pivotCell) continue;

                    foreach (Cell linkCell2 in yCellsSeeingPivot)
                    {
                        if (linkCell2 == linkCell1) continue;

                        // Find strong link on Y from linkCell2
                        List<(Cell, int)> yStrongLinks = FindStrongLinksFromCell(puzzle, linkCell2, candY);

                        foreach ((Cell endCell, int houseIdx2) in yStrongLinks)
                        {
                            if (endCell == pivotCell || endCell == linkCell1 || endCell == startCell) continue;

                            // Found S-Wing! Eliminate from cells seeing both startCell and endCell
                            // The elimination candidate depends on what startCell and endCell share
                            int elimCand = -1;
                            if (startCell.CanBe.Contains(candY) && endCell.CanBe.Contains(candY))
                            {
                                elimCand = candY;
                            }
                            else if (startCell.CanBe.Contains(candX) && endCell.CanBe.Contains(candX))
                            {
                                elimCand = candX;
                            }

                            if (elimCand > 0)
                            {
                                List<SingleStepSolution.Candidate> eliminations = 
                                    FindEliminations(puzzle, startCell, endCell, elimCand);

                                if (eliminations.Count > 0)
                                {
                                    SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);

                                    solution.ContextData = new HintContextData();
                                    solution.ContextData.PrimaryCandidate = elimCand;

                                    solution.ContextData.FocusCells.Add(new int[] { startCell.RowIndex, startCell.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { linkCell1.RowIndex, linkCell1.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { pivotCell.RowIndex, pivotCell.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { linkCell2.RowIndex, linkCell2.ColumnIndex });
                                    solution.ContextData.FocusCells.Add(new int[] { endCell.RowIndex, endCell.ColumnIndex });

                                    solution.ContextData.FocusCandidates = new List<int> { candX, candY };
                                    solution.ContextData.ReasoningCandidates = new List<int> { candX, candY };

                                    return solution;
                                }
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
