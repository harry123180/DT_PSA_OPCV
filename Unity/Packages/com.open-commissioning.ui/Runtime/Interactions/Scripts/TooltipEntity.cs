using OC.MaterialFlow;
using UnityEngine;

namespace OC.UI.Interactions
{
    [DefaultExecutionOrder(1000)]
    public class TooltipEntity : Tooltip
    {
        private void Start()
        {
            if (_interaction.Target.TryGetComponent(out Payload entity))
            {
                _name = entity.name;
                _description = string.Empty;
            }
            else
            {
                Logging.Logger.LogWarning("IDevice can't be found in target GameObject!", this);
            }
        }
    }
}
