using SimpleSudokuSolver.Model;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSudokuSolver.Strategy
{
    /// <summary>
    /// Represents a node in a conjugate pair chain for Simple Coloring.
    /// Each node is a cell position that can hold a specific candidate value.
    /// </summary>
    internal class ColorNode
    {
        /// <summary>Row index (0-8).</summary>
        public int Row { get; }

        /// <summary>Column index (0-8).</summary>
        public int Column { get; }

        /// <summary>
        /// The color assigned to this node (0 or 1).
        /// Null if not yet visited.
        /// </summary>
        public int? ColorValue { get; set; }

        /// <summary>Whether this node has been visited during coloring.</summary>
        public bool Visited { get; set; }

        /// <summary>Neighboring nodes connected by conjugate pairs.</summary>
        public HashSet<ColorNode> Neighbors { get; }

        public ColorNode(int row, int column)
        {
            Row = row;
            Column = column;
            Neighbors = new HashSet<ColorNode>();
        }

        /// <summary>
        /// Creates a bidirectional link between this node and another.
        /// </summary>
        public void Link(ColorNode other)
        {
            Neighbors.Add(other);
            other.Neighbors.Add(this);
        }

        /// <summary>
        /// Checks if this cell "sees" (shares a house with) another cell.
        /// </summary>
        public bool Sees(int otherRow, int otherCol)
        {
            if (Row == otherRow && Column == otherCol) return false; // Same cell
            return Row == otherRow ||  // Same row
                   Column == otherCol ||  // Same column
                   (Row / 3 == otherRow / 3 && Column / 3 == otherCol / 3);  // Same block
        }

        public override bool Equals(object obj)
        {
            if (obj is ColorNode other)
            {
                return Row == other.Row && Column == other.Column;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Row * 9 + Column;
        }
    }

    /// <summary>
    /// Graph of conjugate pairs for a specific candidate value.
    /// Used for Simple Coloring techniques (Color Trap, Color Wrap).
    /// </summary>
    internal class ColorGraph
    {
        /// <summary>All nodes in the graph.</summary>
        public HashSet<ColorNode> Nodes { get; } = new HashSet<ColorNode>();

        /// <summary>Returns true if all nodes have been visited.</summary>
        public bool AllNodesVisited() => Nodes.All(n => n.Visited);

        /// <summary>
        /// Gets or creates a node at the given position.
        /// </summary>
        public ColorNode GetOrAdd(int row, int column)
        {
            ColorNode existing = Nodes.FirstOrDefault(n => n.Row == row && n.Column == column);
            if (existing != null)
            {
                return existing;
            }

            ColorNode newNode = new ColorNode(row, column);
            Nodes.Add(newNode);
            return newNode;
        }

        /// <summary>
        /// Clears all color assignments (for processing multiple chains).
        /// </summary>
        public void ClearColors()
        {
            foreach (ColorNode node in Nodes)
            {
                node.ColorValue = null;
            }
        }

        /// <summary>
        /// Builds a graph of conjugate pairs for a specific candidate value.
        /// A conjugate pair exists when exactly 2 cells in a house share a candidate.
        /// </summary>
        public static ColorGraph BuildForCandidate(SudokuPuzzle puzzle, int candidate)
        {
            ColorGraph graph = new ColorGraph();

            // Check all rows
            for (int row = 0; row < 9; row++)
            {
                Cell[] rowCells = puzzle.Rows[row].Cells;
                AddConjugatePairIfExists(graph, rowCells, candidate);
            }

            // Check all columns
            for (int col = 0; col < 9; col++)
            {
                Cell[] colCells = puzzle.Columns[col].Cells;
                AddConjugatePairIfExists(graph, colCells, candidate);
            }

            // Check all blocks
            for (int block = 0; block < 9; block++)
            {
                Cell[] blockCells = puzzle.Blocks[block].Cells;
                AddConjugatePairIfExists(graph, blockCells, candidate);
            }

            return graph;
        }

        /// <summary>
        /// If exactly 2 cells in the house have the candidate, link them as a conjugate pair.
        /// </summary>
        private static void AddConjugatePairIfExists(ColorGraph graph, Cell[] cells, int candidate)
        {
            List<Cell> cellsWithCandidate = new List<Cell>();
            foreach (Cell cell in cells)
            {
                if (cell.CanBe.Contains(candidate))
                {
                    cellsWithCandidate.Add(cell);
                }
            }

            if (cellsWithCandidate.Count == 2)
            {
                ColorNode node1 = graph.GetOrAdd(cellsWithCandidate[0].RowIndex, cellsWithCandidate[0].ColumnIndex);
                ColorNode node2 = graph.GetOrAdd(cellsWithCandidate[1].RowIndex, cellsWithCandidate[1].ColumnIndex);
                node1.Link(node2);
            }
        }

        /// <summary>
        /// Recursively visits nodes, alternating colors 0 and 1.
        /// </summary>
        public static void ColorChain(ColorNode node, int colorValue)
        {
            node.Visited = true;
            node.ColorValue = colorValue;

            foreach (ColorNode neighbor in node.Neighbors)
            {
                if (!neighbor.Visited)
                {
                    ColorChain(neighbor, 1 - colorValue); // Alternate color
                }
            }
        }
    }
}
