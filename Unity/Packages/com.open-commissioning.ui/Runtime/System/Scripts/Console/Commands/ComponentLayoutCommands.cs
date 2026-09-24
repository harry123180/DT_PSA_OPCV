using Cysharp.Threading.Tasks;
using OC.UI.ComponentLayout;
using UnityEngine;

namespace OC.UI.Console.Commands
{
    public static class ComponentLayoutCommands
    {
        private static LayoutSaveSystem System =>
            LayoutSaveSystem.Instance != null
                ? LayoutSaveSystem.Instance
                : Object.FindAnyObjectByType<LayoutSaveSystem>();

        [ConsoleMethod("layout.save", "Saves component layout to XML (opens file browser)"), UnityEngine.Scripting.Preserve]
        public static void SaveLayout()
        {
            if (System == null)
            {
                Debug.LogError("[ComponentLayout] ComponentLayoutSaveSystem not found in scene.");
                return;
            }

            System.SaveAsync().Forget();
        }

        [ConsoleMethod("layout.load", "Loads component layout from XML (opens file browser)"), UnityEngine.Scripting.Preserve]
        public static void LoadLayout()
        {
            if (System == null)
            {
                Debug.LogError("[ComponentLayout] ComponentLayoutSaveSystem not found in scene.");
                return;
            }

            System.LoadAsync().Forget();
        }

        [ConsoleMethod("layout.status", "Shows component layout save status"), UnityEngine.Scripting.Preserve]
        public static string LayoutStatus()
        {
            if (System == null)
            {
                return "ComponentLayoutSaveSystem not found in scene.";
            }

            return
                $"Dirty: {System.IsDirty}\n" +
                $"Directory: {System.GetLayoutsDirectory()}\n" +
                $"Suggested file: {System.BuildDefaultFileName()}";
        }

        [ConsoleMethod("layout.import", "Imports layout from XML file path"), UnityEngine.Scripting.Preserve]
        public static void ImportLayout(string filePath)
        {
            if (System == null)
            {
                Debug.LogError("[ComponentLayout] ComponentLayoutSaveSystem not found in scene.");
                return;
            }

            System.ImportFromFile(filePath);
        }
    }
}
