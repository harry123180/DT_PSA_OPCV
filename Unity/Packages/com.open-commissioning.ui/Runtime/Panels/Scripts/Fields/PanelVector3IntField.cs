using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelVector3IntField : UnityEngine.UIElements.Vector3IntField
    {
#else
    public class PanelVector3IntField : UnityEngine.UIElements.Vector3IntField
    {
        public new class UxmlFactory : UxmlFactory<PanelVector3IntField, UxmlTraits> { }
#endif
    

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_VECTOR = "panel-field-vector";
        private const string USS_VECTOR_INPUT_FIELD = "panel-field-vector__input-field";
        private const string USS_TEXT_INPUT_READ_ONLY = "panel-field-container_readonly";

        public PanelVector3IntField() : this(""){}

        public PanelVector3IntField(string label, bool isReadOnly = false) : base(label)
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

        public PanelVector3IntField(string label, IProperty<Vector3Int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector3IntField(string label, Property<Vector3Int> property, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.BindProperty(property);
        }

        public PanelVector3IntField(string label, Vector3Int value, bool isReadOnly = false) : this(label, isReadOnly)
        {
            this.value = value;
        }
    }
}