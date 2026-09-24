using System.Collections;
using System.Collections.Generic;
using OC.Interactions;
using UnityEngine;

namespace OC.UI.Interactions.Scripts
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class BoxSelector : MonoBehaviour
    {
        public bool IsActive => _isActive;
        
        [SerializeField]
        private bool _isActive;
        [SerializeField]
        private Vector3 _startPosition;
        [SerializeField]
        private Vector3 _dragPosition;
        [SerializeField]
        private LayerMask _layerMask;
        
        
        private List<GameObject> _objects = new ();

        public void StartSelection(Vector3 startPosition)
        {
            _isActive = true;
            //TODO
            throw new System.NotImplementedException();
        }

        public void DragSelection(Vector3 dragPosition)
        {
            //TODO
            throw new System.NotImplementedException();
        }

        public List<GameObject> EndSelection()
        {
            _isActive = false; 
            //TODO
            throw new System.NotImplementedException();
            return new List<GameObject>();
        }
        
        private void OnTriggerEnter(Collider col)
        {
            if (!col.gameObject.GetComponent<Interaction>()) return;
            if (((1 << col.gameObject.layer) & _layerMask.value) > 0)
            {
                _objects.Add(col.gameObject);
            }
        }
        
        /*private void BoxSelection()
        {
            //TODO Need this?
            if (_mouseClickStartedInEmptySpace)
            {
                if (_isDrawing) _boxDrawer.DrawBox();

                _endMousePos = _inputActionPointer.ReadValue<Vector2>();
                if (Vector3.Magnitude(_startMousePos - _endMousePos) > DRAG_THRESHOLD)
                {
                    _mouseDragging = true;
                    if (!_isDrawing)
                    {
                        _boxDrawer.StartDrawing();
                        _isDrawing = true;
                    }
                }
            }
            if (!_inputActionClick.IsPressed() && _mouseDragging && _mouseClickStartedInEmptySpace)
            {
                DetectObjectsInBox();
                _mouseDragging = false;
                _boxDrawer.StopDrawing();
                _isDrawing = false;
                _mouseClickStartedInEmptySpace = false;
            }
        }*/
        
        /*private void DetectObjectsInBox()
        {
            _selectionCollider.enabled = true;
            var vertices = new Vector3[8];
            var corners = CreateCorners(_startMousePos, _endMousePos);
            var index = 0;

            if (Camera.main == null)
            {
                Logging.Logger.Log(LogType.Error, "Camera Main isn't defined");
                return;
            }

            for (var i = 0; i < corners.Length; ++i)
            {
                _boxSelectionRay = Camera.main.ScreenPointToRay(corners[i]);

                var endPoint = _boxSelectionRay.GetPoint(MAX_DISTANCE);
                vertices[index] = endPoint;
                vertices[index + 4] = Camera.main.ScreenToWorldPoint(corners[i]) - endPoint;
                if (_debug) Debug.DrawLine(Camera.main.ScreenToWorldPoint(corners[i]), endPoint, Color.yellow, 10f);

                index++;
            }

            _currentSelectionMesh = CreateSelectionMesh(vertices);
            _selectionCollider.sharedMesh = _currentSelectionMesh;

            StartCoroutine(ProcessTriggerHits());
            _startMousePos = Vector3.zero;


        }*/
        
        private Mesh CreateSelectionMesh(Vector3[] verts)
        {
            var meshVerts = new Vector3[8];

            for (var i = 0; i < 4; ++i)
            {
                meshVerts[i] = verts[i];
                meshVerts[i + 4] = verts[i] + verts[i + 4];
            }

            var mesh = new Mesh
            {
                name = "SelectionMesh",
                vertices = meshVerts,
                triangles = CubeTriangles
            };

            return mesh;
        }
        private Vector2[] CreateCorners(Vector2 p1, Vector2 p2)
        {
            var bottomLeft = Vector3.Min(p1, p2);
            var topRight = Vector3.Max(p1, p2);

            var corners = new[]
            {
                new Vector2(bottomLeft.x, topRight.y),
                new Vector2(topRight.x, topRight.y),
                new Vector2(bottomLeft.x, bottomLeft.y),
                new Vector2(topRight.x, bottomLeft.y)
            };

            var width = (corners[0] - corners[1]).magnitude;
            var height = (corners[0] - corners[2]).magnitude;

            if (width < 2)
            {
                var diff = 2 - width;
                corners[0].x -= diff * 0.5f;
                corners[1].x += diff * 0.5f;
                corners[2].x -= diff * 0.5f;
                corners[3].x += diff * 0.5f;
            }

            if (height < 2)
            {
                var diff = 2 - height;
                corners[0].y += diff * 0.5f;
                corners[1].y += diff * 0.5f;
                corners[2].y -= diff * 0.5f;
                corners[3].y -= diff * 0.5f;
            }
            return corners;
        }

        private IEnumerator ProcessTriggerHits()
        {
            yield return new WaitForFixedUpdate();
            //_selectionCollider.enabled = false;
        }
        
        private static readonly int[] CubeTriangles = 
        {
            0, 1, 2,
            2, 1, 3,
            4, 6, 0,
            0, 6, 2,
            6, 7, 2,
            2, 7, 3,
            7, 5, 3,
            3, 5, 1,
            5, 0, 1,
            1, 4, 0,
            4, 5, 6,
            6, 5, 7
        };
        
        
        
        
        
        

    }
}