using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    public class SceneManager : MonoBehaviour
    {
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private UiManager _uiManager;
        [SerializeField] private List<CarCustomizer> _carCustomizers;
        [SerializeField] private Car _currentCar;
        
        private int _carIndex = 0;

        private void Awake()
        {
            _currentCar.Initialize(_carCustomizers[0]);

            _uiManager.Initialize(_currentCar.CarCustomizer.Elements);
            _uiManager.OnSwitchCar += OnSwitchCar;
            _uiManager.OnSwitchCarElement += OnSwitchCarElement;
        }

        private void OnDestroy()
        {
            _currentCar.Dispose();
            _uiManager.Dispose();
        }

        private void OnSwitchCarElement(CarElementName elementName, int index)
        {
            _currentCar.CarCustomizer.SwitchCarElement(elementName, index);
        }

        private void OnSwitchCar(int carIndex)
        {
            _carIndex = carIndex;

            if (_carIndex < 0)
            {
                _carIndex = _carCustomizers.Count - 1;
            }

            if (_carIndex >= _carCustomizers.Count)
            {
                _carIndex = 0;
            }

            SwitchCar();
        }

        private void SwitchCar()
        {
            _currentCar.SwitchVisual(_carCustomizers[_carIndex]);
            _uiManager.SwitchCar(_currentCar.CarCustomizer.Elements);
        }
    }
}
