using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Runtime-only occupancy map for vehicles on a fixed rectangular board.
    /// Stores vehicle runtime references in a flat array; never uses GameObjects as truth.
    /// </summary>
    public sealed class VehicleOccupancyGrid
    {
        private readonly int _width;
        private readonly int _height;
        private readonly VehicleRuntimeState[] _occupants;
        private readonly HashSet<VehicleRuntimeState> _registeredVehicles = new HashSet<VehicleRuntimeState>();

        /// <summary>
        /// Creates an empty occupancy grid.
        /// </summary>
        /// <param name="width">Board width in cells.</param>
        /// <param name="height">Board height in cells.</param>
        public VehicleOccupancyGrid(int width, int height)
        {
            _width = width > 0 ? width : 0;
            _height = height > 0 ? height : 0;
            _occupants = new VehicleRuntimeState[_width * _height];
        }

        /// <summary>
        /// Gets the board width in cells.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Gets the board height in cells.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Gets the number of currently registered vehicles.
        /// </summary>
        public int RegisteredVehicleCount => _registeredVehicles.Count;

        /// <summary>
        /// Returns true when the cell lies inside the board.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True if inside.</returns>
        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < _width
                && cell.y < _height;
        }

        /// <summary>
        /// Returns true when the cell is occupied by any vehicle.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True if occupied.</returns>
        public bool IsOccupied(Vector2Int cell)
        {
            if (!IsInside(cell))
            {
                return false;
            }

            return _occupants[ToIndex(cell)] != null;
        }

        /// <summary>
        /// Attempts to get the vehicle occupying a cell.
        /// </summary>
        /// <param name="cell">Cell to query.</param>
        /// <param name="vehicle">Occupying vehicle when found.</param>
        /// <returns>True when an occupant exists.</returns>
        public bool TryGetOccupant(Vector2Int cell, out VehicleRuntimeState vehicle)
        {
            vehicle = null;
            if (!IsInside(cell))
            {
                return false;
            }

            vehicle = _occupants[ToIndex(cell)];
            return vehicle != null;
        }

        /// <summary>
        /// Returns true when every provided cell is inside the board and free.
        /// </summary>
        /// <param name="cells">Cells to test.</param>
        /// <returns>True when the set can be occupied.</returns>
        public bool CanOccupy(IReadOnlyList<Vector2Int> cells)
        {
            return CanOccupy(cells, null);
        }

        /// <summary>
        /// Returns true when every provided cell is inside the board and free,
        /// treating cells already owned by <paramref name="ignoreVehicle"/> as available.
        /// </summary>
        /// <param name="cells">Cells to test.</param>
        /// <param name="ignoreVehicle">Optional vehicle whose current cells may be reused.</param>
        /// <returns>True when the set can be occupied.</returns>
        public bool CanOccupy(IReadOnlyList<Vector2Int> cells, VehicleRuntimeState ignoreVehicle)
        {
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                if (!IsInside(cell))
                {
                    return false;
                }

                VehicleRuntimeState occupant = _occupants[ToIndex(cell)];
                if (occupant != null && occupant != ignoreVehicle)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Registers a vehicle footprint. Fails safely without partial writes.
        /// </summary>
        /// <param name="vehicle">Vehicle to register.</param>
        /// <returns>True when registration succeeds.</returns>
        public bool TryRegister(VehicleRuntimeState vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            if (_registeredVehicles.Contains(vehicle) || vehicle.IsRegisteredInOccupancy)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> cells = vehicle.OccupiedCells;
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            if (!CanOccupy(cells))
            {
                return false;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                _occupants[ToIndex(cells[i])] = vehicle;
            }

            _registeredVehicles.Add(vehicle);
            vehicle.SetOccupancyRegistration(true);
            return true;
        }

        /// <summary>
        /// Unregisters a vehicle footprint.
        /// </summary>
        /// <param name="vehicle">Vehicle to unregister.</param>
        /// <returns>True when the vehicle was registered and is now removed.</returns>
        public bool TryUnregister(VehicleRuntimeState vehicle)
        {
            if (vehicle == null || !_registeredVehicles.Contains(vehicle))
            {
                return false;
            }

            IReadOnlyList<Vector2Int> cells = vehicle.OccupiedCells;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    Vector2Int cell = cells[i];
                    if (!IsInside(cell))
                    {
                        continue;
                    }

                    int index = ToIndex(cell);
                    if (_occupants[index] == vehicle)
                    {
                        _occupants[index] = null;
                    }
                }
            }

            _registeredVehicles.Remove(vehicle);
            vehicle.SetOccupancyRegistration(false);
            return true;
        }

        /// <summary>
        /// Clears all occupancy data and registration flags.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                _occupants[i] = null;
            }

            foreach (VehicleRuntimeState vehicle in _registeredVehicles)
            {
                if (vehicle != null)
                {
                    vehicle.SetOccupancyRegistration(false);
                }
            }

            _registeredVehicles.Clear();
        }

        /// <summary>
        /// Copies currently occupied cells into <paramref name="results"/> for debugging.
        /// Clears the destination list first. The caller owns the list.
        /// </summary>
        /// <param name="results">Destination list.</param>
        public void CopyOccupiedCells(List<Vector2Int> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_occupants[y * _width + x] != null)
                    {
                        results.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        private int ToIndex(Vector2Int cell)
        {
            return cell.y * _width + cell.x;
        }
    }
}
