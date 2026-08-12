using System.Collections.Generic;

namespace SCJam.WaitingAreaSystem
{
    public sealed class WaitingAreaManager
    {
        private readonly List<WaitingSlot> _slots;
        private int _nextArrivalOrder;


        public IReadOnlyList<WaitingSlot> Slots => _slots;


        public WaitingAreaManager(int slotCount)
        {
            _slots = new List<WaitingSlot>(slotCount);

            for (int i = 0; i < slotCount; i++)
            {
                _slots.Add(new WaitingSlot(i));
            }
        }

        /// <summary>
        /// First-available only. Priority among already-occupied waiting vehicles for boarding is
        /// handled separately by IWaitingVehicleSelector, not by slot reservation order.
        /// </summary>
        public bool TryReserveSlot(int vehicleId, out WaitingSlot reservedSlot)
        {
            foreach (WaitingSlot slot in _slots)
            {
                if (slot.State != WaitingSlotState.Available)
                    continue;

                slot.Reserve(vehicleId);
                reservedSlot = slot;
                return true;
            }

            reservedSlot = null;
            return false;
        }

        public void ConfirmOccupied(WaitingSlot slot) => slot.ConfirmOccupied(_nextArrivalOrder++);

        public void ReleaseSlot(WaitingSlot slot) => slot.Release();
    }
}
