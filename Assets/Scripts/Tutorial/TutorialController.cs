using System.Collections.Generic;
using FakeBlade.UI;
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
    ///
    /// P4, director de pasos (TutorialSteps): cada jugador juega a través de un TutorialInputGate que solo deja
    /// las acciones ya enseñadas; la energía del especial se vacía hasta su paso; cada jugador tiene su contador y
    /// se pasa al paso siguiente cuando todos han cumplido el objetivo (ventana TutorialHintPanel).
    /// </summary>
    [RequireComponent(typeof(BattleBootstrap))]
    public class TutorialController : MonoBehaviour
    {
        private const int HintCanvasOrder = 200;
        /// <summary>Tiempo con «¡BIEN!» antes del paso siguiente.</summary>
        private const float AdvanceDelay = 1.5f;
        /// <summary>Moverse: cuenta mientras el stick está a más de la mitad y la peonza va a más de esto (m/s).</summary>
        private const float MoveMinSpeed = 2f;
        /// <summary>Paredes: el golpe contra la pared cuenta para quien golpeó a la víctima en estos segundos.</summary>
        private const float WallCreditTime = 1.5f;

        private BattleBootstrap _bootstrap;
        private Transform _home;
        private PlayerController _dummy;
        private TutorialDummyBrain _dummyBrain;
        private TutorialHintPanel _hint;

        private readonly List<PlayerController> _humans = new List<PlayerController>(3);
        private readonly List<PlayerSetup> _setups = new List<PlayerSetup>(3);
        private readonly List<TutorialInputGate> _gates = new List<TutorialInputGate>(3);
        private int[] _progress = new int[0];
        private float[] _moveTime = new float[0];
        /// <summary>Ataques lanzados por cada jugador y el último que ya contó: un ataque cuenta una vez aunque choque dos.</summary>
        private int[] _attackSerial = new int[0];
        private int[] _countedSerial = new int[0];
        private bool[] _wallDone = new bool[0];
        private bool[] _dashWinDone = new bool[0];

        private TutorialStep _step;
        private float _advanceTimer = -1f;

        public PlayerController Dummy => _dummy;
        public TutorialDummyBrain DummyBrain => _dummyBrain;
        public TutorialStep Step => _step;

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

            HUDTheme theme = _bootstrap.Catalog.uiTheme != null ? _bootstrap.Catalog.uiTheme : HUDTheme.CreateDefault();
            _hint = TutorialHintPanel.Create(transform, theme, HintCanvasOrder);
            _hint.SetVisible(false);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared += HandleMatchPrepared;
                gm.OnPlayerRespawned += HandleRespawned;
            }
            Loc.OnLanguageChanged += ShowStep;
            InputBindings.OnChanged += ShowStep;
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared -= HandleMatchPrepared;
                gm.OnPlayerRespawned -= HandleRespawned;
            }
            Loc.OnLanguageChanged -= ShowStep;
            InputBindings.OnChanged -= ShowStep;
            CollisionResolver.OnClashResolved -= HandleClash;
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
        private void HandleMatchPrepared(IReadOnlyList<PlayerController> players)
        {
            if (_dummy != null) _dummy.SetSpawnPosition(_home);
            if (_humans.Count == 0) SetupPlayers(players);
        }

        private void HandleRespawned(PlayerController player)
        {
            if (player == _dummy) _dummy.SetSpawnPosition(_home);
        }

        #region Jugadores
        /// <summary>Una sola vez: cada jugador juega a través de su filtro de acciones y se cuentan sus logros.</summary>
        private void SetupPlayers(IReadOnlyList<PlayerController> players)
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null || player == _dummy) continue;
                _humans.Add(player);
            }
            _humans.Sort((a, b) => a.PlayerID.CompareTo(b.PlayerID));

            int count = _humans.Count;
            _progress = new int[count];
            _moveTime = new float[count];
            _attackSerial = new int[count];
            _countedSerial = new int[count];
            _wallDone = new bool[count];
            _dashWinDone = new bool[count];
            for (int i = 0; i < count; i++)
            {
                PlayerController player = _humans[i];
                _setups.Add(FindSetup(player.PlayerID));

                var gate = new TutorialInputGate(player.Input);
                _gates.Add(gate);
                player.SetInputSource(gate);

                int index = i;
                player.Blade.OnDashExecuted += () => OnDash(index);
                player.Blade.OnAttackLaunched += level => _attackSerial[index]++;
                FakeBladeController blade = player.Blade;
                blade.OnWallHit += intensity => OnWallHit(blade);
            }

            // Paredes: vale empujar al dummy o a otro jugador
            FakeBladeController dummyBlade = _dummy.Blade;
            dummyBlade.OnWallHit += intensity => OnWallHit(dummyBlade);
            CollisionResolver.OnClashResolved += HandleClash;

            EnterStep(TutorialStep.Move);
        }

        private static PlayerSetup FindSetup(int playerId)
        {
            for (int i = 0; i < MatchSetup.Players.Count; i++)
                if (MatchSetup.Players[i].PlayerIndex == playerId) return MatchSetup.Players[i];
            return null;
        }
        #endregion

        #region Pasos
        private void EnterStep(TutorialStep step)
        {
            _step = step;
            _advanceTimer = -1f;
            for (int i = 0; i < _progress.Length; i++)
            {
                _progress[i] = 0;
                _moveTime[i] = 0f;
                _countedSerial[i] = _attackSerial[i];
                _wallDone[i] = _dashWinDone[i] = false;
            }
            if (_dummyBrain != null) _dummyBrain.ReturnHome = step != TutorialStep.WallsAndDash;

            for (int i = 0; i < _gates.Count; i++)
            {
                TutorialInputGate gate = _gates[i];
                gate.AllowDash = TutorialSteps.AllowsDash(step);
                gate.AllowAttack = TutorialSteps.AllowsAttack(step);
                gate.AllowCharge = TutorialSteps.AllowsCharge(step);
                gate.AllowSpecial = TutorialSteps.AllowsSpecial(step);
            }

            ShowStep();
        }

        /// <summary>Textos del paso y botón de cada jugador (también al cambiar de idioma o de controles).</summary>
        private void ShowStep()
        {
            if (_hint == null || _humans.Count == 0) return;

            string key = TutorialSteps.Key(_step);
            string stepLabel = _step < TutorialStep.Done
                ? Loc.Format("TUT_STEP_N", (int)_step + 1, TutorialSteps.Count)
                : string.Empty;
            _hint.SetStep(stepLabel, Loc.Get("TUT_" + key + "_TITLE"), Loc.Get("TUT_" + key + "_BODY"));

            TutorialButton button = TutorialSteps.Button(_step);
            _hint.SetPlayerCount(button == TutorialButton.None ? 0 : _humans.Count);
            for (int i = 0; i < _humans.Count; i++)
            {
                PlayerController player = _humans[i];
                _hint.SetPlayer(i, Loc.Format("PLAYER_BADGE", player.PlayerID + 1), player.PlayerColor, ButtonLabel(_setups[i], button));
            }
            if (_advanceTimer >= 0f) _hint.ShowWellDone(Loc.Get("TUT_WELL_DONE"));
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || _humans.Count == 0) return;

            bool inMatch = gm.CurrentState == GameManager.GameState.InMatch;
            GameManager.GameState state = gm.CurrentState;
            _hint.SetVisible(inMatch || state == GameManager.GameState.Countdown || state == GameManager.GameState.Paused ||
                             state == GameManager.GameState.Resuming);
            if (!inMatch) return;

            // Hasta su paso, el especial no se carga (tampoco el del dummy)
            if (!TutorialSteps.AllowsSpecial(_step))
            {
                for (int i = 0; i < _humans.Count; i++) _humans[i].Blade.Special.ClearEnergy();
                if (_dummy != null) _dummy.Blade.Special.ClearEnergy();
            }

            if (_step == TutorialStep.Move) TrackMovement(Time.deltaTime);

            int goal = TutorialSteps.Goal(_step);
            bool allDone = goal > 0;
            for (int i = 0; i < _humans.Count; i++)
            {
                _hint.SetProgress(i, _progress[i], goal);
                allDone &= _progress[i] >= goal;
            }

            if (_advanceTimer >= 0f)
            {
                _advanceTimer -= Time.deltaTime;
                if (_advanceTimer < 0f) EnterStep(_step + 1);
            }
            else if (allDone)
            {
                _advanceTimer = AdvanceDelay;
                _hint.ShowWellDone(Loc.Get("TUT_WELL_DONE"));
            }
        }

        /// <summary>Moverse: segundos con el stick a más de la mitad y la peonza en marcha.</summary>
        private void TrackMovement(float dt)
        {
            for (int i = 0; i < _humans.Count; i++)
            {
                if (_gates[i].MovementInput.sqrMagnitude < 0.25f) continue;
                if (_humans[i].Blade.Velocity.sqrMagnitude < MoveMinSpeed * MoveMinSpeed) continue;
                _moveTime[i] += dt;
                _progress[i] = Mathf.FloorToInt(_moveTime[i]);
            }
        }

        private void OnDash(int index)
        {
            if (_step == TutorialStep.Dash && index < _progress.Length) _progress[index]++;
        }

        /// <summary>
        /// Golpes que entran: un choque normal que gana el jugador (llega más rápido) mientras ataca. Cuenta una vez por
        /// ataque; en el paso del cargado, solo los de nivel 1 o más. El dash que gana un choque, en el paso de paredes.
        /// </summary>
        private void HandleClash(CollisionResolver.ClashReport report)
        {
            if (report.Kind != CollisionResolver.ClashKind.Normal || _advanceTimer >= 0f) return;

            bool aWins = report.ApproachA >= report.ApproachB;
            FakeBladeController winner = aWins ? report.A : report.B;
            bool dash = aWins ? report.DashA : report.DashB;
            int i = HumanIndex(winner);
            if (i < 0) return;

            switch (_step)
            {
                case TutorialStep.QuickAttack:
                case TutorialStep.ChargedAttack:
                    AttackSystem attack = winner.Attack;
                    if (dash || !attack.IsAttacking || _countedSerial[i] == _attackSerial[i]) return;
                    if (_step == TutorialStep.ChargedAttack && attack.AttackLevel < 1) return;
                    _countedSerial[i] = _attackSerial[i];
                    _progress[i]++;
                    break;
                case TutorialStep.WallsAndDash:
                    if (!dash) return;
                    _dashWinDone[i] = true;
                    UpdateWallsProgress(i);
                    break;
            }
        }

        /// <summary>Paredes: la víctima choca contra la pared poco después de que un jugador la golpeara.</summary>
        private void OnWallHit(FakeBladeController victim)
        {
            if (_step != TutorialStep.WallsAndDash || _advanceTimer >= 0f) return;
            FakeBladeController source = victim.LastDamageSource;
            if (source == null || source == victim || Time.time - victim.LastDamageTime > WallCreditTime) return;

            int i = HumanIndex(source);
            if (i < 0) return;
            _wallDone[i] = true;
            UpdateWallsProgress(i);
        }

        private void UpdateWallsProgress(int i) => _progress[i] = (_wallDone[i] ? 1 : 0) + (_dashWinDone[i] ? 1 : 0);

        private int HumanIndex(FakeBladeController blade)
        {
            if (blade == null) return -1;
            for (int i = 0; i < _humans.Count; i++)
                if (_humans[i].Blade == blade) return i;
            return -1;
        }
        #endregion

        #region Botones
        /// <summary>El botón del paso en el dispositivo de ese jugador (teclas o botones reasignados incluidos).</summary>
        private static string ButtonLabel(PlayerSetup setup, TutorialButton button)
        {
            if (setup == null || button == TutorialButton.None) return string.Empty;
            InputDeviceKind device = setup.Device;
            switch (button)
            {
                case TutorialButton.Move: return MoveLabel(device);
                case TutorialButton.Dash: return ActionLabel(device, BindingAction.Dash);
                case TutorialButton.Attack: return ActionLabel(device, BindingAction.Attack);
                case TutorialButton.HoldAttack: return Loc.Format("TUT_HOLD", ActionLabel(device, BindingAction.Attack));
                case TutorialButton.AttackAndDash:
                    return ActionLabel(device, BindingAction.Attack) + " + " + ActionLabel(device, BindingAction.Dash);
                case TutorialButton.Special: return ActionLabel(device, BindingAction.Special);
                default: return string.Empty;
            }
        }

        private static string ActionLabel(InputDeviceKind device, BindingAction action)
        {
            if (device == InputDeviceKind.Gamepad) return InputBindings.ButtonName(InputBindings.Pad.Get(action));
            return InputBindings.KeyName(InputBindings.GetKeyboard(device).Get(action));
        }

        private static string MoveLabel(InputDeviceKind device)
        {
            if (device == InputDeviceKind.Gamepad) return Loc.Get("TUT_MOVE_STICK");
            KeyboardScheme keys = InputBindings.GetKeyboard(device);
            if (keys.up == UnityEngine.InputSystem.Key.UpArrow && keys.left == UnityEngine.InputSystem.Key.LeftArrow &&
                keys.down == UnityEngine.InputSystem.Key.DownArrow && keys.right == UnityEngine.InputSystem.Key.RightArrow)
                return Loc.Get("TUT_MOVE_ARROWS");
            return InputBindings.KeyName(keys.up) + InputBindings.KeyName(keys.left) +
                   InputBindings.KeyName(keys.down) + InputBindings.KeyName(keys.right);
        }
        #endregion
    }
}
