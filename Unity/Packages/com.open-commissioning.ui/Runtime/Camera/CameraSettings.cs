using UnityEngine;

namespace OC.UI
{
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "Open Commissioning/UI/Camera Settings", order = 1)]
    public class CameraSettings : ScriptableObject
    {
        [Header("Movement")]
        public float MoveSpeed = 10f;
        public float FastMoveMultiplier = 4f;
        
        [Header("Rotation")]
        public float LookSensitivity = 0.5f;
        public float OrbitSensitivity = 0.25f;
        
        [Header("Pan")]
        public float PanSensitivity = 1f;
        
        [Header("Zoom")]
        public float ZoomSensitivity = 1f;
        
        [Header("Distance")]
        public float MinDistance = 0.5f;
        public float MaxDistance = 200f;
        
        [Header("Pitch Limits")]
        public float MinPitch = -85f;
        public float MaxPitch = 85f;
        
        [Header("Focus")]
        public float FocusMinDistance = 1f;
        
        [Header("Smoothing (Optional)")]
        public float PositionLerpSpeed = 15f;
        public float RotationLerpSpeed = 15f;
    }
}