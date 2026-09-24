using Cysharp.Threading.Tasks;

namespace OC.UI.ComponentLayout
{
    /// <summary>
    /// Default runtime hooks for toolbar/menu. Wire button clicks to these methods.
    /// </summary>
    public class LayoutRuntimeActions : ILayoutRuntimeActions
    {
        public void OnSaveLayoutRequested()
        {
            LayoutSaveSystem.Instance?.SaveAsync().Forget();
        }

        public void OnOpenImportLayoutRequested()
        {
            LayoutSaveSystem.Instance?.LoadAsync().Forget();
        }
    }
}
