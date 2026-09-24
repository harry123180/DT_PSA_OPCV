using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerSensorBinary : PanelHandler
    {
        public override Type ReferenceType => typeof(SensorBinary);
        public override IPanel Create() => new PanelSensorBinary();
    }

    public class PanelSensorBinary : Panel<SensorBinary>
    {
        private PanelBinaryStatusField _value;
        private PanelToggleSlide _override;
        private PanelToggleSlide _state;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_value = new PanelBinaryStatusField("Value"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            groupControl.Add(_state = new PanelToggleSlide("State"));

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(SensorBinary target)
        {
            _value.Bind(_target.Value);
            _override.Bind(_target.Override);
            _state.Bind(_target.State);
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