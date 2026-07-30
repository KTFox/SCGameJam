using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ithappy
{
    public class CarUiElement : MonoBehaviour
    {
        public Action<CarUiElement, int> OnValueChanges;
        
        [SerializeField] private Text _elementText;
        [SerializeField] private Slider _slider;
    
        public CarElementName ElementName { get; private set; }
        
        public void Initialize(CarElementSettings carElementSettings)
        {
            ElementName = carElementSettings.ElementName;
            _elementText.text = carElementSettings.ElementName.ToString();
            _slider.value = 0f;
            _slider.maxValue = carElementSettings.Elements.Count - 1;
            _slider.onValueChanged.AddListener(OnElementSwitch);
        }

        public void Dispose()
        {
            _slider.onValueChanged.RemoveAllListeners();
        }

        private void OnElementSwitch(float index)
        {
            OnValueChanges?.Invoke(this, (int)index);
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
