using System;
using OC.Interactions.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Industrial
{
    public class Knob : VisualElement
    {
        public event Action<bool> ValueChanged;
        public event Action Down;
        public event Action Up;
        
        public bool Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                SetValueWithoutNotify(value);
                ValueChanged?.Invoke(value);
            }
        }
        
        public bool Feedback
        {
            get => _feedback;
            set
            {
                if (_feedback == value) return;
                _feedback = value;
                SetLampStyle(value);
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                if (_color == value) return;
                SetColor(value);
            }
        }
        
        private bool _value;
        private bool _feedback;
        private Color _color;
        
        private readonly MouseEvents _mouseEvents;

        private const string UXML = "UXML/industrial-knob";
        private const string USS_KNOB_ACTIVE = "knob-active";
        
        private readonly VisualElement _lamp;
        private readonly VisualElement _knob;
        private readonly StyleColor _initStyleColor;
        private StyleColor _enableStyleColor;
        
        public Knob(string name, Color color, VisualTreeAsset template = null)
        {
            var container = new VisualElement();
            if (template == null)
            {
                Resources.Load<VisualTreeAsset>(UXML).CloneTree(container);
            }
            else
            {
                template.CloneTree(container);
            }

            hierarchy.Add(container);

            container.Q<Label>("label").text = name.ToUpper();
            _lamp = container.Q("lamp");
            _knob = container.Q("knob");

            _initStyleColor = _enableStyleColor = _lamp.style.backgroundColor;
            SetColor(color);

            _mouseEvents = new MouseEvents()
            {
                target = _knob
            };

            _mouseEvents.Up += () => Up?.Invoke();
            _mouseEvents.Down += () => Down?.Invoke();
        }
        
        public void SetValueWithoutNotify(bool state)
        {
            _value = state;
            SetKnobStyle(state);
        }

        private void SetKnobStyle(bool active)
        {
            if (active)
            {
                _knob.AddToClassList(USS_KNOB_ACTIVE);
            }
            else
            {
                _knob.RemoveFromClassList(USS_KNOB_ACTIVE);
            }
        }

        private void SetLampStyle(bool active)
        {
            _lamp.style.backgroundColor = active ? _enableStyleColor : _initStyleColor;
        }

        private void SetColor(Color color)
        {
            _color = color;
            _knob.style.backgroundColor = _color.ScaleRGB(0.6f);
            _enableStyleColor.value = _color;
        }
    }
}