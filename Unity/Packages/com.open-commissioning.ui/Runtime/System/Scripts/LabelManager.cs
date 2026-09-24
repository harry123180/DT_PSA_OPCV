using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OC.UI.Interactions
{
    public class LabelManager : MonoBehaviour
    {
        public static LabelManager Singleton;

        private List<Label> _labels;

        private void Awake()
        {
            if (Singleton == null) Singleton = this;
            else if (Singleton != this) Destroy(gameObject);
        }

        private void Start()
        {
            _labels = Utils.FindObjectsByType<Label>().ToList();
            Show(false);
        }

        public void Show(bool value)
        {
            foreach (var label in _labels)
            {
                label.gameObject.SetActive(value);
            }
        }
    }
}
