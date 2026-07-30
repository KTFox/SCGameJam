using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ithappy
{
    public class UiManager : MonoBehaviour
    {
        public Action<int> OnSwitchCar;
        public Action<CarElementName, int> OnSwitchCarElement;

        [SerializeField] private CarUiElement _carUiElementPrefab;
        [SerializeField] private RectTransform _carUiElementParent;
        [SerializeField] private Slider _carChooseSlider;

        private List<CarUiElement> _currentCarUiElements = new List<CarUiElement>();

        public void Initialize(List<CarElementSettings> carElements)
        {
            InitCarChooseSlider(carElements);
            InitNewCar(carElements);
        }

        public void Dispose()
        {
            _carChooseSlider.onValueChanged.RemoveAllListeners();
            DisposeCarUiElements();
        }

        private void InitCarChooseSlider(List<CarElementSettings> carElements)
        {
            _carChooseSlider.value = 0f;
            _carChooseSlider.maxValue = carElements.Count - 1;
            _carChooseSlider.onValueChanged.AddListener(OnCarSwitch);
        }

        private void OnCarSwitch(float carIndex)
        {
            OnSwitchCar?.Invoke((int)carIndex);
        }

        public void SwitchCar(List<CarElementSettings> carElements)
        {
            InitNewCar(carElements);
        }

        private void DisposeCarUiElements()
        {
            foreach (CarUiElement oldElement in _currentCarUiElements)
            {
                oldElement.Dispose();
                Destroy(oldElement.gameObject);
            }

            _currentCarUiElements.Clear();
        }

        private void InitNewCar(List<CarElementSettings> carElements)
        {
            DisposeCarUiElements();

            CarUiElement carUiElement;
            foreach (var item in carElements)
            {
                if (item.Elements.Count <= 1)
                {
                    continue;
                }

                carUiElement = Instantiate(_carUiElementPrefab, _carUiElementParent);
                carUiElement.Initialize(item);
                carUiElement.OnValueChanges += ValueChanges;
                _currentCarUiElements.Add(carUiElement);
            }
        }

        private void ValueChanges(CarUiElement uiElement, int index)
        {
            OnSwitchCarElement?.Invoke(uiElement.ElementName, index);
        }
    }
}
