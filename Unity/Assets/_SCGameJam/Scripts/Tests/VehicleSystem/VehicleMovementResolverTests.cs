using System.Collections.Generic;
using NUnit.Framework;
using SCJam.BoardSystem;
using SCJam.Common;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.Tests.VehicleSystem
{
    public class VehicleMovementResolverTests
    {
        private static BoardGrid CreateGrid(int width, int height, IEnumerable<Vector2Int> blockedCells = null)
        {
            ParkingBoardData boardData = new(width, height, blockedCells ?? new Vector2Int[0], GridDirection.Right);
            return new BoardGrid(boardData);
        }

        private static Vehicle CreateVehicle(int id, IReadOnlyList<Vector2Int> footprint, GridDirection direction)
        {
            return new Vehicle(id, PuzzleColor.Orange, capacity: 4, initialFootprint: footprint, movementDirection: direction);
        }

        [Test]
        public void IsPathClear_ReturnsTrue_WhenNothingBlocksTheSweptPath()
        {
            BoardGrid grid = CreateGrid(5, 5);
            Vehicle vehicle = CreateVehicle(1, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            grid.PlaceVehicle(vehicle.Id, vehicle.GridFootprint);
            VehicleMovementResolver resolver = new(grid);

            Assert.IsTrue(resolver.IsPathClear(vehicle));
        }

        [Test]
        public void IsPathClear_ReturnsFalse_WhenBlockedCellIntersectsTheSweptPath()
        {
            Vector2Int[] blocked = { new(3, 0) };
            BoardGrid grid = CreateGrid(5, 5, blocked);
            Vehicle vehicle = CreateVehicle(1, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            grid.PlaceVehicle(vehicle.Id, vehicle.GridFootprint);
            VehicleMovementResolver resolver = new(grid);

            Assert.IsFalse(resolver.IsPathClear(vehicle));
        }

        [Test]
        public void IsPathClear_ReturnsFalse_WhenAnotherVehicleIntersectsTheSweptPath()
        {
            BoardGrid grid = CreateGrid(5, 5);
            Vehicle mover = CreateVehicle(1, new[] { new Vector2Int(0, 0) }, GridDirection.Right);
            Vehicle blocker = CreateVehicle(2, new[] { new Vector2Int(3, 0) }, GridDirection.Up);
            grid.PlaceVehicle(mover.Id, mover.GridFootprint);
            grid.PlaceVehicle(blocker.Id, blocker.GridFootprint);
            VehicleMovementResolver resolver = new(grid);

            Assert.IsFalse(resolver.IsPathClear(mover));
        }

        [Test]
        public void IsPathClear_ExcludesTheVehicleOwnFootprintFromCollisionChecks()
        {
            BoardGrid grid = CreateGrid(5, 1);
            Vehicle vehicle = CreateVehicle(1, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, GridDirection.Right);
            grid.PlaceVehicle(vehicle.Id, vehicle.GridFootprint);
            VehicleMovementResolver resolver = new(grid);

            Assert.IsTrue(resolver.IsPathClear(vehicle));
        }

        [Test]
        public void GetSweptFootprintCells_CoversEveryLaneOfAMultiCellFootprint()
        {
            BoardGrid grid = CreateGrid(3, 2);
            Vehicle vehicle = CreateVehicle(1, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) }, GridDirection.Right);
            VehicleMovementResolver resolver = new(grid);

            IReadOnlyList<Vector2Int> swept = resolver.GetSweptFootprintCells(vehicle);

            CollectionAssert.AreEquivalent(new[]
            {
                new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(1, 1), new Vector2Int(2, 1)
            }, swept);
        }

        [Test]
        public void GetExitCellDistance_ReturnsDistanceFromLeadingEdgeToOffBoard()
        {
            BoardGrid grid = CreateGrid(5, 1);
            Vehicle vehicle = CreateVehicle(1, new[] { new Vector2Int(2, 0) }, GridDirection.Right);
            VehicleMovementResolver resolver = new(grid);

            Assert.AreEqual(3, resolver.GetExitCellDistance(vehicle));
        }
    }
}
