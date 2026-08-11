using SCJam.Common;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    [CreateAssetMenu(fileName = "Vehicle_", menuName = "SCJam/Vehicle Config")]
    public class VehicleConfig : ScriptableObject
    {
        [SerializeField] private PuzzleColor _color;
        [SerializeField] private int _capacity = 4;
        [SerializeField] private Vector2Int _footprintSize = new(1, 2);
        [SerializeField] private GameObject _prefab;


        public PuzzleColor Color => _color;
        public int Capacity => _capacity;
        public Vector2Int FootprintSize => _footprintSize;
        public GameObject Prefab => _prefab;
    }
}
