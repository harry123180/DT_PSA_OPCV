using UnityEngine;
using UnityEngine.UI;

namespace OC.UI.Interactions
{
    public class BoxDrawer : MonoBehaviour
    {
        private Sprite _graphics;
        private Image _image;
        private Canvas _canvas;
        private Vector3 _startMousePosition;
        private Vector3 _endMousePosition;
        private Vector2 _size;
        private RectTransform _rectTransform;
        private RectTransform _referenceRectTransform;

        private void Start()
        {
            var go = new GameObject("BoxDrawer")
            {
                layer = gameObject.layer
            };
            go.transform.SetParent(gameObject.transform.parent, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.transform.SetParent(go.transform, false);

            GameObject referenceRect = new("ReferenceRect");
            referenceRect.AddComponent<RectTransform>();

            _referenceRectTransform = referenceRect.GetComponent<RectTransform>();
            referenceRect.transform.SetParent(transform.parent, false);

            transform.SetParent(_canvas.gameObject.transform, false);
            transform.rotation = Quaternion.identity;
            transform.position = Vector3.zero;

            _rectTransform = gameObject.AddComponent<RectTransform>();
            _rectTransform.sizeDelta = new Vector2(0, 0);
            _rectTransform.pivot = new Vector2(0, 0);
            _rectTransform.anchoredPosition = new Vector3(0, 0);

            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.referencePixelsPerUnit = 1;

            _image = gameObject.AddComponent<Image>();
            _image.type = Image.Type.Sliced;
            _graphics = Resources.Load<Sprite>("Shapes/BoxSelectionTexture");
            _image.sprite = _graphics;
        }

        public void StartDrawing()
        {
            _startMousePosition = Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, _startMousePosition, null, out Vector2 anchor);
            _rectTransform.anchoredPosition = anchor;
        }

        public void DrawBox()
        {
            _endMousePosition = Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_referenceRectTransform, _startMousePosition, null, out Vector2 start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_referenceRectTransform, _endMousePosition, null, out Vector2 end);

            _size = end - start;
            _rectTransform.sizeDelta = new Vector2(Mathf.Abs(_size.x), Mathf.Abs(_size.y));
            _rectTransform.localScale = new Vector3(Mathf.Sign(_size.x), Mathf.Sign(_size.y), 1);
        }

        public void StopDrawing()
        {
            _startMousePosition = Vector2.zero;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.sizeDelta = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
        }
    }
}
