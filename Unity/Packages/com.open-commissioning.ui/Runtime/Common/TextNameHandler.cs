using TMPro;
using UnityEngine;

namespace OC.UI
{
    [RequireComponent(typeof(TextMeshPro))]
    public class TextNameHandler : MonoBehaviour
    {
        [SerializeField]
        private GameObject _target;

        private void Start()
        {
            GetName();
        }

        private void OnValidate()
        {
            GetName();
        }

        private void GetName()
        {
            if (_target == null) return;
            GetComponent<TextMeshPro>().text = _target.name.Replace("_", " ");
        }
    }
}
