using System.Collections.Generic;
using System.Linq;
using SimpleSudokuSolver.Model;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy: Sue de Coq
    /// 
    /// Pattern: Box-line intersection with complements forming a constraint:
    /// - 2-3 cells in intersection with combined candidates
    /// - Block complement: cells in rest of box sharing candidates
    /// - Line complement: cells in rest of line sharing candidates
    /// 
    /// Eliminations occur in:
    /// - Block cells outside the SdC (for block-related candidates)
    /// - Line cells outside the SdC (for line-related candidates)
    /// 
    /// SE Rating: 7.6
    /// </summary>
    public class SueDeCoq : ISudokuSolverStrategy
    {
        public string StrategyName => "Sue de Coq";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // Need at least 4 empty cells for valid SdC
            int emptyCount = CountEmptyCells(sudokuPuzzle);
            if (emptyCount < 4) return null;

            // Iterate through all box-line intersections (minilines)
            for (int blockRow = 0; blockRow < 3; blockRow++)
            {
                for (int blockCol = 0; blockCol < 3; blockCol++)
                {
                    // Check row intersections
                    for (int localRow = 0; localRow < 3; localRow++)
                    {
                        int globalRow = blockRow * 3 + localRow;
                        SingleStepSolution result = TryIntersection(sudokuPuzzle, blockRow, blockCol, globalRow, true);
                        if (result != null) return result;
                    }

                    // Check column intersections
                    for (int localCol = 0; localCol < 3; localCol++)
                    {
                        int globalCol = blockCol * 3 + localCol;
                        SingleStepSolution result = TryIntersection(sudokuPuzzle, blockRow, blockCol, globalCol, false);
                        if (result != null) return result;
                    }
                }
            }

            return null;
        }

        private SingleStepSolution TryIntersection(SudokuPuzzle puzzle, int blockRow, int blockCol, int lineIndex, bool isRow)
        {
            // Get intersection cells (2-3 cells where box meets row/column)
            List<Cell> intersectionCells = GetIntersectionCells(puzzle, blockRow, blockCol, lineIndex, isRow);
            List<Cell> emptyIntersection = intersectionCells.Where(c => !c.HasValue).ToList();

            if (emptyIntersection.Count < 2) return null;

            // Get candidates in intersection
            HashSet<int> intersectionCands = new HashSet<int>();
            foreach (Cell cell in emptyIntersection)
            {
                foreach (int cand in cell.CanBe)
                {
                    intersectionCands.Add(cand);
                }
            }

            // Need more candidates than cells for SdC (not already an ALS/subset)
            if (intersectionCands.Count <= emptyIntersection.Count + 1) return null;

            // Get block complement cells (rest of box, excluding intersection)
            List<Cell> blockComplement = GetBlockComplement(puzzle, blockRow, blockCol, emptyIntersection);

            // Get line complement cells (rest of line, excluding intersection)
            List<Cell> lineComplement = GetLineComplement(puzzle, lineIndex, isRow, emptyIntersection);

            // Try combinations of block and line complement cells
            return TryCombinations(puzzle, emptyIntersection, intersectionCands, 
                blockComplement, lineComplement, blockRow, blockCol, lineIndex, isRow);
        }

        private SingleStepSolution TryCombinations(
            SudokuPuzzle puzzle,
            List<Cell> intersection,
            HashSet<int> interCands,
            List<Cell> blockComplement,
            List<Cell> lineComplement,
            int blockRow, int blockCol, int lineIndex, bool isRow)
        {
            // Try different sizes of block and line subsets
            for (int bSize = 1; bSize <= blockComplement.Count && bSize <= 4; bSize++)
            {
                foreach (List<Cell> blockSubset in GetCombinations(blockComplement, bSize))
                {
                    HashSet<int> blockCands = GetCandidates(blockSubset);
                    
                    // Block candidates must overlap with intersection
                    if (!blockCands.Overlaps(interCands)) continue;

                    for (int lSize = 0; lSize <= lineComplement.Count && lSize <= 4; lSize++)
                    {
                        foreach (List<Cell> lineSubset in GetCombinations(lineComplement, lSize))
                        {
                            if (lSize == 0 && lineSubset.Count > 0) continue;

                            HashSet<int> lineCands = GetCandidates(lineSubset);

                            // Line candidates must overlap with intersection (if any)
                            if (lSize > 0 && !lineCands.Overlaps(interCands)) continue;

                            // Block and line must not share candidates (standard SdC)
                            HashSet<int> overlap = new HashSet<int>(blockCands);
                            overlap.IntersectWith(lineCands);
                            if (overlap.Count > 0) continue;

                            // Check the constraint: N cells with N candidates
                            HashSet<int> allCands = new HashSet<int>(interCands);
                            allCands.UnionWith(blockCands);
                            allCands.UnionWith(lineCands);

                            int totalCells = intersection.Count + blockSubset.Count + lineSubset.Count;
                            
                            // For valid SdC: candidates = cells - shared portion adjustment
                            // This is a simplification; the real check is more nuanced
                            if (allCands.Count != totalCells) continue;

                            // Find eliminations
                            List<SingleStepSolution.Candidate> eliminations = 
                                FindEliminations(puzzle, intersection, blockSubset, lineSubset,
                                    blockCands, lineCands, blockRow, blockCol, lineIndex, isRow);

                            if (eliminations.Count > 0)
                            {
                                SingleStepSolution solution = new SingleStepSolution(eliminations.ToArray(), StrategyName);
                                
                                // Build focus and reasoning cells
                                List<int[]> focusCells = intersection.Select(c => new int[] { c.RowIndex, c.ColumnIndex }).ToList();
                                List<int[]> reasoningCells = new List<int[]>();
                                reasoningCells.AddRange(blockSubset.Select(c => new int[] { c.RowIndex, c.ColumnIndex }));
                                reasoningCells.AddRange(lineSubset.Select(c => new int[] { c.RowIndex, c.ColumnIndex }));

                                // House indices: block + row/col
                                int blockIndex = 18 + blockRow * 3 + blockCol;
                                int lineHouse = isRow ? lineIndex : 9 + lineIndex;

                                solution.ContextData = new HintContextData
                                {
                                    FocusCells = focusCells,
                                    ReasoningCells = reasoningCells,
                                    HouseIndices = new List<int> { blockIndex, lineHouse },
                                    FocusCandidates = interCands.ToList(),
                                    ReasoningCandidates = blockCands.Union(lineCands).ToList()
                                };

                                return solution;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private List<SingleStepSolution.Candidate> FindEliminations(
            SudokuPuzzle puzzle,
            List<Cell> intersection, List<Cell> blockSubset, List<Cell> lineSubset,
            HashSet<int> blockCands, HashSet<int> lineCands,
            int blockRow, int blockCol, int lineIndex, bool isRow)
        {
            List<SingleStepSolution.Candidate> result = new List<SingleStepSolution.Candidate>();
            HashSet<Cell> sdcCells = new HashSet<Cell>(intersection);
            sdcCells.UnionWith(blockSubset);
            sdcCells.UnionWith(lineSubset);

            // Eliminate block candidates from rest of block
            Block block = puzzle.Blocks[blockRow, blockCol];
            foreach (Cell cell in block.Cells)
            {
                if (cell.HasValue || sdcCells.Contains(cell)) continue;

                foreach (int cand in blockCands)
                {
                    if (cell.CanBe.Contains(cand))
                    {
                        result.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, cand));
                    }
                }
            }

            // Eliminate line candidates from rest of line
            IEnumerable<Cell> lineCells = isRow 
                ? puzzle.Rows[lineIndex].Cells 
                : puzzle.Columns[lineIndex].Cells;

            foreach (Cell cell in lineCells)
            {
                if (cell.HasValue || sdcCells.Contains(cell)) continue;

                foreach (int cand in lineCands)
                {
                    if (cell.CanBe.Contains(cand))
                    {
                        result.Add(new SingleStepSolution.Candidate(cell.RowIndex, cell.ColumnIndex, cand));
                    }
                }
            }

            return result;
        }

        // Helper methods

        private int CountEmptyCells(SudokuPuzzle puzzle)
        {
            int count = 0;
            foreach (Row row in puzzle.Rows)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (!cell.HasValue) count++;
                }
            }
            return count;
        }

        private List<Cell> GetIntersectionCells(SudokuPuzzle puzzle, int blockRow, int blockCol, int lineIndex, bool isRow)
        {
            List<Cell> cells = new List<Cell>();
            Block block = puzzle.Blocks[blockRow, blockCol];

            foreach (Cell cell in block.Cells)
            {
                if (isRow && cell.RowIndex == lineIndex)
                    cells.Add(cell);
                else if (!isRow && cell.ColumnIndex == lineIndex)
                    cells.Add(cell);
            }
            return cells;
        }

        private List<Cell> GetBlockComplement(SudokuPuzzle puzzle, int blockRow, int blockCol, List<Cell> exclude)
        {
            List<Cell> cells = new List<Cell>();
            Block block = puzzle.Blocks[blockRow, blockCol];
            HashSet<Cell> excludeSet = new HashSet<Cell>(exclude);

            foreach (Cell cell in block.Cells)
            {
                if (!cell.HasValue && !excludeSet.Contains(cell))
                    cells.Add(cell);
            }
            return cells;
        }

        private List<Cell> GetLineComplement(SudokuPuzzle puzzle, int lineIndex, bool isRow, List<Cell> exclude)
        {
            List<Cell> cells = new List<Cell>();
            HashSet<Cell> excludeSet = new HashSet<Cell>(exclude);

            IEnumerable<Cell> lineCells = isRow
                ? puzzle.Rows[lineIndex].Cells
                : puzzle.Columns[lineIndex].Cells;

            foreach (Cell cell in lineCells)
            {
                if (!cell.HasValue && !excludeSet.Contains(cell))
                    cells.Add(cell);
            }
            return cells;
        }

        private HashSet<int> GetCandidates(List<Cell> cells)
        {
            HashSet<int> cands = new HashSet<int>();
            foreach (Cell cell in cells)
            {
                foreach (int c in cell.CanBe)
                {
                    cands.Add(c);
                }
            }
            return cands;
        }

        private IEnumerable<List<Cell>> GetCombinations(List<Cell> cells, int size)
        {
            if (size == 0)
            {
                yield return new List<Cell>();
                yield break;
            }

            if (size > cells.Count) yield break;

            for (int i = 0; i <= cells.Count - size; i++)
            {
                foreach (List<Cell> rest in GetCombinations(cells.Skip(i + 1).ToList(), size - 1))
                {
                    List<Cell> result = new List<Cell> { cells[i] };
                    result.AddRange(rest);
                    yield return result;
                }
            }
        }
    }
}
