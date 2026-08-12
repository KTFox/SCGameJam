using System.Collections.Generic;
using SCJam.Common;
using UnityEngine;

namespace SCJam.BoardSystem
{
    public sealed class BoardGrid
    {
        private readonly ParkingBoardData _boardData;
        private readonly Dictionary<Vector2Int, int> _vehicleIdByCell = new();
        private readonly Dictionary<int, HashSet<Vector2Int>> _cellsByVehicleId = new();


        public BoardGrid(ParkingBoardData boardData)
        {
            _boardData = boardData;
        }

        public bool IsCellInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _boardData.Width && cell.y >= 0 && cell.y < _boardData.Height;
        }

        /// <summary>
        /// Checks whether a cell is occupied, optionally ignoring a specific vehicle.
        /// </summary>
        public bool IsCellOccupied(Vector2Int cell, int? excludingVehicleId = null)
        {
            if (!_vehicleIdByCell.TryGetValue(cell, out int vehicleId))
                return false;

            return excludingVehicleId == null || vehicleId != excludingVehicleId.Value;
        }

        /// <summary>
        /// Returns the cells from the fromCell in the given direction until the boundary of the board.
        /// </summary>
        public IReadOnlyList<Vector2Int> GetCellsToBoundary(Vector2Int fromCell, GridDirection direction)
        {
            List<Vector2Int> cells = new();
            Vector2Int step = GetStep(direction);
            Vector2Int current = fromCell + step;

            while (IsCellInBounds(current))
            {
                cells.Add(current);
                current += step;
            }

            return cells;
        }

        public void PlaceVehicle(int vehicleId, IReadOnlyCollection<Vector2Int> cells)
        {
            HashSet<Vector2Int> cellSet = new(cells);
            _cellsByVehicleId[vehicleId] = cellSet;

            foreach (Vector2Int cell in cellSet)
            {
                _vehicleIdByCell[cell] = vehicleId;
            }
        }

        public void RemoveVehicle(int vehicleId)
        {
            if (!_cellsByVehicleId.TryGetValue(vehicleId, out HashSet<Vector2Int> cells))
                return;

            foreach (Vector2Int cell in cells)
            {
                _vehicleIdByCell.Remove(cell);
            }

            _cellsByVehicleId.Remove(vehicleId);
        }

        private static Vector2Int GetStep(GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => new Vector2Int(0, 1),
                GridDirection.Down => new Vector2Int(0, -1),
                GridDirection.Left => new Vector2Int(-1, 0),
                GridDirection.Right => new Vector2Int(1, 0),
                _ => Vector2Int.zero
            };
        }
    }
}
