using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace OC.UI.Interactions
{
    public class Clickable : MonoBehaviour, IPointerClickHandler
    {
        public UnityEvent OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke();
        }
    }
}
