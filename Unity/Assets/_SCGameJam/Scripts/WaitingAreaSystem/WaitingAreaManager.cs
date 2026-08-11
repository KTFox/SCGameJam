using System.Collections.Generic;

namespace SCJam.WaitingAreaSystem
{
    public sealed class WaitingAreaManager
    {
        private readonly List<WaitingSlot> _slots;


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
        /// First-available only; priority-based selection arrives in M4 via IWaitingVehicleSelector.
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

        public void ConfirmOccupied(WaitingSlot slot) => slot.ConfirmOccupied();

        public void ReleaseSlot(WaitingSlot slot) => slot.Release();
    }
}
