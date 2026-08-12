using SCJam.Common;

namespace SCJam.BoardSystem
{
    public sealed class ParkingBoardData
    {
        public int Width { get; }
        public int Height { get; }
        public GridDirection ExitDirection { get; }


        public ParkingBoardData(int width, int height, GridDirection exitDirection)
        {
            Width = width;
            Height = height;
            ExitDirection = exitDirection;
        }
    }
}
