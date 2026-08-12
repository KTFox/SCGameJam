using System;
using SCJam.Common;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Authoring data for a single vehicle spawned by LevelController: which VehicleConfig to use,
    /// the board cell its footprint originates from, and the fixed direction it slides toward the exit.
    /// </summary>
    [Serializable]
    public sealed class VehiclePlacement
    {
        [SerializeField] private VehicleConfig _vehicleConfig;
        [SerializeField] private Vector2Int _originCell;
        [SerializeField] private GridDirection _movementDirection;


        public VehicleConfig VehicleConfig => _vehicleConfig;
        public Vector2Int OriginCell => _originCell;
        public GridDirection MovementDirection => _movementDirection;
    }
}
