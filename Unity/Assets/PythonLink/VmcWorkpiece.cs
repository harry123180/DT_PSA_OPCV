using OC.Components;
using OC.Interactions;
using UnityEngine;

namespace PythonLink
{
    /// <summary>
    /// 加工中的工件：高度圖材料移除（平底刀，刀尖往下掃過的圓盤以下全部切掉）。
    ///
    /// 物件放在工件底面中心、世界方向不轉（Y 朝上），掛在工作台底下跟著 X／Y 走。
    /// 每幀把刀尖從上一幀的位置掃到這一幀（每半格取一點），格子比刀尖高就切到刀尖高度。
    /// 主軸沒轉就碰到工件、切深超過刃長、刀尖低於工件底面（撞治具）、主軸沒刀卻碰到工件 → VmcToolChanger 的警報。
    /// 切掉的體積（cm³）寫進 B_CutVolume；Q_ResetStock 上升緣換一塊新料。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VmcWorkpiece : MonoBehaviour
    {
        public VmcToolChanger changer;
        public Vector3 size = new(0.16f, 0.04f, 0.12f);   // m：X、高度、Z
        public float cell = 0.0015f;
        public Lamp resetStock;
        public SensorAnalog volumeSensor;

        public float RemovedVolume { get; private set; }  // mm³

        private int _nx, _nz;
        private float[] _h;
        private Mesh _mesh;
        private Vector3[] _verts;
        private bool _dirty, _reset, _hasLast;
        private Vector3 _lastTip;
        private const float NoseRadius = 0.032f;

        private void Start()
        {
            _nx = Mathf.RoundToInt(size.x / cell) + 1;
            _nz = Mathf.RoundToInt(size.z / cell) + 1;
            _h = new float[_nx * _nz];
            BuildMesh();
            ResetStock();
        }

        private float X(int i) => -size.x / 2 + i * size.x / (_nx - 1);
        private float Z(int j) => -size.z / 2 + j * size.z / (_nz - 1);

        public void ResetStock()
        {
            for (var k = 0; k < _h.Length; k++) _h[k] = size.y;
            RemovedVolume = 0;
            _hasLast = false;
            _dirty = true;
            if (volumeSensor) volumeSensor.SetValue(0);
        }

        // 頂面格點 + 四面側牆（上緣跟著邊上的高度）+ 底面
        private void BuildMesh()
        {
            var top = _nx * _nz;
            var ring = 2 * (_nx + _nz) - 4;
            _verts = new Vector3[top + 2 * ring + 4];
            var tris = new System.Collections.Generic.List<int>();
            for (var j = 0; j < _nz - 1; j++)
            for (var i = 0; i < _nx - 1; i++)
            {
                int a = j * _nx + i, b = a + 1, c = a + _nx, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            // 邊界一圈（逆時針，從上往下看）
            var border = new System.Collections.Generic.List<int>();
            for (var i = 0; i < _nx; i++) border.Add(i);
            for (var j = 1; j < _nz; j++) border.Add(j * _nx + _nx - 1);
            for (var i = _nx - 2; i >= 0; i--) border.Add((_nz - 1) * _nx + i);
            for (var j = _nz - 2; j >= 1; j--) border.Add(j * _nx);
            _border = border.ToArray();
            for (var k = 0; k < ring; k++)
            {
                int t0 = top + k, b0 = top + ring + k, t1 = top + (k + 1) % ring, b1 = top + ring + (k + 1) % ring;
                tris.AddRange(new[] { t0, t1, b0, t1, b1, b0 });
            }
            var bot = top + 2 * ring;
            tris.AddRange(new[] { bot, bot + 1, bot + 2, bot, bot + 2, bot + 3 });
            _mesh = new Mesh { name = "Workpiece" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            UpdateVertices();
            _mesh.SetVertices(_verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private int[] _border;

        private void UpdateVertices()
        {
            var top = _nx * _nz;
            for (var j = 0; j < _nz; j++)
            for (var i = 0; i < _nx; i++)
                _verts[j * _nx + i] = new Vector3(X(i), _h[j * _nx + i], Z(j));
            var ring = _border.Length;
            for (var k = 0; k < ring; k++)
            {
                var v = _verts[_border[k]];
                _verts[top + k] = v;
                _verts[top + ring + k] = new Vector3(v.x, 0, v.z);
            }
            var bot = top + 2 * ring;
            float hx = size.x / 2, hz = size.z / 2;
            _verts[bot] = new Vector3(-hx, 0, -hz);
            _verts[bot + 1] = new Vector3(hx, 0, -hz);
            _verts[bot + 2] = new Vector3(hx, 0, hz);
            _verts[bot + 3] = new Vector3(-hx, 0, hz);
        }

        private void LateUpdate()
        {
            var reset = resetStock != null && resetStock.Value.Value;
            if (reset && !_reset) ResetStock();
            _reset = reset;

            if (changer != null && changer.spindleGauge != null) Sweep();

            if (_dirty)
            {
                UpdateVertices();
                _mesh.SetVertices(_verts);
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
                _dirty = false;
            }
        }

        private void Sweep()
        {
            var tool = changer.CurrentTool;
            var length = tool != null ? tool.length * 0.001f : 0f;
            var radius = tool != null ? tool.diameter * 0.0005f : NoseRadius;
            var flute = tool != null ? tool.flute * 0.001f : 0f;
            var tip = transform.InverseTransformPoint(changer.spindleGauge.position + Vector3.down * length);
            if (!_hasLast) { _lastTip = tip; _hasLast = true; }
            var from = _lastTip;
            _lastTip = tip;
            // 快速略過：整段都在工件上方或水平範圍外
            if (Mathf.Min(from.y, tip.y) >= size.y) return;
            var reach = radius + cell;
            if (Mathf.Max(from.x, tip.x) < -size.x / 2 - reach || Mathf.Min(from.x, tip.x) > size.x / 2 + reach) return;
            if (Mathf.Max(from.z, tip.z) < -size.z / 2 - reach || Mathf.Min(from.z, tip.z) > size.z / 2 + reach) return;

            var steps = Mathf.Clamp(Mathf.CeilToInt((tip - from).magnitude / (cell * 0.5f)), 1, 400);
            var removed = 0f;
            var cellArea = (size.x / (_nx - 1)) * (size.z / (_nz - 1));
            for (var s = 1; s <= steps; s++)
            {
                var p = Vector3.Lerp(from, tip, (float)s / steps);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((p.x - radius + size.x / 2) / size.x * (_nx - 1)));
                int i1 = Mathf.Min(_nx - 1, Mathf.CeilToInt((p.x + radius + size.x / 2) / size.x * (_nx - 1)));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((p.z - radius + size.z / 2) / size.z * (_nz - 1)));
                int j1 = Mathf.Min(_nz - 1, Mathf.CeilToInt((p.z + radius + size.z / 2) / size.z * (_nz - 1)));
                for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    float dx = X(i) - p.x, dz = Z(j) - p.z;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    var k = j * _nx + i;
                    if (p.y >= _h[k]) continue;
                    // 撞機：發警報，料上留下撞痕（高度照樣降下去、不算切削體積），清除警報後同一點才不會一直重新觸發
                    var crash = tool == null || !changer.SpindleRunning;
                    if (tool == null) changer.Raise(VmcToolChanger.AlarmNoseHit, "主軸沒有刀，主軸鼻端撞到工件");
                    else if (!changer.SpindleRunning) changer.Raise(VmcToolChanger.AlarmNoSpinCut, $"主軸沒轉，{tool.name} 就切入工件");
                    if (crash)
                    {
                        _h[k] = Mathf.Max(p.y, 0f);
                        _dirty = true;
                        continue;
                    }
                    if (_h[k] - p.y > flute + 0.0005f)
                        changer.Raise(VmcToolChanger.AlarmTooDeep, $"{tool.name} 切深 {(_h[k] - p.y) * 1000:0.0} mm 超過刃長 {tool.flute} mm（刀把撞到工件）");
                    var bottom = Mathf.Max(p.y, 0f);
                    if (p.y < -0.0005f) changer.Raise(VmcToolChanger.AlarmHitFixture, $"{tool.name} 刀尖低於工件底面，撞到治具");
                    removed += (_h[k] - bottom) * cellArea;
                    _h[k] = bottom;
                    _dirty = true;
                }
            }
            if (removed > 0)
            {
                RemovedVolume += removed * 1e9f;
                if (volumeSensor) volumeSensor.SetValue(Mathf.Round(RemovedVolume / 10f) / 100f);   // cm³，小數兩位
            }
        }
    }
}
