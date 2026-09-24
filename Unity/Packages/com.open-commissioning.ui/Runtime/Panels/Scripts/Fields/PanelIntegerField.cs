using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelIntegerField : UnityEngine.UIElements.IntegerField
    {
#else
    public class PanelIntegerField : UnityEngine.UIElements.IntegerField
    {
        public new class UxmlFactory : UxmlFactory<PanelIntegerField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";

        public PanelIntegerField() : this ("") {}

        public PanelIntegerField(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);

            onIsReadOnlyChanged += readOnly => EnableInClassList(USS_TEXT_INPUT_READ_ONLY, readOnly);
            this.isReadOnly = isReadOnly;
        }

        public PanelIntegerField(string label, IProperty<int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelIntegerField(string label, Property<int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelIntegerField(string label, int value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}