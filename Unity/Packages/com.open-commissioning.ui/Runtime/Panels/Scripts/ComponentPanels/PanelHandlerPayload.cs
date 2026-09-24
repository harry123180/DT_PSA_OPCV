using System;
using System.Collections.Generic;
using OC.MaterialFlow;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class PanelHandlerPayload : PanelHandler
    {
        public override Type ReferenceType => typeof(Payload);
        public override IPanel Create() => new PanelPayload();
    }

    public class PanelPayload : Panel<Payload>
    {
        private PanelIntegerField _typeId;
        private PanelULongField _uniqueId;
        private VisualElement _content;
        private readonly List<PanelEntityTagContainer> _entityTags = new ();
        
        protected override void Create()
        {
            var groupData = new PanelGroupContainer("Data");
            groupData.Add(_typeId = new PanelIntegerField("TypeId", true));
            groupData.Add(_uniqueId = new PanelULongField("UniqueId", true));
            Add(groupData);
            Add(_content = new VisualElement());
        }
        
        protected override void InternalBind(Payload target)
        {
            _typeId.SetValueWithoutNotify(target.TypeId);
            _uniqueId.SetValueWithoutNotify(target.UniqueId);

            if (target.TryGetComponent(out PayloadTag entityTag))
            {
                foreach (var directory in entityTag.DirectoryId)
                {
                    var entityTagContainer = new PanelEntityTagContainer();
                    _content.Add(entityTagContainer);
                    _entityTags.Add(entityTagContainer);
                    entityTagContainer.Bind(entityTag, directory);
                }
            }
        }

        protected override void InternalUnbind()
        {
            foreach (var entityTag in _entityTags)
            {
                entityTag.Unbind();
            }
            
            _entityTags.Clear();
            _content.Clear();
        }
    }
}