using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelToggle : UnityEngine.UIElements.Toggle
    {
#else
    public class PanelToggle : UnityEngine.UIElements.Toggle
    {
        public new class UxmlFactory : UxmlFactory<PanelToggle, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TOGGLE_CHECKBOX = "panel-field-toggle";
        private IProperty<bool> _property;

        public PanelToggle() : this("") {}

        public PanelToggle(string label) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_TOGGLE_CHECKBOX);
        }

        public PanelToggle(string label, Property<bool> property) : this(label) 
        {
            this.BindProperty(property);
        }
        
        public VisualElement Bind(IProperty<bool> property)
        {
            _property = property;
            _property.Subscribe(SetValueWithoutNotify);
            return this;
        }

        public VisualElement Unbind()
        {
            return this;
        }
    }
}