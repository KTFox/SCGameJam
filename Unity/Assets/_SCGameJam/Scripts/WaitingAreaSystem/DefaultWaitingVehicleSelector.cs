using System.Collections.Generic;
using SCJam.Common;
using SCJam.VehicleSystem;

namespace SCJam.WaitingAreaSystem
{
    /// <summary>
    /// Recommended matching priority per WaitingSlot.md: highest occupied-seat count, then
    /// earliest-arrived, then lowest waiting-slot index.
    /// </summary>
    public sealed class DefaultWaitingVehicleSelector : IWaitingVehicleSelector
    {
        public Vehicle SelectVehicle(IReadOnlyList<WaitingVehicleEntry> waitingVehicles, PuzzleColor color)
        {
            WaitingVehicleEntry? bestEntry = null;

            foreach (WaitingVehicleEntry entry in waitingVehicles)
            {
                if (entry.Vehicle.State != VehicleState.Waiting || entry.Vehicle.Color != color)
                    continue;

                if (bestEntry == null || IsHigherPriority(entry, bestEntry.Value))
                    bestEntry = entry;
            }

            return bestEntry?.Vehicle;
        }

        private static bool IsHigherPriority(WaitingVehicleEntry candidate, WaitingVehicleEntry current)
        {
            if (candidate.Vehicle.OccupiedSeatCount != current.Vehicle.OccupiedSeatCount)
                return candidate.Vehicle.OccupiedSeatCount > current.Vehicle.OccupiedSeatCount;

            if (candidate.Slot.ArrivalOrder != current.Slot.ArrivalOrder)
                return candidate.Slot.ArrivalOrder < current.Slot.ArrivalOrder;

            return candidate.Slot.Index < current.Slot.Index;
        }
    }
}
