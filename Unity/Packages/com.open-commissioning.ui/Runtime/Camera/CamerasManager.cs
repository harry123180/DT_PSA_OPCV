using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OC.UI
{
    public class CamerasManager : MonoBehaviourSingleton<CamerasManager>
    {
        public CameraController ActiveCamera => _cameras[_activeCameraIndex];
        public int ActiveCameraIndex => _activeCameraIndex;
        public List<CameraController> Cameras => _cameras;
        
        [Header("State")]
        [SerializeField]
        private int _activeCameraIndex;
        
        [Header("References")]
        [SerializeField]
        private List<CameraController> _cameras;

        private void OnEnable()
        {
            _cameras = Utils.FindObjectsByType<CameraController>().OrderBy(c => GetHierarchyPath(c.transform)).ToList();
            DisableAllCameras();
            
            if (_cameras.Count > 0) SetCameraActive(_cameras[0]);
        }

        public void SetCameraActive(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= _cameras.Count) return;

            for (var i = 0; i < _cameras.Count; i++)
            {
                if (i == cameraIndex)
                {
                    _activeCameraIndex = i; 
                    _cameras[i].gameObject.SetActive(true);
                }
                else
                {
                    _cameras[i].gameObject.SetActive(false);
                }
            }
        }

        public void SetCameraActive(CameraController cameraController)
        {
            if (cameraController == null) return;
            if (!_cameras.Contains(cameraController)) return;
            
            var index = _cameras.IndexOf(cameraController);
            SetCameraActive(index);
        }

        public void DisableAllCameras()
        {
            foreach (var item in _cameras)
            {
                item.gameObject.SetActive(false);
            }
        }
        
        private string GetHierarchyPath(Transform t)
        {
            var path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}