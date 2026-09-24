using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelStringField : UnityEngine.UIElements.TextField
    {
#else
    public class PanelStringField : UnityEngine.UIElements.TextField
    {
        public new class UxmlFactory : UxmlFactory<PanelStringField, UxmlTraits> { }
#endif

        private const string USS = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_FIELD_STRING = "panel-field-string";
        private const string USS_STRING_FIELD_ALT = "panel-field-stringfield_alt";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";
        private const string USS_UNITY_TEXT_INPUT = "unity-text-input";
        
        public sealed override string value
        {
            get => base.value;
            set => base.value = value;
        }

        public PanelStringField() : this(""){}

        public PanelStringField(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_FIELD_STRING);

            onIsReadOnlyChanged += readOnly => EnableInClassList(USS_TEXT_INPUT_READ_ONLY, readOnly);
            multiline = true;
            this.isReadOnly = isReadOnly;
        }

        public PanelStringField(string label, IProperty<string> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelStringField(string label, IPropertyReadOnly<string> property) : this(label, true)
        {
            this.BindProperty(property);
        }

        public PanelStringField(string label, string text) : this(label, true)
        {
            value = text;
        }
        
        public void ToggleStringFieldAltStyle(bool toggle)
        {
            EnableInClassList(USS_STRING_FIELD_ALT, toggle);
        }

        public void SetTextInputAlign(TextAnchor anchor)
        {
            this.Q(USS_UNITY_TEXT_INPUT).style.unityTextAlign = new StyleEnum<TextAnchor>(anchor);
        }
    }
}