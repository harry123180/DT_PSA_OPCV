using UnityEngine;

namespace OC.UI
{
    [CreateAssetMenu(fileName = "VisualConfig", menuName = "OC/Visual Config")]
    public class VisualConfig : ScriptableObject
    {
        [Header("Collision Materials")] 
        public Material Preview;
        public Material Transparent;
        public Material Enable;
        public Material Warning;
        public Material Error;
    }
}
