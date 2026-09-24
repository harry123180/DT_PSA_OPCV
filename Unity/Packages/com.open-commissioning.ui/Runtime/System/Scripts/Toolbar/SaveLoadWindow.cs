using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using OC.UI.ComponentLayout;
using OC.UI.Panel;
using OC.UI.Toolbar;

namespace OC.UI
{
    public class SaveLoadPanel : ToolbarWindow
    {
        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            subsystemPanel.Add(new PanelButton("Save", OnSaveClicked));
            subsystemPanel.Add(new PanelButton("Load", OnLoadClicked));
        }

        [Button]
        private void OnSaveClicked()
        {
            LayoutSaveSystem.Instance.SaveAsync().Forget();
        }
        
        [Button]
        private void OnLoadClicked()
        {
            LayoutSaveSystem.Instance.LoadAsync().Forget();
        }
    }
}
