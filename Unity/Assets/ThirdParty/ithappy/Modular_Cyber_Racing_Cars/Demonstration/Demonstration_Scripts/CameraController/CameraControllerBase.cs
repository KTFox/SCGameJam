using UnityEngine;

namespace ithappy
{
    public class CameraControllerBase : MonoBehaviour
    {
        protected enum CameraMode
        {
            TopDown,
            ThirdPerson
        }
        
        [SerializeField] private Transform _target;
        [SerializeField] protected CameraMode _mode = CameraMode.TopDown;

        [Header("Top Down Settings")]
        [SerializeField] private float _topDownDistance = 30f;
        [SerializeField] private Vector3 _topDownAngle = new Vector3(60, 0, 0);
        [SerializeField] private float _topDownSmoothing = 10f;

        [Header("Third Person Settings")]
        [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0, 2, -10);
        [SerializeField] private Vector3 _thirdPersonAngle = new Vector3(15, 0, 0);
        [SerializeField] private float _thirdPersonCollisionOffset = 0.1f;
        [SerializeField] private float _thirdPersonSmoothing = 10f;

        private Transform _transform;
        
        #region Unity Methods
        public void Start()
        {
            _transform =  transform;
        }

        private void FixedUpdate()
        {
            if (_target == null)
            {
                return;
            }

            switch (_mode)
            {
                case CameraMode.ThirdPerson:
                    UpdateThirdPersonCamera();
                    break;
                
                case CameraMode.TopDown:
                    UpdateTopDownCamera();
                    break;
            }
        }
        #endregion

        #region Camera Updates
        private void UpdateThirdPersonCamera()
        {
            Vector3 rotationEuler = _thirdPersonAngle + Vector3.up * _target.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(rotationEuler);
            
            float rotationInterpolation = _thirdPersonSmoothing <= 0 ? 1 : _thirdPersonSmoothing * Time.fixedDeltaTime;
            _transform.rotation = Quaternion.Lerp(_transform.rotation, targetRotation, Mathf.Clamp01(rotationInterpolation));
            
            Vector3 forward = _transform.rotation * Vector3.forward;
            Vector3 right = _transform.rotation * Vector3.right;
            Vector3 offsetVector = forward * _thirdPersonOffset.z + 
                                  Vector3.up * _thirdPersonOffset.y + 
                                  right * _thirdPersonOffset.x;
            
            Vector3 desiredPosition = _target.position + offsetVector;
            Vector3 direction = offsetVector.normalized;
            float distance = offsetVector.magnitude;

            if (Physics.Raycast(_target.position, direction, out RaycastHit hit, distance))
            {
                _transform.position = _target.position + direction * Mathf.Max(_thirdPersonCollisionOffset, hit.distance - _thirdPersonCollisionOffset);
            }
            else
            {
                _transform.position = desiredPosition;
            }
        }

        private void UpdateTopDownCamera()
        {
            Quaternion targetRotation = Quaternion.Euler(_topDownAngle);
            _transform.rotation = targetRotation;

            Vector3 targetPosition = _target.position + _transform.rotation * Vector3.back * _topDownDistance;
            float positionInterpolation = _topDownSmoothing <= 0 ? 1 : _topDownSmoothing * Time.fixedDeltaTime;
            _transform.position = Vector3.Lerp(_transform.position, targetPosition, Mathf.Clamp01(positionInterpolation));
        }
        #endregion
    }
}
