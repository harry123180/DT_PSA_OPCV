using OC.Interactions;
using UnityEditor;
using UnityEngine;
using OC.UI.Interactions;

namespace OC.UI.Editor
{
    public static class InteractionUtils 
    {
        [MenuItem ("GameObject/Open Commissioning/Add Interaction")]
        public static void AddInteraction(MenuCommand menuCommand)
        {
            var gameObject = menuCommand.context as GameObject;
            if (gameObject == null) return;

            var collider = gameObject.GetOrCreateComponent<BoxCollider>();
            var _ = gameObject.GetOrCreateComponent<Interaction>();

            var bounds = gameObject.GetChildMeshBoundBox();
            collider.center = bounds.center;
            collider.size = bounds.size;
            collider.isTrigger = true;
        }
        
        [MenuItem ("GameObject/Open Commissioning/Add Interaction", true)]
        public static bool AddInteractionValidation()
        {
            return Selection.count > 0;
        }
        
        public static T GetOrCreateComponent<T>(this GameObject gameObject) where T : Component
        {
            var result = gameObject.GetComponent<T>();
            if (result == null)
            {
                result = UnityEditor.Undo.AddComponent<T>(gameObject);
            }
            return result;
        }

        public static Bounds GetChildMeshBoundBox(this GameObject gameObject)
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(gameObject.transform.position, Vector3.one);

            var result = new Bounds(
                    gameObject.transform.InverseTransformPoint(renderers[0].bounds.center),
                    gameObject.transform.InverseTransformVector(renderers[0].bounds.size));

            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                bounds.center = gameObject.transform.InverseTransformPoint(bounds.center);
                bounds.size = gameObject.transform.InverseTransformVector(bounds.size);
                result.Encapsulate(bounds);
            }

            return result;
        }
    }
}
