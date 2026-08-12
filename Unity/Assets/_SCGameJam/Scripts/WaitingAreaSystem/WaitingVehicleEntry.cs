using SCJam.VehicleSystem;

namespace SCJam.WaitingAreaSystem
{
    /// <summary>
    /// Pairs a waiting vehicle with the slot it occupies, so an IWaitingVehicleSelector can rank
    /// candidates without needing a separate lookup between the two systems.
    /// </summary>
    public readonly struct WaitingVehicleEntry
    {
        public Vehicle Vehicle { get; }
        public WaitingSlot Slot { get; }


        public WaitingVehicleEntry(Vehicle vehicle, WaitingSlot slot)
        {
            Vehicle = vehicle;
            Slot = slot;
        }
    }
}
