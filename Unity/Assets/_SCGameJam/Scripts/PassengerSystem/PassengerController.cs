using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace SCJam.PassengerSystem
{
    public class PassengerController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private float _moveSpeed;


        // ===== Private Fields ===== //

        private Passenger _passenger;
        private bool _isMoving;


        // ===== Public Properties ===== //

        public Passenger Passenger => _passenger;
        public bool IsMoving => _isMoving;


        // ===== Methods ===== //

        public void Initialize(Passenger passenger)
        {
            _passenger = passenger;
        }

        /// <summary>
        /// Plays the walk-in animation for a passenger already marked MovingToVehicle by BoardingResolver.
        /// </summary>
        public void MoveToVehicle(Vector3 boardingWorldPosition)
        {
            if (_isMoving || _passenger.State != PassengerState.MovingToVehicle)
                return;

            BoardVehicleRoutine(boardingWorldPosition).Forget();
        }

        private async UniTaskVoid BoardVehicleRoutine(Vector3 boardingWorldPosition)
        {
            _isMoving = true;

            await transform.DOMove(boardingWorldPosition, ComputeDuration(boardingWorldPosition)).SetEase(Ease.Linear).ToUniTask();

            _passenger.ChangeState(PassengerState.Completed);
            _isMoving = false;
            gameObject.SetActive(false);
        }

        private float ComputeDuration(Vector3 targetPosition)
        {
            if (_moveSpeed <= 0f)
                return 0f;

            return Vector3.Distance(transform.position, targetPosition) / _moveSpeed;
        }
    }
}
