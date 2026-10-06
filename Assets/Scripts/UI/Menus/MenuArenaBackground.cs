using System.Collections.Generic;
using FakeBlade.Core;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Fondo del menú principal (GDD 9.2): la arena con varias peonzas controladas por
    /// SimpleAIBrain combatiendo solas. Las que se quedan sin RPM reaparecen.
    /// La cámara orbita despacio alrededor de la arena. Sin sacudida ni vibración.
    /// </summary>
    public class MenuArenaBackground : MonoBehaviour
    {
        [SerializeField] private FakeBladeCatalog catalog;
        [SerializeField][Range(2, 4)] private int bladeCount = 4;
        [SerializeField] private float respawnDelay = 2f;
        [SerializeField] private float orbitSpeed = 6f;
        [SerializeField] private float orbitDistanceFactor = 1.7f;
        [SerializeField] private float orbitHeightFactor = 1.1f;

        private ArenaDefinition _arena;
        private Camera _camera;
        private float _angle;
        private readonly List<FakeBladeController> _blades = new List<FakeBladeController>(4);
        private readonly List<float> _respawnTimers = new List<float>(4);

        private void Start()
        {
            CameraShake.Suppressed = true;
            if (catalog == null || catalog.playerPrefab == null || catalog.arenas.Count == 0 || catalog.arenas[0].prefab == null)
            {
                Debug.LogWarning("[MenuArenaBackground] Falta catálogo, prefab de jugador o arena.", this);
                return;
            }

            GameObject arenaObj = Instantiate(catalog.arenas[0].prefab);
            _arena = arenaObj.GetComponent<ArenaDefinition>();
            if (_arena == null) _arena = arenaObj.AddComponent<ArenaDefinition>();

            Transform[] spawns = _arena.CreateSpawnPoints(bladeCount);
            for (int i = 0; i < bladeCount; i++)
                SpawnBlade(i, spawns[i].position);

            _camera = Camera.main;
            if (_camera != null) SettingsService.ConfigureCamera(_camera);
        }

        private void SpawnBlade(int index, Vector3 position)
        {
            GameObject go = Instantiate(catalog.playerPrefab, position, Quaternion.identity);
            go.name = $"MenuBlade_{index}";

            var player = go.GetComponent<PlayerController>();
            var brain = go.AddComponent<SimpleAIBrain>();
            brain.aggression = Random.Range(0.45f, 0.8f);
            player.SetInputSource(brain);
            player.SetPlayerID(10 + index);
            player.SetPlayerColor(catalog.GetColor(index));
            player.SetColorScheme(BladeColors.Get(catalog, index));

            var preset = catalog.GetPreset(Random.Range(0, Mathf.Max(1, catalog.presets.Count)));
            var setup = new PlayerSetup();
            setup.ApplyPreset(preset);
            setup.EquipOn(go.GetComponent<FakeBladeStats>());

            var blade = go.GetComponent<FakeBladeController>();
            _blades.Add(blade);
            _respawnTimers.Add(0f);
        }

        private void Update()
        {
            for (int i = 0; i < _blades.Count; i++)
            {
                FakeBladeController blade = _blades[i];
                if (blade == null || !blade.IsDestroyed)
                {
                    _respawnTimers[i] = 0f;
                    continue;
                }

                _respawnTimers[i] += Time.deltaTime;
                if (_respawnTimers[i] < respawnDelay) continue;

                _respawnTimers[i] = 0f;
                blade.ResetFakeBlade();
                blade.SetPosition(_arena.RandomPoint(0.5f), Quaternion.identity);
                blade.SetInvulnerable(1f);
                VfxSystem.Play(VfxType.Respawn, blade.Position, Vector3.up, blade.Owner != null ? blade.Owner.PlayerColor : Color.white);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null || _arena == null) return;

            _angle += orbitSpeed * Time.deltaTime;
            float radius = _arena.InnerRadius;
            float rad = _angle * Mathf.Deg2Rad;
            Vector3 center = _arena.Center;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius * orbitDistanceFactor
                             + Vector3.up * radius * orbitHeightFactor;

            _camera.transform.position = center + offset;
            _camera.transform.LookAt(center);
        }

        private void OnDestroy()
        {
            CameraShake.Suppressed = false;
        }
    }
}
