using System.Linq;
using OC.Interactions;
using OC.Interactions.UIElements;
using OC.UI.Panel;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Industrial
{
    [RequireComponent(typeof(PanelSampler))]
    public class PanelSamplerUI : MonoBehaviour, IIndustrialPanel
    {
        [SerializeField]
        private string _name;

        public Component Component => this;
        public string Path => _panelSampler.Link.ClientPath;

        private PanelSampler _panelSampler;

        private void Awake()
        {
            _panelSampler = GetComponent<PanelSampler>();
        }

        public VisualElement Create()
        {
            var groupName = string.IsNullOrEmpty(_name) ? gameObject.name : _name;
            var group = new ComponentsGroup(groupName);

            var scrollView = new PanelScrollView();
            group.Add(scrollView);

            foreach (var visualElement in _panelSampler.Components.Select(Factory.Create))
            {
                scrollView.Add(visualElement);
            }

            // The sidebar is anchored to the bottom of the window and grows upward, so cap the
            // scroll view height to the space between its (fixed) bottom edge and the window top.
            var lastMaxHeight = -1f;
            scrollView.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // worldBound is in panel space; yMax is the scroll view's bottom edge, which is
                // stable because it is determined by the elements stacked below it, not by our height.
                var available = Mathf.Round(scrollView.worldBound.yMax - 40f);
                if (available < 1f || float.IsNaN(available)) return;

                // Only write when the value actually changed to avoid a relayout feedback loop.
                if (Mathf.Approximately(available, lastMaxHeight)) return;
                lastMaxHeight = available;
                scrollView.style.maxHeight = available;
            });

            return group;
        }
    }
}

