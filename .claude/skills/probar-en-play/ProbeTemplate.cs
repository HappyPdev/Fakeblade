// Plantilla de probe para Coplay execute_script. Cópiala al scratchpad, NO a Assets/.
// Renombra las clases (NAME) para que no choquen con probes anteriores.
using System.Collections;
using System.IO;
using System.Text;
using FakeBlade.Core;
using UnityEditor;
using UnityEngine;

public class NAMEProbe
{
    // Ruta absoluta dentro del scratchpad de la sesión.
    public const string LogPath = "<SCRATCHPAD>/name.txt";
    public const string ShotPrefix = "<SCRATCHPAD>/name_";

    public static string Execute()
    {
        if (!EditorApplication.isPlaying) return "no está en Play";
        File.Delete(LogPath);
        var r = new GameObject("NAMERunner").AddComponent<NAMERunner>();
        r.catalog = AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>("Assets/Settings/FakeBladeCatalog.asset");
        return "started";
    }
}

public class NAMERunner : MonoBehaviour
{
    public FakeBladeCatalog catalog;
    private readonly StringBuilder _log = new StringBuilder();
    private void Log(string s) { _log.AppendLine(s); File.WriteAllText(NAMEProbe.LogPath, _log.ToString()); }
    private void Shot(string tag) => ScreenCapture.CaptureScreenshot(NAMEProbe.ShotPrefix + tag + ".png", 2);

    private IEnumerator Start()
    {
        PlayerController p = null;
        foreach (var pc in GameManager.Instance.Players) if (pc.IsHumanControlled) p = pc;
        if (p == null) { Log("sin jugador humano"); Log("hecho"); Destroy(gameObject); yield break; }

        // Guardar el estado del usuario para restaurarlo tal cual.
        var saved = new[] { p.Stats.EquippedTip, p.Stats.EquippedBody, p.Stats.EquippedBlade, p.Stats.EquippedCore };
        var input = new NAMEInput();
        p.SetInputSource(input);

        try
        {
            // --- Prueba ---
            yield return new WaitForSeconds(1f);
            Log($"RPM {p.Blade.CurrentSpinSpeed:F0}/{p.Blade.MaxSpinSpeed:F0}");
            Shot("inicio");
            yield return null;
        }
        finally
        {
            foreach (var part in saved) if (part != null) p.Stats.EquipComponent(part);
            p.SetInputSource(null);
        }
        Log("hecho");
        Destroy(gameObject);
    }
}

public class NAMEInput : IBladeInputSource
{
    public Vector2 Move;
    public bool Held, Dash, Special;
    public Vector2 MovementInput => Move;
    public bool AttackHeld => Held;
    public bool ConsumeDash() { bool d = Dash; Dash = false; return d; }
    public bool ConsumeSpecial() { bool s = Special; Special = false; return s; }
    public void ClearBuffers() { Dash = Special = false; }
    public void Vibrate(float l, float h, float d) { }
}
