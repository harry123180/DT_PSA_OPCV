using UnityEngine;

namespace OC.UI
{
    public class QuitApplication : MonoBehaviour
    {
        public void Quit()
        {
            Logging.Logger.Log(LogType.Log, "Quit Application");
            Application.Quit();
        }
    }
}


