using OC.UI.Panel;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
    public abstract class ToolbarWindow : ToolbarItem
    {
        private SubsystemPanel _subsystemPanel;

        public override void Populate(VisualElement root)
        {
            base.Populate(root);
            root.parent.Add(CreatePanel());
            AddContent(_subsystemPanel);
        }

        protected abstract void AddContent(SubsystemPanel subsystemPanel);

        private VisualElement CreatePanel()
        {
            _subsystemPanel = new SubsystemPanel(name)
            {
                Enable = false
            };
            _subsystemPanel.OnEnableChanged += isEnabled => _toggle.SetValueWithoutNotify(isEnabled);
            _toggle.RegisterCallback<ChangeEvent<bool>>(evt => _subsystemPanel.Enable = evt.newValue);
            return _subsystemPanel;
        }
    } 
}
