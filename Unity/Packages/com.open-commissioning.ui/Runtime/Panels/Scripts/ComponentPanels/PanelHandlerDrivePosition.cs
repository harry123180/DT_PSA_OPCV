using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerDrivePosition : PanelHandler
    {
        public override Type ReferenceType => typeof(DrivePosition);
        public override IPanel Create() => new PanelDrivePosition();
    }

    public class PanelDrivePosition : Panel<DrivePosition>
    {
        private PanelBinaryStatusField _isActive;
        private PanelFloatField _position;
        
        private PanelToggleSlide _override;
        private PanelFloatField _targetValue;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_isActive = new PanelBinaryStatusField("Is Active"));
            groupStatus.Add(_position = new PanelFloatField("Position"));
            
            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            groupControl.Add(_targetValue = new PanelFloatField("Target"));

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(DrivePosition target)
        {
            _isActive.Bind(_target.IsActive);
            _position.Bind(_target.Value);
            _override.Bind(_target.Override);
            _targetValue.Bind(_target.Target);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _isActive.Unbind();
            _position.Unbind();
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