using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Converts between rectangular grid coordinates and board-local / world space.
    /// <para>
    /// Convention: each grid coordinate represents a <b>cell center</b>.
    /// Grid X maps to Unity local X. Grid Y maps to Unity local Z.
    /// Cell (x, y) center is at local position (x * cellSize, 0, y * cellSize).
    /// </para>
    /// Occupancy logic must never depend on this converter; it only affects presentation.
    /// </summary>
    public sealed class GridCoordinateConverter
    {
        private readonly Transform _boardRoot;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly float _cellSize;

        /// <summary>
        /// Creates a converter bound to a board root transform and grid metrics.
        /// </summary>
        /// <param name="boardRoot">Transform that defines board-local space.</param>
        /// <param name="gridWidth">Number of columns (must be &gt; 0).</param>
        /// <param name="gridHeight">Number of rows (must be &gt; 0).</param>
        /// <param name="cellSize">Size of one cell in board-local units (must be &gt; 0).</param>
        public GridCoordinateConverter(Transform boardRoot, int gridWidth, int gridHeight, float cellSize)
        {
            _boardRoot = boardRoot;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
        }

        /// <summary>
        /// Gets the board root transform used for world conversions.
        /// </summary>
        public Transform BoardRoot => _boardRoot;

        /// <summary>
        /// Gets the grid width in cells.
        /// </summary>
        public int GridWidth => _gridWidth;

        /// <summary>
        /// Gets the grid height in cells.
        /// </summary>
        public int GridHeight => _gridHeight;

        /// <summary>
        /// Gets the cell size in board-local units.
        /// </summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Converts an integer cell coordinate into a board-local cell-center position.
        /// </summary>
        /// <param name="cell">Grid cell.</param>
        /// <returns>Board-local position of the cell center.</returns>
        public Vector3 GridToLocalPosition(Vector2Int cell)
        {
            return GridPointToLocalPosition(cell, _cellSize);
        }

        /// <summary>
        /// Converts a continuous grid-space point into a board-local position.
        /// </summary>
        /// <param name="gridPoint">Continuous grid coordinates.</param>
        /// <returns>Board-local position.</returns>
        public Vector3 GridPointToLocalPosition(Vector2 gridPoint)
        {
            return GridPointToLocalPosition(gridPoint, _cellSize);
        }

        /// <summary>
        /// Converts a continuous grid-space point into a board-local position using an explicit cell size.
        /// </summary>
        /// <param name="gridPoint">Continuous grid coordinates.</param>
        /// <param name="cellSize">Cell size in board-local units.</param>
        /// <returns>Board-local position.</returns>
        public static Vector3 GridPointToLocalPosition(Vector2 gridPoint, float cellSize)
        {
            return new Vector3(gridPoint.x * cellSize, 0f, gridPoint.y * cellSize);
        }

        /// <summary>
        /// Converts an integer cell coordinate into a world-space cell-center position.
        /// </summary>
        /// <param name="cell">Grid cell.</param>
        /// <returns>World position of the cell center.</returns>
        public Vector3 GridToWorldPosition(Vector2Int cell)
        {
            Vector3 localPosition = GridToLocalPosition(cell);
            if (_boardRoot == null)
            {
                return localPosition;
            }

            return _boardRoot.TransformPoint(localPosition);
        }

        /// <summary>
        /// Converts a continuous grid-space point into a world-space position.
        /// </summary>
        /// <param name="gridPoint">Continuous grid coordinates.</param>
        /// <returns>World position.</returns>
        public Vector3 GridPointToWorldPosition(Vector2 gridPoint)
        {
            Vector3 localPosition = GridPointToLocalPosition(gridPoint);
            if (_boardRoot == null)
            {
                return localPosition;
            }

            return _boardRoot.TransformPoint(localPosition);
        }

        /// <summary>
        /// Converts a board-local position into the nearest grid cell.
        /// </summary>
        /// <param name="localPosition">Position in board-local space.</param>
        /// <returns>Nearest cell coordinate.</returns>
        public Vector2Int LocalPositionToGrid(Vector3 localPosition)
        {
            return LocalPositionToGrid(localPosition, _cellSize);
        }

        /// <summary>
        /// Converts a board-local position into the nearest grid cell using an explicit cell size.
        /// </summary>
        /// <param name="localPosition">Position in board-local space.</param>
        /// <param name="cellSize">Cell size in board-local units.</param>
        /// <returns>Nearest cell coordinate.</returns>
        public static Vector2Int LocalPositionToGrid(Vector3 localPosition, float cellSize)
        {
            if (cellSize <= 0f)
            {
                return Vector2Int.zero;
            }

            int x = Mathf.RoundToInt(localPosition.x / cellSize);
            int y = Mathf.RoundToInt(localPosition.z / cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Converts a world position into the nearest grid cell using the board root.
        /// </summary>
        /// <param name="worldPosition">World-space position.</param>
        /// <returns>Nearest cell coordinate.</returns>
        public Vector2Int WorldPositionToGrid(Vector3 worldPosition)
        {
            if (_boardRoot == null)
            {
                return LocalPositionToGrid(worldPosition);
            }

            Vector3 localPosition = _boardRoot.InverseTransformPoint(worldPosition);
            return LocalPositionToGrid(localPosition);
        }

        /// <summary>
        /// Returns true when the cell lies inside the configured board bounds.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True if inside the board.</returns>
        public bool IsInsideGrid(Vector2Int cell)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < _gridWidth
                && cell.y < _gridHeight;
        }
    }
}
