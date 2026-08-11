using NUnit.Framework;
using SCJam.WaitingAreaSystem;

namespace SCJam.Tests.WaitingAreaSystem
{
    public class WaitingAreaManagerTests
    {
        [Test]
        public void TryReserveSlot_ReturnsTrue_AndReservesTheFirstAvailableSlot()
        {
            WaitingAreaManager manager = new(3);

            bool reserved = manager.TryReserveSlot(1, out WaitingSlot slot);

            Assert.IsTrue(reserved);
            Assert.AreEqual(0, slot.Index);
            Assert.AreEqual(WaitingSlotState.Reserved, slot.State);
            Assert.AreEqual(1, slot.VehicleId);
        }

        [Test]
        public void TryReserveSlot_SkipsSlotsThatAreNotAvailable()
        {
            WaitingAreaManager manager = new(2);
            manager.TryReserveSlot(1, out WaitingSlot firstSlot);

            bool reserved = manager.TryReserveSlot(2, out WaitingSlot secondSlot);

            Assert.IsTrue(reserved);
            Assert.AreEqual(1, secondSlot.Index);
            Assert.AreNotEqual(firstSlot.Index, secondSlot.Index);
        }

        [Test]
        public void TryReserveSlot_ReturnsFalse_WhenNoSlotsAreAvailable()
        {
            WaitingAreaManager manager = new(1);
            manager.TryReserveSlot(1, out _);

            bool reserved = manager.TryReserveSlot(2, out WaitingSlot slot);

            Assert.IsFalse(reserved);
            Assert.IsNull(slot);
        }

        [Test]
        public void ConfirmOccupied_TransitionsSlotToOccupied()
        {
            WaitingAreaManager manager = new(1);
            manager.TryReserveSlot(1, out WaitingSlot slot);

            manager.ConfirmOccupied(slot);

            Assert.AreEqual(WaitingSlotState.Occupied, slot.State);
        }

        [Test]
        public void ReleaseSlot_MakesItAvailableAndReservableAgain()
        {
            WaitingAreaManager manager = new(1);
            manager.TryReserveSlot(1, out WaitingSlot slot);
            manager.ConfirmOccupied(slot);

            manager.ReleaseSlot(slot);

            Assert.AreEqual(WaitingSlotState.Available, slot.State);
            Assert.IsTrue(manager.TryReserveSlot(2, out WaitingSlot reReservedSlot));
            Assert.AreEqual(slot.Index, reReservedSlot.Index);
        }
    }
}
