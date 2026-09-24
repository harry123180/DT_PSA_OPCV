using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelProgressBarField : ProgressBar
    {
#else
    public class PanelProgressBarField : ProgressBar
    {
        public new class UxmlFactory : UxmlFactory<PanelProgressBarField, UxmlTraits> { }

        public new class UxmlTraits : UnityEngine.UIElements.ProgressBar.UxmlTraits { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_PROGRESS_BAR_FIELD = "panel-field-progress-bar";

        public PanelProgressBarField() : base()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_PROGRESS_BAR_FIELD);
        }

        public PanelProgressBarField(string title, IProperty<float> property, float low = 0, float high = 1) : this(title, low, high)
        {
            value = property.Value;
            property.OnValueChanged += value => SetValueWithoutNotify(value);
            this.RegisterValueChangedCallback(evt => property.Value = evt.newValue);
        }

        public PanelProgressBarField(string title, IPropertyReadOnly<float> property, float low = 0, float high = 1) : this(title, low, high)
        {
            value = property.Value;
            property.OnValueChanged += Property_ValueChanged;
        }

        private void Property_ValueChanged(float value)
        {
            SetValueWithoutNotify(value);
        }

        public PanelProgressBarField(string title, IProperty<float> property, Vector2 limits) : this(title, limits)
        {
            value = property.Value;
            property.OnValueChanged += value => SetValueWithoutNotify(value);
            this.RegisterValueChangedCallback(evt => property.Value = evt.newValue);
        }

        public PanelProgressBarField(string title, IPropertyReadOnly<float> property, Vector2 limits) : this(title, limits)
        {
            value = property.Value;
            property.OnValueChanged += value => SetValueWithoutNotify(value);
        }

        private PanelProgressBarField(string title) : this()
        {
            this.title = title;
        }

        private PanelProgressBarField(string title, float low, float high) : this(title)
        {
            lowValue = low;
            highValue = high;
        }

        private PanelProgressBarField(string title, Vector2 limits) : this(title)
        {
            lowValue = limits.x;
            highValue = limits.y;
        }
    }
}