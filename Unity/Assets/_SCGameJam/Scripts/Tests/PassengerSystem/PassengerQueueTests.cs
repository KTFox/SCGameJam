using System.Collections.Generic;
using NUnit.Framework;
using SCJam.Common;
using SCJam.PassengerSystem;

namespace SCJam.Tests.PassengerSystem
{
    public class PassengerQueueTests
    {
        private static Passenger CreatePassenger(int id, PuzzleColor color) => new(id, color, id);

        [Test]
        public void GetAccessibleFrontGroup_ReturnsAllConsecutiveSameColorPassengersAtTheFront()
        {
            PassengerQueue queue = new(new[]
            {
                CreatePassenger(1, PuzzleColor.Orange),
                CreatePassenger(2, PuzzleColor.Orange),
                CreatePassenger(3, PuzzleColor.Pink)
            });

            IReadOnlyList<Passenger> frontGroup = queue.GetAccessibleFrontGroup();

            Assert.AreEqual(2, frontGroup.Count);
            Assert.AreEqual(1, frontGroup[0].Id);
            Assert.AreEqual(2, frontGroup[1].Id);
        }

        [Test]
        public void GetAccessibleFrontGroup_ReturnsSinglePassenger_WhenFrontColorChangesImmediately()
        {
            PassengerQueue queue = new(new[]
            {
                CreatePassenger(1, PuzzleColor.Orange),
                CreatePassenger(2, PuzzleColor.Pink)
            });

            IReadOnlyList<Passenger> frontGroup = queue.GetAccessibleFrontGroup();

            Assert.AreEqual(1, frontGroup.Count);
            Assert.AreEqual(1, frontGroup[0].Id);
        }

        [Test]
        public void GetAccessibleFrontGroup_ReturnsEmpty_WhenQueueIsEmpty()
        {
            PassengerQueue queue = new(new Passenger[0]);

            IReadOnlyList<Passenger> frontGroup = queue.GetAccessibleFrontGroup();

            Assert.AreEqual(0, frontGroup.Count);
        }

        [Test]
        public void Dequeue_RemovesAndReturnsPassengersFromTheFront()
        {
            Passenger first = CreatePassenger(1, PuzzleColor.Orange);
            Passenger second = CreatePassenger(2, PuzzleColor.Orange);
            Passenger third = CreatePassenger(3, PuzzleColor.Pink);
            PassengerQueue queue = new(new[] { first, second, third });

            IReadOnlyList<Passenger> dequeued = queue.Dequeue(2);

            CollectionAssert.AreEqual(new[] { first, second }, dequeued);
            Assert.AreEqual(1, queue.Passengers.Count);
            Assert.AreEqual(third, queue.Passengers[0]);
        }

        [Test]
        public void Dequeue_ClampsToAvailableCount_WhenRequestingMoreThanRemaining()
        {
            Passenger first = CreatePassenger(1, PuzzleColor.Orange);
            PassengerQueue queue = new(new[] { first });

            IReadOnlyList<Passenger> dequeued = queue.Dequeue(5);

            Assert.AreEqual(1, dequeued.Count);
            Assert.AreEqual(0, queue.Passengers.Count);
        }
    }
}
