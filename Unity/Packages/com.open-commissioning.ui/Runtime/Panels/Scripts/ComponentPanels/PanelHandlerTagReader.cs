using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerTagReader : PanelHandler
    {
        public override Type ReferenceType => typeof(TagReader);
        public override IPanel Create() => new PanelTagReader();
    }

    public class PanelTagReader : Panel<TagReader>
    {
        private PanelBinaryStatusField _collision;
        private PanelULongField _value;
        private PanelToggleSlide _override;
        private PanelULongField _targetValue;
        private PanelLinkStatus _link;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_collision = new PanelBinaryStatusField("Collision"));
            groupStatus.Add(_value = new PanelULongField("Value"));
            
            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_override = new PanelToggleSlide("Override"));
            groupControl.Add(_targetValue = new PanelULongField("Value"));

            Add(groupStatus);
            Add(groupControl);
            Add(_link = new PanelLinkStatus());
        }
        
        protected override void InternalBind(TagReader target)
        {
            _collision.Bind(_target.Collision);
            _value.Bind(_target.Value);
            _override.Bind(_target.Override);
            _targetValue.Bind(_target.Value);
            _link.Bind(_target.Link);
            
            _target.Override.Subscribe(OnOverrideChanged);
        }

        protected override void InternalUnbind()
        {
            _target.Override.Unsubscribe(OnOverrideChanged);
            
            _collision.Unbind();
            _value.Unbind();
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