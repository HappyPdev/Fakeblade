using UnityEngine;
using UnityEngine.SceneManagement;

namespace FakeBlade.Core
{
    /// <summary>
    /// Nombres de escena y carga centralizada (GDD 7.2).
    /// MainMenu → Assembly (selección de peonzas) → BattleArena.
    /// </summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string Lobby = "Assembly";
        public const string Battle = "BattleArena";

        public static bool CanLoad(string scene) =>
            !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

        public static void Load(string scene)
        {
            Time.timeScale = 1f;
            MenuStack.Reset();

            if (!CanLoad(scene))
            {
                Debug.LogError($"[SceneFlow] La escena '{scene}' no está en Build Settings.");
                return;
            }

            SceneManager.LoadScene(scene);
        }
    }

    /// <summary>
    /// Contador de submenús abiertos (opciones, controles...). Mientras haya alguno,
    /// Esc/Start no pausan ni reanudan la partida: los gestiona el submenú.
    /// </summary>
    public static class MenuStack
    {
        private static int _open;

        public static bool IsSubmenuOpen => _open > 0;

        public static void Push() => _open++;

        public static void Pop() => _open = Mathf.Max(0, _open - 1);

        public static void Reset() => _open = 0;
    }
}
