using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Pooling de partículas (GDD 7.3).
    ///
    /// En lugar de instanciar un efecto por golpe, hay UN ParticleSystem por tipo de efecto
    /// (simulación en espacio de mundo) y cada ráfaga se emite con Emit() en la posición y
    /// dirección del evento. Las partículas se reciclan dentro del búfer del propio sistema
    /// (maxParticles hace de límite de memoria): cero Instantiate/Destroy y cero GC en combate.
    ///
    /// - Culling dinámico: no se emite lo que queda fuera de la cámara.
    /// - La cantidad se escala con Opciones → Partículas.
    /// - Se crea solo, por escena, la primera vez que se usa.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class VfxSystem : MonoBehaviour
    {
        private const float ViewportMargin = 0.1f;

        private static VfxSystem _instance;
        private static bool _missingLibraryWarned;

        private VfxLibrary _library;
        private ParticleSystem[] _emitters;
        private int[] _burstCounts;
        private Camera _camera;
        private int _cameraFrame = -1;

        public static VfxLibrary Library => Instance != null ? Instance._library : null;

        private static VfxSystem Instance
        {
            get
            {
                if (_instance != null || !Application.isPlaying) return _instance;

                var library = Resources.Load<VfxLibrary>(VfxLibrary.ResourcesPath);
                if (library == null)
                {
                    if (!_missingLibraryWarned)
                        Debug.LogWarning("[VfxSystem] No hay VfxLibrary en Resources. Usa 'FakeBlade/Setup VFX'.");
                    _missingLibraryWarned = true;
                    return null;
                }

                var go = new GameObject("[VFX]");
                _instance = go.AddComponent<VfxSystem>();
                _instance.Build(library);
                return _instance;
            }
        }

        private void Build(VfxLibrary library)
        {
            _library = library;
            int count = System.Enum.GetValues(typeof(VfxType)).Length;
            _emitters = new ParticleSystem[count];
            _burstCounts = new int[count];

            for (int i = 0; i < count; i++)
            {
                VfxLibrary.Entry entry = library.Get((VfxType)i);
                if (entry == null || entry.prefab == null) continue;

                ParticleSystem emitter = Instantiate(entry.prefab, transform);
                emitter.name = ((VfxType)i).ToString();
                var main = emitter.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.loop = true;
                main.cullingMode = ParticleSystemCullingMode.Automatic;
                var emission = emitter.emission;
                emission.rateOverTime = 0f; // solo emite por código (Emit)
                emitter.Play(); // siempre "vivo" para que Emit simule las partículas
                _emitters[i] = emitter;
                _burstCounts[i] = Mathf.Max(1, entry.burstCount);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        #region API
        /// <summary>Ráfaga de un efecto. direction orienta la forma (cono) del emisor.</summary>
        public static void Play(VfxType type, Vector3 position, Vector3 direction, Color color, float intensity = 1f)
        {
            VfxSystem system = Instance;
            if (system == null) return;
            system.Emit(type, position, direction, color, system.ScaledCount(type, intensity));
        }

        /// <summary>Emite un número exacto de partículas (auras continuas; ya escalado por el llamador).</summary>
        public static void EmitCount(VfxType type, Vector3 position, Color color, int count)
        {
            if (count <= 0) return;
            VfxSystem system = Instance;
            if (system == null) return;
            system.Emit(type, position, Vector3.up, color, count);
        }

        /// <summary>
        /// Una partícula con posición y velocidad propias (efectos continuos de las peonzas).
        /// Sin culling: el llamador lo comprueba una vez por frame con IsOnScreen.
        /// size / lifetime / rotation ≤ 0 usan los valores del prefab.
        /// </summary>
        public static void EmitParticle(VfxType type, Vector3 position, Vector3 velocity, Color color,
            float size = 0f, float lifetime = 0f, float rotation = 0f)
        {
            VfxSystem system = _instance;
            if (system == null) return;
            ParticleSystem emitter = system._emitters[(int)type];
            if (emitter == null) return;

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color
            };
            if (size > 0f) emitParams.startSize = size;
            if (lifetime > 0f) emitParams.startLifetime = lifetime;
            if (rotation > 0f) emitParams.rotation = rotation;
            emitter.Emit(emitParams, 1);
        }

        /// <summary>¿Se ve este punto? (con margen). Crea el sistema si hace falta.</summary>
        public static bool IsOnScreen(Vector3 position)
        {
            VfxSystem system = Instance;
            return system != null && system.IsVisible(position);
        }

        public static Color ParryColor
        {
            get
            {
                VfxLibrary library = Library;
                return library != null ? library.parryColor : Color.cyan;
            }
        }
        #endregion

        private int ScaledCount(VfxType type, float intensity)
        {
            int baseCount = _burstCounts[(int)type];
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * Mathf.Max(0.1f, intensity) * SettingsService.ParticleMultiplier));
        }

        private void Emit(VfxType type, Vector3 position, Vector3 direction, Color color, int count)
        {
            ParticleSystem emitter = _emitters[(int)type];
            if (emitter == null || !IsVisible(position)) return;

            Transform t = emitter.transform;
            if (direction.sqrMagnitude > 0.0001f)
                t.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            else
                t.position = position;

            var emitParams = new ParticleSystem.EmitParams
            {
                startColor = color,
                applyShapeToPosition = true
            };
            emitter.Emit(emitParams, count);
        }

        /// <summary>Culling: descarta efectos fuera de la vista (con margen).</summary>
        private bool IsVisible(Vector3 position)
        {
            if (_cameraFrame != Time.frameCount)
            {
                _cameraFrame = Time.frameCount;
                if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            }
            if (_camera == null) return true;

            Vector3 vp = _camera.WorldToViewportPoint(position);
            return vp.z > 0f &&
                   vp.x > -ViewportMargin && vp.x < 1f + ViewportMargin &&
                   vp.y > -ViewportMargin && vp.y < 1f + ViewportMargin;
        }
    }
}
