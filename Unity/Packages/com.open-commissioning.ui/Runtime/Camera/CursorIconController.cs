using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace OC.UI
{
    [RequireComponent(typeof(CameraController))]
    public class CursorIconController : MonoBehaviour
    {
        [SerializeField]
        private Texture2D _iconPan;
        [SerializeField]
        private Texture2D _iconOrbit;
        [SerializeField]
        private Texture2D _iconFree;
        [FormerlySerializedAs("_mainCameraController")]
        [SerializeField]
        private CameraController _cameraController;

        private void OnEnable()
        {
            _cameraController.State.Subscribe(OnCameraStateChanged);
        }
        
        private void OnDisable()
        {
            OnCameraStateChanged(CameraController.CameraState.None);
            _cameraController.State.Unsubscribe(OnCameraStateChanged);
        }

        private void Reset()
        {
            TryGetComponent(out _cameraController);
        }

        private void OnCameraStateChanged(CameraController.CameraState state)
        {
            switch (state)
            {
                case CameraController.CameraState.None:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                case CameraController.CameraState.Fly:
                    Cursor.SetCursor(_iconFree, Vector2.zero, CursorMode.Auto);
                    break;
                case CameraController.CameraState.Pan:
                    Cursor.SetCursor(_iconPan, Vector2.zero, CursorMode.Auto);
                    break;
                case CameraController.CameraState.Orbit:
                    Cursor.SetCursor(_iconOrbit, Vector2.zero, CursorMode.Auto);
                    break;
                case CameraController.CameraState.Zoom:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}