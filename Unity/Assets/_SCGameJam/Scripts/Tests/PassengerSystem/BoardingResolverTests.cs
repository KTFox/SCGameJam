using System.Collections.Generic;
using NUnit.Framework;
using SCJam.Common;
using SCJam.PassengerSystem;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.Tests.PassengerSystem
{
    public class BoardingResolverTests
    {
        private static Vehicle CreateWaitingVehicle(PuzzleColor color, int capacity, int occupiedSeatCount = 0)
        {
            Vehicle vehicle = new(1, color, capacity, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            vehicle.ChangeState(VehicleState.Waiting);

            if (occupiedSeatCount > 0)
                vehicle.BoardPassengers(occupiedSeatCount);

            return vehicle;
        }

        private static Passenger CreatePassenger(int id, PuzzleColor color) => new(id, color, id);

        [Test]
        public void TryBoard_BoardsWholeFrontGroup_WhenCapacityCoversIt()
        {
            PassengerQueue queue = new(new[]
            {
                CreatePassenger(1, PuzzleColor.Orange),
                CreatePassenger(2, PuzzleColor.Orange),
                CreatePassenger(3, PuzzleColor.Pink)
            });
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 4);
            BoardingResolver resolver = new(queue);

            IReadOnlyList<Passenger> boarded = resolver.TryBoard(vehicle);

            Assert.AreEqual(2, boarded.Count);
            Assert.AreEqual(2, vehicle.OccupiedSeatCount);
            Assert.AreEqual(VehicleState.Boarding, vehicle.State);
            Assert.AreEqual(1, queue.Passengers.Count);
            Assert.AreEqual(PassengerState.MovingToVehicle, boarded[0].State);
        }

        [Test]
        public void TryBoard_BoardsOnlyUpToRemainingCapacity_WhenFrontGroupIsLarger()
        {
            PassengerQueue queue = new(new[]
            {
                CreatePassenger(1, PuzzleColor.Orange),
                CreatePassenger(2, PuzzleColor.Orange),
                CreatePassenger(3, PuzzleColor.Orange)
            });
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 4, occupiedSeatCount: 3);
            BoardingResolver resolver = new(queue);

            IReadOnlyList<Passenger> boarded = resolver.TryBoard(vehicle);

            Assert.AreEqual(1, boarded.Count);
            Assert.AreEqual(4, vehicle.OccupiedSeatCount);
            Assert.AreEqual(2, queue.Passengers.Count);
        }

        [Test]
        public void TryBoard_ReturnsEmpty_WhenFrontGroupColorDoesNotMatchVehicle()
        {
            PassengerQueue queue = new(new[] { CreatePassenger(1, PuzzleColor.Pink) });
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 4);
            BoardingResolver resolver = new(queue);

            IReadOnlyList<Passenger> boarded = resolver.TryBoard(vehicle);

            Assert.AreEqual(0, boarded.Count);
            Assert.AreEqual(0, vehicle.OccupiedSeatCount);
            Assert.AreEqual(VehicleState.Waiting, vehicle.State);
        }

        [Test]
        public void TryBoard_ReturnsEmpty_WhenVehicleIsNotWaiting()
        {
            PassengerQueue queue = new(new[] { CreatePassenger(1, PuzzleColor.Orange) });
            Vehicle vehicle = new(1, PuzzleColor.Orange, 4, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            BoardingResolver resolver = new(queue);

            IReadOnlyList<Passenger> boarded = resolver.TryBoard(vehicle);

            Assert.AreEqual(0, boarded.Count);
            Assert.AreEqual(1, queue.Passengers.Count);
        }

        [Test]
        public void TryBoard_ReturnsEmpty_WhenVehicleIsAlreadyFull()
        {
            PassengerQueue queue = new(new[] { CreatePassenger(1, PuzzleColor.Orange) });
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 2, occupiedSeatCount: 2);
            BoardingResolver resolver = new(queue);

            IReadOnlyList<Passenger> boarded = resolver.TryBoard(vehicle);

            Assert.AreEqual(0, boarded.Count);
        }

        [Test]
        public void CompleteBoarding_TransitionsToFull_WhenCapacityReached()
        {
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 2, occupiedSeatCount: 2);
            vehicle.ChangeState(VehicleState.Boarding);
            BoardingResolver resolver = new(new PassengerQueue(new Passenger[0]));

            resolver.CompleteBoarding(vehicle);

            Assert.AreEqual(VehicleState.Full, vehicle.State);
        }

        [Test]
        public void CompleteBoarding_ReturnsToWaiting_WhenCapacityRemains()
        {
            Vehicle vehicle = CreateWaitingVehicle(PuzzleColor.Orange, capacity: 4, occupiedSeatCount: 2);
            vehicle.ChangeState(VehicleState.Boarding);
            BoardingResolver resolver = new(new PassengerQueue(new Passenger[0]));

            resolver.CompleteBoarding(vehicle);

            Assert.AreEqual(VehicleState.Waiting, vehicle.State);
        }
    }
}
