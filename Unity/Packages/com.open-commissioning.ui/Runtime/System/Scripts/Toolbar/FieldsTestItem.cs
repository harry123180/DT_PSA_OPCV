using OC.UI.Panel;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
    public class FieldsTestItem : ToolbarWindow
    {
        private const string UXML = "UXML/panel-field";

        public Property<bool> ToggleSlideValue = new(false);
        public Property<bool> ToggleValue = new(false);
        public Property<bool> BinaryStatusValue = new(true);
        public Property<string> StringValue = new("test");
        public Property<float> FloatValue = new(0.3f);
        public Property<int> IntValue = new(12);
        public Property<Vector3> Vector3Value;
        public Property<Vector3Int> Vector3IntValue;
        public Property<Vector2> Vector2Value;
        public Property<Vector2Int> Vector2IntValue;
        public Property<int> SliderIntValue = new (12);
        public Property<float> SliderValue = new (0.3f);

        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            var fields = Resources.Load<VisualTreeAsset>(UXML).Instantiate();
            subsystemPanel.Add(fields);

            fields.Q<PanelToggleSlide>().BindProperty(ToggleSlideValue);
            fields.Q<PanelToggle>().BindProperty(ToggleValue);
            fields.Q<PanelBinaryStatusField>().BindProperty(BinaryStatusValue);
            fields.Q<PanelStringField>().BindProperty(StringValue);
            fields.Q<PanelFloatField>().BindProperty(FloatValue);
            fields.Q<PanelIntegerField>().BindProperty(IntValue);
            fields.Q<PanelVector3Field>().BindProperty(Vector3Value);
            fields.Q<PanelVector3IntField>().BindProperty(Vector3IntValue);
            fields.Q<PanelVector2Field>().BindProperty(Vector2Value);
            fields.Q<PanelVector2IntField>().BindProperty(Vector2IntValue);
            fields.Q<PanelSliderInt>().BindProperty(SliderIntValue);
            fields.Q<PanelSlider>().BindProperty(SliderValue);
        }

        private void OnValidate()
        {
            ToggleSlideValue.OnValidate();
            ToggleValue.OnValidate();
            BinaryStatusValue.OnValidate();
            StringValue.OnValidate();
            FloatValue.OnValidate();
            IntValue.OnValidate();
            Vector3Value.OnValidate();
            Vector3IntValue.OnValidate();
            Vector2Value.OnValidate();
            Vector2IntValue.OnValidate();
            SliderIntValue.OnValidate();
            SliderValue.OnValidate();
        }
    }
}
