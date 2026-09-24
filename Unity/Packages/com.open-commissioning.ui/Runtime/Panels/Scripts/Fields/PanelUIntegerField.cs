using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelUIntegerField : UnsignedIntegerField
    {
#else
    public class PanelUIntegerField : UnsignedIntegerField
    {
        public new class UxmlFactory : UxmlFactory<PanelUIntegerField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";


        public PanelUIntegerField() : this(""){}
        
        public PanelUIntegerField(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);

            onIsReadOnlyChanged += readOnly => EnableInClassList(USS_TEXT_INPUT_READ_ONLY, readOnly);
            this.isReadOnly = isReadOnly;
        }

        public PanelUIntegerField(string label, IProperty<uint> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelUIntegerField(string label, IPropertyReadOnly<uint> property, bool isReadOnly = true) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelUIntegerField(string label, Property<uint> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelUIntegerField(string label, uint value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}