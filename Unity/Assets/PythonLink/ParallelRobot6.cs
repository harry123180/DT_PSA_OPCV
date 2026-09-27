using System;
using OC.Components;
using UnityEngine;

namespace PythonLink
{
    /// <summary>
    /// 6-PSS 並聯機器人（Hexaglide）的閉鏈運動學：6 支滑座（各自由 Axis 帶動）＋6 根定長連桿＋上平台。
    ///
    /// 串接機構可以用 Axis 一層掛一層，閉鏈不行：平台姿態由 6 個滑座位置共同決定。這裡每幀做
    /// 正向運動學（牛頓法，從上一幀的解出發），然後擺好平台與每根連桿，並把平台姿態寫進 6 個
    /// SensorAnalog（PLC／Python 讀得到）。
    ///
    /// 姿態座標（機器人座標系，Z 朝上，右手系）：X、Y、Z 是平台相對原始位置的位移（mm），
    /// Roll、Pitch、Yaw 是繞 X、Y、Z 的角度（度，R = Rz·Ry·Rx）。跟 Python 端的 hexapod.py 一致。
    /// </summary>
    [DefaultExecutionOrder(2000)]   // Axis 是 1000：滑座先動，這裡再解平台
    public class ParallelRobot6 : MonoBehaviour
    {
        [Serializable]
        public class Leg
        {
            public Transform joint;            // 滑座上的球關節中心（掛在滑座的 Axis 底下）
            public Transform rod;              // 連桿，原點在下端球心
            public Vector3 rodRestDir;         // 原始（CAD）姿態下，下端→上端的世界方向
            public Quaternion rodRestRot;
            public Vector3 platformJoint;      // 上端球心，平台區域座標（相對平台原點、原始旋轉）
        }

        public Leg[] legs = new Leg[6];
        public Transform platform;
        public float rodLength;                // 公尺
        public SensorAnalog[] pose = new SensorAnalog[6];
        [Tooltip("CAD→Unity 的軸向正負（FBX 匯入會翻 X）：Unity = (sx·x, y, sz·z)")]
        public Vector3 axisSign = new(-1, 1, 1);

        private Vector3 _homePos;
        private Quaternion _homeRot;
        private readonly double[] _x = new double[6];   // 平台相對原始：位移（m）＋旋轉向量（rad），Unity 世界座標
        private bool _warned;

        public bool Solved { get; private set; } = true;

        private double[][] _jointHome;

        private void Start()
        {
            _homePos = platform.position;
            _homeRot = platform.rotation;
            _jointHome = new double[6][];
            for (var i = 0; i < 6; i++)
            {
                var o = _homeRot * legs[i].platformJoint;
                _jointHome[i] = new double[] { o.x, o.y, o.z };
            }
        }

        private void LateUpdate()
        {
            Solve();
            Apply();
        }

        // ── 正向運動學 ───────────────────────────────────────
        private void Solve()
        {
            var f = new double[6];
            var j = new double[6, 6];
            var trial = new double[6];
            Residual(_x, f);
            for (var iter = 0; iter < 12 && MaxAbs(f) > 1e-14; iter++)
            {
                const double h = 1e-6;
                for (var c = 0; c < 6; c++)
                {
                    Array.Copy(_x, trial, 6);
                    trial[c] += h;
                    var fp = new double[6];
                    Residual(trial, fp);
                    trial[c] -= 2 * h;
                    var fm = new double[6];
                    Residual(trial, fm);
                    for (var r = 0; r < 6; r++) j[r, c] = (fp[r] - fm[r]) / (2 * h);
                }
                var dx = SolveLinear(j, f);
                if (dx == null) break;
                for (var k = 0; k < 6; k++) _x[k] -= dx[k];
                Residual(_x, f);
            }
            Solved = MaxAbs(f) < 1e-10;  // 長度誤差約 1e-10 / (2L) < 1 µm
            if (!Solved && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"[PythonLink] 並聯機器人：這組滑座位置平台到不了（殘差 {MaxAbs(f):E1}），保持上一個姿態");
            }
            if (Solved) _warned = false;
        }

        /// <summary>
        /// f_i = |B_i - C_i|² - L²，全程用 double：float 只有 7 位有效數字，L² 約 0.044 m²，
        /// 殘差會卡在 1e-9 附近，牛頓法看起來像沒收斂。
        /// </summary>
        private void Residual(double[] x, double[] f)
        {
            Rodrigues(x[3], x[4], x[5], out var r);
            for (var i = 0; i < 6; i++)
            {
                var o = _jointHome[i];   // 平台原點→上端球心（世界座標，原始姿態）
                var c = legs[i].joint.position;
                var bx = _homePos.x + x[0] + r[0] * o[0] + r[1] * o[1] + r[2] * o[2];
                var by = _homePos.y + x[1] + r[3] * o[0] + r[4] * o[1] + r[5] * o[2];
                var bz = _homePos.z + x[2] + r[6] * o[0] + r[7] * o[1] + r[8] * o[2];
                double dx = bx - c.x, dy = by - c.y, dz = bz - c.z;
                f[i] = dx * dx + dy * dy + dz * dz - (double)rodLength * rodLength;
            }
        }

        /// <summary>旋轉向量 → 3×3 旋轉矩陣（列優先），double。</summary>
        private static void Rodrigues(double rx, double ry, double rz, out double[] m)
        {
            var th = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            m = new double[9];
            if (th < 1e-12) { m[0] = m[4] = m[8] = 1; return; }
            double kx = rx / th, ky = ry / th, kz = rz / th, c = Math.Cos(th), s = Math.Sin(th), v = 1 - c;
            m[0] = c + kx * kx * v;      m[1] = kx * ky * v - kz * s; m[2] = kx * kz * v + ky * s;
            m[3] = ky * kx * v + kz * s; m[4] = c + ky * ky * v;      m[5] = ky * kz * v - kx * s;
            m[6] = kz * kx * v - ky * s; m[7] = kz * ky * v + kx * s; m[8] = c + kz * kz * v;
        }

        private void Pose(double[] x, out Vector3 pos, out Quaternion rot)
        {
            pos = _homePos + new Vector3((float)x[0], (float)x[1], (float)x[2]);
            var r = new Vector3((float)x[3], (float)x[4], (float)x[5]);
            var angle = r.magnitude;
            rot = angle < 1e-9f ? _homeRot : Quaternion.AngleAxis(angle * Mathf.Rad2Deg, r / angle) * _homeRot;
        }

        // ── 擺模型、回報姿態 ─────────────────────────────────
        private void Apply()
        {
            Pose(_x, out var pos, out var rot);
            platform.SetPositionAndRotation(pos, rot);
            foreach (var leg in legs)
            {
                var c = leg.joint.position;
                var b = pos + rot * leg.platformJoint;
                leg.rod.SetPositionAndRotation(c, Quaternion.FromToRotation(leg.rodRestDir, (b - c).normalized) * leg.rodRestRot);
            }
            if (pose.Length < 6 || pose[0] == null) return;

            // Unity 世界 → 機器人座標：CAD =（sx·ux, uy, sz·uz），機器人 =（CAD.x, -CAD.z, CAD.y）
            Vector3 ToRobot(Vector3 u) => new(axisSign.x * u.x, -axisSign.z * u.z, u.y);
            var p = ToRobot(pos - _homePos) * 1000f;
            var rel = rot * Quaternion.Inverse(_homeRot);
            var ex = ToRobot(rel * UnityFromRobot(new Vector3(1, 0, 0)));
            var ey = ToRobot(rel * UnityFromRobot(new Vector3(0, 1, 0)));
            var ez = ToRobot(rel * UnityFromRobot(new Vector3(0, 0, 1)));
            // 旋轉矩陣的欄＝基底向量的像；R = Rz·Ry·Rx → yaw = atan2(R10, R00)，pitch = -asin(R20)，roll = atan2(R21, R22)
            var yaw = Mathf.Atan2(ex.y, ex.x) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(ex.z, -1f, 1f)) * Mathf.Rad2Deg;
            var roll = Mathf.Atan2(ey.z, ez.z) * Mathf.Rad2Deg;
            float[] values = { p.x, p.y, p.z, roll, pitch, yaw };
            for (var i = 0; i < 6; i++) pose[i].SetValue(Mathf.Round(values[i] * 1000f) / 1000f);
        }

        /// <summary>機器人座標向量 → Unity 世界向量。機器人 (x, y, z) =（CAD.x, -CAD.z, CAD.y），Unity =（sx·CAD.x, CAD.y, sz·CAD.z）。</summary>
        private Vector3 UnityFromRobot(Vector3 r) => new(axisSign.x * r.x, r.z, -axisSign.z * r.y);

        private static double MaxAbs(double[] v)
        {
            var m = 0.0;
            foreach (var x in v) m = Math.Max(m, Math.Abs(x));
            return m;
        }

        /// <summary>6×6 高斯消去（部分主元）；奇異時回傳 null。</summary>
        private static double[] SolveLinear(double[,] a, double[] b)
        {
            const int n = 6;
            var m = new double[n, n + 1];
            for (var r = 0; r < n; r++)
            {
                for (var c = 0; c < n; c++) m[r, c] = a[r, c];
                m[r, n] = b[r];
            }
            for (var c = 0; c < n; c++)
            {
                var piv = c;
                for (var r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[piv, c])) piv = r;
                if (Math.Abs(m[piv, c]) < 1e-18) return null;
                if (piv != c) for (var k = 0; k <= n; k++) (m[c, k], m[piv, k]) = (m[piv, k], m[c, k]);
                for (var r = 0; r < n; r++)
                {
                    if (r == c) continue;
                    var factor = m[r, c] / m[c, c];
                    for (var k = c; k <= n; k++) m[r, k] -= factor * m[c, k];
                }
            }
            var x = new double[n];
            for (var r = 0; r < n; r++) x[r] = m[r, n] / m[r, r];
            return x;
        }
    }
}
