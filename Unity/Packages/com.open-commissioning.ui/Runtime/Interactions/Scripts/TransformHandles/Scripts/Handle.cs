using System.Collections.Generic;
using System.Linq;
using OC.UI.TransformHandles;
using UnityEngine;

namespace OC.UI
{
    public class Handle : MonoBehaviour
    {
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Refresh();
            }
        }
        
        [SerializeField]
        private bool _enabled;
        [SerializeField]
        private List<HandleBase> _handles;
        
        private List<Collider> _colliders = new ();

        private void Awake()
        {
            _handles = GetComponentsInChildren<HandleBase>().ToList();
            _colliders = GetComponentsInChildren<Collider>().ToList();
            Refresh();
        }

        private void Reset()
        {
            _handles = GetComponentsInChildren<HandleBase>().ToList();
        }
        
        private void Refresh()
        {
            foreach (var handle in _handles)
            {
                handle.Enabled = _enabled;
            }

            foreach (var item in _colliders)
            {
                item.enabled = _enabled;
            }
        }
    }
}
