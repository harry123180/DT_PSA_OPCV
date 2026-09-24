using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class TimeControlPanel : MonoBehaviour
    {
        private Toolbar.Toggle _toggle;
        private SliderInt _slider;
        private Label _sliderValue;
        private Label _timeValue;
        
        private const string UXML = "UXML/time_control";
        private const string STYLE_SHEET = "StyleSheet/time_control";
        
        private void Start()
        {
            var uiDocument = GetComponent<UIDocument>();
            var container = Resources.Load<VisualTreeAsset>(UXML).Instantiate().Q("container");
            container.AddDefaultTheme();
            container.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            
            uiDocument.rootVisualElement.Add(container);
            
            _toggle = container.Q<Toolbar.Toggle>();
            _slider = container.Q<SliderInt>();
            _sliderValue = container.Q<Label>("sliderValue");
            _timeValue = container.Q<Label>("timeValue");

            if (TimeManager.Instance == null)
            {
                Debug.LogError("Time Manager instance can't be found!. Add TimeManager in scene", this);
                return;
            }

            _toggle.SetValueWithoutNotify(TimeManager.Instance.Pause);
            OnTimeScaleChanged((int)TimeManager.Instance.TimeScale);

            _toggle.RegisterValueChangedCallback(evt => TimeManager.Instance.Pause.Value = evt.newValue);
            _slider.RegisterValueChangedCallback(evt =>
            {
                TimeManager.Instance.TimeScale.Value = evt.newValue;
            });

            TimeManager.Instance.TimeScale.OnValueChanged += value => OnTimeScaleChanged((int)value);
            TimeManager.Instance.Pause.OnValueChanged += value => _toggle.SetValueWithoutNotify(value);
            TimeManager.Instance.OnSecondTick += OnSecondTick;
        }

        private void OnSecondTick()
        {
            var currentTime = TimeSpan.FromSeconds(Time.time);
            _timeValue.text = currentTime.ToString(@"hh\:mm\:ss");
        }

        private void OnTimeScaleChanged(int value)
        {
            _slider.SetValueWithoutNotify(value);
            _sliderValue.text = $"x{value}";
        }
    } 
}
