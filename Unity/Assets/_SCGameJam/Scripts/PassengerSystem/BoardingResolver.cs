using System;
using System.Collections.Generic;
using SCJam.VehicleSystem;

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
        /// Matches the accessible front group of the queue against a waiting vehicle of the same color,
        /// boarding as many as its remaining capacity allows and moving the vehicle into the Boarding
        /// state. Call CompleteBoarding once the boarding animation finishes.
        /// </summary>
        public IReadOnlyList<Passenger> TryBoard(Vehicle vehicle)
        {
            if (vehicle.State != VehicleState.Waiting)
                return Array.Empty<Passenger>();

            IReadOnlyList<Passenger> frontGroup = _passengerQueue.GetAccessibleFrontGroup();
            if (frontGroup.Count == 0 || frontGroup[0].Color != vehicle.Color)
                return Array.Empty<Passenger>();

            int remainingCapacity = vehicle.Capacity - vehicle.OccupiedSeatCount;
            int boardingCount = Math.Min(frontGroup.Count, remainingCapacity);
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
