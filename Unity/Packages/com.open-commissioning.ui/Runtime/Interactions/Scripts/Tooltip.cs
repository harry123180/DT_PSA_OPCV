using OC.Interactions;
using UnityEngine;

namespace OC.UI.Interactions
{
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Interaction))]
    public class Tooltip : MonoBehaviour, ITooltip
    {
        public string Name => _name;
        public string Description => _description;

        [SerializeField]
        protected string _name;
        [SerializeField]
        protected string _description;

        protected Interaction _interaction;
        
        private void Awake()
        {
            _interaction = GetComponent<Interaction>();
            if (string.IsNullOrEmpty(_name)) _name = _interaction.Target.name;
        }

        private void OnEnable()
        {
            _interaction.State.OnValueChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _interaction.State.OnValueChanged -= OnStateChanged;
        }
        
        private void OnStateChanged(InteractionState interactionState)
        {
            if (interactionState.HasFlag(InteractionState.Hovered))
            {
                TooltipManager.Instance.Show(this);
            }
            else
            {
                TooltipManager.Instance.Hide();
            }
        }
    }
}
