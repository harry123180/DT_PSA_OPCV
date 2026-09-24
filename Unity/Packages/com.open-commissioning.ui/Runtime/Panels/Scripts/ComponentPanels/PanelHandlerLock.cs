using System;
using OC.Interactions;

namespace OC.UI.Panel
{
    public class PanelHandlerLock : PanelHandler
    {
        public override Type ReferenceType => typeof(Lock);
        public override IPanel Create() => new PanelLock();
    }

    public class PanelLock : Panel<Lock>
    {
        private PanelBinaryStatusField _closed;
        private PanelBinaryStatusField _locked;
        private PanelToggleSlide _override;
        private PanelToggleButton _lock;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_closed = new PanelBinaryStatusField("Closed"));
            groupStatus.Add(_locked = new PanelBinaryStatusField("Locked"));
            
            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            var horizontalGroup = new PanelHorizontalGroup();
            horizontalGroup.Add(_lock = new PanelToggleButton("Lock"));
            groupControl.Add(horizontalGroup);

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(Lock target)
        {
            _closed.Bind(_target.Closed);
            _locked.Bind(_target.Locked);
            _override.Bind(_target.Override);
            _lock.Bind(_target.LockSignal);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _closed.Unbind();
            _locked.Unbind();
            _override.Unbind();
            _lock.Unbind();
            _link.Unbind();
        }

        private void OnOverrideChanged(bool value)
        {
            _lock.SetEnabled(value);
        }
    }
}