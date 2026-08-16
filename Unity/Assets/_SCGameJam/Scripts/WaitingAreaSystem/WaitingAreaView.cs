using UnityEngine;

namespace SCJam.WaitingAreaSystem
{
    public class WaitingAreaView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private WaitingSlotVisual[] _slotVisuals;


        // ===== Public Properties ===== //

        public int SlotCount => _slotVisuals != null ? _slotVisuals.Length : 0;


        // ===== Methods ===== //

        public Transform GetSlotAnchor(int slotIndex)
        {
            return _slotVisuals[slotIndex].transform;
        }

        public void ApplyActiveSlotCount(int activeSlotCount)
        {
            if (_slotVisuals == null)
                return;

            for (int i = 0; i < _slotVisuals.Length; i++)
            {
                if (_slotVisuals[i] == null)
                    continue;

                _slotVisuals[i].SetLocked(i >= activeSlotCount);
            }
        }
    }
}
