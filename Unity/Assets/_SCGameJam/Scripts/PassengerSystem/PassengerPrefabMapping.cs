using System;
using SCJam.Common;
using UnityEngine;

namespace SCJam.PassengerSystem
{
    /// <summary>
    /// Associates a PuzzleColor with the passenger prefab used to represent it within a single level.
    /// The same color may map to a different prefab in a different level's LevelConfig.
    /// </summary>
    [Serializable]
    public sealed class PassengerPrefabMapping
    {
        [SerializeField, Tooltip("Passenger color this mapping applies to.")]
        private PuzzleColor _color;

        [SerializeField, Tooltip("Prefab (with a PassengerController component) spawned for passengers of this color.")]
        private PassengerController _prefab;


        public PuzzleColor Color => _color;
        public PassengerController Prefab => _prefab;


        public PassengerPrefabMapping(PuzzleColor color, PassengerController prefab)
        {
            _color = color;
            _prefab = prefab;
        }
    }
}
