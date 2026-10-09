using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public class RunEditModeTests
{
    public const string Out = "<SCRATCHPAD>/tests.txt";
    public static string Execute()
    {
        File.Delete(Out);
        // Con la escena modificada el Test Runner abre una ventana modal y Unity se bloquea hasta que el usuario conteste.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (scene.isDirty) return "escena con cambios sin guardar: " + scene.name + " (pregunta al usuario antes de lanzar los tests)";
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Collector());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "FakeBlade.Tests.EditMode" } }));
        return "started";
    }

    private class Collector : ICallbacks
    {
        private readonly StringBuilder _sb = new StringBuilder();
        public void RunStarted(ITestAdaptor t) { }
        public void TestStarted(ITestAdaptor t) { }
        public void TestFinished(ITestResultAdaptor r)
        {
            if (r.Test.IsSuite) return;
            _sb.AppendLine(r.TestStatus + "  " + r.Test.FullName);
            if (r.TestStatus != TestStatus.Passed) _sb.AppendLine("    " + (r.Message ?? "").Trim().Replace("\n", "\n    "));
        }
        public void RunFinished(ITestResultAdaptor r)
        {
            _sb.Insert(0, "Pasan " + r.PassCount + ", fallan " + r.FailCount + ", omitidos " + (r.SkipCount + r.InconclusiveCount) + "\n");
            _sb.AppendLine("hecho");
            File.WriteAllText(Out, _sb.ToString());
        }
    }
}
