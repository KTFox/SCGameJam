using System;
using DG.Tweening;
using SCJam.AudioSystem;
using UnityEngine;

namespace SCJam.UISystem
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class PopupBase : MonoBehaviour
    {
        // ===== Constants ===== //

        private const Ease OPEN_EASE = Ease.OutBack;
        private const Ease CLOSE_EASE = Ease.InBack;
        private const float OPEN_DURATION = 0.25f;
        private const float CLOSE_DURATION = 0.2f;


        // ===== Serialized Fields ===== //

        [SerializeField] private PopupId _popupId;
        [SerializeField] private SoundSO _openSound;
        [SerializeField] private SoundSO _closeSound;


        // ===== Private Fields ===== //

        private RectTransform _rectTransform;
        private Tweener _scaleTweener;


        // ===== Public Properties ===== //

        public PopupId PopupId => _popupId;


        // ===== Events ===== //

        public event Action<PopupBase> Closed;


        // ===== Methods ===== //

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void OnDisable()
        {
            _scaleTweener?.Kill();
            _scaleTweener = null;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            AudioManager.Instance?.PlaySound(_openSound);

            _scaleTweener?.Kill();
            _rectTransform.localScale = Vector3.zero;
            _scaleTweener = _rectTransform.DOScale(Vector3.one, OPEN_DURATION)
                .SetEase(OPEN_EASE)
                .SetUpdate(true);
        }

        public void Close()
        {
            AudioManager.Instance?.PlaySound(_closeSound);

            _scaleTweener?.Kill();
            _scaleTweener = _rectTransform.DOScale(Vector3.zero, CLOSE_DURATION)
                .SetEase(CLOSE_EASE)
                .SetUpdate(true)
                .OnComplete(OnCloseAnimationComplete);
        }

        private void OnCloseAnimationComplete()
        {
            _scaleTweener = null;
            gameObject.SetActive(false);
            Closed?.Invoke(this);
        }
    }
}
