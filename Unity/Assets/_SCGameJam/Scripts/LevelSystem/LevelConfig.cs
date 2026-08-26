using System.Collections.Generic;
using SCJam.Common;
using SCJam.PassengerSystem;
using UnityEngine;

namespace SCJam.LevelSystem
{
    [CreateAssetMenu(fileName = "Level_", menuName = "SCJam/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Vector2Int _boardSize = new(6, 6);
        [SerializeField, Range(1, 7)] private int _waitingSlotCount = 4;
        [SerializeField] private VehiclePlacement[] _vehiclePlacements;
        [SerializeField] private PassengerPrefabMapping[] _passengerPrefabMappings;
        [SerializeField] private PuzzleColor[] _passengerColorSequence;


        // ===== Public Properties ===== //

        public Vector2Int BoardSize => _boardSize;
        public int WaitingSlotCount => _waitingSlotCount;
        public IReadOnlyList<VehiclePlacement> VehiclePlacements => _vehiclePlacements;
        public IReadOnlyList<PuzzleColor> PassengerColorSequence => _passengerColorSequence;
        public IReadOnlyList<PassengerPrefabMapping> PassengerPrefabMappings => _passengerPrefabMappings;


        // ===== Methods ===== //

        private void OnValidate()
        {
            List<string> errors = new();
            PassengerPrefabLookup.Build(_passengerPrefabMappings, _passengerColorSequence, errors);

            foreach (string error in errors)
                Debug.LogWarning($"Level '{name}': {error}", this);
        }
    }
}
