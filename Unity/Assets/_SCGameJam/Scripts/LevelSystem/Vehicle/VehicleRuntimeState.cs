using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Mutable runtime state for one vehicle in an active level session.
    /// Does not modify the original placement definition and does not own scene views.
    /// </summary>
    public sealed class VehicleRuntimeState
    {
        private readonly string _vehicleId;
        private readonly VehiclePlacementDefinition _placement;
        private readonly VehicleTypeDefinition _vehicleType;
        private readonly VehicleColorId _colorId;
        private readonly List<Vector2Int> _occupiedCells = new List<Vector2Int>(8);
        private readonly IReadOnlyList<Vector2Int> _occupiedCellsReadOnly;

        private Vector2Int _anchorCell;
        private GridDirection _direction;
        private VehicleGameplayState _gameplayState;
        private int _passengerCount;
        private bool _isRegisteredInOccupancy;

        /// <summary>
        /// Creates runtime state from an authored placement.
        /// </summary>
        /// <param name="placement">Immutable placement definition.</param>
        public VehicleRuntimeState(VehiclePlacementDefinition placement)
        {
            _placement = placement;
            _vehicleId = placement != null ? placement.VehicleId : string.Empty;
            _vehicleType = placement != null ? placement.VehicleType : null;
            _colorId = placement != null ? placement.ColorId : default;
            _anchorCell = placement != null ? placement.AnchorCell : Vector2Int.zero;
            _direction = placement != null ? placement.Direction : GridDirection.North;
            _gameplayState = VehicleGameplayState.OnBoard;
            _passengerCount = 0;
            _occupiedCellsReadOnly = _occupiedCells.AsReadOnly();
            RefreshOccupiedCells();
        }

        /// <summary>
        /// Gets the stable runtime vehicle ID.
        /// </summary>
        public string VehicleId => _vehicleId;

        /// <summary>
        /// Gets the immutable source placement definition.
        /// </summary>
        public VehiclePlacementDefinition Placement => _placement;

        /// <summary>
        /// Gets the immutable vehicle type definition.
        /// </summary>
        public VehicleTypeDefinition VehicleType => _vehicleType;

        /// <summary>
        /// Gets the gameplay color identity.
        /// </summary>
        public VehicleColorId ColorId => _colorId;

        /// <summary>
        /// Gets the current front-left anchor cell.
        /// </summary>
        public Vector2Int AnchorCell => _anchorCell;

        /// <summary>
        /// Gets the current facing direction.
        /// </summary>
        public GridDirection Direction => _direction;

        /// <summary>
        /// Gets the current logical gameplay state.
        /// </summary>
        public VehicleGameplayState GameplayState => _gameplayState;

        /// <summary>
        /// Gets the cached occupied cells. Do not mutate the returned list.
        /// </summary>
        public IReadOnlyList<Vector2Int> OccupiedCells => _occupiedCellsReadOnly;

        /// <summary>
        /// Gets the current passenger count (reserved for future boarding systems).
        /// </summary>
        public int PassengerCount => _passengerCount;

        /// <summary>
        /// Gets whether this vehicle is currently registered in an occupancy grid.
        /// </summary>
        public bool IsRegisteredInOccupancy => _isRegisteredInOccupancy;

        /// <summary>
        /// Gets the footprint width from the vehicle type, or 0 when missing.
        /// </summary>
        public int FootprintWidth => _vehicleType != null ? _vehicleType.FootprintWidth : 0;

        /// <summary>
        /// Gets the footprint length from the vehicle type, or 0 when missing.
        /// </summary>
        public int FootprintLength => _vehicleType != null ? _vehicleType.FootprintLength : 0;

        /// <summary>
        /// Updates the vehicle pose and refreshes the occupied-cell cache.
        /// Callers must update occupancy registration separately to avoid stale grid data.
        /// </summary>
        /// <param name="anchorCell">New front-left anchor cell.</param>
        /// <param name="direction">New facing direction.</param>
        public void SetPose(Vector2Int anchorCell, GridDirection direction)
        {
            _anchorCell = anchorCell;
            _direction = direction;
            RefreshOccupiedCells();
        }

        /// <summary>
        /// Sets the logical gameplay state. No transition rules are enforced yet.
        /// </summary>
        /// <param name="state">New gameplay state.</param>
        public void SetGameplayState(VehicleGameplayState state)
        {
            _gameplayState = state;
        }

        /// <summary>
        /// Sets the passenger count for future boarding systems.
        /// </summary>
        /// <param name="passengerCount">Non-negative passenger count.</param>
        public void SetPassengerCount(int passengerCount)
        {
            _passengerCount = passengerCount < 0 ? 0 : passengerCount;
        }

        /// <summary>
        /// Marks whether this vehicle is registered in an occupancy grid.
        /// Intended for use by <see cref="VehicleOccupancyGrid"/> only.
        /// </summary>
        /// <param name="isRegistered">Registration flag.</param>
        internal void SetOccupancyRegistration(bool isRegistered)
        {
            _isRegisteredInOccupancy = isRegistered;
        }

        private void RefreshOccupiedCells()
        {
            int width = FootprintWidth;
            int length = FootprintLength;
            VehicleFootprintUtility.GetOccupiedCells(_anchorCell, _direction, width, length, _occupiedCells);
        }
    }
}
