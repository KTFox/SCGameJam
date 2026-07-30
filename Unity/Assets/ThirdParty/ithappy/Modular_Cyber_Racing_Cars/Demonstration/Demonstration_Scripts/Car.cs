using UnityEngine;

namespace ithappy
{
    public class Car : MonoBehaviour
    {
        [SerializeField] private Transform _carCustomizerParent;
        [SerializeField] private CarFxController _carFxController;
        [SerializeField] private CarController _carController;
        
        private CarCustomizer _carCustomizer;
        
        public CarCustomizer CarCustomizer => _carCustomizer;

        public void Initialize(CarCustomizer carCustomizerPrefab)
        {
            _carController.Initialize();
            
            SwitchVisual(carCustomizerPrefab);
        }

        public void Dispose()
        {
            _carController.Dispose();
            _carCustomizer.Dispose();
        }

        public void SwitchVisual(CarCustomizer carCustomizerPrefab)
        {
            if (_carCustomizer != null)
            {   
                _carCustomizer.Dispose();
                Destroy(_carCustomizer.gameObject);
            }
            
            _carCustomizer = Instantiate(carCustomizerPrefab, _carCustomizerParent);
            _carCustomizer.Initialize(_carFxController);
        }
    }
}
