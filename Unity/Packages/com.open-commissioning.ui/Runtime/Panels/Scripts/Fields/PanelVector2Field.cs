using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelVector2Field : UnityEngine.UIElements.Vector2Field
    {
#else
    public class PanelVector2Field : UnityEngine.UIElements.Vector2Field
    {
        public new class UxmlFactory : UxmlFactory<PanelVector2Field, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_VECTOR = "panel-field-vector";
        private const string USS_VECTOR_INPUT_FIELD = "panel-field-vector__input-field";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";

        public PanelVector2Field() : this(""){}

        public PanelVector2Field(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_VECTOR);

            foreach (UnityEngine.UIElements.FloatField floatField in this.Query<UnityEngine.UIElements.FloatField>().ToList())
            {
                floatField.AddToClassList(USS_CONTAINER);
                floatField.AddToClassList(USS_VECTOR_INPUT_FIELD);
                floatField.EnableInClassList(USS_TEXT_INPUT_READ_ONLY, isReadOnly);
                floatField.isReadOnly = isReadOnly;
            }
        }

        public PanelVector2Field(string label, IProperty<Vector2> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector2Field(string label, Property<Vector2> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector2Field(string label, Vector2 value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}