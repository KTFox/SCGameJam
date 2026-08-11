using System;
using System.Collections.Generic;
using SCJam.Common;
using UnityEngine;

namespace SCJam.BoardSystem
{
    public sealed class ParkingBoardData
    {
        private readonly HashSet<Vector2Int> _blockedCells;


        public int Width { get; }
        public int Height { get; }
        public GridDirection ExitDirection { get; }
        public IReadOnlyCollection<Vector2Int> BlockedCells => _blockedCells;


        public ParkingBoardData(int width, int height, IEnumerable<Vector2Int> blockedCells, GridDirection exitDirection)
        {
            Width = width;
            Height = height;
            ExitDirection = exitDirection;
            _blockedCells = new HashSet<Vector2Int>(blockedCells ?? Array.Empty<Vector2Int>());
        }

        public bool IsCellBlocked(Vector2Int cell) => _blockedCells.Contains(cell);
    }
}
