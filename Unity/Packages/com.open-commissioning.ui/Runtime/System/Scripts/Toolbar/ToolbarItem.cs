using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
    public class ToolbarItem : MonoBehaviour, IPopulateVisualTree
    {
        [Header("Settings")]
        [SerializeField]
        protected Sprite _defaultIcon;
        [SerializeField]
        protected Sprite _activeIcon;

        public UnityEvent<bool> OnToggleChanged;

        protected Toggle _toggle;

        public virtual void Populate(VisualElement root)
        {
            _toggle = new Toggle(_defaultIcon);
            _toggle.RegisterValueChangedCallback( evt =>
            {
                OnToggleChanged?.Invoke(evt.newValue);
            });
            root.Add(_toggle);
        }
    }
}