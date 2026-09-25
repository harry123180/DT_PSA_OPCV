using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OC.Communication;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PythonLink
{
    /// <summary>
    /// 部件檢視器：網頁（暫存器表、裝置清單）指定一個裝置 → 3D 畫面替它加上選取外框、可選擇讓相機飛過去；
    /// 反過來，在 3D 畫面左鍵點一個部件 → 回報它是哪個裝置（或屬於哪個模組）給網頁。
    ///
    /// 網頁呼叫：unityInstance.SendMessage("PythonLinkHud", "Highlight", "路徑;路徑")、"Focus"、"ClearHighlight"。
    /// 路徑可以是 PLC 路徑（MAIN.FG_Transport.Index03_Y_Stopper）或場景路徑（Project/FG_Transport/Index03）。
    /// 外框沿用 OC UI 的選取外框（渲染層 Outline_2），效果跟原本 UI 選取一樣。
    /// </summary>
    public class PythonLinkInspector : MonoBehaviour
    {
        [SerializeField] private string _outlineLayer = "Outline_2";
        [SerializeField] private Color _tint = new(1f, 0.55f, 0.05f, 1f);
        [SerializeField, Range(0f, 1f)] private float _tintAmount = 0.75f;
        [SerializeField] private bool _log = true;
        [SerializeField] private float _devicePreference = 0.25f;   // 公尺：裝置零件在結構件後方這個距離內，點選優先選裝置
        [SerializeField] private float _focusMinDistance = 1.2f;   // 公尺；小零件對焦時不要把相機塞進機構裡

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color_ = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _block;

        private readonly Dictionary<string, GameObject> _byPath = new();
        private readonly Dictionary<GameObject, string> _pathOf = new();
        // 裝置 → 真正的模型：OC 的 Cylinder／Drive 多半只是邏輯元件，模型掛在它用 UnityEvent 推動的 Axis 物件上
        private readonly Dictionary<string, List<GameObject>> _visualsOf = new();
        private readonly List<(Renderer renderer, uint mask)> _highlighted = new();
        private readonly List<GameObject> _highlightedObjects = new();
        private readonly List<GameObject> _overlays = new();
        private Material _overlayMaterial;
        private const string OverlayName = "PythonLinkHighlight";
        private uint _outlineMask;
        private bool _mapped;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PLWS_Selected(string json);
#else
        private static void PLWS_Selected(string json) => Debug.Log($"[PythonLink] selected {json}");
#endif

        private void Awake()
        {
            _outlineMask = RenderingLayerMask.GetMask(_outlineLayer);
        }

        private void EnsureMap()
        {
            if (_mapped) return;
            foreach (var link in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (link is not ILink l || l.Link == null || string.IsNullOrEmpty(l.Link.ClientPath)) continue;
                _byPath[l.Link.ClientPath] = link.gameObject;
                _pathOf[link.gameObject] = l.Link.ClientPath;
            }
            foreach (var (path, go) in _byPath)
            {
                var visuals = new List<GameObject>();
                if (go.GetComponentInChildren<Renderer>(true) != null) visuals.Add(go);
                foreach (var comp in go.GetComponents<MonoBehaviour>()) CollectEventTargets(comp, go, visuals);
                _visualsOf[path] = visuals;
                foreach (var v in visuals)
                {
                    if (v != go && !_pathOf.ContainsKey(v)) _pathOf[v] = path;   // 點到模型也能反查回裝置
                }
                if (_log) Debug.Log($"[PythonLink] map {path}: {string.Join(", ", visuals.ConvertAll(v => v.name))}");
            }
            _mapped = _byPath.Count > 0;
        }

        private static void CollectEventTargets(MonoBehaviour comp, GameObject device, List<GameObject> visuals)
        {
            if (comp == null) return;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            for (var type = comp.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (!typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(field.FieldType)) continue;
                    if (field.GetValue(comp) is not UnityEngine.Events.UnityEventBase ev) continue;
                    for (var i = 0; i < ev.GetPersistentEventCount(); i++)
                    {
                        var target = ev.GetPersistentTarget(i) switch
                        {
                            Component c => c.gameObject,
                            GameObject g => g,
                            _ => null,
                        };
                        if (target == null || target == device || visuals.Contains(target)) continue;
                        if (device.transform.IsChildOf(target.transform)) continue;   // 目標是裝置的上層（整站），太大
                        if (target.GetComponentInChildren<Renderer>(true) == null) continue;
                        visuals.Add(target);
                    }
                }
            }
        }

        private IEnumerable<GameObject> VisualsFor(string path, GameObject go)
        {
            if (_visualsOf.TryGetValue(path.Trim(), out var list) && list.Count > 0) return list;
            return new[] { go };
        }

        private GameObject Resolve(string path)
        {
            path = path.Trim();
            if (path.Length == 0) return null;
            EnsureMap();
            if (_byPath.TryGetValue(path, out var go)) return go;
            return GameObject.Find(path.StartsWith("/") ? path : "/" + path);  // 場景路徑
        }

        // ── 網頁呼叫 ──────────────────────────────────────────
        public void Highlight(string paths)
        {
            ClearHighlight();
            foreach (var p in paths.Split(';'))
            {
                var go = Resolve(p);
                if (go == null) continue;
                _highlightedObjects.Add(go);
                _block ??= new MaterialPropertyBlock();
                var count = 0;
                foreach (var visual in VisualsFor(p, go))
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name == OverlayName) continue;
                    count++;
                    AddOverlay(r);
                    _highlighted.Add((r, r.renderingLayerMask));
                    r.renderingLayerMask |= _outlineMask;
                    var mats = r.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        var id = m.HasProperty(BaseColor) ? BaseColor : m.HasProperty(Color_) ? Color_ : -1;
                        if (id < 0) continue;
                        var c = Color.Lerp(m.GetColor(id), _tint, _tintAmount);
                        c.a = m.GetColor(id).a;
                        _block.Clear();
                        _block.SetColor(id, c);
                        r.SetPropertyBlock(_block, i);
                    }
                }
                if (_log) Debug.Log($"[PythonLink] highlight {p.Trim()}: {count} renderers");
            }
        }

        /// <summary>在零件上疊一層不做深度測試的半透明橘色，零件被輸送帶等模型擋住也看得到。</summary>
        private void AddOverlay(Renderer r)
        {
            if (r is not MeshRenderer || !r.enabled || !r.gameObject.activeInHierarchy) return;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            if (_overlayMaterial == null)
            {
                var shader = Resources.Load<Shader>("PythonLinkHighlight");
                if (shader == null) return;
                _overlayMaterial = new Material(shader) { color = new Color(_tint.r, _tint.g, _tint.b, 0.55f) };
            }
            if (r is MeshRenderer { isPartOfStaticBatch: true }) return;   // 建置時已關掉 static batching；保險起見略過
            var mesh = mf.sharedMesh;
            var go = new GameObject(OverlayName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[mesh.subMeshCount];
            for (var i = 0; i < mats.Length; i++) mats[i] = _overlayMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _overlays.Add(go);
        }

        public void Focus(string path)
        {
            Highlight(path);
            var first = path.Split(';')[0];
            var go = Resolve(first);
            if (go == null) return;
            var cam = FindAnyObjectByType<OC.UI.CameraController>();
            if (cam == null) return;
            // 相機以目標的原點為中心、依子物件外框決定距離；邏輯元件沒有模型時改對準模型本身
            var aim = go;
            foreach (var v in VisualsFor(first, go)) { aim = v; break; }
            cam.Target.Value = aim.transform;   // 換目標會重設為「依外框大小對焦」
            cam.FocusOnTarget();
            // OC 的對焦距離＝外框半徑×倍率，小零件會貼到鏡頭上；設一個下限
            var field = typeof(OC.UI.CameraController).GetField("_distance",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.GetValue(cam) is float d && d < _focusMinDistance) field.SetValue(cam, _focusMinDistance);
        }

        public void ClearHighlight()
        {
            foreach (var (r, mask) in _highlighted)
            {
                if (r == null) continue;
                r.renderingLayerMask = mask;
                var n = r.sharedMaterials.Length;
                for (var i = 0; i < n; i++) r.SetPropertyBlock(null, i);
            }
            _highlighted.Clear();
            _highlightedObjects.Clear();
            foreach (var go in _overlays)
            {
                if (go == null) continue;
                go.transform.SetParent(null, false);   // Destroy 要到這一幀結束才生效，先拆開免得被當成零件
                Destroy(go);
            }
            _overlays.Clear();
        }

        // ── 3D 畫面點選 → 回報網頁 ────────────────────────────
        private void Update()
        {
            if (_overlayMaterial != null && _overlays.Count > 0)
            {
                var c = _overlayMaterial.color;
                c.a = 0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));   // 閃爍，一眼找到
                _overlayMaterial.color = c;
            }
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var kb = Keyboard.current;
            // Ctrl／Shift／Alt＋拖曳是相機操作，不當作點選
            if (kb != null && (kb.ctrlKey.isPressed || kb.shiftKey.isPressed || kb.altKey.isPressed)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (_log) Debug.Log("[PythonLink] click over UI, ignored");
                return;
            }
            var cam = Camera.main;
            if (cam == null) { if (_log) Debug.Log("[PythonLink] no main camera"); return; }
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            EnsureMap();
            var picked = PickTransform(ray);
            if (picked == null) { if (_log) Debug.Log("[PythonLink] click hit nothing"); return; }
            string device = "", module = "";
            for (var t = picked; t != null; t = t.parent)
            {
                if (device.Length == 0 && _pathOf.TryGetValue(t.gameObject, out var p)) device = p;
                if (module.Length == 0 && t.GetComponent<Hierarchy>() != null) module = ScenePath(t);
                if (device.Length > 0 && module.Length > 0) break;
            }
            if (device.Length == 0 && module.Length == 0) return;
            Highlight(device.Length > 0 ? device : module);
            if (_log) Debug.Log($"[PythonLink] picked {picked.name} (mesh pick) → device={device} module={module}");
            PLWS_Selected($"{{\"device\":\"{Escape(device)}\",\"scene\":\"{Escape(module)}\",\"object\":\"{Escape(picked.name)}\"}}");
        }

        /// <summary>
        /// 精確點選：對射線經過外框盒的每個模型做三角形判定，取最近的面（CAD 模型已開 Read/Write）。
        /// 被外框擋住的零件點不到，點到外框就是外框，跟眼睛看到的一致。
        /// 網格不可讀時退回物理射線。
        /// </summary>
        private Transform PickTransform(Ray ray)
        {
            _renderers ??= FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            Transform best = null, bestDevice = null;
            float bestDist = float.MaxValue, deviceDist = float.MaxValue;
            foreach (var r in _renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.gameObject.name == OverlayName) continue;
                if (!r.bounds.IntersectRay(ray, out var boxDist) || boxDist > Mathf.Min(deviceDist, bestDist + _devicePreference)) continue;
                if (IsTransparent(r)) continue;   // 工作站的玻璃罩：點穿過去
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                if (!RayMesh(ray, r, mesh, float.MaxValue, out var d)) continue;
                if (d < bestDist) { bestDist = d; best = r.transform; }
                if (d < deviceDist && BelongsToDevice(r.transform)) { deviceDist = d; bestDevice = r.transform; }
            }
            // 感測器、氣缸常貼在鋁軌或輸送帶旁邊，被結構件擋住一點點；就在後面不遠時選裝置
            if (bestDevice != null && deviceDist - bestDist <= _devicePreference) return bestDevice;
            if (best != null) return best;
            return Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore) ? hit.transform : null;
        }

        private MeshRenderer[] _renderers;
        private readonly Dictionary<Mesh, Vector3[]> _vertices = new();
        private readonly Dictionary<(Mesh, int), int[]> _triangles = new();

        private bool RayMesh(Ray ray, MeshRenderer r, Mesh mesh, float maxDist, out float dist)
        {
            dist = maxDist;
            // 靜態合併的網格已在世界座標，只看屬於這個 renderer 的子網格
            var batched = r.isPartOfStaticBatch;
            var toLocal = batched ? Matrix4x4.identity : r.transform.worldToLocalMatrix;
            var o = toLocal.MultiplyPoint3x4(ray.origin);
            var dir = toLocal.MultiplyVector(ray.direction);
            var scale = dir.magnitude;
            if (scale < 1e-9f) return false;
            dir /= scale;   // 在區域座標求距離，再換回世界距離
            if (!_vertices.TryGetValue(mesh, out var v)) _vertices[mesh] = v = mesh.vertices;
            int first = 0, count = mesh.subMeshCount;
            if (batched) { first = r.subMeshStartIndex; count = r.sharedMaterials.Length; }
            var hit = false;
            for (var sm = first; sm < first + count && sm < mesh.subMeshCount; sm++)
            {
                if (!_triangles.TryGetValue((mesh, sm), out var t)) _triangles[(mesh, sm)] = t = mesh.GetTriangles(sm);
                for (var i = 0; i + 2 < t.Length; i += 3)
                {
                    if (!RayTriangle(o, dir, v[t[i]], v[t[i + 1]], v[t[i + 2]], out var local)) continue;
                    var world = local / scale;
                    if (world < dist) { dist = world; hit = true; }
                }
            }
            return hit;
        }

        private static bool IsTransparent(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.renderQueue < 3000) return false;
            }
            return true;
        }

        /// <summary>Möller–Trumbore，雙面。</summary>
        private static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(d, e2);
            var det = Vector3.Dot(e1, p);
            if (det > -1e-9f && det < 1e-9f) return false;
            var inv = 1f / det;
            var s = o - a;
            var u = Vector3.Dot(s, p) * inv;
            if (u < 0 || u > 1) return false;
            var q = Vector3.Cross(s, e1);
            var w = Vector3.Dot(d, q) * inv;
            if (w < 0 || u + w > 1) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t > 1e-5f;
        }

        private bool BelongsToDevice(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (_pathOf.ContainsKey(t.gameObject)) return true;
            }
            return false;
        }

        private static string ScenePath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
