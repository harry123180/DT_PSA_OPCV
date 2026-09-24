using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelBinaryStatusField : BaseField<bool>
    {
#else
    public class PanelBinaryStatusField : BaseField<bool>
    {
        public new class UxmlFactory : UxmlFactory<PanelBinaryStatusField, UxmlTraits> { }

        public new class UxmlTraits : BaseField<bool>.UxmlTraits
        {
            readonly UxmlBoolAttributeDescription _value = new UxmlBoolAttributeDescription { name = "Value", defaultValue = false };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                if (ve is PanelBinaryStatusField ate) ate.value = _value.GetValueFromBag(bag, cc);
            }
        }
#endif

        private readonly VisualElement _checkMark;
        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_BINARY_STATUS_FIELD = "panel-field-binary-status";
        private const string USS_BINARY_STATUS_FIELD_CHECKBOX = "panel-field-binary-status_checkbox";
        private const string USS_BINARY_STATUS_FIELD_CHECKBOX_CHECKED = "panel-field-binary-status_checkbox__checked";
        private IProperty<bool> _property;

        public PanelBinaryStatusField() : this("") { }

        public PanelBinaryStatusField(string label) : base(label, null)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_BINARY_STATUS_FIELD);

            _checkMark = new VisualElement
            {
                name = "unity-checkmark",
                pickingMode = PickingMode.Ignore
            };

            _checkMark.AddToClassList(USS_BINARY_STATUS_FIELD_CHECKBOX);

            hierarchy.Add(_checkMark);
        }

        public sealed override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            _checkMark.EnableInClassList(USS_BINARY_STATUS_FIELD_CHECKBOX_CHECKED, newValue);
        }
    }
}