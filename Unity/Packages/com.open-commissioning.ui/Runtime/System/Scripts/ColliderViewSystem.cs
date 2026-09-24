using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OC.UI.Interactions
{
    public class ColliderViewSystem : MonoBehaviour
    {
        public static ColliderViewSystem Singleton;

        private List<ColliderMaterial> _colliderViews;

        private void Awake()
        {
            if (Singleton == null) Singleton = this;
            else if (Singleton != this) Destroy(gameObject);
        }

        private void Start()
        {
            _colliderViews = Utils.FindObjectsByType<ColliderMaterial>().ToList();
            Show(false);
        }

        public void Show(bool show)
        {
            foreach (var colliderView in _colliderViews)
            {
                colliderView.EnableView(show);
            }
        }
    }
}
