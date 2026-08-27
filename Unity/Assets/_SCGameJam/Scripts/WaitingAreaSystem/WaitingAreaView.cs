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

        public void ApplyActiveSlotCount(int activeSlotCount) => ApplyActiveSlotCount(activeSlotCount, false);

        /// <summary>
        /// Locks/unlocks slot visuals so exactly activeSlotCount of them are active. When animate is true,
        /// slots whose locked state actually changes play their scale-swap bounce; on level load it is
        /// false so every slot snaps to its configured state.
        /// </summary>
        public void ApplyActiveSlotCount(int activeSlotCount, bool animate)
        {
            if (_slotVisuals == null)
                return;

            for (int i = 0; i < _slotVisuals.Length; i++)
            {
                if (_slotVisuals[i] == null)
                    continue;

                _slotVisuals[i].SetLocked(i >= activeSlotCount, animate);
            }
        }
    }
}
