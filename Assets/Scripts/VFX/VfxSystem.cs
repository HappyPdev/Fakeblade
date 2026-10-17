using System.Collections.Generic;
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
                // Siempre simulando: con Automatic, un emisor vacío y fuera de la vista se pausa y, al volver a emitir,
                // «se pone al día» con todo el tiempo pausado y la partícula nueva envejece de golpe (los impactos no
                // se llegaban a ver). El culling ya lo hace IsVisible al emitir; sin partículas no cuesta nada
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = emitter.emission;
                emission.rateOverTime = 0f; // solo emite por código (Emit)
                emitter.Play(); // siempre "vivo" para que Emit simule las partículas
                _emitters[i] = emitter;
                _burstCounts[i] = Mathf.Max(1, entry.burstCount);
            }
            BuildImpactEmitters(library);
            // Las partículas se pintan nítidas encima de la escena pixelada (O2); los impactos, además, siempre delante
            ScreenPixelation.MoveToVfxLayer(gameObject);
            foreach (ImpactEmitter impact in _impactEmitters.Values) ScreenPixelation.MoveToVfxTopLayer(impact.System.gameObject);
            CollisionResolver.OnClashResolved += HandleClash;
            CollisionResolver.OnParry += HandleParry;
            CollisionResolver.OnBlock += HandleBlock;
        }

        private void OnDestroy()
        {
            CollisionResolver.OnClashResolved -= HandleClash;
            CollisionResolver.OnParry -= HandleParry;
            CollisionResolver.OnBlock -= HandleBlock;
            if (_instance == this) _instance = null;
        }

        /// <summary>Tipo de impacto con sprite (2026-10-17).</summary>
        public enum ImpactKind { Hit, Charged, Parry, Block }

        /// <summary>
        /// Impacto con sprite animado en cada choque (2026-10-17), entre las dos peonzas: golpe normal o cargado con el
        /// color y el tamaño según el daño, parry (círculo blanco) o bloqueo (cruz). Se suma a las chispas de siempre.
        /// </summary>
        private void HandleClash(CollisionResolver.ClashReport r)
        {
            if (r.A == null || r.B == null) return;
            Vector3 point = (r.A.Position + r.B.Position) * 0.5f;
            switch (r.Kind)
            {
                // El parry y el bloqueo van por sus eventos, que dicen quién para (su color)
                case CollisionResolver.ClashKind.Normal:
                case CollisionResolver.ClashKind.Even:
                case CollisionResolver.ClashKind.Launched:
                {
                    float damage = Mathf.Max(r.DamageToA, r.DamageToB);
                    if (damage < _library.impactMinDamage) return;
                    // Cargado si quien hace más daño venía con un ataque cargado
                    bool charged = (r.DamageToB >= r.DamageToA ? r.ChargeA : r.ChargeB) > 0;
                    PlayImpact(charged ? ImpactKind.Charged : ImpactKind.Hit, point, damage, Color.white);
                    break;
                }
            }
        }

        /// <summary>Parry: capas con el color de quien para (en un doble parry, blanco).</summary>
        private static void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool doubleParry) =>
            PlayImpact(ImpactKind.Parry, (parrier.Position + attacker.Position) * 0.5f, 0f,
                doubleParry ? Color.white : PlayerColorOf(parrier));

        /// <summary>Bloqueo: capas con el color de quien bloquea.</summary>
        private static void HandleBlock(FakeBladeController blocker, FakeBladeController attacker) =>
            PlayImpact(ImpactKind.Block, (blocker.Position + attacker.Position) * 0.5f, 0f, PlayerColorOf(blocker));

        private static Color PlayerColorOf(FakeBladeController blade)
        {
            if (blade.IsGhost && blade.GhostOwner != null) blade = blade.GhostOwner;
            return blade.Owner != null ? blade.Owner.PlayerColor : Color.white;
        }

        /// <summary>
        /// Lanza un impacto en un punto del suelo (choques y galería del Sandbox). Sale un poco hacia la cámara
        /// (impactCameraOffset), para que se vea delante de las peonzas y no cortado por ellas. playerColor tiñe las capas
        /// marcadas con «color del jugador» (quien hace el parry o el bloqueo).
        /// </summary>
        public static void PlayImpact(ImpactKind kind, Vector3 groundPoint, float damage, Color playerColor)
        {
            VfxSystem system = Instance;
            if (system == null) return;
            VfxLibrary lib = system._library;

            Vector3 point = groundPoint + Vector3.up * lib.impactHeight;
            if (system._camera == null || !system._camera.isActiveAndEnabled) system._camera = Camera.main;
            if (system._camera != null)
                point += (system._camera.transform.position - point).normalized * lib.impactCameraOffset;

            List<ImpactLayer> layers;
            float size;
            switch (kind)
            {
                case ImpactKind.Parry:
                    layers = lib.parryLayers;
                    size = lib.parryImpactSize;
                    break;
                case ImpactKind.Block:
                    layers = lib.blockLayers;
                    size = lib.blockImpactSize;
                    break;
                case ImpactKind.Charged:
                    layers = lib.chargedLayers;
                    size = lib.ImpactSize(damage) * lib.chargedImpactScale;
                    break;
                default:
                    layers = damage < lib.smallHitMaxDamage && lib.smallHitLayers.Count > 0 ? lib.smallHitLayers : lib.hitLayers;
                    size = lib.ImpactSize(damage);
                    break;
            }

            // Todas las capas con la misma rotación, para que se superpongan bien (la sombra, detrás de su impacto)
            float rotation = Random.Range(0f, 360f);
            Color damageColor = lib.ImpactColor(damage);
            for (int i = 0; i < layers.Count; i++)
            {
                ImpactLayer layer = layers[i];
                if (layer == null || !system._impactEmitters.TryGetValue(layer.sprite ?? "", out ImpactEmitter emitter)) continue;
                Color color = layer.playerColor ? new Color(playerColor.r, playerColor.g, playerColor.b, layer.color.a)
                    : layer.damageColor ? new Color(damageColor.r, damageColor.g, damageColor.b, layer.color.a)
                    : layer.color;
                if (layer.lighten > 0f)
                {
                    Color light = Color.Lerp(color, Color.white, layer.lighten);
                    color = new Color(light.r, light.g, light.b, color.a);
                }
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = Vector3.zero,
                    startColor = color,
                    startSize = size * layer.size,
                    rotation = (emitter.Sprite.randomRotation ? rotation : 0f) + layer.rotation
                };
                emitter.System.Emit(emitParams, 1);
            }
        }

        private readonly struct ImpactEmitter
        {
            public readonly ParticleSystem System;
            public readonly ImpactSprite Sprite;

            public ImpactEmitter(ParticleSystem system, ImpactSprite sprite)
            {
                System = system;
                Sprite = sprite;
            }
        }

        private readonly Dictionary<string, ImpactEmitter> _impactEmitters = new Dictionary<string, ImpactEmitter>();

        /// <summary>Un emisor por sprite de impacto (las capas los buscan por id al lanzar cada impacto).</summary>
        private void BuildImpactEmitters(VfxLibrary library)
        {
            for (int i = 0; i < library.impactSprites.Count; i++)
            {
                ImpactSprite sprite = library.impactSprites[i];
                if (sprite == null || sprite.prefab == null || string.IsNullOrEmpty(sprite.id) || _impactEmitters.ContainsKey(sprite.id)) continue;
                ParticleSystem emitter = Instantiate(sprite.prefab, transform);
                emitter.name = "Impact_" + sprite.id;
                var main = emitter.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.loop = true;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = emitter.emission;
                emission.rateOverTime = 0f;
                emitter.Play();
                _impactEmitters.Add(sprite.id, new ImpactEmitter(emitter, sprite));
            }
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
