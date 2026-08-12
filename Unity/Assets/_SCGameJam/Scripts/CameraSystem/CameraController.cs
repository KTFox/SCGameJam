using SCJam.Core;
using UnityEngine;

namespace SCJam.CameraSystem
{
    [ExecuteAlways]
    public class CameraController : MonoSingleton<CameraController>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Camera _uiCamera;


        // ===== Public Properties ===== //

        public Camera MainCamera => _mainCamera;
        public Camera UICamera => _uiCamera;


        // ===== Methods ===== //

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
            {
                SyncUICameraWithMainCamera();
            }
        }
#endif

        // Keeps the UI Camera's projection in line with the Main Camera while authoring in the Scene view,
        // so world-space UI rendered by the UI Camera doesn't drift from what the Main Camera frames.
        private void SyncUICameraWithMainCamera()
        {
            if (_mainCamera == null || _uiCamera == null)
                return;

            _uiCamera.orthographic = _mainCamera.orthographic;
            _uiCamera.fieldOfView = _mainCamera.fieldOfView;
            _uiCamera.orthographicSize = _mainCamera.orthographicSize;
            _uiCamera.nearClipPlane = _mainCamera.nearClipPlane;
            _uiCamera.farClipPlane = _mainCamera.farClipPlane;
        }
    }
}
