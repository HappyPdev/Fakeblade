using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>Menú del banco de pruebas de equilibrio (C10): se usa con Play en la escena Sandbox.</summary>
    public static class BalanceBenchMenu
    {
        private const string CatalogPath = "Assets/Settings/FakeBladeCatalog.asset";

        [MenuItem("FakeBlade/Banco de equilibrio")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Banco de equilibrio",
                    "Abre la escena Sandbox, dale a Play y vuelve a lanzar el banco.", "Vale");
                return;
            }

            string error = BalanceBench.Run(AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>(CatalogPath));
            if (error != null) EditorUtility.DisplayDialog("Banco de equilibrio", error, "Vale");
            else Debug.Log("[BalanceBench] En marcha (unos minutos). No toques el mando hasta que acabe.");
        }

        [MenuItem("FakeBlade/Abrir último informe del banco")]
        public static void OpenLastReport()
        {
            if (string.IsNullOrEmpty(BalanceBench.LastReportPath)) return;
            EditorUtility.OpenWithDefaultApp(BalanceBench.LastReportPath);
        }

        [MenuItem("FakeBlade/Abrir último informe del banco", true)]
        private static bool CanOpenLastReport() => !string.IsNullOrEmpty(BalanceBench.LastReportPath);
    }
}
