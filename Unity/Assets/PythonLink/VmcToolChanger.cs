using System;
using OC.Components;
using OC.Interactions;
using UnityEngine;

namespace PythonLink
{
    /// <summary>
    /// 立式加工中心的傘式換刀（LinuxCNC VMC_toolchange 的機構）：刀具在主軸與刀庫刀位之間交接，並偵測撞機。
    ///
    /// 交接看「那一刻」：
    ///   鬆刀（Y_ToolRelease 到伸出端）時主軸有刀、換刀臂在「進」、主軸鼻端對準一個空刀位 → 刀交給刀庫
    ///   開始夾刀（拉刀桿離開鬆開端）時主軸沒刀、換刀臂在「進」、對準一個有刀的刀位 → 刀交給主軸
    ///   主軸沒刀、拉刀桿夾緊、降到對準有刀的刀位 → 刀交給主軸（LinuxCNC 範例的換刀程式就是這樣）
    /// 對準＝主軸鼻端與刀位基準點距離小於 alignTolerance（所以 Z 一定要在換刀高度、刀庫要轉到整數刀位）。
    ///
    /// 警報碼寫進 B_Alarm，第一個警報保持到 Q_ResetAlarm 上升緣；主軸上的刀號寫進 B_Tool（0＝沒刀）。
    ///
    /// 沒有刀庫（pockets 空的）＝手動換刀：操作員換刀，manualSelect（DrivePosition，值＝刀號）一變，
    /// 主軸停著就把那支刀裝上（其他刀不顯示）；主軸在轉時換刀是警報 14。
    /// </summary>
    public class VmcToolChanger : MonoBehaviour
    {
        [Serializable]
        public class Tool
        {
            public string name, label;
            public Transform transform;
            public float diameter, length, flute;   // mm
            public int home;                        // 原本的刀位（0 起算），掉刀後重置回這裡
            public bool inSpindle;                  // 開機時在主軸上
        }

        public const int AlarmLockedRotate = 1, AlarmPocketOccupied = 2, AlarmPullTool = 3, AlarmToolDropped = 4,
            AlarmReleaseSpinning = 5, AlarmSpinArmIn = 6, AlarmArmHitHead = 7, AlarmPocketMisaligned = 8,
            AlarmNoSpinCut = 10, AlarmTooDeep = 11, AlarmHitFixture = 12, AlarmNoseHit = 13, AlarmManualSpinning = 14;

        public Transform spindleGauge;
        public Transform[] pockets;
        public Tool[] tools;
        public float changeZ = -100f;
        public float alignTolerance = 0.003f;       // m

        public DriveSpeed spindle;
        public DrivePosition carousel, zAxis, manualSelect;
        public Cylinder arm, release, lockPin;
        public Lamp resetAlarm;
        public SensorAnalog toolSensor, alarmSensor;

        public int SpindleTool { get; private set; } = -1;
        public int Alarm { get; private set; }
        public Tool CurrentTool => SpindleTool >= 0 ? tools[SpindleTool] : null;
        public bool SpindleRunning => spindle != null && Mathf.Abs(spindle.Value.Value) > 50f;
        public bool Manual => pockets == null || pockets.Length == 0;

        private int[] _pocketTool;
        private bool _released, _armIn, _resetAlarm, _tooLow, _offHeight, _spinning;
        private float _lastCarousel;
        private int _lastSelect;
        private bool _settled, _releasedLimit;
        private bool _started;

        private void Start()
        {
            _pocketTool = new int[pockets.Length];
            for (var i = 0; i < _pocketTool.Length; i++) _pocketTool[i] = -1;
            SpindleTool = -1;
            for (var i = 0; i < tools.Length; i++)
            {
                if (tools[i].inSpindle) { SpindleTool = i; Attach(i, spindleGauge); }
                else if (Manual) { Attach(i, spindleGauge); tools[i].transform.gameObject.SetActive(false); }
                else { _pocketTool[tools[i].home] = i; Attach(i, pockets[tools[i].home]); }
            }
            if (carousel) _lastCarousel = carousel.Value.Value;
            if (manualSelect) _lastSelect = Mathf.RoundToInt(manualSelect.Value.Value);
            Publish();
            _started = true;
        }

        private void Attach(int tool, Transform anchor)
        {
            var t = tools[tool].transform;
            t.gameObject.SetActive(true);
            t.SetParent(anchor, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }

        public void Raise(int code, string why)
        {
            if (Alarm != 0) return;
            Alarm = code;
            Debug.LogWarning($"[VMC] 警報 {code}：{why}");
            Publish();
        }

        private void Publish()
        {
            if (toolSensor) toolSensor.SetValue(SpindleTool >= 0 ? SpindleTool + 1 : 0);
            if (alarmSensor) alarmSensor.SetValue(Alarm);
        }

        /// <summary>主軸鼻端正對的刀位（距離在容差內），沒有就 -1。horizontal＝只看水平距離。</summary>
        private int AlignedPocket(bool horizontal, out float distance)
        {
            var best = -1;
            distance = float.MaxValue;
            var g = spindleGauge.position;
            for (var i = 0; i < pockets.Length; i++)
            {
                var d = pockets[i].position - g;
                if (horizontal) d.y = 0;
                if (d.magnitude < distance) { distance = d.magnitude; best = i; }
            }
            return distance <= alignTolerance ? best : -1;
        }

        private void LateUpdate()
        {
            if (!_started) return;
            if (Manual) { ManualUpdate(); return; }
            var z = zAxis.Value.Value;
            var car = carousel.Value.Value;
            var armOut = arm.OnLimitMin.Value;
            var armIn = arm.OnLimitMax.Value;
            var released = release.OnLimitMax.Value;
            var clamped = release.OnLimitMin.Value;

            // 刀庫鎖住時轉刀庫
            if (Mathf.Abs(car - _lastCarousel) > 1e-4f && lockPin.OnLimitMax.Value)
                Raise(AlarmLockedRotate, "刀庫鎖銷插著時旋轉刀庫");

            // 換刀臂不在原位：主軸頭往下會撞到刀庫；主軸有刀時只能停在換刀高度。
            // 這幾個是「狀態」：只在剛成立的那一刻發警報，清除後狀態還在也不會一直重發（要把機台移回安全位置）
            var tooLow = !armOut && z < changeZ - 1f;
            var offHeight = !armOut && !tooLow && SpindleTool >= 0 && Mathf.Abs(z - changeZ) > 1f;
            var spinning = !armOut && SpindleRunning;
            if (tooLow && !_tooLow) Raise(AlarmArmHitHead, $"換刀臂不在原位時 Z 降到 {z:0.0}（低於換刀高度 {changeZ}），主軸頭撞到刀庫");
            if (offHeight && !_offHeight)
                Raise(armIn ? AlarmPullTool : AlarmArmHitHead,
                    armIn ? $"刀具被刀庫爪抓著時 Z 移到 {z:0.0}（扯刀）" : $"主軸有刀、Z 不在換刀高度（{z:0.0}）時擺動換刀臂，刀具撞到刀庫");
            if (spinning && !_spinning) Raise(AlarmSpinArmIn, "換刀臂不在原位時主軸旋轉");
            _tooLow = tooLow; _offHeight = offHeight; _spinning = spinning;

            if (armIn && !_armIn)
            {
                var p = AlignedPocket(true, out var dist);
                if (p < 0) Raise(AlarmPocketMisaligned, $"換刀臂擺進時刀庫沒有對準刀位（偏 {dist * 1000:0.0} mm）");
                else if (SpindleTool >= 0 && _pocketTool[p] >= 0)
                    Raise(AlarmPocketOccupied, $"主軸上有刀，換刀臂卻把有刀（T{_pocketTool[p] + 1}）的刀位 {p + 1} 擺過來（撞刀）");
            }

            if (released && !_released)
            {
                if (SpindleRunning) Raise(AlarmReleaseSpinning, "主軸旋轉中鬆刀");
                if (SpindleTool >= 0)
                {
                    var p = armIn ? AlignedPocket(false, out _) : -1;
                    if (p >= 0 && _pocketTool[p] < 0)
                    {
                        _pocketTool[p] = SpindleTool;
                        Attach(SpindleTool, pockets[p]);
                        Debug.Log($"[VMC] T{SpindleTool + 1} 放回刀位 {p + 1}");
                        SpindleTool = -1;
                    }
                    else
                    {
                        Raise(AlarmToolDropped, $"鬆刀時刀具 T{SpindleTool + 1} 沒有被刀庫接住，刀具掉落");
                        tools[SpindleTool].transform.gameObject.SetActive(false);
                        SpindleTool = -1;
                    }
                    Publish();
                }
            }

            // 裝刀：拉刀桿「開始夾」那一刻（離開鬆開端；LinuxCNC 的 toolchange.ngc 等到「不是鬆開」就馬上擺回換刀臂，
            // 等夾到底刀庫已經移開了），或主軸在夾緊狀態下降到對準有刀的刀位（主軸空的時候它不鬆刀就直接下去套刀把）
            var aligned = armIn ? AlignedPocket(false, out _) : -1;
            var clampStart = _releasedLimit && !released;
            _releasedLimit = released;
            // 到位要等 Z 停在換刀高度（對準容差 3 mm 比「扯刀」判斷的 1 mm 寬，下降途中先對準就交接會變成扯刀）
            var settled = aligned >= 0 && Mathf.Abs(z - changeZ) < 0.5f;
            var arrived = settled && !_settled && clamped;
            _settled = settled;
            if ((clampStart || (clamped && _released) || arrived) && SpindleTool < 0 && armIn)
            {
                var p = aligned;
                if (p >= 0 && _pocketTool[p] >= 0)
                {
                    SpindleTool = _pocketTool[p];
                    _pocketTool[p] = -1;
                    Attach(SpindleTool, spindleGauge);
                    Debug.Log($"[VMC] 主軸夾住 T{SpindleTool + 1}（來自刀位 {p + 1}）");
                    Publish();
                }
            }

            var reset = resetAlarm != null && resetAlarm.Value.Value;
            if (reset && !_resetAlarm) ResetAlarm();
            _resetAlarm = reset;

            _released = released || (!clamped && _released);
            _armIn = armIn;
            _lastCarousel = car;
        }

        /// <summary>手動換刀：選刀值（刀號，0＝卸刀）跟主軸上的不同時，主軸停著就換。</summary>
        private void ManualUpdate()
        {
            // 只在選刀值「改變」時換刀：開機時選刀值是 0，不能因此把主軸上的刀卸掉
            var want = manualSelect ? Mathf.RoundToInt(manualSelect.Value.Value) : SpindleTool + 1;
            var changed = want != _lastSelect;
            if (changed && (want == SpindleTool + 1 || want < 0 || want > tools.Length)) _lastSelect = want;
            else if (changed)
            {
                // 主軸在轉：拒絕並發警報，請求保留（_lastSelect 不更新），主軸停了、警報清了再換
                if (SpindleRunning) Raise(AlarmManualSpinning, $"主軸旋轉中手動換刀（T{SpindleTool + 1} → T{want}）");
                else if (Alarm == 0)
                {
                    _lastSelect = want;
                    if (SpindleTool >= 0) tools[SpindleTool].transform.gameObject.SetActive(false);
                    SpindleTool = want - 1;
                    if (SpindleTool >= 0) Attach(SpindleTool, spindleGauge);
                    Debug.Log($"[VMC] 手動換刀：主軸上 T{want}");
                    Publish();
                }
            }
            var reset = resetAlarm != null && resetAlarm.Value.Value;
            if (reset && !_resetAlarm) { Alarm = 0; Publish(); Debug.Log("[VMC] 警報清除"); }
            _resetAlarm = reset;
        }

        /// <summary>清除警報；掉落的刀放回原本的刀位（被占用就放第一個空刀位）。</summary>
        public void ResetAlarm()
        {
            for (var i = 0; i < tools.Length; i++)
            {
                if (tools[i].transform.gameObject.activeSelf) continue;
                var p = tools[i].home >= 0 && _pocketTool[tools[i].home] < 0 ? tools[i].home : Array.IndexOf(_pocketTool, -1);
                if (p < 0) continue;
                _pocketTool[p] = i;
                Attach(i, pockets[p]);
            }
            Alarm = 0;
            Publish();
            Debug.Log("[VMC] 警報清除");
        }
    }
}
