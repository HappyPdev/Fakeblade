using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Aprende a jugar (GDD 6.7, Fase P). Va en la escena Sandbox: lo añade SandboxController cuando
    /// MatchSetup.IsTutorial, en lugar de su panel y sus dummies. Las reglas son las del sandbox
    /// (sin tiempo y quien llega a 0 RPM reaparece), y BattleBootstrap ya ha creado la arena y los jugadores.
    ///
    /// P3: un dummy Balanceada en el centro (la cuarta plaza: el tutorial admite hasta 3 jugadores),
    /// que vuelve solo a su sitio si lo mueven (TutorialDummyBrain) y reaparece en el centro si cae.
    /// </summary>
    [RequireComponent(typeof(BattleBootstrap))]
    public class TutorialController : MonoBehaviour
    {
        private BattleBootstrap _bootstrap;
        private Transform _home;
        private PlayerController _dummy;
        private TutorialDummyBrain _dummyBrain;

        public PlayerController Dummy => _dummy;
        public TutorialDummyBrain DummyBrain => _dummyBrain;

        private void Awake()
        {
            _bootstrap = GetComponent<BattleBootstrap>();
        }

        private void Start()
        {
            // BattleBootstrap (orden -200) ya ha creado la arena, los jugadores y la cámara
            ArenaDefinition arena = ArenaDefinition.Current;
            _home = new GameObject("TutorialDummyHome").transform;
            _home.position = arena != null ? arena.ProjectToFloor(arena.Center) : Vector3.up * 0.6f;

            SpawnDummy();

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared += HandleMatchPrepared;
                gm.OnPlayerRespawned += HandleRespawned;
            }
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared -= HandleMatchPrepared;
                gm.OnPlayerRespawned -= HandleRespawned;
            }
        }

        /// <summary>El dummy Balanceada (el último preset del catálogo), en la plaza que sigue a los jugadores.</summary>
        private void SpawnDummy()
        {
            _dummy = _bootstrap.SpawnDummy(MatchSetup.Players.Count, 1);

            // Sustituye al cerebro del sandbox, que obedece al panel (SandboxSettings)
            if (_dummy.TryGetComponent(out SandboxDummyBrain sandboxBrain)) Destroy(sandboxBrain);
            _dummyBrain = _dummy.gameObject.AddComponent<TutorialDummyBrain>();
            _dummyBrain.Home = _home.position;
            _dummy.SetInputSource(_dummyBrain);

            _bootstrap.RefreshCameraTargets();
        }

        /// <summary>BeginMatch coloca a cada peonza en su plaza del anillo: el dummy va al centro.</summary>
        private void HandleMatchPrepared(System.Collections.Generic.IReadOnlyList<PlayerController> players)
        {
            if (_dummy != null) _dummy.SetSpawnPosition(_home);
        }

        private void HandleRespawned(PlayerController player)
        {
            if (player == _dummy) _dummy.SetSpawnPosition(_home);
        }
    }
}
