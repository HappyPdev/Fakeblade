using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


namespace FakeBlade.Core
{
    /// <summary>Resultado de una partida.</summary>
    public sealed class MatchResult
    {
        public readonly List<PlayerController> Winners = new List<PlayerController>(4);
        public bool IsDraw;
        /// <summary>Equipo ganador (-1 si no es por equipos o empate).</summary>
        public int WinningTeam = -1;
        public bool EndedByTime;
        public float Duration;
    }

    /// <summary>
    /// Flujo de partida (GDD 6 y 9.2): registro de jugadores, cuenta atrás, reglas
    /// (vidas, tiempo, puntos, equipos), K.O. y reaparición, pausa y resultado.
    ///
    /// Es de escena (no persiste entre escenas). Los menús le pasan la configuración
    /// con SetRules antes de BeginMatch.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        private static GameManager _instance;

        /// <summary>
        /// Instancia de la escena (se asigna en Awake, que se ejecuta antes que el resto gracias
        /// a DefaultExecutionOrder). Null en escenas sin partida, como el menú principal.
        /// No se busca en escena para no pagar un Find por frame cuando no existe.
        /// </summary>
        public static GameManager Instance => _instance;

        public static bool HasInstance => _instance != null;
        #endregion

        #region Enums
        public enum GameState
        {
            MainMenu,
            Lobby,
            Countdown,
            InMatch,
            Paused,
            Resuming,
            MatchEnd
        }
        #endregion

        #region Events
        public event Action<GameState, GameState> OnStateChanged;
        public event Action<PlayerController> OnPlayerRegistered;
        public event Action<PlayerController> OnPlayerUnregistered;
        /// <summary>Partida preparada: jugadores colocados y reseteados (el HUD se construye aquí).</summary>
        public event Action<IReadOnlyList<PlayerController>> OnMatchPrepared;
        /// <summary>Segundos restantes de la cuenta atrás. 0 = ¡ya!</summary>
        public event Action<int> OnCountdownTick;
        /// <summary>K.O.: (víctima, quien lo provocó o null).</summary>
        public event Action<PlayerController, PlayerController> OnPlayerKO;
        public event Action<PlayerController> OnPlayerRespawned;
        public event Action<PlayerController> OnPlayerEliminated;
        public event Action<bool> OnPauseChanged;
        public event Action<MatchResult> OnMatchEnded;
        #endregion

        #region Serialized Fields
        [Header("Configuración")]
        [Tooltip("Reglas de la partida. Vacío = Último en pie sin tiempo")]
        [SerializeField] private MatchRules rules;
        [Tooltip("Ajuste global de combate. Vacío = valores por defecto")]
        [SerializeField] private CombatConfig combatConfig;

        [Header("Flujo")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private int countdownSeconds = 3;
        [Tooltip("Cuenta atrás corta al reanudar tras la pausa")]
        [SerializeField] private int resumeCountdownSeconds = 2;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private GameState _state = GameState.MainMenu;
        private readonly List<PlayerController> _players = new List<PlayerController>(4);
        private readonly List<RespawnEntry> _respawnQueue = new List<RespawnEntry>(4);
        private readonly MatchResult _lastResult = new MatchResult();

        private float _matchTime;
        private float _countdownTimer;
        private int _lastCountdownTick;

        private struct RespawnEntry
        {
            public PlayerController Player;
            public float TimeLeft;
        }
        #endregion

        #region Properties
        public GameState CurrentState => _state;
        public MatchRules Rules => rules;
        public IReadOnlyList<PlayerController> Players => _players;
        public int PlayerCount => _players.Count;
        public float MatchTime => _matchTime;
        /// <summary>Segundos restantes o -1 si no hay límite.</summary>
        public float RemainingTime => rules.HasTimeLimit ? Mathf.Max(0f, rules.timeLimit - _matchTime) : -1f;
        public bool IsMatchActive => _state == GameState.InMatch;
        public bool IsPaused => _state == GameState.Paused;
        public MatchResult LastResult => _lastResult;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (rules == null) rules = MatchRules.CreateDefault();
            CombatConfig.SetActive(combatConfig);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            switch (_state)
            {
                case GameState.Countdown:
                case GameState.Resuming:
                    UpdateCountdown();
                    break;

                case GameState.InMatch:
                    UpdateMatch(Time.deltaTime);
                    PollPauseInput();
                    break;

                case GameState.Paused:
                    PollPauseInput();
                    break;
            }
        }
        #endregion

        #region State
        public void ChangeState(GameState newState)
        {
            if (_state == newState) return;

            GameState previous = _state;
            _state = newState;

            bool simulate = newState == GameState.InMatch;
            for (int i = 0; i < _players.Count; i++)
                _players[i].Blade.SetSimulationActive(simulate);

            OnStateChanged?.Invoke(previous, newState);

            if (debugMode) Debug.Log($"[GameManager] {previous} → {newState}", this);
        }
        #endregion

        #region Players
        public bool RegisterPlayer(PlayerController player)
        {
            if (player == null || _players.Contains(player)) return false;

            if (_players.Count >= maxPlayers)
            {
                Debug.LogWarning($"[GameManager] Máximo de jugadores ({maxPlayers}) alcanzado", this);
                return false;
            }

            _players.Add(player);
            _players.Sort((a, b) => a.PlayerID.CompareTo(b.PlayerID));
            player.OnPlayerDefeated += HandlePlayerDefeated;
            player.Blade.SetSimulationActive(_state == GameState.InMatch);

            OnPlayerRegistered?.Invoke(player);
            if (debugMode) Debug.Log($"[GameManager] P{player.PlayerID} registrado ({_players.Count})", this);
            return true;
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (player == null || !_players.Remove(player)) return;

            player.OnPlayerDefeated -= HandlePlayerDefeated;
            RemoveFromRespawnQueue(player);
            OnPlayerUnregistered?.Invoke(player);

            if (_state == GameState.InMatch) CheckWinCondition();
        }

        public void SetSpawnPoints(Transform[] points) => spawnPoints = points;

        public Transform GetSpawnPoint(int playerIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            return spawnPoints[Mathf.Abs(playerIndex) % spawnPoints.Length];
        }
        #endregion

        #region Match Flow
        public void SetRules(MatchRules newRules)
        {
            if (newRules != null) rules = newRules;
        }

        /// <summary>
        /// Prepara y arranca una partida con los jugadores registrados:
        /// resetea peonzas y marcadores, las coloca en sus spawns y lanza la cuenta atrás.
        /// </summary>
        public void BeginMatch()
        {
            Time.timeScale = 1f;
            _matchTime = 0f;
            _respawnQueue.Clear();

            int startingLives = rules.StartingLives;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerController player = _players[i];
                player.Blade.ResetFakeBlade();
                player.SetSpawnPosition(GetSpawnPoint(i));
                player.ResetMatchState(startingLives);
            }

            ChangeState(GameState.Lobby);
            OnMatchPrepared?.Invoke(_players);
            StartCountdown(countdownSeconds, GameState.Countdown);
        }

        public void RestartMatch() => BeginMatch();

        private void StartCountdown(int seconds, GameState countdownState)
        {
            _countdownTimer = Mathf.Max(0, seconds);
            _lastCountdownTick = int.MaxValue;
            ChangeState(countdownState);
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            _countdownTimer -= Time.unscaledDeltaTime;

            int tick = Mathf.Max(0, Mathf.CeilToInt(_countdownTimer));
            if (tick != _lastCountdownTick)
            {
                _lastCountdownTick = tick;
                OnCountdownTick?.Invoke(tick);
            }

            if (_countdownTimer <= 0f)
            {
                Time.timeScale = 1f;
                ChangeState(GameState.InMatch);
            }
        }

        private void UpdateMatch(float dt)
        {
            _matchTime += dt;

            for (int i = _respawnQueue.Count - 1; i >= 0; i--)
            {
                RespawnEntry entry = _respawnQueue[i];
                entry.TimeLeft -= dt;
                if (entry.TimeLeft > 0f)
                {
                    _respawnQueue[i] = entry;
                    continue;
                }

                _respawnQueue.RemoveAt(i);
                Respawn(entry.Player);
            }

            if (rules.HasTimeLimit && _matchTime >= rules.timeLimit)
                EndByTimeout();
        }

        private void HandlePlayerDefeated(int playerId)
        {
            if (_state != GameState.InMatch) return;

            PlayerController victim = FindPlayer(playerId);
            if (victim == null) return;

            PlayerController killer = ResolveKiller(victim);
            if (killer != null && rules.winCondition == WinCondition.Points)
                killer.AddScore(1);

            OnPlayerKO?.Invoke(victim, killer);

            bool eliminated;
            switch (rules.winCondition)
            {
                case WinCondition.Points:
                case WinCondition.Practice:
                    eliminated = false;
                    break;
                case WinCondition.Stocks:
                    victim.LoseLife();
                    eliminated = victim.Lives <= 0;
                    break;
                default:
                    victim.LoseLife();
                    eliminated = true;
                    break;
            }

            if (eliminated)
            {
                victim.SetEliminated(true);
                OnPlayerEliminated?.Invoke(victim);
            }
            else
            {
                _respawnQueue.Add(new RespawnEntry { Player = victim, TimeLeft = rules.respawnDelay });
            }

            if (debugMode)
                Debug.Log($"[GameManager] K.O. P{victim.PlayerID} por {(killer != null ? "P" + killer.PlayerID : "nadie")} " +
                          $"(vidas {victim.Lives}, eliminado {eliminated})", this);

            CheckWinCondition();
        }

        private PlayerController ResolveKiller(PlayerController victim)
        {
            FakeBladeController source = victim.Blade.LastDamageSource;
            if (source == null || source.Owner == null || source.Owner == victim) return null;
            if (Time.time - victim.Blade.LastDamageTime > CombatConfig.Active.killCreditWindow) return null;
            if (rules.teams && source.Owner.TeamID == victim.TeamID) return null;
            return source.Owner;
        }

        private void Respawn(PlayerController player)
        {
            if (player == null || player.IsEliminated) return;

            int index = _players.IndexOf(player);
            player.Blade.ResetFakeBlade();
            player.SetSpawnPosition(GetSpawnPoint(index));
            player.Blade.SetSimulationActive(_state == GameState.InMatch);
            player.Blade.SetInvulnerable(rules.respawnInvulnerability);
            VfxSystem.Play(VfxType.Respawn, player.Blade.Position, Vector3.up, player.PlayerColor);

            OnPlayerRespawned?.Invoke(player);
        }

        private void RemoveFromRespawnQueue(PlayerController player)
        {
            for (int i = _respawnQueue.Count - 1; i >= 0; i--)
                if (_respawnQueue[i].Player == player) _respawnQueue.RemoveAt(i);
        }

        private PlayerController FindPlayer(int playerId)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].PlayerID == playerId) return _players[i];
            return null;
        }
        #endregion

        #region Win Conditions
        private void CheckWinCondition()
        {
            if (_state != GameState.InMatch || _players.Count == 0) return;
            if (rules.IsPractice) return; // la práctica no termina sola

            if (rules.winCondition == WinCondition.Points)
            {
                if (rules.pointsToWin > 0)
                {
                    for (int i = 0; i < _players.Count; i++)
                    {
                        if (SideScore(_players[i]) >= rules.pointsToWin)
                        {
                            EndMatch(false);
                            return;
                        }
                    }
                }
                return;
            }

            // Last Standing / Stocks: termina cuando queda un bando (o ninguno)
            int remainingSide = int.MinValue;
            int sidesAlive = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerController p = _players[i];
                if (p.IsEliminated) continue;

                int side = SideKey(p);
                if (side == remainingSide) continue;
                remainingSide = side;
                sidesAlive++;
                if (sidesAlive > 1) return;
            }

            // Si solo había un bando desde el principio (pruebas en solitario) no se termina
            if (sidesAlive == 1 && CountSides() <= 1) return;

            EndMatch(false);
        }

        private void EndByTimeout()
        {
            EndMatch(true);
        }

        /// <summary>
        /// Calcula ganadores. Criterio: puntos (modo Points) o vidas, y como desempate el % de RPM.
        /// </summary>
        private void EndMatch(bool byTime)
        {
            if (_state == GameState.MatchEnd) return;

            _lastResult.Winners.Clear();
            _lastResult.IsDraw = false;
            _lastResult.WinningTeam = -1;
            _lastResult.EndedByTime = byTime;
            _lastResult.Duration = _matchTime;

            int bestSide = int.MinValue;
            float bestPrimary = float.MinValue;
            float bestSecondary = float.MinValue;
            bool tie = false;

            // Evalúa cada bando (jugador o equipo) una sola vez
            for (int i = 0; i < _players.Count; i++)
            {
                int side = SideKey(_players[i]);
                if (IsSideEvaluatedBefore(i, side)) continue;

                SideRanking(side, out float primary, out float secondary);

                if (primary > bestPrimary || (Mathf.Approximately(primary, bestPrimary) && secondary > bestSecondary + 0.0001f))
                {
                    bestSide = side;
                    bestPrimary = primary;
                    bestSecondary = secondary;
                    tie = false;
                }
                else if (Mathf.Approximately(primary, bestPrimary) && Mathf.Abs(secondary - bestSecondary) <= 0.0001f)
                {
                    tie = true;
                }
            }

            _lastResult.IsDraw = tie || bestSide == int.MinValue;
            if (!_lastResult.IsDraw)
            {
                for (int i = 0; i < _players.Count; i++)
                    if (SideKey(_players[i]) == bestSide) _lastResult.Winners.Add(_players[i]);

                if (rules.teams) _lastResult.WinningTeam = bestSide;
            }

            _respawnQueue.Clear();
            ChangeState(GameState.MatchEnd);
            OnMatchEnded?.Invoke(_lastResult);

            if (debugMode)
                Debug.Log($"[GameManager] Fin de partida ({(byTime ? "tiempo" : "condición")}). " +
                          $"Empate:{_lastResult.IsDraw} Ganadores:{_lastResult.Winners.Count}", this);
        }

        private void SideRanking(int side, out float primary, out float secondary)
        {
            primary = 0f;
            secondary = 0f;
            bool anyAlive = false;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerController p = _players[i];
                if (SideKey(p) != side) continue;

                if (rules.winCondition == WinCondition.Points)
                    primary += p.Score;
                else if (!p.IsEliminated)
                    primary += Mathf.Max(1, p.Lives);

                if (!p.IsEliminated && p.IsAlive)
                {
                    secondary += p.SpinPercentage;
                    anyAlive = true;
                }
            }

            // En Last Standing/Stocks un bando eliminado nunca gana
            if (rules.winCondition != WinCondition.Points && !anyAlive && primary <= 0f)
                primary = -1f;
        }

        private bool IsSideEvaluatedBefore(int index, int side)
        {
            for (int j = 0; j < index; j++)
                if (SideKey(_players[j]) == side) return true;
            return false;
        }

        private int SideKey(PlayerController p) => rules.teams ? p.TeamID : p.PlayerID;

        private int SideScore(PlayerController p)
        {
            if (!rules.teams) return p.Score;
            int total = 0;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].TeamID == p.TeamID) total += _players[i].Score;
            return total;
        }

        private int CountSides()
        {
            int count = 0;
            for (int i = 0; i < _players.Count; i++)
                if (!IsSideEvaluatedBefore(i, SideKey(_players[i]))) count++;
            return count;
        }
        #endregion

        #region Pause
        /// <summary>Cualquier jugador puede pausar: Esc o Start de cualquier mando (GDD 9.2).</summary>
        private void PollPauseInput()
        {
            // Con un submenú abierto (opciones, controles) Esc/Start los gestiona el submenú
            if (MenuStack.IsSubmenuOpen) return;

            bool pressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

            if (!pressed)
            {
                var pads = Gamepad.all;
                for (int i = 0; i < pads.Count; i++)
                {
                    if (pads[i].startButton.wasPressedThisFrame)
                    {
                        pressed = true;
                        break;
                    }
                }
            }

            if (pressed) TogglePause();
        }

        public void TogglePause()
        {
            if (_state == GameState.InMatch) PauseGame();
            else if (_state == GameState.Paused) ResumeGame();
        }

        public void PauseGame()
        {
            if (_state != GameState.InMatch) return;

            Time.timeScale = 0f;
            ChangeState(GameState.Paused);
            OnPauseChanged?.Invoke(true);
        }

        /// <summary>Reanuda con una cuenta atrás corta para que todos empiecen igual.</summary>
        public void ResumeGame()
        {
            if (_state != GameState.Paused) return;

            OnPauseChanged?.Invoke(false);
            StartCountdown(resumeCountdownSeconds, GameState.Resuming);
        }

        /// <summary>Sale al menú principal (la configuración de jugadores se descarta).</summary>
        public void ReturnToMenu()
        {
            if (SceneFlow.CanLoad(SceneFlow.MainMenu))
            {
                MatchSetup.Clear();
                ChangeState(GameState.MainMenu);
                SceneFlow.Load(SceneFlow.MainMenu);
                return;
            }

            Debug.LogWarning($"[GameManager] La escena '{SceneFlow.MainMenu}' no está en Build Settings. Se reinicia la partida.", this);
            RestartMatch();
        }

        /// <summary>"Cambiar peonzas": vuelve a la selección manteniendo jugadores y montaje.</summary>
        public void ReturnToLobby()
        {
            if (SceneFlow.CanLoad(SceneFlow.Lobby))
            {
                ChangeState(GameState.Lobby);
                SceneFlow.Load(SceneFlow.Lobby);
                return;
            }

            Debug.LogWarning($"[GameManager] La escena '{SceneFlow.Lobby}' no está en Build Settings. Se reinicia la partida.", this);
            RestartMatch();
        }
        #endregion
    }
}
