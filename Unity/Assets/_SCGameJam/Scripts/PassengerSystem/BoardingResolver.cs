using System;
using System.Collections.Generic;
using SCJam.Common;
using SCJam.VehicleSystem;
using SCJam.WaitingAreaSystem;

namespace SCJam.PassengerSystem
{
    public sealed class BoardingResolver
    {
        private readonly PassengerQueue _passengerQueue;


        public BoardingResolver(PassengerQueue passengerQueue)
        {
            _passengerQueue = passengerQueue;
        }

        /// <summary>
        /// Picks the best waiting vehicle among candidates via selector, then attempts to board it.
        /// Drop-in for TryBoard(Vehicle) when multiple waiting vehicles may share the front group's color.
        /// </summary>
        public IReadOnlyList<Passenger> TryBoard(
            IReadOnlyList<WaitingVehicleEntry> waitingVehicles,
            PuzzleColor color,
            IWaitingVehicleSelector selector)
        {
            Vehicle vehicle = selector.SelectVehicle(waitingVehicles, color);
            return vehicle != null ? TryBoard(vehicle) : Array.Empty<Passenger>();
        }

        /// <summary>
        /// Matches the accessible front group of the queue against a waiting or already-boarding vehicle of
        /// the same color, boarding a single passenger at a time (one board call per matching passenger).
        /// A vehicle already in the Boarding state stays eligible while it has free seats, so passengers can
        /// keep streaming toward it without waiting for an earlier passenger's boarding animation to finish.
        /// Call CompleteBoarding once every pending boarding animation for the vehicle has finished.
        /// </summary>
        public IReadOnlyList<Passenger> TryBoard(Vehicle vehicle)
        {
            bool isBoardable = vehicle.State == VehicleState.Waiting
                || (vehicle.State == VehicleState.Boarding && vehicle.OccupiedSeatCount < vehicle.Capacity);
            if (!isBoardable)
                return Array.Empty<Passenger>();

            IReadOnlyList<Passenger> frontGroup = _passengerQueue.GetAccessibleFrontGroup();
            if (frontGroup.Count == 0 || frontGroup[0].Color != vehicle.Color)
                return Array.Empty<Passenger>();

            int remainingCapacity = vehicle.Capacity - vehicle.OccupiedSeatCount;
            int boardingCount = Math.Min(1, remainingCapacity);
            if (boardingCount <= 0)
                return Array.Empty<Passenger>();

            IReadOnlyList<Passenger> boardedPassengers = _passengerQueue.Dequeue(boardingCount);
            foreach (Passenger passenger in boardedPassengers)
            {
                passenger.ChangeState(PassengerState.MovingToVehicle);
            }

            vehicle.BoardPassengers(boardedPassengers.Count);
            vehicle.ChangeState(VehicleState.Boarding);

            return boardedPassengers;
        }

        /// <summary>
        /// Called once the boarding animation finishes; settles the vehicle into Full or back to Waiting
        /// so it can still receive a later matching front group.
        /// </summary>
        public void CompleteBoarding(Vehicle vehicle)
        {
            vehicle.ChangeState(vehicle.OccupiedSeatCount >= vehicle.Capacity ? VehicleState.Full : VehicleState.Waiting);
        }
    }
}
