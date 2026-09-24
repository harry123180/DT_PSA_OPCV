using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelLongField : UnityEngine.UIElements.LongField
    {
#else
    public class PanelLongField : UnityEngine.UIElements.LongField
    {
        public new class UxmlFactory : UxmlFactory<PanelLongField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";

        public PanelLongField() : this(""){}
        
        public PanelLongField(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);

            onIsReadOnlyChanged += readOnly => EnableInClassList(USS_TEXT_INPUT_READ_ONLY, readOnly);
            this.isReadOnly = isReadOnly;
        }

        public PanelLongField(string label, IProperty<long> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelLongField(string label, Property<long> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelLongField(string label, long value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}