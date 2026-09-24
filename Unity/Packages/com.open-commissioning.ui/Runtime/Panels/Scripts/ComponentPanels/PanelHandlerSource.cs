using System;
using OC.MaterialFlow;

namespace OC.UI.Panel
{
    public class PanelHandlerSource : PanelHandler
    {
        public override Type ReferenceType => typeof(Source);
        public override IPanel Create() => new PanelSource();
    }

    public class PanelSource : Panel<Source>
    {
        private PanelBinaryStatusField _collision;
        private PanelToggleSlide _auto;
        private PanelIntegerField _typeId;
        private PanelULongField _uniqueId;
        private PanelButton _create;
        private PanelButton _delete;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_collision = new PanelBinaryStatusField("Collision"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_auto = new PanelToggleSlide("Auto"));
            groupControl.Add(_typeId = new PanelIntegerField("TypeId"));
            groupControl.Add(_uniqueId = new PanelULongField("UniqueId"));
            groupControl.Add(_create = new PanelButton("Create"));
            groupControl.Add(_delete = new PanelButton("Delete"));

            Add(groupStatus);
            Add(groupControl);
        }

        protected override void InternalBind(Source target)
        {
            _collision.Bind(_target.Collision);
            _auto.Bind(_target.Auto);
            _typeId.Bind(_target.TypeId);
            _uniqueId.Bind(_target.UniqueId);
            _create.Bind(_target.Create);
            _delete.Bind(_target.Delete);
        }

        protected override void InternalUnbind()
        {
            _collision.Unbind();
            _auto.Unbind();
            _typeId.Unbind();
            _uniqueId.Unbind();
            _create.Unbind();
            _delete.Unbind();
        }
    }
}