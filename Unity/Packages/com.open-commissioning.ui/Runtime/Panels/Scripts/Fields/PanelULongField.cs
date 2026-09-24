using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelULongField : UnsignedLongField
    {
#else
    public class PanelULongField : UnsignedLongField
    {
        public new class UxmlFactory : UxmlFactory<PanelULongField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";


        public PanelULongField() : this("") { }
        
        public PanelULongField(string label, bool isReadonly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);

            onIsReadOnlyChanged += readOnly => EnableInClassList(USS_TEXT_INPUT_READ_ONLY, readOnly);
            this.isReadOnly = isReadonly;
        }

        public PanelULongField(string label, IProperty<ulong> property, bool isReadonly = false) : this(label, isReadonly)
        {
            this.BindProperty(property);
        }

        public PanelULongField(string label, IPropertyReadOnly<ulong> property, bool isReadonly = true) : this(label, isReadonly)
        {
            this.BindProperty(property);
        }

        public PanelULongField(string label, Property<ulong> property, bool isReadonly = false) : this(label, isReadonly)
        {
            this.BindProperty(property);
        }

        public PanelULongField(string label, IProperty<ulong> property, IProperty<bool> isReadOnly) : this(label)
        {
            this.BindProperty(property);
            this.BindReadOnlyProperty(isReadOnly);
        }

        public PanelULongField(string label, Property<ulong> property, IProperty<bool> isReadOnly) : this(label)
        {
            this.BindProperty(property);
            this.BindReadOnlyProperty(isReadOnly);
        }

        public PanelULongField(string label, ulong value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}