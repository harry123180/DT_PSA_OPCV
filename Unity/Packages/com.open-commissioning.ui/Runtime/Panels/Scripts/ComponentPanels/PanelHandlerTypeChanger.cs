using System;
using OC.MaterialFlow;

namespace OC.UI.Panel
{
    public class PanelHandlerTypeChanger : PanelHandler
    {
        public override Type ReferenceType => typeof(TypeChanger);
        public override IPanel Create() => new PanelTypeChanger();
    }

    public class PanelTypeChanger : Panel<TypeChanger>
    {
        private PanelBinaryStatusField _collision;
        private PanelIntegerField _typeId;
        private PanelButton _replace;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_collision = new PanelBinaryStatusField("Collision"));

            var groupControl = new PanelGroupContainer("Control");
            groupControl.Add(_typeId = new PanelIntegerField("Type ID"));
            groupControl.Add(_replace = new PanelButton("Replace"));

            Add(groupStatus);
            Add(groupControl);
        }
        
        protected override void InternalBind(TypeChanger target)
        {
            _collision.Bind(_target.Collision);
            _typeId.Bind(_target.TargetTypeID);
            _replace.Bind(_target.Replace);
        }

        protected override void InternalUnbind()
        {
            _collision.Unbind();
            _typeId.Unbind();
            _replace.Unbind();
        }
    }
}