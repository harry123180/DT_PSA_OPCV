using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace OC.UI.Interactions.SceneGizmo
{
    public class CameraGizmoListener : MonoBehaviour
    {
        private readonly float _cameraAdjustmentSpeed = 3f;
        private readonly float _projectionTransitionSpeed = 2f;
        private Camera _mainCamera;

        private IEnumerator _cameraRotateCoroutine;
        private IEnumerator _projectionChangeCoroutine;

        public UnityEvent OnCameraRotated;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void OnDisable()
        {
            _cameraRotateCoroutine = _projectionChangeCoroutine = null;
        }

        public void OnGizmoComponentClicked(GizmoComponent component)
        {
            if (component == GizmoComponent.Center)
                SwitchOrthographicMode();
            else if (component == GizmoComponent.XNegative)
                RotateCameraInDirection(Vector3.right);
            else if (component == GizmoComponent.XPositive)
                RotateCameraInDirection(-Vector3.right);
            else if (component == GizmoComponent.YNegative)
                RotateCameraInDirection(Vector3.up);
            else if (component == GizmoComponent.YPositive)
                RotateCameraInDirection(-Vector3.up);
            else if (component == GizmoComponent.ZNegative)
                RotateCameraInDirection(Vector3.forward);
            else
                RotateCameraInDirection(-Vector3.forward);
        }

        public void SwitchOrthographicMode()
        {
            if (_projectionChangeCoroutine != null)
                return;

            _projectionChangeCoroutine = SwitchProjection(_mainCamera);

            StartCoroutine(_projectionChangeCoroutine);
            StartCoroutine(SwitchProjection(SceneGizmoRenderer.Instance.Controller._gizmoCamera));
        }

        public void RotateCameraInDirection(Vector3 direction)
        {
            if (_cameraRotateCoroutine != null)
                return;

            _cameraRotateCoroutine = SetCameraRotation(direction);
            StartCoroutine(_cameraRotateCoroutine);
        }

        // Credit: https://forum.unity.com/threads/smooth-transition-between-perspective-and-orthographic-modes.32765/#post-212814
        private IEnumerator SwitchProjection(Camera cam)
        {
            bool isOrthographic = cam.orthographic;

            Matrix4x4 dest, src = cam.projectionMatrix;
            if (isOrthographic)
                dest = Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, cam.nearClipPlane, cam.farClipPlane);
            else
            {
                float orthographicSize = cam.orthographicSize;
                float aspect = cam.aspect;
                dest = Matrix4x4.Ortho(-orthographicSize * aspect, orthographicSize * aspect, -orthographicSize, orthographicSize, cam.nearClipPlane, cam.farClipPlane);
            }

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * _projectionTransitionSpeed)
            {
                float lerpModifier = isOrthographic ? t * t : Mathf.Pow(t, 0.2f);
                Matrix4x4 matrix = new Matrix4x4();
                for (int i = 0; i < 16; i++)
                    matrix[i] = Mathf.LerpUnclamped(src[i], dest[i], lerpModifier);

                cam.projectionMatrix = matrix;
                yield return null;
            }

            cam.orthographic = !isOrthographic;
            cam.ResetProjectionMatrix();

            _projectionChangeCoroutine = null;
        }

        private IEnumerator SetCameraRotation(Vector3 targetForward)
        {
            Quaternion initialRotation = transform.localRotation;
            Quaternion targetRotation;
            if (Mathf.Abs(targetForward.y) < 0.99f)
                targetRotation = Quaternion.LookRotation(targetForward);
            else
            {
                Vector3 cameraForward = transform.forward;
                if (cameraForward.x == 0f && cameraForward.z == 0f)
                    cameraForward.y = 1f;
                else if (Mathf.Abs(cameraForward.x) > Mathf.Abs(cameraForward.z))
                {
                    cameraForward.x = Mathf.Sign(cameraForward.x);
                    cameraForward.y = 0f;
                    cameraForward.z = 0f;
                }
                else
                {
                    cameraForward.x = 0f;
                    cameraForward.y = 0f;
                    cameraForward.z = Mathf.Sign(cameraForward.z);
                }

                if (targetForward.y > 0f)
                    cameraForward = -cameraForward;

                targetRotation = Quaternion.LookRotation(targetForward, cameraForward);
            }

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * _cameraAdjustmentSpeed)
            {
                transform.localRotation = Quaternion.LerpUnclamped(initialRotation, targetRotation, t);
                yield return null;
            }

            transform.localRotation = targetRotation;
            _cameraRotateCoroutine = null;
            OnCameraRotated?.Invoke();
        }
    }
}