using NaughtyAttributes;
using OC.Interactions;
using UnityEngine;

namespace OC.UI.Interactions
{
    [RequireComponent(typeof(Interaction))]
    public class ColliderMaterial : MaterialChanger
    {
        [Header("Settings")] 
        [SerializeField]
        private Material _material;

        [Header("Settings")] 
        [ReadOnly]
        [SerializeField]
        private bool _isEnabled;

        private Interaction _interaction;
        private Collider _interactionCollider;

        private new void OnEnable()
        {
            base.OnEnable(); 
            _interaction = GetComponent<Interaction>();
            _interactionCollider = _interaction.GetComponent<Collider>();
            SetMaterial(_material);
        }

        [Button]
        public void Change()
        {
            if (!Application.isPlaying) return;
            _isEnabled = !_isEnabled;
            EnableView(_isEnabled);
        }

        public void EnableView(bool enable)
        {
            Hide(!enable);
            _interaction.enabled = enable;
            _interactionCollider.enabled = enable;
        }
    }
}



