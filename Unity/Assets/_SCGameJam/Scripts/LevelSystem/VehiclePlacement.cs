using System;
using SCJam.Common;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Defines where a vehicle is placed and the direction it faces and moves.
    /// OriginCell is the bottom-left cell of the vehicle's axis-aligned footprint after that footprint
    /// has been rotated to match MovementDirection.
    /// </summary>
    [Serializable]
    public sealed class VehiclePlacement
    {
        [SerializeField, Tooltip("Vehicle data and prefab used for this placement.")]
        private VehicleConfig _vehicleConfig;

        [SerializeField, Tooltip("Bottom-left cell of the vehicle footprint after applying Movement Direction.")]
        private Vector2Int _originCell;

        [SerializeField, Tooltip("Direction the vehicle faces and moves. Up corresponds to the grid's local +Z axis.")]
        private GridDirection _movementDirection;


        public VehicleConfig VehicleConfig => _vehicleConfig;
        public Vector2Int OriginCell => _originCell;
        public GridDirection MovementDirection => _movementDirection;
    }
}
