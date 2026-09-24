using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelVector2IntField : UnityEngine.UIElements.Vector2IntField
    {
#else
    public class PanelVector2IntField : UnityEngine.UIElements.Vector2IntField
    {
        public new class UxmlFactory : UxmlFactory<PanelVector2IntField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_VECTOR = "panel-field-vector";
        private const string USS_VECTOR_INPUT_FIELD = "panel-field-vector__input-field";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";

        public PanelVector2IntField() : this(""){}

        public PanelVector2IntField(string label, bool isReadOnly = false) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_VECTOR);

            foreach (UnityEngine.UIElements.IntegerField intField in this.Query<UnityEngine.UIElements.IntegerField>().ToList())
            {
                intField.AddToClassList(USS_CONTAINER);
                intField.AddToClassList(USS_VECTOR_INPUT_FIELD);
                intField.EnableInClassList(USS_TEXT_INPUT_READ_ONLY, isReadOnly);
                intField.isReadOnly = isReadOnly;
            }
        }

        public PanelVector2IntField(string label, IProperty<Vector2Int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector2IntField(string label, Property<Vector2Int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector2IntField(string label, Vector2Int value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}