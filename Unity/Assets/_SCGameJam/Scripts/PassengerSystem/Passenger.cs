using SCJam.Common;

namespace SCJam.PassengerSystem
{
    public sealed class Passenger
    {
        public int Id { get; }
        public PuzzleColor Color { get; }
        public int QueueIndex { get; }
        public PassengerState State { get; private set; }


        public Passenger(int id, PuzzleColor color, int queueIndex)
        {
            Id = id;
            Color = color;
            QueueIndex = queueIndex;
            State = PassengerState.Queued;
        }

        public void ChangeState(PassengerState newState)
        {
            State = newState;
        }
    }
}
