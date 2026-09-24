using System.IO;
using System.Linq;
using OC.Communication;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PythonLink.Editor
{
    /// <summary>
    /// 從 Beckhoff 場景產生 Python 版場景，並建置 WebGL。
    /// 批次模式：Unity.exe -batchmode -quit -projectPath Unity -executeMethod PythonLink.Editor.PythonLinkBuild.BuildWebGL
    /// </summary>
    public static class PythonLinkBuild
    {
        private const string SourceScene = "Assets/Demo_1/Scenes/VC_Demo_1_Beckhoff_1.unity";
        public const string PythonScene = "Assets/Demo_1/Scenes/VC_Demo_1_Python.unity";
        private const string OutputDir = "../Build/WebGL";

        [MenuItem("Python Link/Create Python Scene")]
        public static void CreateScene()
        {
            if (File.Exists(PythonScene)) AssetDatabase.DeleteAsset(PythonScene);
            if (!AssetDatabase.CopyAsset(SourceScene, PythonScene))
                throw new System.Exception($"無法複製 {SourceScene}");

            var scene = EditorSceneManager.OpenScene(PythonScene, OpenSceneMode.Single);
            var clients = Object.FindObjectsByType<Client>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c is not PythonLinkClient).ToList();
            if (clients.Count == 0) throw new System.Exception("場景裡找不到 Client");

            foreach (var old in clients)
            {
                var go = old.gameObject;
                // 解包 prefab 實例：只改這個場景，不動其他場景共用的 Machine_1.prefab
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

                var rootName = old.RootName;
                Object.DestroyImmediate(old, true);
                var client = go.AddComponent<PythonLinkClient>();
                var so = new SerializedObject(client);
                so.FindProperty("_rootName").stringValue = rootName;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[PythonLink] {go.name}: 換成 PythonLinkClient（root={rootName}）");
            }

            if (Object.FindAnyObjectByType<PythonLinkHud>() == null)
            {
                var hud = new GameObject("PythonLinkHud");
                hud.AddComponent<PythonLinkHud>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PythonLink] 已建立 {PythonScene}");
        }

        [MenuItem("Python Link/Build WebGL")]
        public static void BuildWebGL()
        {
            CreateScene();

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // 任何靜態主機都能開，不必設 Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;               // 切到 Python 視窗時孿生繼續跑
            PlayerSettings.productName = "DT PSA OPCV (Python Link)";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PythonScene },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[PythonLink] Build {s.result}: {s.totalSize / 1e6:0.0} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
