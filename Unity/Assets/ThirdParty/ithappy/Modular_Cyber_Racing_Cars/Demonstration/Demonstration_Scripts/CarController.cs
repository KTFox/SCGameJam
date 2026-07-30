using UnityEngine;

namespace ithappy
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class CarController : MonoBehaviour
    {
        private const float GroundCheckDistanceDelta = 0.1f;
        private const float GroundCheckSkinWidthDelta = 0.05f;

        private enum GravityMode
        {
            AlwaysDown, 
            TowardsGround
        }
        
        [Header("Physics")]
        [SerializeField] private float _colliderRadius = 2f;
        [SerializeField] private float _vehicleMass = 10f;
        [SerializeField] private GravityMode _gravityMode = GravityMode.TowardsGround;
        [SerializeField] private float _gravityAcceleration = 80;
        [SerializeField] private float _maxGravityAcceleration = 50;
        [SerializeField] private float _maxClimbAngle = 50f;
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [SerializeField] private bool _shouldScalePhysics = true;

        [Header("Engine")] 
        [SerializeField] private AnimationCurve _accelerationCurve = new AnimationCurve(
            new Keyframe(0, 1), 
            new Keyframe(1, 0));
        [SerializeField] private float _maxForwardAcceleration = 50;
        [SerializeField] private float _maxForwardSpeed = 40;
        [SerializeField] private float _maxReverseAcceleration = 50;
        [SerializeField] private float _maxReverseSpeed = 20;
        [SerializeField] private float _brakeForce = 100;
        [SerializeField] private float _slopeFrictionFactor = 1f;

        [Header("Steering")] 
        [SerializeField] private float _maxSteeringRate = 200;
        [SerializeField] private float _airSteeringMultiplier = 0.25f;
        [SerializeField] private AnimationCurve _steeringResponseCurve = new AnimationCurve(
            new Keyframe(0, 0), 
            new Keyframe(0.25f, 1), 
            new Keyframe(1, 1));
        [SerializeField] private float _rollingResistance = 40;
        [SerializeField] private float _lateralGrip = 80;

        private Rigidbody _rigidbody;
        private SphereCollider _sphereCollider;
        private PhysicsMaterial _customPhysicMaterial;
        private bool _isGrounded;
        private Vector3 _groundNormal;
        private Vector3 _forwardDirection;
        private Vector3 _rightDirection;
        private Quaternion _groundRotation;
        private float _slopeDelta;
        private float _forwardSpeed;
        private float _lateralSpeed;
        private Vector3 _gravityDirection;
        private float _steeringInput;
        private float _motorInput;
        private float _boostMultiplier = 1f;
        private float _averageScale;
        private float _realColliderRadius;
        private float _scaleAdjustment;
        private float _cubicScale;
        
        public Vector3 GetBodyPosition => _rigidbody.position;
        public bool IsGrounded => _isGrounded;
        public Quaternion GetGroundRotation => _groundRotation;
        public float GetForwardVelocity => _forwardSpeed;
        public float GetLateralVelocity => _lateralSpeed;
        public float GetForwardVelocityDelta => Mathf.Abs(_forwardSpeed) / GetMaxSpeed();
        public float GetSteering => _steeringInput;
        public int GetVelocityDirection => Mathf.Approximately(_forwardSpeed, 0) ? 0 : (int)Mathf.Sign(_forwardSpeed);
        public float GetBoostMultiplier => _boostMultiplier;

        #region Unity Lifecycle

        private void Update()
        {
            UpdateComponentSettings();
        }

        private void FixedUpdate()
        {
            UpdateScaleValues();
            UpdateGroundDetection();
            UpdateMovement();
        }
        #endregion

        public void Initialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _sphereCollider = GetComponent<SphereCollider>();
            InitializePhysicMaterial();
            
            _groundRotation = transform.rotation;
            _forwardDirection = transform.forward;
            _rightDirection = transform.right;
            _groundNormal = transform.up;
        }

        public void Dispose()
        {
        }

        public void SetBoostMultiplier(float multiplier)
        {
            _boostMultiplier = Mathf.Max(0, multiplier);
        }
        
        public bool IsBraking()
        {
            return !Mathf.Approximately(_motorInput, 0) && 
                   Mathf.Approximately(GetVelocityDirection, -Mathf.Sign(_motorInput));
        }

        public float GetMaxSpeed()
        {
            return GetVelocityDirection >= 0 ? 
                _maxForwardSpeed * _scaleAdjustment : 
                _maxReverseSpeed * _scaleAdjustment;
        }

        public float GetMaxAcceleration()
        {
            return GetVelocityDirection >= 0 ? 
                _maxForwardAcceleration * _scaleAdjustment : 
                _maxReverseAcceleration * _scaleAdjustment;
        }

        public void SetSteering(float value)
        {
            _steeringInput = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetMotor(float value)
        {
            _motorInput = Mathf.Clamp(value, -1f, 1f);
        }

        private void InitializePhysicMaterial()
        {
            _customPhysicMaterial = new PhysicsMaterial
            {
                bounciness = 0,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                staticFriction = 0,
                dynamicFriction = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        private void UpdateComponentSettings()
        {
            _rigidbody.hideFlags = HideFlags.NotEditable;
            _sphereCollider.hideFlags = HideFlags.NotEditable;

            _rigidbody.mass = _vehicleMass * (_shouldScalePhysics ? _cubicScale : 1);
            _rigidbody.linearDamping = 0;
            _rigidbody.angularDamping = 0;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Extrapolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _sphereCollider.radius = _colliderRadius;
            _sphereCollider.isTrigger = false;
            _sphereCollider.material = _customPhysicMaterial;
        }

        private void UpdateScaleValues()
        {
            _averageScale = (transform.lossyScale.x + transform.lossyScale.y + transform.lossyScale.z) / 3f;
            _scaleAdjustment = _shouldScalePhysics ? _averageScale : 1f;
            _realColliderRadius = _sphereCollider.radius * _averageScale;
            _cubicScale = _shouldScalePhysics ? Mathf.Pow(_averageScale, 3) : 1f;
        }

        private void UpdateGroundDetection()
        {
            float skinWidth = _realColliderRadius * GroundCheckSkinWidthDelta;
            float checkDistance = _realColliderRadius * GroundCheckDistanceDelta;

            _isGrounded = false;
            _groundNormal = transform.up;
            
            if (Physics.SphereCast(_sphereCollider.bounds.center, _realColliderRadius - skinWidth, 
                Vector3.down, out var hit, checkDistance + skinWidth, _collisionLayers))
            {
                _groundNormal = hit.normal;
                _isGrounded = Vector3.Angle(_groundNormal, Vector3.up) <= _maxClimbAngle;
            }

            UpdateGroundNormalWithMultiRaycast();
            UpdateMovementVectors();
        }

        private void UpdateGroundNormalWithMultiRaycast()
        {
            Vector3[] rayOrigins = {
                _sphereCollider.bounds.center + Vector3.forward * _realColliderRadius + Vector3.left * _realColliderRadius,
                _sphereCollider.bounds.center + Vector3.forward * _realColliderRadius + Vector3.right * _realColliderRadius,
                _sphereCollider.bounds.center + Vector3.back * _realColliderRadius + Vector3.left * _realColliderRadius,
                _sphereCollider.bounds.center + Vector3.back * _realColliderRadius + Vector3.right * _realColliderRadius
            };

            Vector3[] hitPoints = new Vector3[rayOrigins.Length];
            bool[] hits = new bool[rayOrigins.Length];

            for (int i = 0; i < rayOrigins.Length; i++)
            {
                hits[i] = Physics.Raycast(rayOrigins[i], Vector3.down, out var hit, _realColliderRadius * 2, _collisionLayers);
                if (hits[i])
                {
                    hitPoints[i] = hit.point;
                }
            }

            Vector3 refinedNormal = _groundNormal;
            
            if (hits[0] && hits[1] && hits[2])
            {
                refinedNormal += GetTriangleNormal(hitPoints[0], hitPoints[1], hitPoints[2]);
            }
            
            if (hits[1] && hits[3] && hits[2])
            {
                refinedNormal += GetTriangleNormal(hitPoints[1], hitPoints[3], hitPoints[2]);
            }

            _groundNormal = refinedNormal.normalized;
        }

        private void UpdateMovementVectors()
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            
            _forwardDirection = Vector3.Cross(-_groundNormal, transform.right).normalized;
            _rightDirection = Vector3.Cross(-_groundNormal, transform.forward).normalized;
            
            _forwardSpeed = Vector3.Dot(velocity, _forwardDirection);
            _lateralSpeed = Vector3.Dot(velocity, _rightDirection);

            _groundRotation = Quaternion.LookRotation(_forwardDirection, _groundNormal);
            float groundAngle = _groundRotation.eulerAngles.x;
            _slopeDelta = Mathf.Clamp(groundAngle > 180 ? groundAngle - 360 : groundAngle, -90, 90) / 90f;
        }

        private void UpdateMovement()
        {
            float deltaTime = Time.fixedDeltaTime;
            
            ApplySteering(deltaTime);
            UpdateGravityDirection();
            ApplyMovementForces(deltaTime);
        }

        private void ApplySteering(float deltaTime)
        {
            float steeringFactor = _isGrounded ? 1f : _airSteeringMultiplier;
            float directionFactor = _forwardSpeed < 0 ? -1f : 1f;
            float speedFactor = _steeringResponseCurve.Evaluate(GetForwardVelocityDelta);

            float steeringForce = steeringFactor * directionFactor * speedFactor;
            float steeringAmount = _maxSteeringRate * _steeringInput * deltaTime * steeringForce;

            _rigidbody.MoveRotation(Quaternion.Euler(
                0, 
                transform.rotation.eulerAngles.y + steeringAmount, 
                0));
        }

        private void UpdateGravityDirection()
        {
            _gravityDirection = _gravityMode switch
            {
                GravityMode.AlwaysDown => Vector3.down,
                GravityMode.TowardsGround => _isGrounded ? -_groundNormal : Vector3.down,
                _ => Vector3.down
            };
        }

        private void ApplyMovementForces(float deltaTime)
        {
            Vector3 velocity = _rigidbody.linearVelocity;

            if (_isGrounded)
            {
                ApplyGroundFriction(ref velocity, deltaTime);
                ApplyMotorForce(ref velocity, deltaTime);
            }

            ApplyGravity(ref velocity, deltaTime);

            _rigidbody.linearVelocity = velocity;
        }

        private void ApplyGroundFriction(ref Vector3 velocity, float deltaTime)
        {
            if (_motorInput == 0 || !Mathf.Approximately(Mathf.Sign(_motorInput), Mathf.Sign(_forwardSpeed)))
            {
                float frictionAmount = Mathf.Min(
                    Mathf.Abs(_forwardSpeed), 
                    _rollingResistance * _scaleAdjustment * deltaTime);
                
                velocity -= _forwardDirection * (Mathf.Sign(_forwardSpeed) * frictionAmount);
            }

            float lateralFrictionAmount = Mathf.Min(
                Mathf.Abs(_lateralSpeed), 
                _lateralGrip * _scaleAdjustment * deltaTime);
            
            velocity -= _rightDirection * (Mathf.Sign(_lateralSpeed) * lateralFrictionAmount);
        }

        private void ApplyMotorForce(ref Vector3 velocity, float deltaTime)
        {
            float slopeFactor = Mathf.Max(0, Mathf.Sign(_motorInput) * _slopeDelta * _slopeFrictionFactor * _scaleAdjustment + 1f);
            float acceleration = GetAcceleration(_boostMultiplier * slopeFactor, _boostMultiplier * slopeFactor);
            velocity += _forwardDirection * (_motorInput * acceleration * deltaTime);
        }

        private void ApplyGravity(ref Vector3 velocity, float deltaTime)
        {
            float adjustedMaxGravity = _maxGravityAcceleration * _scaleAdjustment;
            float adjustedGravityVelocity = _gravityAcceleration * _scaleAdjustment;
            
            float gravityForce = Mathf.Min(
                Mathf.Max(0, adjustedMaxGravity + velocity.y), 
                adjustedGravityVelocity * deltaTime);
            
            velocity += _gravityDirection * gravityForce;
        }

        private float GetAcceleration(float accelerationMultiplier = 1f, float speedMultiplier = 1f)
        {
            if (IsBraking() || Mathf.Approximately(speedMultiplier, 0))
            {
                return _brakeForce;
            }

            float speedRatio = Mathf.Abs(_forwardSpeed) / (GetMaxSpeed() * speedMultiplier);
            
            if (speedRatio > 1f)
            {
                return -GetMaxAcceleration() * accelerationMultiplier;
            }
            if (speedRatio < 0f)
            {
                return GetMaxAcceleration() * accelerationMultiplier;
            }
            
            return _accelerationCurve.Evaluate(speedRatio) * GetMaxAcceleration() * accelerationMultiplier;
        }

        private Vector3 GetTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            return Vector3.Cross(ab, ac).normalized;
        }
    }
}
