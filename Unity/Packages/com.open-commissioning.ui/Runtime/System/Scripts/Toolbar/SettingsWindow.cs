using OC.UI.ComponentLayout;
using OC.UI.Panel;

namespace OC.UI.Toolbar
{
    public class SettingsWindow: ToolbarWindow
    {
        private PanelSliderInt _mouseSensitivity;
        private PanelToggleSlide _autoLoadSave;

        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            _mouseSensitivity = new PanelSliderInt("Mouse sensitivity")
            {
                showInputField = true,
                lowValue = 1,
                highValue = 10,
                value = SettingsManager.Instance.MouseSensitivity
            };

            _autoLoadSave = new PanelToggleSlide("Auto load save")
            {
                value = SettingsManager.Instance.AutoLoadSave
            };

            var applyButton = new PanelButton("Apply", ApplySettings);
            
            subsystemPanel.Add(_mouseSensitivity);
            subsystemPanel.Add(_autoLoadSave);
            subsystemPanel.Add(applyButton);
        }
        
        private void ApplySettings()
        {
            var enableAutoLoad = _autoLoadSave.value;
            SettingsManager.Instance.MouseSensitivity = _mouseSensitivity.value;
            SettingsManager.Instance.AutoLoadSave = enableAutoLoad;

            if (enableAutoLoad)
            {
                LayoutSaveSystem.Instance?.TryAutoLoadLatestIfEnabled();
            }
        }
    }
} 