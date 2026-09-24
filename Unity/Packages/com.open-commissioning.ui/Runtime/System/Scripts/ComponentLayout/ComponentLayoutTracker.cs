using OC.Components;
using UnityEngine;

namespace OC.UI.ComponentLayout
{
    public static class ComponentLayoutTracker
    {
        public static bool IsTrackedTransform(Transform transform)
        {
            if (transform == null) return false;

            return transform.GetComponent<SensorBinary>() != null
                   || transform.GetComponent<SensorAnalog>() != null
                   || transform.GetComponent<Cylinder>() != null;
        }

        public static void NotifyTransformChanged(Transform transform)
        {
            if (!IsTrackedTransform(transform)) return;

            var system = LayoutSaveSystem.Instance;
            if (system != null)
            {
                system.MarkDirty();
            }
        }
    }
}
