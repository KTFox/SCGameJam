using DG.Tweening;
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
        [SerializeField] private Transform _scaleTarget;
        [SerializeField] private float _scaleUpMultiplier;
        [SerializeField] private float _scaleUpDuration;
        [SerializeField] private float _scaleDownDuration;
        [SerializeField] private Ease _scaleEase;


        // ===== Private Fields ===== //

        private Vector3 _baseScale;
        private bool _hasState;
        private bool _isLocked;
        private Sequence _updateRoutine;


        // ===== Methods ===== //

        private void Awake()
        {
            _baseScale = _scaleTarget.localScale;
        }

        private void OnDisable()
        {
            _updateRoutine?.Kill();
            _updateRoutine = null;
            _scaleTarget.localScale = _baseScale;
        }

        /// <summary>
        /// Applies the locked/unlocked visual immediately, without any animation. Used on level load when
        /// every slot is (re)configured at once.
        /// </summary>
        public void SetLocked(bool isLocked)
        {
            _updateRoutine?.Kill();
            _updateRoutine = null;
            _scaleTarget.localScale = _baseScale;

            ApplyLockedVisual(isLocked);
            _isLocked = isLocked;
            _hasState = true;
        }

        /// <summary>
        /// Applies the locked/unlocked visual with a bounce: scale up, swap the visual at the peak, then
        /// scale back to the original size. No-ops (no animation) when the state is unchanged.
        /// </summary>
        public void SetLocked(bool isLocked, bool animate)
        {
            if (!animate)
            {
                SetLocked(isLocked);
                return;
            }

            if (_hasState && _isLocked == isLocked)
                return;

            _isLocked = isLocked;
            _hasState = true;

            _updateRoutine?.Kill();
            _scaleTarget.localScale = _baseScale;

            _updateRoutine = DOTween.Sequence();
            _updateRoutine.Append(_scaleTarget.DOScale(_baseScale * _scaleUpMultiplier, _scaleUpDuration).SetEase(_scaleEase));
            _updateRoutine.AppendCallback(() => ApplyLockedVisual(isLocked));
            _updateRoutine.Append(_scaleTarget.DOScale(_baseScale, _scaleDownDuration).SetEase(_scaleEase));
            _updateRoutine.OnKill(() => _updateRoutine = null);
        }

        private void ApplyLockedVisual(bool isLocked)
        {
            _lockedIcon.gameObject.SetActive(isLocked);
            _border.color = isLocked ? _lockedColor : _normalColor;
        }
    }
}
