using SCJam.CameraSystem;
using UnityEngine;

namespace SCJam.UISystem
{
    /// <summary>
    /// Screen-space UI hint that points at a world-space target (e.g. the vehicle the player should tap
    /// next). Follows the target every frame while visible so it stays correct as the camera moves.
    /// </summary>
    public class GuideFingerController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Vector3 _worldOffset;


        // ===== Private Fields ===== //

        private RectTransform _rectTransform;
        private RectTransform _canvasRectTransform;
        private Transform _worldTarget;


        // ===== Public Properties ===== //

        public bool IsShown => gameObject.activeSelf;


        // ===== Methods ===== //

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasRectTransform = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (_worldTarget == null)
                return;

            UpdatePositionToTarget();
        }

        public void Show(Transform worldTarget)
        {
            if (worldTarget == null)
            {
                Debug.LogWarning($"[{nameof(GuideFingerController)}] Cannot show with a null {nameof(worldTarget)}.", this);
                return;
            }

            _worldTarget = worldTarget;
            gameObject.SetActive(true);
            UpdatePositionToTarget();
        }

        public void Hide()
        {
            _worldTarget = null;
            gameObject.SetActive(false);
        }

        private void UpdatePositionToTarget()
        {
            Camera mainCamera = CameraController.Instance.MainCamera;
            Vector2 screenPosition = mainCamera.WorldToScreenPoint(_worldTarget.position + _worldOffset);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPosition, null, out Vector2 localPosition))
                return;

            _rectTransform.anchoredPosition = localPosition;
        }
    }
}
