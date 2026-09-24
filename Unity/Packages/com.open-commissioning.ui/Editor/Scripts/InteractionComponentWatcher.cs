using OC.UI.Interactions;
using UnityEditor;
using UnityEngine;
using Interaction = OC.Interactions.Interaction;

namespace OC.UI.Editor
{
    [InitializeOnLoad]
    public static class InteractionComponentWatcher
    {
        static InteractionComponentWatcher()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            var gameObject = component.gameObject;

            if (component is Interaction)
            {
                if (gameObject.GetComponent<Outline>() == null)
                {
                    UnityEditor.Undo.AddComponent<Outline>(gameObject);
                    Logging.Logger.Log(LogType.Log, $"Added Outline to {gameObject.name}", gameObject);
                }
            }
        }
    }
}