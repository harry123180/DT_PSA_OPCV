using System;
using OC.Interactions;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace OC.UI.Panel
{
    public abstract class Panel<T> : VisualElement, IPanel where T : Component
    {
        public Interaction Interaction => _interaction;
        public VisualElement Root => this;
        public Type ReferenceType => typeof(T);
        
        public bool ShowCloseButton
        {
            set => _buttonClose.style.display = value ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }
        
        public bool ShowFocusButton
        {
            set => _buttonFocus.style.display = value ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }
        
        public bool ShowPinButton
        {
            set => _buttonPin.style.display = value ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        public bool Enable
        {
            get => _enable;
            set
            {
                if (_enable == value) return;
                _enable = value;
                SetEnabled(value);
                style.display = _enable ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        public bool Pinned { get; set; }

        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        private const string UXML = "UXML/panel_component";
        private const string USS = "StyleSheet/panel";
        private const string USS_CONTAINER = "panel-container";
        private const string USS_BUTTON_ACTIVE = "panel-header-button-active";
        private const string USS_COMPONENT_PANEL = "component-panel";
        
        private Interaction _interaction;
        protected T _target;
        private bool _enable;
        private readonly VisualElement _content;
        private readonly Label _title;
        private readonly Button _buttonFocus;
        private readonly Button _buttonPin;
        private readonly Button _buttonClose;

        private readonly TransformComponent _transformComponent;
        
        protected abstract void Create();

        public event Action<IPanel> OnFocusClicked;
        public event Action<IPanel> OnPinClicked;
        public event Action<IPanel> OnCloseClicked;

        public Panel()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            this.AddDefaultTheme();
            AddToClassList(USS_COMPONENT_PANEL);
            AddToClassList(USS_CONTAINER);
            
            var template = Resources.Load<VisualTreeAsset>(UXML).CloneTree();
            var header = template.Q("header");
            _content = template.Q("content");
            
            hierarchy.Add(header);
            hierarchy.Add(_content);
            this.AddManipulator(new PanelDragAndDrop(this));
            
            _title = header.Q<Label>("title");
            _buttonFocus = header.Q<Button>("focus");
            _buttonClose = header.Q<Button>("close");
            _buttonPin = header.Q<Button>("pin");
            
            _buttonFocus.clicked += () => { OnFocusClicked?.Invoke(this); };
            _buttonClose.clicked += () => { OnCloseClicked?.Invoke(this); };
            _buttonPin.clicked += OnPinClickedAction;
            
            Add(_transformComponent = new TransformComponent());
            
            // ReSharper disable once VirtualMemberCallInConstructor
            Create();
        }

        public void Bind(Interaction interaction)
        {
            _interaction = interaction;

            if (_interaction.Interactable == null)
            {
                throw new ArgumentException("Interaction must have an interactable");
            }

            _target = _interaction.Interactable.Component as T;

            if (_target == null)
            {
                throw new AggregateException("Interaction target must be of type " + typeof(T).Name + "");
            }
            
            Title = _interaction.Target.name;
            
            _transformComponent.Bind(_target.transform);
            
            InternalBind(_target);
        }

        public void Unbind()
        {
            if (_target == null) return;
            InternalUnbind();
            _transformComponent.Unbind();
        }

        protected abstract void InternalBind(T component); 
        protected abstract void InternalUnbind();
    

        protected new void Add(VisualElement visualElement)
        {
            _content.Add(visualElement);
        }

        private void OnPinClickedAction()
        {
            Pinned = !Pinned;
            _buttonPin.EnableInClassList(USS_BUTTON_ACTIVE, Pinned);
            OnPinClicked?.Invoke(this);
        }
    }
}