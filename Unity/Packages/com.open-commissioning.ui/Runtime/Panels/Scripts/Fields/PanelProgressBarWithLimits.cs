using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelProgressBarWithLimits : ProgressBar
    {
#else
    public class PanelProgressBarWithLimits : ProgressBar
    {
        public new class UxmlFactory : UxmlFactory<PanelProgressBarWithLimits, UxmlTraits> { }

        public new class UxmlTraits : UnityEngine.UIElements.ProgressBar.UxmlTraits
        {
            private readonly UxmlColorAttributeDescription _colorProgressBar = new() { name = "Color-Bar", defaultValue = Color.white };
            private readonly UxmlColorAttributeDescription _colorBackground = new() { name = "Color-Background", defaultValue = new Color(0.5f, 0.5f, 0.5f, 0.5f) };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                if (ve is not PanelProgressBarWithLimits progressBar) return;
                progressBar.ColorBar = _colorProgressBar.GetValueFromBag(bag, cc);
                progressBar.ColorBackground = _colorBackground.GetValueFromBag(bag, cc);
            }
        }
#endif

#if UNITY_6000_3_OR_NEWER
        [UxmlAttribute("Color-Bar")]
#endif
        public Color ColorBar
        {
            get => _colorBar;
            set
            {
                _progressBar.style.backgroundColor = new StyleColor(value);
                _colorBar = value;
            }
        }

#if UNITY_6000_3_OR_NEWER
        [UxmlAttribute("Color-Background")]
#endif
        public Color ColorBackground
        {
            get => _colorBackground;
            set
            {
                _background.style.backgroundColor = new StyleColor(value);
                _colorBackground = value;
            }
        }

        public bool Min
        {
            get => _min;
            set
            {
                if (_min == value) return;
                _min = value;
                if (_min)
                {
                    _indicatorMin.AddToClassList(USS_BOX_INDICATOR_ACTIVE);
                }
                else
                {
                    _indicatorMin.RemoveFromClassList(USS_BOX_INDICATOR_ACTIVE);
                }
            }
        }
        
        public bool Max
        {
            get => _max;
            set
            {
                if (_max == value) return;
                _max = value;
                if (_max)
                {
                    _indicatorMax.AddToClassList(USS_BOX_INDICATOR_ACTIVE);
                }
                else
                {
                    _indicatorMax.RemoveFromClassList(USS_BOX_INDICATOR_ACTIVE);
                }
            }
        }

        private Color _colorBar;
        private Color _colorBackground;
        
        private readonly VisualElement _progressBar;
        private readonly VisualElement _background;
        private readonly VisualElement _indicatorMin;
        private readonly VisualElement _indicatorMax;

        private bool _min;
        private bool _max;
        private IPropertyReadOnly<float> _property;
        
        private const string USS = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_PROGRESS_BAR_FIELD = "panel-field-progress-bar";
        private const string USS_BOX_INDICATOR = "panel-field-progress-bar_indicator";
        private const string USS_BOX_INDICATOR_ACTIVE = "panel-field-progress-bar_indicator-active";

        
        public PanelProgressBarWithLimits() : this("") { }

        public PanelProgressBarWithLimits(string title)
        {
            this.title = title;
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_PROGRESS_BAR_FIELD);
            
            _progressBar = this.Q<VisualElement>(className: progressUssClassName);
            _background = this.Q<VisualElement>(className: backgroundUssClassName);

            _indicatorMin = new VisualElement();
            _indicatorMin.AddToClassList(USS_BOX_INDICATOR);
            _indicatorMax = new VisualElement();
            _indicatorMax.AddToClassList(USS_BOX_INDICATOR);
            
            var container = this.Q<VisualElement>(className: containerUssClassName);
            container.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);

            container.Add(_indicatorMin);
            _indicatorMin.SendToBack();
            container.Add(_indicatorMax);
            _indicatorMax.BringToFront();
        }
        
        public PanelProgressBarWithLimits(string title, IPropertyReadOnly<float> property, float low = 0, float high = 1) : this(title)
        {
            Bind(property, low, high);
        }

        public void Bind(IPropertyReadOnly<float> property, float low = 0, float high = 1)
        {
            lowValue = low;
            highValue = high;
            
            _property = property;
            _property.Subscribe(OnPropertyValueChanged);
        }

        public void Unbind()
        {
            _property?.Unsubscribe(OnPropertyValueChanged);
        }
        
        private void OnPropertyValueChanged(float propertyValue)
        {
            value = propertyValue;
            SetValueWithoutNotify(propertyValue);
            UpdateIndicators();
        }

        private void UpdateIndicators()
        {
            Min = value <= lowValue;
            Max = value >= highValue;
        }
    }
}