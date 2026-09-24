using TMPro;
using UnityEngine;

namespace OC.UI.Interactions
{
    public class Label : MonoBehaviour
    {
        [SerializeField]
        private string _label;

        private void Start()
        {
            SetLabel();
        }

        private void OnValidate()
        {
            SetLabel();
        }

        private void SetLabel()
        {
            var textMeshPro = GetComponentInChildren<TextMeshPro>();
            if (textMeshPro == null)
            {
                Logging.Logger.LogWarning("Renderer reference is null!", this);
                return;
            }
            textMeshPro.text = string.IsNullOrWhiteSpace(_label) ? gameObject.name : _label;
        }
    }
}


