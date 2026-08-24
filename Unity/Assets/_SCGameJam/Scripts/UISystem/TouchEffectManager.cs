using SCJam.Core;
using SCJam.InputSystem;
using UnityEngine;

namespace SCJam.UISystem
{
    public class TouchEffectManager : MonoSingleton<TouchEffectManager>
    {
        // ===== Constants ===== //

        private const float EFFECT_LIFETIME = 1f;


        // ===== Serialized Fields ===== //

        [SerializeField] private RectTransform _effectParent;
        [SerializeField] private RectTransform _touchEffectPrefab;


        // ===== Methods ===== //

        private void OnEnable()
        {
            InputManager.OnTouchPerformed += OnTouchPerformed;
        }

        private void OnDisable()
        {
            InputManager.OnTouchPerformed -= OnTouchPerformed;
        }

        private void OnTouchPerformed(Vector2 screenPosition)
        {
            if (_effectParent == null || _touchEffectPrefab == null)
            {
                Debug.LogWarning($"[{nameof(TouchEffectManager)}] Missing {nameof(_effectParent)} or {nameof(_touchEffectPrefab)} reference.", this);
                return;
            }

            // The effect parent is a Screen Space - Overlay canvas, so the screen-to-local conversion must use
            // a null camera; passing a camera here would misproject the point since overlay canvases aren't
            // rendered through one.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_effectParent, screenPosition, null, out Vector2 localPosition))
                return;

            RectTransform effectInstance = Instantiate(_touchEffectPrefab, _effectParent);
            effectInstance.anchoredPosition = localPosition;

            Destroy(effectInstance.gameObject, EFFECT_LIFETIME);
        }
    }
}
