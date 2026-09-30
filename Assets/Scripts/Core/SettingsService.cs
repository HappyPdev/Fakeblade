using System;
using System.Collections.Generic;
using FakeBlade.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FakeBlade.Core
{
    /// <summary>Opciones del jugador (GDD 9.2.3). Se guardan en PlayerPrefs como JSON.</summary>
    [Serializable]
    public class GameSettings
    {
        [Header("Gráficos")]
        public int resolutionWidth;
        public int resolutionHeight;
        public bool fullscreen = true;
        /// <summary>Índice de QualitySettings. -1 = el del proyecto.</summary>
        public int qualityLevel = -1;
        /// <summary>0 = sin sombras, 1 = duras, 2 = suaves.</summary>
        public int shadows = 2;
        /// <summary>0 = no, 1 = FXAA, 2 = SMAA.</summary>
        public int antialiasing = 0;
        public bool bloom = true;
        /// <summary>0 = bajas, 1 = medias, 2 = altas.</summary>
        public int particles = 2;
        public bool vsync = true;
        public bool showFps = false;

        [Header("Audio")]
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;

        [Header("Juego")]
        public bool vibration = true;
        public bool cameraShake = true;
    }

    /// <summary>
    /// Carga, guarda y aplica las opciones. Se inicializa sola antes de cargar la primera escena
    /// y vuelve a aplicar lo que depende de la escena (cámaras, luces) en cada carga.
    ///
    /// No modifica los assets de URP: el antialiasing y las sombras se aplican por cámara/luz,
    /// y el bloom con un Volume global propio.
    /// </summary>
    public static class SettingsService
    {
        private const string PrefsKey = "fakeblade.settings";
        private static readonly float[] ParticleMultipliers = { 0.35f, 0.7f, 1f };

        private static GameSettings _current;
        private static Volume _postFx;
        private static Bloom _bloom;
        private static readonly List<Light> LightBuffer = new List<Light>(8);

        public static event Action OnApplied;

        public static GameSettings Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        public static float ParticleMultiplier => ParticleMultipliers[Mathf.Clamp(Current.particles, 0, 2)];
        public static float SfxVolume => Current.sfxVolume;
        public static float MusicVolume => Current.musicVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _current = null;
            _postFx = null;
            _bloom = null;
            Load();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyGlobal();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyScene();

        public static void Load()
        {
            _current = null;
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { _current = JsonUtility.FromJson<GameSettings>(json); }
                catch (Exception e) { Debug.LogWarning($"[Settings] No se pudieron leer las opciones: {e.Message}"); }
            }

            if (_current == null) _current = new GameSettings();
            if (_current.resolutionWidth <= 0)
            {
                _current.resolutionWidth = Screen.currentResolution.width;
                _current.resolutionHeight = Screen.currentResolution.height;
            }
            if (_current.qualityLevel < 0) _current.qualityLevel = QualitySettings.GetQualityLevel();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
        }

        /// <summary>Aplica todo, guarda y avisa a quien escuche (p. ej. el reproductor de música).</summary>
        public static void Apply()
        {
            ApplyGlobal();
            ApplyScene();
            Save();
            OnApplied?.Invoke();
        }

        #region Global
        private static void ApplyGlobal()
        {
            GameSettings s = Current;

            if (s.qualityLevel >= 0 && s.qualityLevel < QualitySettings.names.Length &&
                QualitySettings.GetQualityLevel() != s.qualityLevel)
            {
                QualitySettings.SetQualityLevel(s.qualityLevel, true);
            }

            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            AudioListener.volume = Mathf.Clamp01(s.masterVolume);

#if !UNITY_EDITOR && !UNITY_WEBGL
            var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.width != s.resolutionWidth || Screen.height != s.resolutionHeight || Screen.fullScreenMode != mode)
                Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, mode);
#endif

            FpsCounter.SetVisible(s.showFps);
        }
        #endregion

        #region Scene
        private static void ApplyScene()
        {
            GameSettings s = Current;

            EnsurePostFx();
            _bloom.active = s.bloom;
            _bloom.intensity.value = s.bloom ? 0.8f : 0f;

            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
                ConfigureCamera(cameras[i]);

            LightBuffer.Clear();
            LightBuffer.AddRange(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None));
            for (int i = 0; i < LightBuffer.Count; i++)
            {
                Light light = LightBuffer[i];
                if (light.type != LightType.Directional) continue;
                light.shadows = s.shadows == 0 ? LightShadows.None : s.shadows == 1 ? LightShadows.Hard : LightShadows.Soft;
            }
        }

        /// <summary>Aplica AA/sombras/postprocesado a una cámara creada en runtime.</summary>
        public static void ConfigureCamera(Camera camera, bool allowPostProcessing = true)
        {
            if (camera == null) return;
            GameSettings s = Current;

            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;

            data.antialiasing = s.antialiasing == 1 ? AntialiasingMode.FastApproximateAntialiasing
                : s.antialiasing == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.None;
            data.renderShadows = s.shadows > 0;
            data.renderPostProcessing = allowPostProcessing;
        }

        private static void EnsurePostFx()
        {
            if (_postFx != null) return;

            var go = new GameObject("[PostFX]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _postFx = go.AddComponent<Volume>();
            _postFx.isGlobal = true;
            _postFx.priority = 100f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RuntimePostFX";
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.value = 0.9f;
            _bloom.scatter.value = 0.6f;
            _postFx.sharedProfile = profile;
        }
        #endregion

        #region Options helpers
        /// <summary>Resoluciones disponibles sin duplicados (ignora la frecuencia).</summary>
        public static List<Vector2Int> GetResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!list.Contains(size)) list.Add(size);
            }

            var current = new Vector2Int(Current.resolutionWidth, Current.resolutionHeight);
            if (current.x > 0 && !list.Contains(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
        #endregion
    }
}
