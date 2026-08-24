using System;
using SCJam.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SCJam.InputSystem
{
    public class InputManager : MonoSingleton<InputManager>
    {
        private TouchInputActions _touchInputActions;
        private bool _hasPendingTouch;
        private Vector2 _pendingTouchPosition;


        public static event Action<Vector2> OnTouchPerformed;


        protected override void Awake()
        {
            base.Awake();

            _touchInputActions = new TouchInputActions();
            _touchInputActions.Gameplay.TouchPress.performed += OnTouchPressPerformed;
            _touchInputActions.Gameplay.Enable();
        }

        private void Update()
        {
            if (!_hasPendingTouch)
                return;

            _hasPendingTouch = false;

            // Checked here instead of inside the input callback: IsPointerOverGameObject() reflects the
            // previous frame's UI raycast when queried from an InputAction callback, since that callback
            // runs before the EventSystem updates for the current frame.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            OnTouchPerformed?.Invoke(_pendingTouchPosition);
        }

        private void OnDestroy()
        {
            _touchInputActions.Gameplay.TouchPress.performed -= OnTouchPressPerformed;
            _touchInputActions.Gameplay.Disable();
            _touchInputActions.Dispose();
        }

        private void OnTouchPressPerformed(InputAction.CallbackContext context)
        {
            _pendingTouchPosition = _touchInputActions.Gameplay.TouchPosition.ReadValue<Vector2>();
            _hasPendingTouch = true;
        }
    }
}
