using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerDriveSimple : PanelHandler
    {
        public override Type ReferenceType => typeof(DriveSimple);
        public override IPanel Create() => new PanelDriveSimple();
    }

    public class PanelDriveSimple : Panel<DriveSimple>
    {
        private PanelBinaryStatusField _isActive;
        private PanelFloatField _speed;
        
        private PanelToggleSlide _override;
        private PanelToggleButton _backward;
        private PanelToggleButton _forward;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_isActive = new PanelBinaryStatusField("Is Active"));
            groupStatus.Add(_speed = new PanelFloatField("Speed"));
            
            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            var horizontalGroup = new PanelHorizontalGroup();
            horizontalGroup.Add(_backward = new PanelToggleButton("Backward"));
            horizontalGroup.Add(_forward = new PanelToggleButton("Forward"));
            groupControl.Add(horizontalGroup);

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(DriveSimple target)
        {
            _isActive.Bind(_target.IsActive);
            _speed.Bind(_target.Speed);
            _override.Bind(_target.Override);
            _backward.Bind(_target.Backward);
            _forward.Bind(_target.Forward);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _isActive.Unbind();
            _speed.Unbind();
            _override.Unbind();
            _backward.Unbind();
            _forward.Unbind();
            _link.Unbind();
        }

        private void OnOverrideChanged(bool value)
        {
            _backward.SetEnabled(value);
            _forward.SetEnabled(value);
        }
    }
}