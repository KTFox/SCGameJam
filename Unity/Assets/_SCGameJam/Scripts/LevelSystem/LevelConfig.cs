using System.Collections.Generic;
using SCJam.Common;
using UnityEngine;

namespace SCJam.LevelSystem
{
    [CreateAssetMenu(fileName = "Level_", menuName = "SCJam/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [SerializeField] private int _boardWidth = 6;
        [SerializeField] private int _boardHeight = 6;
        [SerializeField] private GridDirection _exitDirection = GridDirection.Right;
        [SerializeField] private VehiclePlacement[] _vehiclePlacements;
        [SerializeField] private int _waitingSlotCount = 3;
        [SerializeField] private PuzzleColor[] _passengerColorSequence;


        public int BoardWidth => _boardWidth;
        public int BoardHeight => _boardHeight;
        public GridDirection ExitDirection => _exitDirection;
        public IReadOnlyList<VehiclePlacement> VehiclePlacements => _vehiclePlacements;
        public int WaitingSlotCount => _waitingSlotCount;
        public IReadOnlyList<PuzzleColor> PassengerColorSequence => _passengerColorSequence;
    }
}
