using UnityEngine;

namespace SCJam.PassengerSystem
{
    public class PassengerQueueView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Transform[] _queuePositionAnchors;


        // ===== Public Properties ===== //

        public int VisiblePositionCount => _queuePositionAnchors != null ? _queuePositionAnchors.Length : 0;


        // ===== Methods ===== //

        public Vector3 GetQueueWorldPosition(int positionIndex)
        {
            return _queuePositionAnchors[positionIndex].position;
        }
    }
}
