namespace SCJam.WaitingAreaSystem
{
    public sealed class WaitingSlot
    {
        public int Index { get; }
        public WaitingSlotState State { get; private set; }
        public int? VehicleId { get; private set; }
        public int ArrivalOrder { get; private set; }


        public WaitingSlot(int index)
        {
            Index = index;
            State = WaitingSlotState.Available;
            ArrivalOrder = -1;
        }

        public void Reserve(int vehicleId)
        {
            State = WaitingSlotState.Reserved;
            VehicleId = vehicleId;
        }

        /// <summary>
        /// arrivalOrder is a monotonic sequence number stamped by WaitingAreaManager, used to break
        /// matching-priority ties by earliest-arrived (see IWaitingVehicleSelector).
        /// </summary>
        public void ConfirmOccupied(int arrivalOrder)
        {
            State = WaitingSlotState.Occupied;
            ArrivalOrder = arrivalOrder;
        }

        public void Release()
        {
            State = WaitingSlotState.Available;
            VehicleId = null;
            ArrivalOrder = -1;
        }
    }
}
