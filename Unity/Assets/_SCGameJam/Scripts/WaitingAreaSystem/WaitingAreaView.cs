using UnityEngine;

namespace SCJam.WaitingAreaSystem
{
    public class WaitingAreaView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Transform[] _slotAnchors;


        // ===== Public Properties ===== //

        public int SlotCount => _slotAnchors != null ? _slotAnchors.Length : 0;


        // ===== Methods ===== //

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            return _slotAnchors[slotIndex].position;
        }
    }
}
