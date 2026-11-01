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
    ///
    /// P7: en el paso del especial la esfera se da llena (también al reaparecer sin haberlo usado); en el combate el
    /// dummy pasa a ser una IA Fácil Balanceada y el paso se cumple cuando un jugador la deja K.O.; en el final la IA
    /// sigue para jugar libre. Al empezar cada paso se rellenan las RPM de todas las peonzas.
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
        /// <summary>La IA Fácil del combate (null hasta su paso).</summary>
        private AIBrain _fightBrain;
        private bool _dummyFighting;
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
        /// <summary>Ajustes de debug del sandbox del jugador (se restauran al salir).</summary>
        private bool[] _savedDebug;

        private TutorialStep _step;
        private float _advanceTimer = -1f;

        /// <summary>El tutorial en marcha (null fuera de él): la pausa enseña la suya (MatchMenuUI, P8).</summary>
        public static TutorialController Current { get; private set; }

        public PlayerController Dummy => _dummy;
        public TutorialDummyBrain DummyBrain => _dummyBrain;
        public TutorialStep Step => _step;
        /// <summary>En el final (juego libre).</summary>
        public bool IsFinished => _step == TutorialStep.Done;

        private void Awake()
        {
            _bootstrap = GetComponent<BattleBootstrap>();
            Current = this;
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

            // Anillo de la ventana de parry (el debug del sandbox) en los pasos del parry y del bloqueo. Los demás
            // ajustes de debug se apagan aquí y vuelven como los tenía el jugador al salir
            _savedDebug = new[]
            {
                SandboxSettings.ShowParryWindow, SandboxSettings.ShowVelocity, SandboxSettings.ShowDamageNumbers,
                SandboxSettings.ShowStatus, SandboxSettings.ShowFps, SandboxSettings.ShowEventLog
            };
            SandboxSettings.ShowParryWindow = SandboxSettings.ShowVelocity = SandboxSettings.ShowDamageNumbers =
                SandboxSettings.ShowStatus = SandboxSettings.ShowFps = SandboxSettings.ShowEventLog = false;
            SandboxDebugView.Create(transform, theme, HintCanvasOrder - 10);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared += HandleMatchPrepared;
                gm.OnPlayerRespawned += HandleRespawned;
                gm.OnPlayerKO += HandleKO;
            }
            Loc.OnLanguageChanged += ShowStep;
            InputBindings.OnChanged += ShowStep;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnMatchPrepared -= HandleMatchPrepared;
                gm.OnPlayerRespawned -= HandleRespawned;
                gm.OnPlayerKO -= HandleKO;
            }
            Loc.OnLanguageChanged -= ShowStep;
            InputBindings.OnChanged -= ShowStep;
            CollisionResolver.OnClashResolved -= HandleClash;
            CollisionResolver.OnParry -= HandleParry;
            CollisionResolver.OnBlock -= HandleBlock;

            if (_savedDebug != null)
            {
                SandboxSettings.ShowParryWindow = _savedDebug[0];
                SandboxSettings.ShowVelocity = _savedDebug[1];
                SandboxSettings.ShowDamageNumbers = _savedDebug[2];
                SandboxSettings.ShowStatus = _savedDebug[3];
                SandboxSettings.ShowFps = _savedDebug[4];
                SandboxSettings.ShowEventLog = _savedDebug[5];
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
        private void HandleMatchPrepared(IReadOnlyList<PlayerController> players)
        {
            if (_dummy != null) _dummy.SetSpawnPosition(_home);
            if (_humans.Count == 0) SetupPlayers(players);
        }

        /// <summary>
        /// El dummy reaparece en el centro. En el paso del especial, quien cae sin haberlo usado vuelve con la esfera
        /// llena (al reaparecer se vacía).
        /// </summary>
        private void HandleRespawned(PlayerController player)
        {
            if (player == _dummy)
            {
                _dummy.SetSpawnPosition(_home);
                return;
            }
            int i = HumanIndex(player.Blade);
            if (_step == TutorialStep.Special && i >= 0 && _progress[i] < TutorialSteps.Goal(_step))
                player.Blade.Special.FillEnergy();
        }

        /// <summary>Combate: un jugador deja K.O. a la IA y el paso se cumple para todos (ganarle una vez, GDD 6.7).</summary>
        private void HandleKO(PlayerController victim, PlayerController killer)
        {
            if (_step != TutorialStep.Fight || victim != _dummy || killer == null || _advanceTimer >= 0f) return;
            if (HumanIndex(killer.Blade) < 0) return;
            int goal = TutorialSteps.Goal(_step);
            for (int i = 0; i < _progress.Length; i++) _progress[i] = goal;
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
                player.SetInputFilter(gate);

                int index = i;
                player.Blade.OnDashExecuted += () => OnDash(index);
                player.Blade.OnAttackLaunched += level => _attackSerial[index]++;
                FakeBladeController blade = player.Blade;
                blade.OnWallHit += intensity => OnWallHit(blade);
                blade.OnSpecialActivated += type => OnSpecial(index);
            }

            // Paredes: vale empujar al dummy o a otro jugador
            FakeBladeController dummyBlade = _dummy.Blade;
            dummyBlade.OnWallHit += intensity => OnWallHit(dummyBlade);
            CollisionResolver.OnClashResolved += HandleClash;
            CollisionResolver.OnParry += HandleParry;
            CollisionResolver.OnBlock += HandleBlock;

            EnterStep(TutorialStep.Move);
        }

        private static PlayerSetup FindSetup(int playerId)
        {
            for (int i = 0; i < MatchSetup.Players.Count; i++)
                if (MatchSetup.Players[i].PlayerIndex == playerId) return MatchSetup.Players[i];
            return null;
        }
        #endregion

        #region Pausa (P8)
        /// <summary>Vuelve a empezar el paso actual (contadores a cero y RPM llenas).</summary>
        public void RepeatStep()
        {
            if (_humans.Count > 0) EnterStep(_step);
        }

        public void SkipStep()
        {
            if (_humans.Count > 0 && _step < TutorialStep.Done) EnterStep(_step + 1);
        }

        /// <summary>Desde el final: todo el tutorial otra vez, desde el paso 1.</summary>
        public void RestartTutorial()
        {
            if (_humans.Count > 0) EnterStep(TutorialStep.Move);
        }

        /// <summary>
        /// Tras cambiar las peonzas en la pausa (ChangeBladeScreen): un núcleo nuevo vacía la esfera, así que en el paso
        /// del especial se vuelve a llenar a quien aún no lo ha usado; y el color nuevo, en la ventana de indicaciones.
        /// </summary>
        public void HandleBladesChanged()
        {
            if (_step == TutorialStep.Special)
            {
                int goal = TutorialSteps.Goal(_step);
                for (int i = 0; i < _humans.Count; i++)
                    if (_progress[i] < goal) _humans[i].Blade.Special.FillEnergy();
            }
            ShowStep();
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
            if (_dummyBrain != null)
            {
                _dummyBrain.ReturnHome = step != TutorialStep.WallsAndDash;
                _dummyBrain.SetMode(step == TutorialStep.Parry ? TutorialDummyMode.QuickAttacks
                    : step == TutorialStep.Block ? TutorialDummyMode.ChargedAttacks
                    : TutorialDummyMode.Idle);
                SetDummyFighting(step >= TutorialStep.Fight);
            }
            SandboxSettings.ShowParryWindow = step == TutorialStep.Parry || step == TutorialStep.Block;

            // Cada paso empieza con las RPM llenas (§12, dificultad del bloqueo); en el del especial, con la esfera llena
            for (int i = 0; i < _humans.Count; i++)
            {
                FakeBladeController blade = _humans[i].Blade;
                blade.AddSpin(blade.MaxSpinSpeed - blade.CurrentSpinSpeed);
                if (step == TutorialStep.Special) blade.Special.FillEnergy();
            }
            if (_dummy != null) _dummy.Blade.AddSpin(_dummy.Blade.MaxSpinSpeed - _dummy.Blade.CurrentSpinSpeed);

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
            if (_step == TutorialStep.Parry || _step == TutorialStep.Block) PickDummyTarget();

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

        private void OnSpecial(int index)
        {
            if (_step == TutorialStep.Special && _advanceTimer < 0f && index < _progress.Length) _progress[index]++;
        }

        /// <summary>
        /// Desde el combate el dummy lo juega una IA Fácil Balanceada, con todo activo; antes, su TutorialDummyBrain.
        /// Solo al cambiar (AIProfiles.Ensure no se llama cada frame).
        /// </summary>
        private void SetDummyFighting(bool fight)
        {
            if (_dummy == null || fight == _dummyFighting) return;
            _dummyFighting = fight;
            _dummyBrain.ClearBuffers();
            _dummyBrain.enabled = !fight;
            if (fight)
            {
                if (_fightBrain == null) _fightBrain = AIProfiles.Ensure(_dummy.gameObject, AIProfiles.ForStyle(AIStyle.Balanced));
                _fightBrain.Difficulty = AIDifficulty.Easy;
                _fightBrain.ClearBuffers();
                _fightBrain.enabled = true;
                _dummy.SetInputSource(_fightBrain);
            }
            else
            {
                if (_fightBrain != null)
                {
                    _fightBrain.ClearBuffers();
                    _fightBrain.enabled = false;
                }
                _dummy.SetInputSource(_dummyBrain);
            }
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

        /// <summary>
        /// Parry y bloqueo: el dummy ataca al jugador más cercano que aún no ha cumplido el objetivo (si ya lo han
        /// cumplido todos, al más cercano), para que nadie se quede sin practicar.
        /// </summary>
        private void PickDummyTarget()
        {
            if (_dummyBrain == null) return;
            int goal = TutorialSteps.Goal(_step);
            FakeBladeController best = null, bestAny = null;
            float bestDist = float.MaxValue, bestAnyDist = float.MaxValue;
            Vector3 dummyPos = _dummy.Blade.Position;
            for (int i = 0; i < _humans.Count; i++)
            {
                FakeBladeController blade = _humans[i].Blade;
                if (blade.IsDestroyed) continue;
                float dist = (blade.Position - dummyPos).sqrMagnitude;
                if (dist < bestAnyDist) { bestAnyDist = dist; bestAny = blade; }
                if (_progress[i] < goal && dist < bestDist) { bestDist = dist; best = blade; }
            }
            _dummyBrain.Target = best != null ? best : bestAny;
        }

        /// <summary>Parry (no vale el doble parry): quien para es un jugador.</summary>
        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool doubleParry)
        {
            if (_step != TutorialStep.Parry || doubleParry || _advanceTimer >= 0f) return;
            int i = HumanIndex(parrier);
            if (i >= 0) _progress[i]++;
        }

        /// <summary>Bloqueo: un parry contra un cargado de nivel 2 o más (GDD 2.5).</summary>
        private void HandleBlock(FakeBladeController parrier, FakeBladeController attacker)
        {
            if (_step != TutorialStep.Block || _advanceTimer >= 0f) return;
            int i = HumanIndex(parrier);
            if (i >= 0) _progress[i]++;
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
