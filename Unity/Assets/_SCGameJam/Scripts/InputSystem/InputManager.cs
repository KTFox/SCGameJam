using System;
using SCJam.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SCJam.InputSystem
{
    public class InputManager : MonoSingleton<InputManager>
    {
        private TouchInputActions _touchInputActions;


        public static event Action<Vector2> OnTouchPerformed;


        protected override void Awake()
        {
            base.Awake();

            _touchInputActions = new TouchInputActions();
            _touchInputActions.Gameplay.TouchPress.performed += OnTouchPressPerformed;
            _touchInputActions.Gameplay.Enable();
        }

        private void OnDestroy()
        {
            _touchInputActions.Gameplay.TouchPress.performed -= OnTouchPressPerformed;
            _touchInputActions.Gameplay.Disable();
            _touchInputActions.Dispose();
        }

        private void OnTouchPressPerformed(InputAction.CallbackContext context)
        {
            Vector2 touchPosition = _touchInputActions.Gameplay.TouchPosition.ReadValue<Vector2>();
            OnTouchPerformed?.Invoke(touchPosition);
        }
    }
}
