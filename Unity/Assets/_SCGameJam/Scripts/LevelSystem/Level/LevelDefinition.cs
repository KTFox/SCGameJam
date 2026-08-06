using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Immutable authored definition of one puzzle level.
    /// Runtime systems must never mutate this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelDefinition",
        menuName = "SCJam/Level/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stable identifier for this level.")]
        private string _levelId = "level_01";

        [SerializeField]
        [Min(1)]
        [Tooltip("Board width in cells (columns / grid X).")]
        private int _gridWidth = 8;

        [SerializeField]
        [Min(1)]
        [Tooltip("Board height in cells (rows / grid Y).")]
        private int _gridHeight = 8;

        [SerializeField]
        [Min(0.0001f)]
        [Tooltip("Size of one cell in board-local units.")]
        private float _cellSize = 1f;

        [SerializeField]
        [Tooltip("Authored vehicle placements for this level.")]
        private List<VehiclePlacementDefinition> _vehiclePlacements = new List<VehiclePlacementDefinition>();

        /// <summary>
        /// Gets the stable level identifier.
        /// </summary>
        public string LevelId => _levelId;

        /// <summary>
        /// Gets the board width in cells.
        /// </summary>
        public int GridWidth => _gridWidth;

        /// <summary>
        /// Gets the board height in cells.
        /// </summary>
        public int GridHeight => _gridHeight;

        /// <summary>
        /// Gets the cell size in board-local units.
        /// </summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Gets the authored vehicle placements as a read-only list.
        /// </summary>
        public IReadOnlyList<VehiclePlacementDefinition> VehiclePlacements => _vehiclePlacements;

        /// <summary>
        /// Returns true when core grid metrics are valid.
        /// </summary>
        public bool HasValidGridMetrics =>
            _gridWidth > 0 && _gridHeight > 0 && _cellSize > 0f;

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

            if (_vehiclePlacements == null)
            {
                _vehiclePlacements = new List<VehiclePlacementDefinition>();
            }
        }

        [ContextMenu("Validate Level")]
        private void ValidateLevelFromContextMenu()
        {
            LevelDefinitionValidator.ValidateAndLog(this);
        }
#endif
    }
}
