using UnityEngine;

namespace SCJam.CameraSystem
{
    public class LookAtCamera : MonoBehaviour
    {
        private enum Mode
        {
            LookAt,
            LookAtInverted,
            CameraForward,
            CameraForwardInverted
        }

        [SerializeField] private Mode mode;
        private Camera _mainCamera;


        private void Start()
        {
            _mainCamera = CameraController.Instance.MainCamera;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
                return;

            switch (mode)
            {
                case Mode.LookAt:
                    transform.LookAt(_mainCamera.transform);
                    break;
                case Mode.LookAtInverted:
                    Vector3 dirFromCamera = transform.position - _mainCamera.transform.position;
                    transform.LookAt(dirFromCamera);
                    break;
                case Mode.CameraForward:
                    transform.forward = _mainCamera.transform.forward;
                    break;
                case Mode.CameraForwardInverted:
                    transform.forward = -_mainCamera.transform.forward;
                    break;
            }
        }
    }
}
