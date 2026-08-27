using DG.Tweening;
using SCJam.LevelSystem;
using TMPro;
using UnityEngine;

namespace SCJam.UISystem
{
    /// <summary>
    /// HUD panel that shows how many passengers are still queued in the current level. Refreshes its label
    /// whenever <see cref="LevelController.RemainingPassengerCountChanged"/> fires (level load and every
    /// boarding), playing a bounce-scale on the label each time the number changes.
    /// </summary>
    public class RemainingPassengerPanel : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private LevelController _levelController;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private RectTransform _bounceTarget;
        [SerializeField] private float _bounceScale;
        [SerializeField] private float _bounceDuration;
        [SerializeField] private Ease _bounceEase;


        // ===== Private Fields ===== //

        private Vector3 _bounceBaseScale;
        private int _displayedCount;
        private Tweener _bounceRoutine;


        // ===== Methods ===== //

        private void Awake()
        {
            _bounceBaseScale = _bounceTarget.localScale;
            _displayedCount = -1;
        }

        private void OnEnable()
        {
            _levelController.RemainingPassengerCountChanged += OnRemainingPassengerCountChanged;
            OnRemainingPassengerCountChanged(_levelController.RemainingPassengerCount);
        }

        private void OnDisable()
        {
            _levelController.RemainingPassengerCountChanged -= OnRemainingPassengerCountChanged;

            _bounceRoutine?.Kill();
            _bounceRoutine = null;
            _bounceTarget.localScale = _bounceBaseScale;
        }

        private void OnRemainingPassengerCountChanged(int remainingCount)
        {
            if (remainingCount == _displayedCount)
                return;

            _displayedCount = remainingCount;
            _countText.text = remainingCount.ToString();
            PlayBounce();
        }

        private void PlayBounce()
        {
            _bounceRoutine?.Kill();
            _bounceTarget.localScale = _bounceBaseScale;

            _bounceRoutine = _bounceTarget
                .DOScale(_bounceBaseScale * _bounceScale, _bounceDuration)
                .SetEase(_bounceEase)
                .SetLoops(2, LoopType.Yoyo)
                .OnKill(() => _bounceRoutine = null);
        }
    }
}
