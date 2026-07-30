using UnityEngine;

namespace ithappy
{
    public class StandardInput : InputBase
    {
        protected override void Update()
        {
            _moveInput = Input.GetAxisRaw("Vertical");
            _steerInput = Input.GetAxisRaw("Horizontal");
            _shouldBoost = Input.GetKey(KeyCode.LeftShift);
            
            base.Update();
        }
    }
}