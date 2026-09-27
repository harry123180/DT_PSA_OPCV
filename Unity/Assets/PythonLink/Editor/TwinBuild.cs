using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OC.Communication;
using OC.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PythonLink.Editor
{
    /// <summary>
    /// 通用的「CAD → 可程式控制場景」建置器：讀一份 twin.json（CAD 流程產生，座標都是 CAD 的 mm），
    /// 依 OC 的慣例「邏輯元件 → Axis → 模型」組出場景，並建置 WebGL。
    ///
    /// 批次：Unity.exe -batchmode -quit -buildTarget WebGL -projectPath Unity
    ///        -executeMethod PythonLink.Editor.TwinBuild.BuildFromCommandLine -twin Assets/ParallelRobot/twin.json -out ../Build/Robot/WebGL
    ///
    /// twin.json：
    ///   name、root（Hierarchy 名稱，PLC 路徑 MAIN.root.裝置）、model（Assets/name/Models/model.fbx）
    ///   devices  [{name, type: DrivePosition|DriveSimple|Cylinder|SensorBinary|SensorAnalog, mesh?, speed?, parent?}]
    ///   statics  [{name, mesh?, parent?}]                固定件；沒有 mesh 就是空的群組節點
    ///   axes     [{name, mesh, actor, type: translation|rotation, dir[3], factor, offset, parent?}]
    ///            translation：位移（m）=（程式給的值 + offset）× factor，沿 dir；rotation：角度（度）= 值 × factor，繞 dir
    ///   free     [{name, mesh, parent?}]                 由自訂元件擺放（例如並聯機構的連桿、平台）
    ///   custom   {type: PythonLink.ParallelRobot6, …}    閉鏈機構的運動學元件
    ///   pivots   [{mesh, p[3]}]                          模型原點的 CAD 座標，用來確認 CAD→Unity 的軸向
    /// parent 可以是另一個 axis 的名稱（串接：掛在它底下一起動），省略就放在 root。
    /// </summary>
    public static class TwinBuild
    {
        [Serializable] private class Spec { public string name, root, model; public Device[] devices; public Part[] statics, free; public AxisSpec[] axes; public Custom custom; public Pivot[] pivots; }
        [Serializable] private class Device { public string name, type, mesh, parent; public float speed; }
        [Serializable] private class Part { public string name, mesh, parent; }
        [Serializable] private class AxisSpec { public string name, mesh, actor, type, parent; public float[] dir; public float factor, offset; }
        [Serializable] private class Pivot { public string mesh; public float[] p; }
        [Serializable] private class Custom { public string type, platform; public float rodLength; public float[] platformCenter; public LegSpec[] legs; public string[] pose; }
        [Serializable] private class LegSpec { public string carriage, rod; public float[] bottom, top, u; public float home, travel; }

        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            var twin = Arg("-twin") ?? throw new Exception("缺 -twin <twin.json>");
            var output = Arg("-out") ?? throw new Exception("缺 -out <輸出目錄>");
            var scene = CreateScene(twin);
            PythonLinkBuild.ApplyPlayerSettings();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[PythonLink] Twin build {s.result}: {s.totalSize / 1e6:0.0} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static string CreateScene(string twinPath)
        {
            var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(twinPath));
            var dir = Path.GetDirectoryName(twinPath)!.Replace('\\', '/');
            var modelPath = $"{dir}/Models/{spec.model}.fbx";
            var scenePath = $"{dir}/VC_{spec.name}.unity";

            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath) ?? throw new Exception($"找不到 {modelPath}");
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }   // 部件點選要讀網格

            if (!File.Exists(PythonLinkBuild.PythonScene)) PythonLinkBuild.CreateScene();
            if (File.Exists(scenePath)) AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.CopyAsset(PythonLinkBuild.PythonScene, scenePath);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) if (go.name == "Project") Object.DestroyImmediate(go);   // 拿掉原本的產線

            var project = new GameObject("Project");
            var client = project.AddComponent<PythonLinkClient>();
            var cso = new SerializedObject(client);
            cso.FindProperty("_rootName").stringValue = "MAIN";
            cso.ApplyModifiedPropertiesWithoutUndo();
            var root = new GameObject(spec.root).transform;
            root.SetParent(project.transform, false);
            root.gameObject.AddComponent<Hierarchy>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var meshes = model.GetComponentsInChildren<Transform>().Where(t => t != model.transform).ToDictionary(t => t.name);
            Transform Mesh(string n) => meshes.TryGetValue(n, out var t) ? t : throw new Exception($"FBX 裡找不到 {n}");

            var sign = DetectAxisSign(spec, meshes);
            Vector3 Point(float[] p) => new Vector3(sign.x * p[0], p[1], sign.z * p[2]) * 0.001f;
            Vector3 Dir(float[] d) => new Vector3(sign.x * d[0], d[1], sign.z * d[2]).normalized;

            var nodes = new Dictionary<string, Transform>();
            Transform Parent(string n) => string.IsNullOrEmpty(n) ? root : nodes[n];

            // 依相依關係建節點：parent（和 axis 的 actor）已經建好的先建，反覆掃到全部完成。
            // 串接機構會有「裝置掛在群組、群組掛在上一軸的滑座」這種交錯，不能照類別順序建
            var actors = new Dictionary<string, Component>();
            var pending = new List<(string name, string parent, string needs, Action build)>();
            foreach (var d in spec.devices ?? Array.Empty<Device>())
            {
                var dd = d;
                pending.Add((d.name, d.parent, null, () =>
                {
                    var go = new GameObject(dd.name);
                    go.transform.SetParent(Parent(dd.parent), false);
                    Component c = dd.type switch
                    {
                        "DrivePosition" => go.AddComponent<DrivePosition>(),
                        "DriveSimple" => go.AddComponent<DriveSimple>(),
                        "Cylinder" => go.AddComponent<Cylinder>(),
                        "SensorBinary" => go.AddComponent<SensorBinary>(),
                        "SensorAnalog" => go.AddComponent<SensorAnalog>(),
                        _ => throw new Exception($"不支援的裝置類型 {dd.type}"),
                    };
                    if (dd.speed > 0)
                    {
                        var so = new SerializedObject(c);
                        var v = so.FindProperty("_speed")?.FindPropertyRelative("_value");
                        if (v != null) { v.floatValue = dd.speed; so.ApplyModifiedPropertiesWithoutUndo(); }
                    }
                    if (!string.IsNullOrEmpty(dd.mesh)) Mesh(dd.mesh).SetParent(go.transform, true);   // 馬達模型放在裝置底下：高亮、點選對得到
                    actors[dd.name] = c;
                    nodes[dd.name] = go.transform;
                }));
            }
            foreach (var p in spec.statics ?? Array.Empty<Part>())
            {
                var pp = p;
                pending.Add((p.name, p.parent, null, () =>
                {
                    if (string.IsNullOrEmpty(pp.mesh))
                    {
                        // 沒有模型＝群組節點：把一整組（例如掛在上一軸滑座上的整支模組）包起來，
                        // 部件檢視器碰到含別的裝置的分支會停，高亮上一軸時就不會連這整組一起亮
                        var g = new GameObject(pp.name).transform;
                        g.SetParent(Parent(pp.parent), false);
                        nodes[pp.name] = g;
                    }
                    else nodes[pp.name] = Wrap(pp.name, Parent(pp.parent), Mesh(pp.mesh), Quaternion.identity);
                }));
            }
            // 軸：物件放在模型原點，區域 Z 軸對準運動方向，Axis 的方向固定用 Z
            foreach (var a in spec.axes ?? Array.Empty<AxisSpec>())
            {
                var aa = a;
                pending.Add((a.name, a.parent, a.actor, () =>
                {
                    var t = Wrap(aa.name, Parent(aa.parent), Mesh(aa.mesh),
                        Quaternion.LookRotation(Dir(aa.dir), Math.Abs(aa.dir[1]) > 0.99f ? Vector3.forward : Vector3.up));
                    var axis = t.gameObject.AddComponent<Axis>();
                    var so = new SerializedObject(axis);
                    so.FindProperty("_actor").objectReferenceValue = actors[aa.actor];
                    so.FindProperty("_type").enumValueIndex = (int)(aa.type == "rotation" ? AxisType.Rotation : AxisType.Translation);
                    so.FindProperty("_direction").enumValueIndex = (int)AxisDirection.Z;
                    so.FindProperty("_controlMode").enumValueIndex = (int)AxisControlMode.Position;
                    so.FindProperty("_factor").floatValue = aa.factor;
                    so.FindProperty("_offset").floatValue = aa.offset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    nodes[aa.name] = t;
                }));
            }
            foreach (var p in spec.free ?? Array.Empty<Part>())
            {
                var pp = p;
                pending.Add((p.name, p.parent, null, () => nodes[pp.name] = Wrap(pp.name, Parent(pp.parent), Mesh(pp.mesh), Quaternion.identity)));
            }
            while (pending.Count > 0)
            {
                var ready = pending.FindIndex(x => (string.IsNullOrEmpty(x.parent) || nodes.ContainsKey(x.parent))
                                                   && (x.needs == null || actors.ContainsKey(x.needs)));
                if (ready < 0) throw new Exception("twin.json 的 parent／actor 有循環或指到不存在的節點：" + string.Join(", ", pending.Select(x => $"{x.name}←{x.parent}")));
                pending[ready].build();
                pending.RemoveAt(ready);
            }

            if (spec.custom != null && spec.custom.type == "PythonLink.ParallelRobot6") BuildParallelRobot(spec, root, nodes, actors, Point, sign);

            Object.DestroyImmediate(model);
            FrameCamera(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PythonLink] 已建立 {scenePath}");
            return scenePath;
        }

        /// <summary>包一層物件放在模型原點，把模型掛進去（模型本身帶的旋轉不影響之後的運動方向）。</summary>
        private static Transform Wrap(string name, Transform parent, Transform mesh, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.SetPositionAndRotation(mesh.position, rotation);
            mesh.SetParent(t, true);
            return t;
        }

        /// <summary>FBX 匯入會翻轉軸向（通常是 X）：拿模型原點跟 twin.json 的 CAD 座標比，挑誤差最小的正負號。</summary>
        private static Vector3 DetectAxisSign(Spec spec, Dictionary<string, Transform> meshes)
        {
            var best = new Vector3(-1, 1, 1);
            var bestErr = float.MaxValue;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var err = 0f;
                foreach (var p in spec.pivots ?? Array.Empty<Pivot>())
                {
                    if (!meshes.TryGetValue(p.mesh, out var t)) continue;
                    err += (t.position - new Vector3(sx * p.p[0], p.p[1], sz * p.p[2]) * 0.001f).magnitude;
                }
                if (err < bestErr) { bestErr = err; best = new Vector3(sx, 1, sz); }
            }
            var n = spec.pivots?.Length ?? 0;
            Debug.Log($"[PythonLink] CAD→Unity 軸向 {best}，原點平均誤差 {(n > 0 ? bestErr / n * 1000 : -1):0.000} mm（{n} 個）");
            if (n > 0 && bestErr / n > 0.001f) Debug.LogWarning("[PythonLink] 原點誤差超過 1 mm：FBX 的軸向或原點跟 CAD 對不上，先檢查 to_fbx.py 的 --up");
            return best;
        }

        private static void BuildParallelRobot(Spec spec, Transform root, Dictionary<string, Transform> nodes,
            Dictionary<string, Component> actors, Func<float[], Vector3> point, Vector3 sign)
        {
            var c = spec.custom;
            var robot = root.gameObject.AddComponent<ParallelRobot6>();
            var platform = nodes[c.platform];
            robot.platform = platform;
            robot.rodLength = c.rodLength * 0.001f;
            robot.axisSign = sign;
            robot.legs = new ParallelRobot6.Leg[c.legs.Length];
            for (var i = 0; i < c.legs.Length; i++)
            {
                var l = c.legs[i];
                var joint = new GameObject($"Joint{i + 1}").transform;
                joint.SetParent(nodes[l.carriage], false);
                joint.position = point(l.bottom);
                var rod = nodes[l.rod];
                rod.position = point(l.bottom);    // 連桿原點＝下端球心（CAD 流程已把 FBX 原點放在這裡）
                robot.legs[i] = new ParallelRobot6.Leg
                {
                    joint = joint, rod = rod,
                    rodRestDir = (point(l.top) - point(l.bottom)).normalized, rodRestRot = rod.rotation,
                    platformJoint = Quaternion.Inverse(platform.rotation) * (point(l.top) - platform.position),
                };
            }
            robot.pose = c.pose.Select(n => (SensorAnalog)actors[n]).ToArray();
            Debug.Log($"[PythonLink] ParallelRobot6：{c.legs.Length} 支腳、連桿 {c.rodLength} mm、平台原點 {platform.position}");
        }

        private static void FrameCamera(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var cam = Object.FindAnyObjectByType<OC.UI.CameraController>();
            if (cam == null) return;
            var distance = Mathf.Max(bounds.extents.magnitude * 1.9f, 0.5f);
            var rot = Quaternion.Euler(24f, -35f, 0f);
            cam.transform.SetPositionAndRotation(bounds.center - rot * Vector3.forward * distance, rot);
            var so = new SerializedObject(cam);
            so.FindProperty("_distance").floatValue = distance;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[PythonLink] camera: bounds={bounds} distance={distance}");
        }
    }
}
