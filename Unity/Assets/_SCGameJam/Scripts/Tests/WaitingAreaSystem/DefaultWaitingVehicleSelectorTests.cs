using NUnit.Framework;
using SCJam.Common;
using SCJam.VehicleSystem;
using SCJam.WaitingAreaSystem;
using UnityEngine;

namespace SCJam.Tests.WaitingAreaSystem
{
    public class DefaultWaitingVehicleSelectorTests
    {
        private static Vehicle CreateWaitingVehicle(int id, PuzzleColor color, int occupiedSeatCount)
        {
            Vehicle vehicle = new(id, color, capacity: 4, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            vehicle.ChangeState(VehicleState.Waiting);

            if (occupiedSeatCount > 0)
                vehicle.BoardPassengers(occupiedSeatCount);

            return vehicle;
        }

        private static WaitingSlot CreateOccupiedSlot(int index, int arrivalOrder)
        {
            WaitingSlot slot = new(index);
            slot.Reserve(vehicleId: index);
            slot.ConfirmOccupied(arrivalOrder);
            return slot;
        }

        [Test]
        public void SelectVehicle_ReturnsNull_WhenNoCandidateMatchesColor()
        {
            WaitingVehicleEntry[] entries =
            {
                new(CreateWaitingVehicle(1, PuzzleColor.Orange, 0), CreateOccupiedSlot(0, 0))
            };
            DefaultWaitingVehicleSelector selector = new();

            Vehicle selected = selector.SelectVehicle(entries, PuzzleColor.Pink);

            Assert.IsNull(selected);
        }

        [Test]
        public void SelectVehicle_IgnoresVehiclesThatAreNotWaiting()
        {
            Vehicle notWaiting = new(1, PuzzleColor.Orange, 4, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            WaitingVehicleEntry[] entries = { new(notWaiting, CreateOccupiedSlot(0, 0)) };
            DefaultWaitingVehicleSelector selector = new();

            Vehicle selected = selector.SelectVehicle(entries, PuzzleColor.Orange);

            Assert.IsNull(selected);
        }

        [Test]
        public void SelectVehicle_PrefersHighestOccupiedSeatCount()
        {
            Vehicle lessFull = CreateWaitingVehicle(1, PuzzleColor.Orange, occupiedSeatCount: 1);
            Vehicle mostFull = CreateWaitingVehicle(2, PuzzleColor.Orange, occupiedSeatCount: 3);
            WaitingVehicleEntry[] entries =
            {
                new(lessFull, CreateOccupiedSlot(0, arrivalOrder: 0)),
                new(mostFull, CreateOccupiedSlot(1, arrivalOrder: 1))
            };
            DefaultWaitingVehicleSelector selector = new();

            Vehicle selected = selector.SelectVehicle(entries, PuzzleColor.Orange);

            Assert.AreEqual(mostFull.Id, selected.Id);
        }

        [Test]
        public void SelectVehicle_BreaksSeatCountTies_WithEarliestArrived()
        {
            Vehicle arrivedFirst = CreateWaitingVehicle(1, PuzzleColor.Orange, occupiedSeatCount: 2);
            Vehicle arrivedSecond = CreateWaitingVehicle(2, PuzzleColor.Orange, occupiedSeatCount: 2);
            WaitingVehicleEntry[] entries =
            {
                new(arrivedSecond, CreateOccupiedSlot(0, arrivalOrder: 1)),
                new(arrivedFirst, CreateOccupiedSlot(1, arrivalOrder: 0))
            };
            DefaultWaitingVehicleSelector selector = new();

            Vehicle selected = selector.SelectVehicle(entries, PuzzleColor.Orange);

            Assert.AreEqual(arrivedFirst.Id, selected.Id);
        }

        [Test]
        public void SelectVehicle_BreaksRemainingTies_WithLowestSlotIndex()
        {
            Vehicle higherSlot = CreateWaitingVehicle(1, PuzzleColor.Orange, occupiedSeatCount: 2);
            Vehicle lowerSlot = CreateWaitingVehicle(2, PuzzleColor.Orange, occupiedSeatCount: 2);
            WaitingVehicleEntry[] entries =
            {
                new(higherSlot, CreateOccupiedSlot(2, arrivalOrder: 0)),
                new(lowerSlot, CreateOccupiedSlot(0, arrivalOrder: 0))
            };
            DefaultWaitingVehicleSelector selector = new();

            Vehicle selected = selector.SelectVehicle(entries, PuzzleColor.Orange);

            Assert.AreEqual(lowerSlot.Id, selected.Id);
        }
    }
}
