using OC.Interactions;
using UnityEngine;

namespace OC.UI.Interactions
{
    [AddComponentMenu("Open Commissioning/UI/Outline")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interaction))]
    public class Outline : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Interaction _interaction;
        
        [Header("Settings")]
        [SerializeField]
        private string _layerNameHover = "Outline_1";
        [SerializeField]
        private string _layerNameSelection = "Outline_2";
        
        private uint _layerMaskHover;
        private uint _layerMaskSelection;

        private void Awake()
        {
            TryGetComponent(out _interaction);
            _layerMaskHover = RenderingLayerMask.GetMask(_layerNameHover);
            _layerMaskSelection = RenderingLayerMask.GetMask(_layerNameSelection);
        }
        
        private void OnEnable()
        {
            if (_interaction != null) _interaction.State.OnValueChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_interaction != null) _interaction.State.OnValueChanged -= OnStateChanged;
        }

        private void Reset()
        {
            TryGetComponent(out _interaction);
        }

        private void OnStateChanged(InteractionState interactionState)
        {
            if (!isActiveAndEnabled) return;

            if (interactionState.HasFlag(InteractionState.Selected))
            {
                SetRenderLayerMask(_interaction, _layerMaskSelection);
                return;
            }
            
            if (interactionState.HasFlag(InteractionState.Hovered))
            {
                SetRenderLayerMask(_interaction, _layerMaskHover);
                return;
            }
            
            SetRenderLayerMask(_interaction, 1);
        }
        
        private static void SetRenderLayerMask(Interaction interaction, uint layerMask)
        {
            foreach (var item in interaction.Renderers)
            {
                item.renderingLayerMask = layerMask;
            }
        }
    }
}