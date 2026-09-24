using OC.Communication;
using UnityEngine;

namespace OC.UI.Interactions
{
    [DefaultExecutionOrder(1000)]
    public class TooltipDevice : Tooltip
    {
        private void Start()
        {
            if (_interaction.Target.TryGetComponent(out IDevice device))
            {
                _name = device.Component.name;
                _description = device.Link.ClientPath;
            }
            else
            {
                Logging.Logger.LogWarning("IDevice can't be found in target GameObject!", this);
            }
        }
    }
}
