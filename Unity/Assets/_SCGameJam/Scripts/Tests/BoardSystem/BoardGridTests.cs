using System.Collections.Generic;
using NUnit.Framework;
using SCJam.BoardSystem;
using SCJam.Common;
using UnityEngine;

namespace SCJam.Tests.BoardSystem
{
    public class BoardGridTests
    {
        private static ParkingBoardData CreateBoardData(
            int width,
            int height,
            GridDirection exitDirection = GridDirection.Right)
        {
            return new ParkingBoardData(width, height, exitDirection);
        }

        [Test]
        public void IsCellInBounds_ReturnsTrue_ForCellsWithinGrid()
        {
            BoardGrid grid = new(CreateBoardData(4, 3));

            Assert.IsTrue(grid.IsCellInBounds(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsCellInBounds(new Vector2Int(3, 2)));
        }

        [Test]
        public void IsCellInBounds_ReturnsFalse_ForCellsOutsideGrid()
        {
            BoardGrid grid = new(CreateBoardData(4, 3));

            Assert.IsFalse(grid.IsCellInBounds(new Vector2Int(-1, 0)));
            Assert.IsFalse(grid.IsCellInBounds(new Vector2Int(4, 0)));
            Assert.IsFalse(grid.IsCellInBounds(new Vector2Int(0, 3)));
        }

        [Test]
        public void PlaceVehicle_MarksItsFootprintCellsOccupied()
        {
            BoardGrid grid = new(CreateBoardData(4, 4));
            Vector2Int[] cells = { new(0, 0), new(1, 0) };

            grid.PlaceVehicle(1, cells);

            Assert.IsTrue(grid.IsCellOccupied(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsCellOccupied(new Vector2Int(1, 0)));
            Assert.IsFalse(grid.IsCellOccupied(new Vector2Int(2, 0)));
        }

        [Test]
        public void IsCellOccupied_ExcludingVehicleId_IgnoresItsOwnFootprint()
        {
            BoardGrid grid = new(CreateBoardData(4, 4));
            grid.PlaceVehicle(1, new[] { new Vector2Int(0, 0) });

            Assert.IsFalse(grid.IsCellOccupied(new Vector2Int(0, 0), excludingVehicleId: 1));
            Assert.IsTrue(grid.IsCellOccupied(new Vector2Int(0, 0), excludingVehicleId: 2));
        }

        [Test]
        public void RemoveVehicle_ClearsItsFootprintCells()
        {
            BoardGrid grid = new(CreateBoardData(4, 4));
            grid.PlaceVehicle(1, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) });

            grid.RemoveVehicle(1);

            Assert.IsFalse(grid.IsCellOccupied(new Vector2Int(0, 0)));
            Assert.IsFalse(grid.IsCellOccupied(new Vector2Int(1, 0)));
        }

        [Test]
        public void GetCellsToBoundary_ReturnsCellsAfterOrigin_UpToTheEdge()
        {
            BoardGrid grid = new(CreateBoardData(5, 5));

            IReadOnlyList<Vector2Int> cells = grid.GetCellsToBoundary(new Vector2Int(1, 0), GridDirection.Right);

            CollectionAssert.AreEqual(new[]
            {
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0)
            }, cells);
        }

        [Test]
        public void GetCellsToBoundary_ReturnsEmpty_WhenOriginIsAlreadyAtTheBoundary()
        {
            BoardGrid grid = new(CreateBoardData(3, 3));

            IReadOnlyList<Vector2Int> cells = grid.GetCellsToBoundary(new Vector2Int(2, 0), GridDirection.Right);

            Assert.IsEmpty(cells);
        }

        [Test]
        public void ParkingBoardData_ExposesConfiguredExitDirection()
        {
            ParkingBoardData boardData = CreateBoardData(3, 3, exitDirection: GridDirection.Up);

            Assert.AreEqual(GridDirection.Up, boardData.ExitDirection);
        }
    }
}
