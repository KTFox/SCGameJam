using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SCJam.CameraSystem;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.UISystem
{
    /// <summary>
    /// Screen-space UI hand that simulates a player's touch: whenever a vehicle is selected, it tweens
    /// from its current position to that vehicle's world position, projected onto the canvas. While
    /// enabled, it is wired as the VehicleSelectionController's selection delay source so the vehicle
    /// only starts moving once the hand has finished arriving.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PlayerHandSimulator : MonoBehaviour, IVehicleSelectionDelay
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private bool _isSimulationEnabled;
        [SerializeField] private Vector3 _worldOffset;
        [SerializeField] private float _moveDuration;
        [SerializeField] private Ease _moveEase;
        [SerializeField] private float _punchScaleAmount;
        [SerializeField] private float _punchScaleDuration;


        // ===== Private Fields ===== //

        private RectTransform _rectTransform;
        private RectTransform _canvasRectTransform;
        private Vector3 _originalScale;
        private Tweener _moveTweener;
        private Tweener _punchScaleTweener;
        private CancellationToken _destroyCancellationToken;
        private bool _hasAppeared;


        // ===== Methods ===== //

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _canvasRectTransform = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            KillTweens();
        }

        public void Hide()
        {
            KillTweens();
            _hasAppeared = false;
            gameObject.SetActive(false);
        }

        public async UniTask WaitForSelectionDelayAsync(VehicleController vehicleController)
        {
            if (!_isSimulationEnabled || vehicleController == null)
                return;

            if (!TryGetAnchoredPosition(vehicleController.transform.position + _worldOffset, out Vector2 targetAnchoredPosition))
                return;

            _moveTweener?.Kill();
            gameObject.SetActive(true);

            if (!_hasAppeared)
            {
                _hasAppeared = true;
                _rectTransform.anchoredPosition = targetAnchoredPosition;
            }
            else
            {
                _moveTweener = DOTween.To(
                        () => _rectTransform.anchoredPosition,
                        value => _rectTransform.anchoredPosition = value,
                        targetAnchoredPosition,
                        _moveDuration)
                    .SetEase(_moveEase);

                await _moveTweener.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);
            }

            await PlayPunchScaleAsync();
        }

        private UniTask PlayPunchScaleAsync()
        {
            _punchScaleTweener?.Kill();
            _rectTransform.localScale = _originalScale;

            _punchScaleTweener = _rectTransform.DOPunchScale(Vector3.one * _punchScaleAmount, _punchScaleDuration);

            return _punchScaleTweener.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);
        }

        private bool TryGetAnchoredPosition(Vector3 worldPosition, out Vector2 anchoredPosition)
        {
            Camera mainCamera = CameraController.Instance.MainCamera;
            Vector2 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPosition, null, out anchoredPosition);
        }

        private void KillTweens()
        {
            _moveTweener?.Kill();
            _moveTweener = null;

            _punchScaleTweener?.Kill();
            _punchScaleTweener = null;
            _rectTransform.localScale = _originalScale;
        }
    }
}
