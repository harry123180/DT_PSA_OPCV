using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerCylinder : PanelHandler
    {
        public override Type ReferenceType => typeof(Cylinder);
        public override IPanel Create() => new PanelCylinder();
    }

    public class PanelCylinder : Panel<Cylinder>
    {
        private PanelBinaryStatusField _isActive;
        private PanelProgressBarWithLimits _progress;
        private PanelToggleSlide _override;
        private PanelPushButton _minus;
        private PanelPushButton _plus;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_isActive = new PanelBinaryStatusField("Is Active"));
            groupStatus.Add(_progress= new PanelProgressBarWithLimits("Progress"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override= new PanelToggleSlide("Override"));
            var horizontalGroup = new PanelHorizontalGroup();
            horizontalGroup.Add(_minus = new PanelPushButton("Minus"));
            horizontalGroup.Add(_plus = new PanelPushButton("Plus"));
            groupControl.Add(horizontalGroup);

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(Cylinder target)
        {
            _isActive.Bind(_target.IsActive);
            _progress.Bind(_target.Progress);
            _override.Bind(_target.Override);
            _minus.Bind(_target.Minus);
            _plus.Bind(_target.Plus);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _isActive.Unbind();
            _progress.Unbind();
            _override.Unbind();
            _minus.Unbind();
            _plus.Unbind();
            _link.Unbind();
        }

        private void OnOverrideChanged(bool value)
        {
            _minus.SetEnabled(value);
            _plus.SetEnabled(value);
        }
    }
}