using UnityEngine;

namespace ithappy
{
    public class CarFxController : MonoBehaviour
    {
        [SerializeField] private CarController _carController;
        
        [SerializeField] private bool _adjustToScale = true;
        
        [Header("Visual Settings")]
        [SerializeField] private Transform _vehicleContainer;
        [SerializeField] private float _wheelsSpinForce = 100f;
        [SerializeField] private float _wheelsMaxTurnAngle = 45f;
        [SerializeField] private float _wheelsTurnSmoothing = 10f;
        [SerializeField] private Transform[] _frontWheels;
        [SerializeField] private Transform[] _rearWheels;
        [SerializeField] private bool _rotatePitch = true;
        [SerializeField] private bool _rotateRoll = true;
        
        [Header("Air Rotation")]
        [SerializeField] private float _airPitchMax = 45f;
        [SerializeField] private float _airPitchSpeed = 1f;
        [SerializeField] private float _airPitchSpeedGrounded = 10f;
        [SerializeField] private float _groundRotationSmoothing = 10f;
        [SerializeField] private float _airRotationSmoothing = 5f;
        
        [Header("Particle Effects")]
        [SerializeField] private float _minDriftingSpeed = 10f;
        [SerializeField] private ParticleSystem _driftParticles;
        [SerializeField] private ParticleSystem _boostParticles;
        
        private float _wheelRotationAngle;
        private float _wheelSpinValue;
        private float _currentPitchModifier;
        private Quaternion _smoothedGroundRotation;
        
        private void FixedUpdate()
        {
            if (!_carController.gameObject.activeInHierarchy)
            {
                return;
            }

            UpdateVehicleVisuals();
            UpdateWheelRotation();
            UpdateParticleEffects();
        }
        
        public void SetWheels(Transform[] frontWheels, Transform[] rearWheels)
        {
            _frontWheels = frontWheels;
            _rearWheels = rearWheels;
        }

        #region Visual Updates
        private void UpdateVehicleVisuals()
        {
            if (_vehicleContainer == null)
            {
                return;
            }

            UpdateVehicleRotation();
            _vehicleContainer.position = _carController.GetBodyPosition;
        }

        private void UpdateVehicleRotation()
        {
            float deltaTime = Time.fixedDeltaTime;
            
            Quaternion targetRotation = CalculateTargetRotation();
            float rotationSmoothing = _carController.IsGrounded ? _groundRotationSmoothing : _airRotationSmoothing;
            _smoothedGroundRotation = Quaternion.Slerp(
                _smoothedGroundRotation, 
                targetRotation, 
                CalculateInterpolationFactor(rotationSmoothing, deltaTime));
            
            UpdatePitchModifier(deltaTime);
            Vector3 finalRotation = ApplyRotationConstraints(_smoothedGroundRotation.eulerAngles);
            finalRotation.x += _currentPitchModifier;

            _vehicleContainer.rotation = Quaternion.Euler(finalRotation);
        }

        private Quaternion CalculateTargetRotation()
        {
            Quaternion groundRotation = _carController.GetGroundRotation;
            return Quaternion.Euler(
                groundRotation.eulerAngles.x,
                groundRotation.eulerAngles.y,
                _smoothedGroundRotation.eulerAngles.z);
        }

        private Vector3 ApplyRotationConstraints(Vector3 rotation)
        {
            if (!_rotatePitch)
            {
                rotation.x = 0f;
            }
            if (!_rotateRoll)
            {
                rotation.z = 0f;
            }
            return rotation;
        }

        private void UpdatePitchModifier(float deltaTime)
        {
            if (!_carController.IsGrounded)
            {
                _currentPitchModifier = Mathf.Lerp(
                    _currentPitchModifier, 
                    _airPitchMax, 
                    CalculateInterpolationFactor(_airPitchSpeed, deltaTime));
            }
            else
            {
                _currentPitchModifier = Mathf.Lerp(
                    _currentPitchModifier, 
                    0f, 
                    CalculateInterpolationFactor(_airPitchSpeedGrounded, deltaTime));
            }
        }
        #endregion

        #region Wheel Updates
        private void UpdateWheelRotation()
        {
            float deltaTime = Time.fixedDeltaTime;
            float scaleFactor = _adjustToScale ? CalculateAverageScale() : 1f;

            _wheelSpinValue += _carController.GetForwardVelocity * deltaTime * _wheelsSpinForce * scaleFactor;
            _wheelRotationAngle = Mathf.Lerp(
                _wheelRotationAngle,
                _carController.GetSteering * _wheelsMaxTurnAngle,
                CalculateInterpolationFactor(_wheelsTurnSmoothing, deltaTime));

            ApplyWheelRotations();
        }

        private void ApplyWheelRotations()
        {
            Quaternion frontWheelRotation = Quaternion.Euler(_wheelSpinValue, _wheelRotationAngle, 0);
            Quaternion rearWheelRotation = Quaternion.Euler(_wheelSpinValue, 0, 0);

            foreach (Transform wheel in _frontWheels)
            {
                if (wheel != null)
                {
                    wheel.localRotation = frontWheelRotation;
                }
            }

            foreach (Transform wheel in _rearWheels)
            {
                if (wheel != null)
                {
                    wheel.localRotation = rearWheelRotation;
                }
            }
        }
        #endregion

        #region Particle Effects
        private void UpdateParticleEffects()
        {
            float scaleFactor = _adjustToScale ? CalculateAverageScale() : 1f;
            UpdateDriftParticles(scaleFactor);
            UpdateBoostParticles();
        }

        private void UpdateDriftParticles(float scaleFactor)
        {
            if (_driftParticles == null)
            {
                return;
            }

            bool shouldDrift = Mathf.Abs(_carController.GetLateralVelocity) > _minDriftingSpeed * scaleFactor 
                              && _carController.IsGrounded;

            if (shouldDrift && !_driftParticles.isPlaying)
            {
                _driftParticles.Play();
            }
            else if (!shouldDrift && _driftParticles.isPlaying)
            {
                _driftParticles.Stop();
            }
        }

        private void UpdateBoostParticles()
        {
            if (_boostParticles == null)
            {
                return;
            }

            if (_carController.GetBoostMultiplier > 1f)
            {
                _boostParticles.Play();
            }
            else
            {
                _boostParticles.Stop();
            }
        }
        #endregion

        #region Helper Methods
        private float CalculateAverageScale()
        {
            Vector3 scale = transform.lossyScale;
            return (scale.x + scale.y + scale.z) / 3f;
        }

        private float CalculateInterpolationFactor(float smoothingValue, float deltaTime)
        {
            return smoothingValue <= 0 ? 1f : Mathf.Clamp01(smoothingValue * deltaTime);
        }
        #endregion
    }
}
