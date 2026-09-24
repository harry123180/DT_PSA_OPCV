using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OC.UI.Interactions
{
    public class HideGroup : MonoBehaviour
    {
        public string Name => string.IsNullOrEmpty(_name) ? name : _name;
        
        public ViewModeType ViewModeType
        {
            get => _viewModeType;
            set => SetViewMode(value);
        }

        public ViewModeState ViewModeState
        {
            get => _viewModeState;
            set => SetViewState(value);
        }

        public bool Hide
        {
            set
            {
                if (value)
                {
                    _viewModeType = ViewModeType.Hide;
                }
                else
                {
                    if (_viewModeType == ViewModeType.Hide) _viewModeType = ViewModeType.Default;
                }
                SetViewMode(_viewModeType);
            }
        }
        
        public bool Transparent
        {
            get => _viewModeType == ViewModeType.Transparent;
            set
            {
                if (value)
                {
                    _viewModeType = ViewModeType.Transparent;
                }
                else
                {
                    if (_viewModeType == ViewModeType.Transparent) _viewModeType = ViewModeType.Default;
                }
                SetViewMode(_viewModeType);
            }
        }

        [Header("Settings")] 
        [SerializeField] 
        private string _name;
        [SerializeField] 
        private ViewModeType _viewModeType;
        [SerializeField] 
        private ViewModeState _viewModeState;

        private ViewModeType _lastViewModeType;
        private ViewModeState _lastViewModeState;
        private List<Geometry> _geometries = new();

        private void Start()
        {
            _geometries = gameObject.GetComponentsInChildren<Geometry>().ToList();
        }

        private void SetViewMode(ViewModeType type)
        {
            foreach (var item in _geometries)
            {
                item.ViewModeType = type;
            }
        }

        private void SetViewState(ViewModeState state)
        {
            foreach (var item in _geometries)
            {
                item.ViewModeState = state;
            }
        }
    }
}
