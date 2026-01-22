using SimpleSudokuSolver.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Strategy is examining rows and columns, and is looking for candidates which are grouped together in just one block.
    /// If such a candidate is found, we can exclude it from the rest of the block.
    /// </summary>
    /// <remarks>
    /// See also:
    /// - http://www.sudokuwiki.org/Intersection_Removal (Box Line Reduction)
    /// </remarks>
    public class LockedCandidatesClaiming : ISudokuSolverStrategy
    {
        public string StrategyName => "Locked Candidates (Claiming)";

        public SingleStepSolution SolveSingleStep(SudokuPuzzle sudokuPuzzle)
        {
            // DEBUG: Log to file for tracing (controlled by EnableVerboseFileLogging)
            var logPath = @"C:\github.com\sudoku-app\memory-bank\short-term\logs\sss_claiming_debug.log";
            System.Text.StringBuilder log = null;
            if (Model.SudokuPuzzle.EnableVerboseFileLogging)
            {
                log = new System.Text.StringBuilder();
                log.AppendLine($"\n=== LockedCandidatesClaiming.SolveSingleStep @ {DateTime.Now:HH:mm:ss} ===");
            }

            var cellCandidatePairsPerRow = new List<Tuple<Cell, int>>();
            var cellCandidatePairsPerColumn = new List<Tuple<Cell, int>>();

            foreach (var row in sudokuPuzzle.Rows)
            {
                var pairs = GetCellCandidatePairsWhichAppearOnlyInSingleBlock(row.Cells, x => x.ColumnIndex).ToList();
                if (pairs.Count > 0)
                {
                    log?.AppendLine($"  Row {row.Cells[0].RowIndex} (0-based): Found {pairs.Count} locked patterns");
                    foreach (var p in pairs)
                    {
                        log?.AppendLine($"    - Candidate {p.Item2} locked in cell R{p.Item1.RowIndex + 1}C{p.Item1.ColumnIndex + 1} (1-based)");
                    }
                }
                cellCandidatePairsPerRow.AddRange(pairs);
            }

            foreach (var column in sudokuPuzzle.Columns)
            {
                var pairs = GetCellCandidatePairsWhichAppearOnlyInSingleBlock(column.Cells, x => x.RowIndex).ToList();
                if (pairs.Count > 0)
                {
                    log?.AppendLine($"  Column {column.Cells[0].ColumnIndex} (0-based): Found {pairs.Count} locked patterns");
                    foreach (var p in pairs)
                    {
                        log?.AppendLine($"    - Candidate {p.Item2} locked in cell R{p.Item1.RowIndex + 1}C{p.Item1.ColumnIndex + 1} (1-based)");
                    }
                }
                cellCandidatePairsPerColumn.AddRange(pairs);
            }

            // Try row-based patterns first
            log?.AppendLine($"\n  Computing ROW-based eliminations from {cellCandidatePairsPerRow.Count} patterns...");
            var rowEliminations = GetEliminations(cellCandidatePairsPerRow, true, sudokuPuzzle).ToList();
            log?.AppendLine($"  -> Found {rowEliminations.Count} row-based eliminations:");
            foreach (var e in rowEliminations)
            {
                log?.AppendLine($"      Eliminate {e.Value} from R{e.IndexOfRow + 1}C{e.IndexOfColumn + 1} (1-based) [raw: row={e.IndexOfRow}, col={e.IndexOfColumn} 0-based]");
            }

            if (rowEliminations.Count > 0)
            {
                log?.AppendLine($"\n  RETURNING {rowEliminations.Distinct().Count()} row-based eliminations");
                if (Model.SudokuPuzzle.EnableVerboseFileLogging && log != null)
                    System.IO.File.AppendAllText(logPath, log.ToString());
                return new SingleStepSolution(rowEliminations.Distinct().ToArray(), StrategyName);
            }

            // Then try column-based patterns
            log?.AppendLine($"\n  Computing COLUMN-based eliminations from {cellCandidatePairsPerColumn.Count} patterns...");
            var colEliminations = GetEliminations(cellCandidatePairsPerColumn, false, sudokuPuzzle).ToList();
            log?.AppendLine($"  -> Found {colEliminations.Count} column-based eliminations:");
            foreach (var e in colEliminations)
            {
                log?.AppendLine($"      Eliminate {e.Value} from R{e.IndexOfRow + 1}C{e.IndexOfColumn + 1} (1-based) [raw: row={e.IndexOfRow}, col={e.IndexOfColumn} 0-based]");
            }

            if (colEliminations.Count > 0)
            {
                log?.AppendLine($"\n  RETURNING {colEliminations.Distinct().Count()} column-based eliminations");
                if (Model.SudokuPuzzle.EnableVerboseFileLogging && log != null)
                    System.IO.File.AppendAllText(logPath, log.ToString());
                return new SingleStepSolution(colEliminations.Distinct().ToArray(), StrategyName);
            }

            log?.AppendLine("\n  No eliminations found, returning null");
            if (Model.SudokuPuzzle.EnableVerboseFileLogging && log != null)
                System.IO.File.AppendAllText(logPath, log.ToString());
            return null;
        }

        /// <summary>
        /// Method examines all the cells from <paramref name="cellsOfSingleRowOrColumn"/> and
        /// returns all the cells that have a candidate value that is present only in a single block.
        /// </summary>
        /// <param name="cellsOfSingleRowOrColumn">Cells of a row or cells of a column.</param>
        /// <param name="getIndexOfColumnOrRow">
        /// Returns column index of a cell if <paramref name="cellsOfSingleRowOrColumn"/> 
        /// are part of a row and row index if they are part of a column.
        /// That index is later used to determine which block the cells belongs to.
        /// </param>
        /// <returns>Item1 of tuple is cell, and Item2 is cell's candidate value.</returns>
        private IEnumerable<Tuple<Cell, int>> GetCellCandidatePairsWhichAppearOnlyInSingleBlock(
          Cell[] cellsOfSingleRowOrColumn, Func<Cell, int> getIndexOfColumnOrRow)
        {
            var cellsWithNoValue = cellsOfSingleRowOrColumn.Where(x => !x.HasValue).ToArray();
            var indexesPerCandidate = new Dictionary<int, HashSet<int>>();

            foreach (var cellWithNoValue in cellsWithNoValue)
            {
                var index = getIndexOfColumnOrRow(cellWithNoValue);

                foreach (var candidate in cellWithNoValue.CanBe)
                {
                    if (!indexesPerCandidate.ContainsKey(candidate))
                    {
                        indexesPerCandidate[candidate] = new HashSet<int>();
                    }
                    indexesPerCandidate[candidate].Add(index);
                }
            }

            var candidateValuesWhichAppearOnlyInSingleBlock = new List<int>();

            foreach (var item in indexesPerCandidate)
            {
                // We assume block contains 3 cells
                var allInSingleBlock = item.Value.Select(x => x / 3).Distinct().Count() == 1;
                if (allInSingleBlock)
                {
                    candidateValuesWhichAppearOnlyInSingleBlock.Add(item.Key);
                }
            }

            var result = new List<Tuple<Cell, int>>();

            foreach (var item in candidateValuesWhichAppearOnlyInSingleBlock)
            {
                // We could return all the cells, but just one is enough to eliminate other candidates
                var cell = cellsWithNoValue.First(x => x.CanBe.Contains(item));
                result.Add(new Tuple<Cell, int>(cell, item));
            }

            return result;
        }

        private IEnumerable<SingleStepSolution.Candidate> GetEliminations(
          IEnumerable<Tuple<Cell, int>> cellCandidatePairs, bool perRow, SudokuPuzzle sudokuPuzzle)
        {
            var eliminations = new List<SingleStepSolution.Candidate>();

            foreach (var cellCandidatePair in cellCandidatePairs)
            {
                var cell = cellCandidatePair.Item1;
                var candidate = cellCandidatePair.Item2;
                var blockIndex = sudokuPuzzle.GetBlockIndex(cell);
                var block = sudokuPuzzle.Blocks[blockIndex.RowIndex, blockIndex.ColumnIndex];

                foreach (var blockCell in block.Cells)
                {
                    // Ignore the same cell, or block cells that do not contain candidate
                    if (blockCell == cell || !blockCell.CanBe.Contains(candidate))
                    {
                        continue;
                    }

                    // If outside cells are part of the same row,
                    // ignore block cells that are part of that same row (similar for columns)
                    if ((perRow && cell.RowIndex == blockCell.RowIndex) ||
                      (!perRow && cell.ColumnIndex == blockCell.ColumnIndex))
                    {
                        continue;
                    }

                    eliminations.Add(new SingleStepSolution.Candidate(
                      blockCell.RowIndex, blockCell.ColumnIndex, candidate));
                }
            }

            return eliminations;
        }
    }
}
