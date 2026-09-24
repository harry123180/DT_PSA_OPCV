using OC.UI.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace OC.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class TooltipManager : MonoBehaviourSingleton<TooltipManager>
    {
        [SerializeField]
        private bool _enabled;

        private VisualElement _container;
        private Label _name;
        private Label _description;

        private const string USS = "StyleSheet/tooltip";

        private void Start()
        {
            var uiDocument = GetComponent<UIDocument>();

            _container = new VisualElement();
            _container.AddDefaultTheme();
            _container.styleSheets.Add(Resources.Load<StyleSheet>(USS));
            _container.AddToClassList("container");

            _name = new Label();
            _description = new Label();
            _container.Add(_name);
            _container.Add(_description);

            _name.AddToClassList("name");
            _description.AddToClassList("description");

            uiDocument.rootVisualElement.Add(_container);
            _container.BringToFront();
            Enable(false);
        }

        private void Update()
        {
            if (!_enabled || Mouse.current == null) return;

            var mousePosition = Mouse.current.position.ReadValue();
            _container.style.left = mousePosition.x + 25;
            _container.style.top = Screen.height - mousePosition.y + 25;
        }

        public void Show(ITooltip tooltip)
        {
            _name.text = tooltip.Name;

            if (string.IsNullOrEmpty(tooltip.Description))
            {
                _description.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            else
            {
                _description.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                _description.text = tooltip.Description;
            }

            Enable(true);
        }

        public void Hide()
        {
            _name.text = string.Empty;
            _description.text = string.Empty;
            Enable(false);
        }

        private void Enable(bool enable)
        {
            if (_container == null) return;
            _enabled = enable;
            _container.EnableInClassList("show", _enabled);
            _container.EnableInClassList("hide", !_enabled);
        }
    }
}
