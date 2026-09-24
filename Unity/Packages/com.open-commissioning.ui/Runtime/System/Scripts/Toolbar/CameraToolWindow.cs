using System.Collections.Generic;
using OC.UI.Panel;

namespace OC.UI.Toolbar
{
    public class CameraToolWindow : ToolbarWindow
    {
        private List<CameraController> _cameras;
        private readonly List<PanelToggleSlide> _toggles = new ();

        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            _cameras = CamerasManager.Instance.Cameras;

            for (var i = 0; i < _cameras.Count; i++)
            {
                var toggle = new PanelToggleSlide(_cameras[i].name);
                var index = i;
                toggle.OnValueChanged += isOn =>
                {
                    SetCameraState(index, isOn);
                };
                _toggles.Add(toggle);
                subsystemPanel.Add(toggle);
            }

            if (_cameras.Count > 0)
            {
                SetCameraState(0, true);
            }
        }

        private void SetCameraState(int index, bool enable)
        {
            if (!enable)
            {
                if (index == CamerasManager.Instance.ActiveCameraIndex)
                {
                    _toggles[index].SetValueWithoutNotify(true);
                }
                
                return;
            }
            
            for (var i = 0; i < _toggles.Count; i++)
            {
                _toggles[i].SetValueWithoutNotify(i == index);
            }

            CamerasManager.Instance.SetCameraActive(index);
        }
    }
} 

