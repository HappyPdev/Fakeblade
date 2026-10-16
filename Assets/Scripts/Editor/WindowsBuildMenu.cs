using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Build de Windows (J1, GDD 7.5) con un clic: escenas activas de Build Settings, en Builds/Windows/FakeBlade.exe.
    /// Se lanza en el siguiente ciclo del editor (bloquea el editor unos minutos) y deja el resultado en
    /// Logs/Build/ultima_build.txt. Para compartirla: la carpeta sin FakeBlade_BurstDebugInformation_DoNotShip.
    /// </summary>
    public static class WindowsBuildMenu
    {
        private const string OutputPath = "Builds/Windows/FakeBlade.exe";

        public static string ResultPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Build", "ultima_build.txt"));

        [MenuItem("FakeBlade/Build de Windows")]
        public static void Schedule()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Build de Windows", "Para el Play antes de hacer la build.", "Vale");
                return;
            }
            WriteResult("en marcha");
            EditorApplication.delayCall += Build;
        }

        private static void Build()
        {
            string text;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = ActiveScenes(),
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                BuildSummary s = BuildPipeline.BuildPlayer(options).summary;
                text = $"resultado={s.result}\nerrores={s.totalErrors}\navisos={s.totalWarnings}\n" +
                       $"tamaño={s.totalSize / (1024 * 1024)} MB\ntiempo={s.totalTime}\nruta={Path.GetFullPath(OutputPath)}";
            }
            catch (Exception e)
            {
                text = "excepción: " + e.Message;
            }
            WriteResult(text + "\nhecho");
            Debug.Log("[Build] " + text.Replace('\n', ' '));
        }

        private static string[] ActiveScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);
            return scenes.ToArray();
        }

        private static void WriteResult(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, text + "\n");
        }
    }
}
