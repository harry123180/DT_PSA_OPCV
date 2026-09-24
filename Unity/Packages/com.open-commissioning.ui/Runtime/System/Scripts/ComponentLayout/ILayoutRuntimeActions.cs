namespace OC.UI.ComponentLayout
{
    public interface ILayoutRuntimeActions
    {
        void OnSaveLayoutRequested();
        void OnOpenImportLayoutRequested();
    }

    public class LayoutRuntimeActionsStub : ILayoutRuntimeActions
    {
        public void OnSaveLayoutRequested()
        {
        }

        public void OnOpenImportLayoutRequested()
        {
        }
    }
}
