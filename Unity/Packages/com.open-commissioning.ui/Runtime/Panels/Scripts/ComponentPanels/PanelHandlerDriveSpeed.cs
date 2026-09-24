using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerDriveSpeed : PanelHandler
    {
        public override Type ReferenceType => typeof(DriveSpeed);
        public override IPanel Create() => new PanelDriveSpeed();
    }

    public class PanelDriveSpeed : Panel<DriveSpeed>
    {
        private PanelBinaryStatusField _isActive;
        private PanelFloatField _acceleration;
        
        private PanelToggleSlide _override;
        private PanelFloatField _targetValue;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_isActive = new PanelBinaryStatusField("Is Active"));
            groupStatus.Add(_acceleration = new PanelFloatField("Acceleration"));
            
            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            groupControl.Add(_targetValue = new PanelFloatField("Target"));

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(DriveSpeed target)
        {
            _isActive.Bind(_target.IsActive);
            _acceleration.Bind(_target.Acceleration);
            _override.Bind(_target.Override);
            _targetValue.Bind(_target.Target);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _isActive.Unbind();
            _acceleration.Unbind();
            _override.Unbind();
            _targetValue.Unbind();
            _link.Unbind();
        }

        private void OnOverrideChanged(bool value)
        {
            _targetValue.SetEnabled(value);
        }
    }
}