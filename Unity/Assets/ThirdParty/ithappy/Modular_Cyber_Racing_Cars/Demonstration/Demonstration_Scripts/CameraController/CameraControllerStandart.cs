using UnityEngine;

namespace ithappy
{
    public class CameraControllerStandart : CameraControllerBase
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _mode = CameraMode.TopDown;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _mode = CameraMode.ThirdPerson;
            }
        }
    }
}
