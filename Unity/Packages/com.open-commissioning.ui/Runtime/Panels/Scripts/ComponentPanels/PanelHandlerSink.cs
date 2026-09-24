using System;
using OC.MaterialFlow;

namespace OC.UI.Panel
{
    public class PanelHandlerSink : PanelHandler
    {
        public override Type ReferenceType => typeof(Sink);
        public override IPanel Create() => new PanelSink();
    }

    public class PanelSink : Panel<Sink>
    {
        private PanelBinaryStatusField _collision;
        private PanelToggleSlide _auto;
        private PanelButton _delete;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_collision = new PanelBinaryStatusField("Collision"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_auto = new PanelToggleSlide("Auto"));
            groupControl.Add(_delete = new PanelButton("Delete"));

            Add(groupStatus);
            Add(groupControl);
        }

        protected override void InternalBind(Sink target)
        {
            _collision.Bind(_target.Collision);
            _auto.Bind(_target.Auto);
            _delete.Bind(_target.Delete);
        }

        protected override void InternalUnbind()
        {
            _collision.Unbind();
            _auto.Unbind();
            _delete.Unbind();
        }
    }
}