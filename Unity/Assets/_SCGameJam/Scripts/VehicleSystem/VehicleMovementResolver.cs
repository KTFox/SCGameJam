using System.Collections.Generic;
using SCJam.BoardSystem;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    public sealed class VehicleMovementResolver
    {
        private readonly BoardGrid _boardGrid;


        public VehicleMovementResolver(BoardGrid boardGrid)
        {
            _boardGrid = boardGrid;
        }

        public bool IsPathClear(Vehicle vehicle)
        {
            foreach (Vector2Int cell in GetSweptFootprintCells(vehicle))
            {
                if (_boardGrid.IsCellBlocked(cell) || _boardGrid.IsCellOccupied(cell, vehicle.Id))
                    return false;
            }

            return true;
        }

        public IReadOnlyList<Vector2Int> GetSweptFootprintCells(Vehicle vehicle)
        {
            HashSet<Vector2Int> sweptCells = new();

            foreach (Vector2Int cell in vehicle.GridFootprint)
            {
                foreach (Vector2Int boundaryCell in _boardGrid.GetCellsToBoundary(cell, vehicle.MovementDirection))
                {
                    sweptCells.Add(boundaryCell);
                }
            }

            return new List<Vector2Int>(sweptCells);
        }

        public int GetExitCellDistance(Vehicle vehicle)
        {
            int maxDistance = 0;

            foreach (Vector2Int cell in vehicle.GridFootprint)
            {
                int distance = _boardGrid.GetCellsToBoundary(cell, vehicle.MovementDirection).Count + 1;
                maxDistance = Mathf.Max(maxDistance, distance);
            }

            return maxDistance;
        }
    }
}
