using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Mutable runtime session for one loaded level.
    /// Remains independent of scene GameObjects and never mutates the source definition.
    /// </summary>
    public sealed class LevelRuntimeState
    {
        private readonly List<VehicleRuntimeState> _vehicles = new List<VehicleRuntimeState>();
        private readonly Dictionary<string, VehicleRuntimeState> _vehiclesById =
            new Dictionary<string, VehicleRuntimeState>();
        private readonly IReadOnlyList<VehicleRuntimeState> _vehiclesReadOnly;

        private LevelSO _definition;
        private VehicleOccupancyGrid _occupancyGrid;
        private LevelLifecycleState _lifecycleState = LevelLifecycleState.None;
        private bool _isInitialized;

        /// <summary>
        /// Creates an uninitialized runtime state.
        /// </summary>
        public LevelRuntimeState()
        {
            _vehiclesReadOnly = _vehicles.AsReadOnly();
        }

        /// <summary>
        /// Gets the immutable source level definition.
        /// </summary>
        public LevelSO Definition => _definition;

        /// <summary>
        /// Gets the current lifecycle state.
        /// </summary>
        public LevelLifecycleState LifecycleState => _lifecycleState;

        /// <summary>
        /// Gets whether initialization completed successfully.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the runtime occupancy grid.
        /// </summary>
        public VehicleOccupancyGrid OccupancyGrid => _occupancyGrid;

        /// <summary>
        /// Gets the runtime vehicles as a read-only list.
        /// </summary>
        public IReadOnlyList<VehicleRuntimeState> Vehicles => _vehiclesReadOnly;

        /// <summary>
        /// Attempts to initialize runtime state from a level definition.
        /// On failure, no partial occupancy remains registered.
        /// </summary>
        /// <param name="definition">Immutable level definition.</param>
        /// <param name="runtimeState">Created runtime state on success.</param>
        /// <param name="issues">Validation issues from the attempt.</param>
        /// <returns>True when initialization succeeds.</returns>
        public static bool TryInitialize(
            LevelSO definition,
            out LevelRuntimeState runtimeState,
            out IReadOnlyList<LevelValidationIssue> issues)
        {
            runtimeState = null;
            LevelValidationResult validation = LevelDefinitionValidator.Validate(definition);
            issues = validation.Issues;

            if (!validation.IsValid)
            {
                return false;
            }

            var state = new LevelRuntimeState();
            if (!state.InitializeInternal(definition, out issues))
            {
                state.Dispose();
                return false;
            }

            runtimeState = state;
            return true;
        }

        /// <summary>
        /// Clears any existing session and initializes from the provided definition.
        /// </summary>
        /// <param name="definition">Immutable level definition.</param>
        /// <param name="issues">Validation or initialization issues.</param>
        /// <returns>True when initialization succeeds.</returns>
        public bool TryReinitialize(
            LevelSO definition,
            out IReadOnlyList<LevelValidationIssue> issues)
        {
            Dispose();
            _lifecycleState = LevelLifecycleState.None;

            LevelValidationResult validation = LevelDefinitionValidator.Validate(definition);
            issues = validation.Issues;
            if (!validation.IsValid)
            {
                return false;
            }

            return InitializeInternal(definition, out issues);
        }

        /// <summary>
        /// Attempts to find a vehicle by stable ID.
        /// </summary>
        /// <param name="vehicleId">Stable vehicle ID.</param>
        /// <param name="vehicle">Found vehicle.</param>
        /// <returns>True when found.</returns>
        public bool TryGetVehicle(string vehicleId, out VehicleRuntimeState vehicle)
        {
            if (string.IsNullOrEmpty(vehicleId))
            {
                vehicle = null;
                return false;
            }

            return _vehiclesById.TryGetValue(vehicleId, out vehicle);
        }

        /// <summary>
        /// Updates a vehicle pose while keeping occupancy consistent.
        /// Unregisters, applies pose, then re-registers. Rolls back on failure.
        /// </summary>
        /// <param name="vehicle">Vehicle to update.</param>
        /// <param name="anchorCell">New anchor cell.</param>
        /// <param name="direction">New direction.</param>
        /// <returns>True when occupancy accepts the new pose.</returns>
        public bool TrySetVehiclePose(
            VehicleRuntimeState vehicle,
            Vector2Int anchorCell,
            GridDirection direction)
        {
            if (!_isInitialized
                || _lifecycleState == LevelLifecycleState.Disposed
                || vehicle == null
                || _occupancyGrid == null
                || !_vehiclesById.ContainsKey(vehicle.VehicleId))
            {
                return false;
            }

            Vector2Int previousAnchor = vehicle.AnchorCell;
            GridDirection previousDirection = vehicle.Direction;
            bool wasRegistered = vehicle.IsRegisteredInOccupancy;

            if (wasRegistered)
            {
                _occupancyGrid.TryUnregister(vehicle);
            }

            vehicle.SetPose(anchorCell, direction);

            if (!_occupancyGrid.TryRegister(vehicle))
            {
                vehicle.SetPose(previousAnchor, previousDirection);
                if (wasRegistered)
                {
                    _occupancyGrid.TryRegister(vehicle);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Clears runtime data so the instance can be reinitialized.
        /// </summary>
        public void Dispose()
        {
            if (_occupancyGrid != null)
            {
                _occupancyGrid.Clear();
            }

            _vehicles.Clear();
            _vehiclesById.Clear();
            _occupancyGrid = null;
            _definition = null;
            _isInitialized = false;
            _lifecycleState = LevelLifecycleState.Disposed;
        }

        private bool InitializeInternal(
            LevelSO definition,
            out IReadOnlyList<LevelValidationIssue> issues)
        {
            var localIssues = new List<LevelValidationIssue>();
            issues = localIssues;

            _lifecycleState = LevelLifecycleState.Initializing;
            _definition = definition;
            _occupancyGrid = new VehicleOccupancyGrid(definition.GridWidth, definition.GridHeight);

            IReadOnlyList<VehiclePlacementDefinition> placements = definition.VehiclePlacements;
            for (int i = 0; i < placements.Count; i++)
            {
                VehiclePlacementDefinition placement = placements[i];
                var vehicle = new VehicleRuntimeState(placement);

                if (!_occupancyGrid.TryRegister(vehicle))
                {
                    localIssues.Add(new LevelValidationIssue(
                        ValidationSeverity.Error,
                        "Failed to register vehicle footprint during initialization.",
                        vehicleId: vehicle.VehicleId,
                        cell: vehicle.AnchorCell,
                        context: definition));

                    _occupancyGrid.Clear();
                    _vehicles.Clear();
                    _vehiclesById.Clear();
                    _isInitialized = false;
                    _lifecycleState = LevelLifecycleState.None;
                    return false;
                }

                _vehicles.Add(vehicle);
                _vehiclesById[vehicle.VehicleId] = vehicle;
            }

            _isInitialized = true;
            _lifecycleState = LevelLifecycleState.Playing;
            return true;
        }
    }
}
