using UnityEngine;

namespace SCJam.WaitingAreaSystem
{
    public class WaitingSlotVisual : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private SpriteRenderer _border;
        [SerializeField] private SpriteRenderer _lockedIcon;
        [SerializeField] private Color _normalColor;
        [SerializeField] private Color _lockedColor;


        // ===== Methods ===== //

        public void SetLocked(bool isLocked)
        {
            _lockedIcon.gameObject.SetActive(isLocked);
            _border.color = isLocked ? _lockedColor : _normalColor;
        }
    }
}
