using SCJam.LevelSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    /// <summary>
    /// HUD booster button that spends one charge to enter "pick a vehicle" mode: every parked vehicle
    /// highlights, and the one the player taps is dropped straight into a free waiting slot regardless of
    /// whether its path is blocked. Stays interactable only while
    /// <see cref="LevelController.CanTeleportVehicle"/> is true (charges left, a free waiting slot, and no
    /// targeting already in progress), and shows the remaining charge count when a label is assigned.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TeleportVehicleBoosterButton : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private LevelController _levelController;
        [SerializeField] private TextMeshProUGUI _chargeCountText;


        // ===== Private Fields ===== //

        private Button _button;


        // ===== Methods ===== //

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClicked);
            _levelController.TeleportVehicleChargesChanged += OnChargesChanged;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
            _levelController.TeleportVehicleChargesChanged -= OnChargesChanged;
        }

        private void Refresh()
        {
            _button.interactable = _levelController.CanTeleportVehicle;

            if (_chargeCountText != null)
                _chargeCountText.text = _levelController.RemainingTeleportVehicleCharges.ToString();
        }

        private void OnClicked()
        {
            _levelController.TryBeginTeleportVehicleTargeting();
        }

        private void OnChargesChanged(int remainingCharges)
        {
            Refresh();
        }
    }
}
