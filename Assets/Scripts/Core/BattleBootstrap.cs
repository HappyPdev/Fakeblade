using System.Collections;
using System.Collections.Generic;
using FakeBlade.UI;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Arranque de la escena BattleArena: construye la partida a partir de MatchSetup
    /// (arena, jugadores con su dispositivo/piezas/color/equipo y el dummy en práctica),
    /// prepara cámara, GameManager y HUD, y lanza la cuenta atrás.
    ///
    /// Si se abre la escena directamente desde el editor, crea una partida por defecto de 2 jugadores.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] private FakeBladeCatalog catalog;

        private GameManager _gm;
        private ArenaDefinition _arena;
        private readonly List<GameObject> _spawned = new List<GameObject>(5);

        private void Awake()
        {
            if (catalog == null)
            {
                Debug.LogError("[BattleBootstrap] Falta el catálogo (FakeBladeCatalog).", this);
                enabled = false;
                return;
            }

            MenuStack.Reset();
            CombatConfig.SetActive(catalog.combatConfig);
            if (!MatchSetup.HasPlayers) MatchSetup.CreateDefault(catalog);

            _gm = GameManager.Instance;
            if (_gm == null) _gm = new GameObject("[GameManager]").AddComponent<GameManager>();
            _gm.SetRules(MatchSetup.Rules != null ? MatchSetup.Rules : MatchRules.CreateDefault());
        }

        private void Start()
        {
            if (catalog == null) return;

            BuildArena();

            bool practice = MatchSetup.IsPractice;
            int total = MatchSetup.Players.Count + (practice ? 1 : 0);
            Transform[] spawns = _arena.CreateSpawnPoints(total);
            _gm.SetSpawnPoints(spawns);

            for (int i = 0; i < MatchSetup.Players.Count; i++)
                SpawnPlayer(MatchSetup.Players[i], spawns[i].position);

            if (practice)
                SpawnDummy(MatchSetup.Players.Count, spawns[total - 1].position);

            SetupCamera();

            if (CombatHUDManager.Instance == null)
                CombatHUDManager.Create(catalog.uiTheme);

            StartCoroutine(BeginWhenReady());
        }

        private void BuildArena()
        {
            ArenaData data = MatchSetup.Arena != null ? MatchSetup.Arena : (catalog.arenas.Count > 0 ? catalog.arenas[0] : null);
            GameObject arenaObj = data != null && data.prefab != null
                ? Instantiate(data.prefab)
                : new GameObject("Arena (vacía)");

            _arena = arenaObj.GetComponent<ArenaDefinition>();
            if (_arena == null) _arena = arenaObj.AddComponent<ArenaDefinition>();
        }

        private void SpawnPlayer(PlayerSetup setup, Vector3 position)
        {
            GameObject go = Instantiate(catalog.playerPrefab, position, Quaternion.identity);
            var player = go.GetComponent<PlayerController>();

            player.SetPlayerID(setup.PlayerIndex);
            player.SetPlayerName($"Player {setup.PlayerIndex + 1}");
            player.SetPlayerColor(setup.Color);
            player.SetTeamID(setup.Team);
            player.SetInputDeviceById(setup.Device, setup.GamepadDeviceId);
            setup.EquipOn(go.GetComponent<FakeBladeStats>());

            _spawned.Add(go);
        }

        private void SpawnDummy(int playerIndex, Vector3 position)
        {
            GameObject go = Instantiate(catalog.playerPrefab, position, Quaternion.identity);
            var player = go.GetComponent<PlayerController>();

            player.SetPlayerID(playerIndex);
            player.SetPlayerName("Dummy");
            player.SetPlayerColor(catalog.dummyColor);
            player.SetTeamID(1);
            player.SetInputSource(go.AddComponent<DummyBrain>());

            var setup = new PlayerSetup();
            setup.ApplyPreset(catalog.GetPreset(catalog.presets.Count - 1));
            setup.EquipOn(go.GetComponent<FakeBladeStats>());

            _spawned.Add(go);
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            float radius = _arena.InnerRadius;
            Vector3 center = _arena.Center;
            cam.transform.position = center + new Vector3(0f, radius * 1.3f, -radius * 0.6f);
            cam.transform.LookAt(center);
            cam.fieldOfView = 60f;

            if (!cam.TryGetComponent(out SimpleCameraFollow follow))
                follow = cam.gameObject.AddComponent<SimpleCameraFollow>();
            follow.minZoom = radius * 1.1f;
            follow.maxZoom = radius * 2.1f;
            follow.zoomLimiter = radius * 1.6f;
            follow.SetTargets(_spawned.ToArray());

            SettingsService.ConfigureCamera(cam);
        }

        /// <summary>Espera a que los jugadores se registren (en su Start) y arranca la cuenta atrás.</summary>
        private IEnumerator BeginWhenReady()
        {
            yield return null;
            yield return null;
            _gm.BeginMatch();
        }
    }
}
