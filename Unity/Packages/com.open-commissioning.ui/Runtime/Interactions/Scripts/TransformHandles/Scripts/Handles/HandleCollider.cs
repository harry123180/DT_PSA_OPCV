using OC.UI.TransformHandles;
using UnityEngine;

namespace OC.UI
{
    [RequireComponent(typeof(Collider))]
    public class HandleCollider : MonoBehaviour
    {
        public HandleBase Handle => _handle;
        
        [SerializeField]
        private HandleBase _handle;
        
        public void Reset()
        {
            _handle = transform.GetComponentInParent<HandleBase>(2);
        }
    }
}
