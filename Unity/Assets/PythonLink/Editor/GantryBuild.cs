using System.IO;
using OC.Communication;
using OC.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PythonLink.Editor
{
    /// <summary>
    /// 從 CAD 轉出來的雙軸直線模組（LinearGantry.fbx）建一個可用 Python 控制的場景並建置 WebGL。
    ///
    /// FBX 由 CAD 流程產生（STEP → 幾何分群 → Blender），物件名稱固定：
    ///   A_* 是水平軸（X），B_* 是掛在 A 滑座上的垂直軸（Z）；
    ///   *_Base 固定座、*_Motor 馬達、*_Carriage 滑座、*_Screw 滾珠螺桿。
    /// 行程來自 LinearGantry.motion.json（CAD 算出來的，單位 mm）。
    ///
    /// 結構（OC 的慣例：邏輯元件 → Axis → 模型）：
    ///   Project（PythonLinkClient，MAIN）
    ///     FG_Gantry（Hierarchy）
    ///       M_AxisX（DrivePosition，FB_Drive）   模型：A_Motor
    ///       Base_X                                A_Base
    ///       AxisX_Screw（Axis 旋轉）              A_Screw
    ///       AxisX_Carriage（Axis 直線）           A_Carriage 與整個 Z 模組
    ///         Module_Z
    ///           M_AxisZ（DrivePosition）          B_Motor
    ///           Base_Z / AxisZ_Screw / AxisZ_Carriage
    /// </summary>
    public static class GantryBuild
    {
        private const string SourceScene = PythonLinkBuild.PythonScene;
        private const string GantryScene = "Assets/LinearGantry/VC_LinearGantry.unity";
        private const string ModelPath = "Assets/LinearGantry/Models/LinearGantry.fbx";
        private const string MotionPath = "Assets/LinearGantry/Models/LinearGantry.motion.json";
        private const string OutputDir = "../Build/Gantry/WebGL";
        private const float ScrewLead = 10f;   // mm／轉：只影響螺桿轉動的視覺速度

        [System.Serializable] private class Motion { public Module[] modules; }
        [System.Serializable] private class Module { public string name; public string axis; public float travel_minus; public float travel_plus; }

        [MenuItem("Python Link/Create Gantry Scene")]
        public static void CreateScene()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (!importer.isReadable)
            {
                importer.isReadable = true;   // 部件點選要讀網格
                importer.SaveAndReimport();
            }
            var motion = JsonUtility.FromJson<Motion>(File.ReadAllText(MotionPath));

            if (!File.Exists(SourceScene)) PythonLinkBuild.CreateScene();
            if (File.Exists(GantryScene)) AssetDatabase.DeleteAsset(GantryScene);
            AssetDatabase.CopyAsset(SourceScene, GantryScene);
            var scene = EditorSceneManager.OpenScene(GantryScene, OpenSceneMode.Single);

            // 拿掉原本的產線，保留相機、UI、環境與 PythonLinkHud
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Project") Object.DestroyImmediate(root);
            }

            var project = new GameObject("Project");
            var client = project.AddComponent<PythonLinkClient>();
            var so = new SerializedObject(client);
            so.FindProperty("_rootName").stringValue = "MAIN";
            so.ApplyModifiedPropertiesWithoutUndo();

            var fg = new GameObject("FG_Gantry");
            fg.transform.SetParent(project.transform, false);
            fg.AddComponent<Hierarchy>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform Part(string n)
            {
                var t = model.transform.Find(n);
                if (t == null) throw new System.Exception($"FBX 裡找不到 {n}");
                return t;
            }

            var a = System.Array.Find(motion.modules, m => m.name == "A");
            var b = System.Array.Find(motion.modules, m => m.name == "B");

            // X 軸
            var motorX = Part("A_Motor");
            var driveX = Drive("M_AxisX", fg.transform, motorX);
            Group("Base_X", fg.transform, Part("A_Base"));
            AxisFor("AxisX_Screw", fg.transform, Part("A_Screw"), driveX, motorX, a, true);
            var carriageX = AxisFor("AxisX_Carriage", fg.transform, Part("A_Carriage"), driveX, motorX, a, false);

            // Z 軸：整組掛在 X 滑座上
            var moduleZ = new GameObject("Module_Z").transform;
            moduleZ.SetParent(carriageX, false);
            var motorZ = Part("B_Motor");
            var driveZ = Drive("M_AxisZ", moduleZ, motorZ);
            Group("Base_Z", moduleZ, Part("B_Base"));
            AxisFor("AxisZ_Screw", moduleZ, Part("B_Screw"), driveZ, motorZ, b, true);
            AxisFor("AxisZ_Carriage", moduleZ, Part("B_Carriage"), driveZ, motorZ, b, false);

            Object.DestroyImmediate(model);
            FrameCamera(fg);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PythonLink] 已建立 {GantryScene}");
        }

        private static DrivePosition Drive(string name, Transform parent, Transform motorMesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var drive = go.AddComponent<DrivePosition>();
            var so = new SerializedObject(drive);
            var speed = so.FindProperty("_speed");
            var value = speed?.FindPropertyRelative("_value");
            if (value != null) value.floatValue = 100f;   // mm/s
            so.ApplyModifiedPropertiesWithoutUndo();
            motorMesh.SetParent(go.transform, true);   // 馬達模型放在伺服底下：高亮、點選都對得到
            return drive;
        }

        private static void Group(string name, Transform parent, Transform mesh)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            mesh.SetParent(go, true);
        }

        /// <summary>
        /// 建一個沒有旋轉的 Axis 物件，放在模型的中心（螺桿就是螺桿軸心），把模型掛進去。
        /// 正方向：0 mm＝滑座在惰輪端，往馬達端為正；依模型的實際位置判斷，不受 FBX 軸向轉換影響。
        /// </summary>
        private static Transform AxisFor(string name, Transform parent, Transform mesh, Actor drive,
            Transform motorMesh, Module m, bool rotate)
        {
            var bounds = mesh.GetComponent<Renderer>().bounds;
            var go = new GameObject(name).transform;
            go.SetParent(parent, true);
            go.SetPositionAndRotation(bounds.center, Quaternion.identity);
            mesh.SetParent(go, true);

            // CAD 的 X、Y 對到 Unity 的 X、Y（CAD 是 Y 朝上）；方向正負在下面依模型判斷
            var dirIndex = m.axis == "X" ? 0 : m.axis == "Y" ? 1 : 2;
            var world = Vector3.zero;
            world[dirIndex] = 1;
            var toMotor = Mathf.Sign(Vector3.Dot(motorMesh.GetComponent<Renderer>().bounds.center - bounds.center, world));

            var axis = go.gameObject.AddComponent<Axis>();
            var so = new SerializedObject(axis);
            so.FindProperty("_actor").objectReferenceValue = drive;
            so.FindProperty("_type").enumValueIndex = (int)(rotate ? AxisType.Rotation : AxisType.Translation);
            so.FindProperty("_direction").enumValueIndex = dirIndex;
            so.FindProperty("_controlMode").enumValueIndex = (int)AxisControlMode.Position;
            if (rotate)
            {
                so.FindProperty("_factor").floatValue = 360f / ScrewLead;   // 每 mm 轉幾度
                so.FindProperty("_offset").floatValue = 0f;
            }
            else
            {
                // 模型在行程中間；0 mm＝惰輪端
                so.FindProperty("_factor").floatValue = 0.001f * toMotor;
                so.FindProperty("_offset").floatValue = m.travel_minus;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[PythonLink] {name}: rotate={rotate} axis={m.axis} toMotor={toMotor} center={bounds.center} size={bounds.size}");
            return go;
        }

        private static void FrameCamera(GameObject target)
        {
            var bounds = new Bounds(target.transform.position, Vector3.zero);
            var first = true;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            var cam = Object.FindAnyObjectByType<OC.UI.CameraController>();
            if (cam == null) { Debug.LogWarning("[PythonLink] 找不到 CameraController"); return; }
            var distance = bounds.extents.magnitude * 2.4f;
            var rot = Quaternion.Euler(20f, -35f, 0f);
            cam.transform.SetPositionAndRotation(bounds.center - rot * Vector3.forward * distance, rot);
            var so = new SerializedObject(cam);
            so.FindProperty("_distance").floatValue = distance;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[PythonLink] camera: bounds={bounds} distance={distance}");
        }

        [MenuItem("Python Link/Build Gantry WebGL")]
        public static void BuildWebGL()
        {
            CreateScene();
            PythonLinkBuild.ApplyPlayerSettings();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GantryScene },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[PythonLink] Gantry build {s.result}: {s.totalSize / 1e6:0.0} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
