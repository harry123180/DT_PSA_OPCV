using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace OC.UI.Interactions
{
    [AddComponentMenu("Open Commissioning/UI/Point Event Handler")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class PointerEventHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField]
        private PointerEventData.InputButton _buttonType;

        public UnityEvent PointerClick;
        public UnityEvent PointerDown;
        public UnityEvent PointerUp;

        private void Start()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == _buttonType) PointerClick?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == _buttonType) PointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == _buttonType) PointerUp?.Invoke();
        }
    }
}
