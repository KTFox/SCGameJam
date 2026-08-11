using System.Collections.Generic;
using SCJam.Common;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    public sealed class Vehicle
    {
        private readonly List<Vector2Int> _gridFootprint;


        public int Id { get; }
        public PuzzleColor Color { get; }
        public int Capacity { get; }
        public int OccupiedSeatCount { get; private set; }
        public IReadOnlyList<Vector2Int> GridFootprint => _gridFootprint;
        public GridDirection MovementDirection { get; }
        public VehicleState State { get; private set; }


        public Vehicle(int id, PuzzleColor color, int capacity, IReadOnlyList<Vector2Int> initialFootprint, GridDirection movementDirection)
        {
            Id = id;
            Color = color;
            Capacity = capacity;
            MovementDirection = movementDirection;
            State = VehicleState.Parked;
            _gridFootprint = new List<Vector2Int>(initialFootprint);
        }

        public void ChangeState(VehicleState newState)
        {
            State = newState;
        }

        public void BoardPassengers(int count)
        {
            OccupiedSeatCount += count;
        }

        /// <summary>
        /// Called once the vehicle's footprint has fully left the board (footprint-clear release rule).
        /// </summary>
        public void ClearFootprint()
        {
            _gridFootprint.Clear();
        }
    }
}
