using System;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Immutable authored placement of one vehicle inside a level.
    /// <para>
    /// Anchor convention: <see cref="AnchorCell"/> is the <b>front-left</b> cell of the
    /// oriented footprint. Occupied cells are derived from anchor, direction, width, and length
    /// and must never be authored manually.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class VehiclePlacementDefinition
    {
        [SerializeField]
        [Tooltip("Stable unique vehicle ID within the owning level.")]
        private string _vehicleId = "vehicle_01";

        [SerializeField]
        [Tooltip("Reusable vehicle type definition.")]
        private VehicleTypeDefinition _vehicleType;

        [SerializeField]
        [Tooltip("Gameplay color identity for this vehicle.")]
        private VehicleColorId _colorId = VehicleColorId.Red;

        [SerializeField]
        [Tooltip("Front-left cell of the oriented vehicle footprint.")]
        private Vector2Int _anchorCell;

        [SerializeField]
        [Tooltip("Facing direction of the vehicle on the board.")]
        private GridDirection _direction = GridDirection.North;

        /// <summary>
        /// Gets the stable unique vehicle ID within the level.
        /// </summary>
        public string VehicleId => _vehicleId;

        /// <summary>
        /// Gets the referenced vehicle type definition.
        /// </summary>
        public VehicleTypeDefinition VehicleType => _vehicleType;

        /// <summary>
        /// Gets the gameplay color identity.
        /// </summary>
        public VehicleColorId ColorId => _colorId;

        /// <summary>
        /// Gets the front-left anchor cell of the oriented footprint.
        /// </summary>
        public Vector2Int AnchorCell => _anchorCell;

        /// <summary>
        /// Gets the authored facing direction.
        /// </summary>
        public GridDirection Direction => _direction;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only helper for constructing placements in tests or tooling.
        /// </summary>
        public static VehiclePlacementDefinition CreateForEditor(
            string vehicleId,
            VehicleTypeDefinition vehicleType,
            VehicleColorId colorId,
            Vector2Int anchorCell,
            GridDirection direction)
        {
            return new VehiclePlacementDefinition
            {
                _vehicleId = vehicleId,
                _vehicleType = vehicleType,
                _colorId = colorId,
                _anchorCell = anchorCell,
                _direction = direction
            };
        }
#endif
    }
}
