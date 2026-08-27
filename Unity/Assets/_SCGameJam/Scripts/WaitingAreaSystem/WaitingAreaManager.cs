using System.Collections.Generic;

namespace SCJam.WaitingAreaSystem
{
    public sealed class WaitingAreaManager
    {
        private readonly List<WaitingSlot> _slots;
        private int _nextArrivalOrder;


        public IReadOnlyList<WaitingSlot> Slots => _slots;

        public bool HasAvailableSlot
        {
            get
            {
                foreach (WaitingSlot slot in _slots)
                {
                    if (slot.State == WaitingSlotState.Available)
                        return true;
                }

                return false;
            }
        }


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

        /// <summary>
        /// Appends one more Available slot at the next index and returns it. Used by the "add waiting slot"
        /// booster; existing slots (and any vehicles occupying them) are left untouched.
        /// </summary>
        public WaitingSlot AddSlot()
        {
            WaitingSlot slot = new(_slots.Count);
            _slots.Add(slot);
            return slot;
        }
    }
}
