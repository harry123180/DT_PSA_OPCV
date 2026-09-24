using System;
using OC.Interactions;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public interface IPanel
    {
        public Interaction Interaction { get; }
        public VisualElement Root { get; }
        public Type ReferenceType { get; }
        public bool Enable { get; set; }
        public string Title { get; set; }
        public bool Pinned { get; set; }

        public void Bind(Interaction target);
        public void Unbind();
        
        public event Action<IPanel> OnFocusClicked;
        public event Action<IPanel> OnPinClicked;
        public event Action<IPanel> OnCloseClicked;
    }
}