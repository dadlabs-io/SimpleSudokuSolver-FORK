using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: XYZ-Wing
    /// 
    /// Pattern:
    /// - Pivot cell has exactly 3 candidates {X, Y, Z}
    /// - Wing1 (XZ-cell): In same box as pivot, has candidates {X, Z} (subset of pivot)
    /// - Wing2 (YZ-cell): Sees pivot (same row/col), has candidates {Y, Z}
    /// 
    /// Logic:
    /// - Either pivot is X (forcing Wing1 to be Z), or pivot is Y (forcing Wing2 to be Z), or pivot is Z
    /// - In all cases, one of the three cells must be Z
    /// - Eliminate Z from cells that see ALL THREE cells
    /// 
    /// Key difference from XY-Wing: Pivot has 3 candidates instead of 2.
    /// </summary>
    public class XYZWing : ISudokuSolverStrategy
    {
        public string StrategyName => "XYZ-Wing";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Find cells with exactly 3 candidates (potential pivots)
            var pivotCells = new List<Cell>();
            foreach (var row in sudokuPuzzle.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.CanBe.Count == 3)
                    {
                        pivotCells.Add(cell);
                    }
                }
            }

            foreach (var pivot in pivotCells)
            {
                var xyzCandidates = pivot.CanBe.ToList(); // {X, Y, Z}
                int pivotBox = SolverUtility.GetBoxIndex(pivot);

                // Find XZ-cell (Wing1): In same box as pivot, with 2 candidates that are subset of pivot
                var boxCells = SolverUtility.GetBoxCells(sudokuPuzzle, pivotBox);
                foreach (var xzCell in boxCells)
                {
                    if (xzCell == pivot) continue;
                    if (xzCell.CanBe.Count != 2) continue;
                    
                    // XZ-cell must have 2 candidates that are both in pivot's candidates
                    if (!xzCell.CanBe.All(c => xyzCandidates.Contains(c))) continue;

                    // Find the candidate in pivot that's NOT in XZ-cell (this is Y)
                    var yCandidate = xyzCandidates.First(c => !xzCell.CanBe.Contains(c));
                    var xzCandidates = xzCell.CanBe.ToList();

                    // Find YZ-cell (Wing2): Sees pivot (same row or column), has {Y, Z}
                    var seenCells = GetCellsSeeingPivot(sudokuPuzzle, pivot, pivotBox);
                    foreach (var yzCell in seenCells)
                    {
                        if (yzCell.CanBe.Count != 2) continue;
                        if (!yzCell.CanBe.Contains(yCandidate)) continue;

                        // YZ-cell must share exactly one candidate with XZ-cell (this is Z)
                        var sharedWithXZ = xzCandidates.Intersect(yzCell.CanBe).ToList();
                        if (sharedWithXZ.Count != 1) continue;

                        int zCandidate = sharedWithXZ[0];
                        
                        // Verify YZ-cell has {Y, Z}
                        if (!yzCell.CanBe.Contains(yCandidate) || !yzCell.CanBe.Contains(zCandidate)) continue;

                        // FOUND XYZ-Wing! Find eliminations
                        // Cells must see ALL THREE cells (pivot, xzCell, yzCell)
                        var eliminations = FindEliminations(sudokuPuzzle, pivot, xzCell, yzCell, zCandidate);
                        
                        if (eliminations.Count > 0)
                        {
                            var solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                            
                            // Populate ContextData
                            solution.ContextData = new HintContextData();
                            solution.ContextData.PrimaryCandidate = zCandidate;
                            
                            // FocusCells: [pivot, xzCell, yzCell]
                            solution.ContextData.FocusCells.Add(new int[] { pivot.RowIndex, pivot.ColumnIndex });
                            solution.ContextData.FocusCells.Add(new int[] { xzCell.RowIndex, xzCell.ColumnIndex });
                            solution.ContextData.FocusCells.Add(new int[] { yzCell.RowIndex, yzCell.ColumnIndex });
                            
                            // Store pivot candidates for hint display
                            solution.ContextData.FocusCandidates = xyzCandidates;
                            
                            // ReasoningCells: Store XZ and YZ cell candidates
                            // Actually, let's use ReasoningCandidates for the X and Y values
                            int xCandidate = xzCandidates.First(c => c != zCandidate);
                            solution.ContextData.ReasoningCandidates = new List<int> { xCandidate, yCandidate, zCandidate };

                            return solution;
                        }
                    }
                }
            }

            return null;
        }

        private List<Cell> GetCellsSeeingPivot(SudokuPuzzle puzzle, Cell pivot, int pivotBox)
        {
            var cells = new List<Cell>();
            
            // Cells in same row (but different box)
            foreach (var cell in puzzle.Rows[pivot.RowIndex].Cells)
            {
                int cellBox = SolverUtility.GetBoxIndex(cell);
                if (cellBox != pivotBox && cell != pivot)
                {
                    cells.Add(cell);
                }
            }
            
            // Cells in same column (but different box)
            foreach (var cell in puzzle.Columns[pivot.ColumnIndex].Cells)
            {
                int cellBox = SolverUtility.GetBoxIndex(cell);
                if (cellBox != pivotBox && cell != pivot)
                {
                    cells.Add(cell);
                }
            }
            
            return cells;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle, Cell pivot, Cell xzCell, Cell yzCell, int zCandidate)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();
            
            // The key insight: eliminations happen in cells that see ALL THREE cells
            // Since XZ-cell is in the same box as pivot, cells in that box that also see YZ-cell
            // (i.e., cells in the box that are in the same row or column as YZ-cell)
            
            int pivotBox = SolverUtility.GetBoxIndex(pivot);
            var boxCells = SolverUtility.GetBoxCells(puzzle, pivotBox);
            
            foreach (var cell in boxCells)
            {
                // Skip the three wing cells
                if (cell == pivot || cell == xzCell || cell == yzCell) continue;
                
                // Must have the Z candidate
                if (!cell.CanBe.Contains(zCandidate)) continue;
                
                // Must see YZ-cell (same row or column)
                bool seesYZ = (cell.RowIndex == yzCell.RowIndex) || (cell.ColumnIndex == yzCell.ColumnIndex);
                if (!seesYZ) continue;
                
                eliminations.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, zCandidate));
            }
            
            return eliminations;
        }
    }
}
