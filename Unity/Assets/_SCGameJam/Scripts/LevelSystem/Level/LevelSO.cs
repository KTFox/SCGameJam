using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Immutable authored definition of one puzzle level.
    /// Runtime systems must never mutate this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "SCJam/Level/Level SO")]
    public sealed class LevelSO : ScriptableObject
    {
        [SerializeField][Tooltip("Stable identifier for this level.")] private string _levelId;
        [SerializeField][Min(1)][Tooltip("Board width in cells (columns / grid X).")] private int _gridWidth;
        [SerializeField][Min(1)][Tooltip("Board height in cells (rows / grid Y).")] private int _gridHeight;
        [SerializeField][Min(0.0001f)][Tooltip("Size of one cell in board-local units.")] private float _cellSize;
        [SerializeField][Tooltip("Authored vehicle placements for this level.")] private List<VehiclePlacementDefinition> _vehiclePlacements;

        public string LevelId => _levelId;
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        public IReadOnlyList<VehiclePlacementDefinition> VehiclePlacements => _vehiclePlacements;
    }
}
