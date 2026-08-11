using System;
using System.Collections.Generic;
using SCJam.Common;

namespace SCJam.PassengerSystem
{
    public sealed class PassengerQueue
    {
        private readonly List<Passenger> _passengers;


        public IReadOnlyList<Passenger> Passengers => _passengers;


        public PassengerQueue(IEnumerable<Passenger> passengers)
        {
            _passengers = new List<Passenger>(passengers);
        }

        /// <summary>
        /// Grouped-front rule: all consecutive passengers of the same color at the front of the queue.
        /// </summary>
        public IReadOnlyList<Passenger> GetAccessibleFrontGroup()
        {
            List<Passenger> group = new();

            if (_passengers.Count == 0)
                return group;

            PuzzleColor frontColor = _passengers[0].Color;

            foreach (Passenger passenger in _passengers)
            {
                if (passenger.Color != frontColor)
                    break;

                group.Add(passenger);
            }

            return group;
        }

        /// <summary>
        /// Removes up to count passengers from the front of the queue and returns them.
        /// </summary>
        public IReadOnlyList<Passenger> Dequeue(int count)
        {
            int actualCount = Math.Min(count, _passengers.Count);
            List<Passenger> dequeued = _passengers.GetRange(0, actualCount);
            _passengers.RemoveRange(0, actualCount);
            return dequeued;
        }
    }
}
