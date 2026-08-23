using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AntTime.EditorTools
{
    // เกมทั้งหมดสร้างจากโค้ดตอนรัน (ดู Game.Boot) scene จึงเป็นฉากว่าง ๆ ก็พอ
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("AntTime/Create Main Scene")]
        public static string EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return ScenePath;
        }

        [MenuItem("AntTime/Build Windows Player")]
        public static void BuildWindows()
        {
            EnsureScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/AntTime.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BuildTools] result=" + report.summary.result +
                      " errors=" + report.summary.totalErrors +
                      " size=" + report.summary.totalSize);
        }
    }
}
