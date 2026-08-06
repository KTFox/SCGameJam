using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Scene component that exposes board-local grid conversion for a level.
    /// Camera orientation does not affect grid logic; only <see cref="_boardRoot"/> does.
    /// </summary>
    public sealed class BoardGridTransform : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Transform that defines board-local space. Defaults to this transform when unset.")]
        private Transform _boardRoot;

        [SerializeField]
        [Tooltip("Optional level definition used to initialize converter metrics.")]
        private LevelSO _levelDefinition;

        [SerializeField]
        [Min(1)]
        [Tooltip("Grid width in cells when no level definition is assigned.")]
        private int _gridWidth = 8;

        [SerializeField]
        [Min(1)]
        [Tooltip("Grid height in cells when no level definition is assigned.")]
        private int _gridHeight = 8;

        [SerializeField]
        [Min(0.0001f)]
        [Tooltip("Cell size in board-local units when no level definition is assigned.")]
        private float _cellSize = 1f;

        private GridCoordinateConverter _converter;

        /// <summary>
        /// Gets the board root transform.
        /// </summary>
        public Transform BoardRoot => _boardRoot != null ? _boardRoot : transform;

        /// <summary>
        /// Gets the optional authored level definition used for converter metrics.
        /// </summary>
        public LevelSO LevelDefinition => _levelDefinition;

        /// <summary>
        /// Gets the active coordinate converter, creating it on demand.
        /// </summary>
        public GridCoordinateConverter Converter
        {
            get
            {
                EnsureConverter();
                return _converter;
            }
        }

        /// <summary>
        /// Gets the grid width in cells.
        /// </summary>
        public int GridWidth => Converter.GridWidth;

        /// <summary>
        /// Gets the grid height in cells.
        /// </summary>
        public int GridHeight => Converter.GridHeight;

        /// <summary>
        /// Gets the cell size in board-local units.
        /// </summary>
        public float CellSize => Converter.CellSize;

        private void Awake()
        {
            EnsureConverter();
        }

        /// <summary>
        /// Rebuilds the converter from an authored level definition.
        /// </summary>
        /// <param name="levelDefinition">Level definition supplying grid metrics.</param>
        public void Configure(LevelSO levelDefinition)
        {
            _levelDefinition = levelDefinition;
            if (levelDefinition != null)
            {
                _gridWidth = levelDefinition.GridWidth;
                _gridHeight = levelDefinition.GridHeight;
                _cellSize = levelDefinition.CellSize;
            }

            RebuildConverter();
        }

        /// <summary>
        /// Rebuilds the converter from explicit grid metrics.
        /// </summary>
        /// <param name="gridWidth">Grid width in cells.</param>
        /// <param name="gridHeight">Grid height in cells.</param>
        /// <param name="cellSize">Cell size in board-local units.</param>
        public void Configure(int gridWidth, int gridHeight, float cellSize)
        {
            _levelDefinition = null;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
            RebuildConverter();
        }

        /// <summary>
        /// Converts a grid cell into a board-local cell-center position.
        /// </summary>
        public Vector3 GridToLocalPosition(Vector2Int cell) => Converter.GridToLocalPosition(cell);

        /// <summary>
        /// Converts a grid cell into a world-space cell-center position.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int cell) => Converter.GridToWorldPosition(cell);

        /// <summary>
        /// Converts a board-local position into the nearest grid cell.
        /// </summary>
        public Vector2Int LocalPositionToGrid(Vector3 localPosition) => Converter.LocalPositionToGrid(localPosition);

        /// <summary>
        /// Converts a world position into the nearest grid cell.
        /// </summary>
        public Vector2Int WorldPositionToGrid(Vector3 worldPosition) => Converter.WorldPositionToGrid(worldPosition);

        /// <summary>
        /// Returns true when the cell lies inside the board.
        /// </summary>
        public bool IsInsideGrid(Vector2Int cell) => Converter.IsInsideGrid(cell);

        private void EnsureConverter()
        {
            if (_converter == null)
            {
                RebuildConverter();
            }
        }

        private void RebuildConverter()
        {
            int width = _gridWidth;
            int height = _gridHeight;
            float cellSize = _cellSize;

            if (_levelDefinition != null)
            {
                width = _levelDefinition.GridWidth;
                height = _levelDefinition.GridHeight;
                cellSize = _levelDefinition.CellSize;
            }

            _converter = new GridCoordinateConverter(BoardRoot, width, height, cellSize);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_gridWidth < 1)
            {
                _gridWidth = 1;
            }

            if (_gridHeight < 1)
            {
                _gridHeight = 1;
            }

            if (_cellSize <= 0f)
            {
                _cellSize = 0.0001f;
            }

            _converter = null;
        }
#endif
    }
}
