namespace SCJam.WaitingAreaSystem
{
    public sealed class WaitingSlot
    {
        public int Index { get; }
        public WaitingSlotState State { get; private set; }
        public int? VehicleId { get; private set; }


        public WaitingSlot(int index)
        {
            Index = index;
            State = WaitingSlotState.Available;
        }

        public void Reserve(int vehicleId)
        {
            State = WaitingSlotState.Reserved;
            VehicleId = vehicleId;
        }

        public void ConfirmOccupied()
        {
            State = WaitingSlotState.Occupied;
        }

        public void Release()
        {
            State = WaitingSlotState.Available;
            VehicleId = null;
        }
    }
}
