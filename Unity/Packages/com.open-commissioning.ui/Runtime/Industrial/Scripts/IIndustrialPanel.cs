using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Industrial
{
    public interface IIndustrialPanel
    {
        public Component Component { get; }
        public string Path { get; }
        public VisualElement Create();
    }
}