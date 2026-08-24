using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    [RequireComponent(typeof(Button))]
    public class SettingsButton : MonoBehaviour
    {
        // ===== Private Fields ===== //

        private Button _button;


        // ===== Methods ===== //

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnSettingsClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnSettingsClicked);
        }

        private void OnSettingsClicked()
        {
            if (PopupManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(SettingsButton)}] {nameof(PopupManager)} is not available in the loaded scenes.", this);
                return;
            }

            PopupManager.Instance.Show(PopupId.Setting);
        }
    }
}
