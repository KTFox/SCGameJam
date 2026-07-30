using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    public class CarCustomizer : MonoBehaviour
    {
        [SerializeField] private Transform _elementParent;
        [SerializeField] private List<CarElementSettings> _elements;
        [SerializeField] private Transform[] _wheelParents;
        [SerializeField] private Transform[] _visualWheelsFront;
        [SerializeField] private Transform[] _visualWheelsRear;
        [SerializeField] private Transform _racerParent;
        
        private Dictionary<CarElementName, List<GameObject>> _carElementInfos = new Dictionary<CarElementName, List<GameObject>>();
        
        public List<CarElementSettings> Elements => _elements;

        public void Initialize(CarFxController carFxController)
        {
            carFxController.SetWheels(_visualWheelsFront,  _visualWheelsRear);
            
            foreach (var element in _elements)
            {
                SwitchCarElement(element.ElementName, 0);
            }
        }

        public void Dispose()
        {
        }
        
        public void SwitchCarElement(CarElementName elementName, int index)
        {
            _carElementInfos.TryAdd(elementName, new List<GameObject>());

            foreach (var element in _carElementInfos[elementName])
            {
                Destroy(element.gameObject);
            }
            _carElementInfos[elementName].Clear();

            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].ElementName == elementName && _elements[i].Elements.Count > index)
                {
                    GameObject element;
                    if (elementName == CarElementName.Wheel)
                    {
                        foreach (var parent in _wheelParents)
                        {
                            element = Instantiate(_elements[i].Elements[index], parent);
                            _carElementInfos[elementName].Add(element);
                            parent.localPosition = Vector3.zero;
                        }
                    }
                    if (elementName == CarElementName.Racer)
                    {
                        element = Instantiate(_elements[i].Elements[index], _racerParent);
                        _carElementInfos[elementName].Add(element);
                    }
                    else
                    {
                        element = Instantiate(_elements[i].Elements[index], _elementParent);
                        _carElementInfos[elementName].Add(element);
                    }
                }
            }
        }
    }
}
