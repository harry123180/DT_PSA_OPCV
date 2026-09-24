using System;
using OC.Components;

namespace OC.UI.Panel
{
    public class PanelHandlerDataReader : PanelHandler
    {
        public override Type ReferenceType => typeof(DataReader);
        public override IPanel Create() => new PanelDataReader();
    }

    public class PanelDataReader : Panel<DataReader>
    {
        private PanelBinaryStatusField _collision;
        private PanelStringField _value;
        
        protected override void Create()
        {
            var groupStatus = new PanelGroupContainer("Status");
            groupStatus.Add(_collision = new PanelBinaryStatusField("Collision"));
            groupStatus.Add(_value = new PanelStringField("Value"));

            Add(groupStatus);
        }
        
        protected override void InternalBind(DataReader target)
        {
            _collision.Bind(_target.Collision);
            _value.Bind(_target.TargetData);
        }

        protected override void InternalUnbind()
        {
            _collision.Unbind();
            _value.Unbind();
        }
    }
}