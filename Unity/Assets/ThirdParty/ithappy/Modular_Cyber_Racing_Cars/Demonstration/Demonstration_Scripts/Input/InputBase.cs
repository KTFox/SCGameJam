using UnityEngine;

namespace ithappy
{
    public class InputBase : MonoBehaviour
    {
        [SerializeField] private CarController _carController;
        [SerializeField] private float _boostMultiplier = 2f;
        
        protected float _moveInput;
        protected float _steerInput;
        protected bool _shouldBoost;

        protected virtual void Update()
        {
            _carController.SetMotor(_moveInput);
            _carController.SetSteering(_steerInput);
            _carController.SetBoostMultiplier(_shouldBoost ? _boostMultiplier : 1f);
        }
    }
}
