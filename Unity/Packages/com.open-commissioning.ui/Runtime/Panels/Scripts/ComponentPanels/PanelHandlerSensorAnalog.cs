using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerSensorAnalog : PanelHandler
    {
        public override Type ReferenceType => typeof(SensorAnalog);
        public override IPanel Create() => new PanelSensorAnalog();
    }

    public class PanelSensorAnalog : Panel<SensorAnalog>
    {
        private PanelFloatField _value;
        private PanelToggleSlide _override;
        private PanelFloatField _state;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_value = new PanelFloatField("Value"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            groupControl.Add(_state = new PanelFloatField("Target"));

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(SensorAnalog target)
        {
            _value.Bind(_target.Value);
            _override.Bind(_target.Override);
            _state.Bind(_target.Value);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _value.Unbind();
            _override.Unbind();
            _state.Unbind();
            _link.Unbind();
        }

        private void OnOverrideChanged(bool value)
        {
            _state.SetEnabled(value);
        }
    }
}