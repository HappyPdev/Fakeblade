using UnityEngine;
using System;
using System.Collections.Generic;

namespace FakeBlade.Core
{
    /// <summary>
    /// Gestor principal del juego. Controla estados, jugadores y flujo de partida.
    /// Implementa Singleton thread-safe con lazy initialization.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        private static GameManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static GameManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[GameManager] Instance already destroyed. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType<GameManager>();

                        if (_instance == null)
                        {
                            var go = new GameObject("[GameManager]");
                            _instance = go.AddComponent<GameManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                    return _instance;
                }
            }
        }
        #endregion

        #region Enums
        public enum GameState
        {
            MainMenu,
            CharacterSelect,
            Assembly,
            Countdown,
            InMatch,
            Paused,
            MatchEnd,
            Results
        }

        public enum GameMode
        {
            FreeForAll,
            Teams,
            LastStanding,
            TimedMatch
        }
        #endregion

        #region Events
        public event Action<GameState, GameState> OnStateChanged;
        public event Action<PlayerController> OnPlayerRegistered;
        public event Action<PlayerController> OnPlayerUnregistered;
        public event Action<PlayerController> OnPlayerEliminated;
        public event Action<PlayerController> OnMatchWinner;
        public event Action OnMatchStart;
        public event Action OnMatchEnd;
        public event Action<int> OnCountdownTick;
        #endregion

        #region Serialized Fields
        [Header("Game Settings")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float matchTimeLimit = 180f;
        [SerializeField] private int countdownSeconds = 3;

        [Header("Game Mode")]
        [SerializeField] private GameMode currentMode = GameMode.FreeForAll;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private GameState _currentState = GameState.MainMenu;
        private readonly List<PlayerController> _activePlayers = new List<PlayerController>(4);
        private readonly List<PlayerController> _eliminatedPlayers = new List<PlayerController>(4);
        private float _matchTimer;
        private int _currentCountdown;
        private bool _isInitialized;
        #endregion

        #region Properties
        public GameState CurrentState => _currentState;
        public GameMode CurrentMode => currentMode;
        public IReadOnlyList<PlayerController> ActivePlayers => _activePlayers;
        public IReadOnlyList<PlayerController> EliminatedPlayers => _eliminatedPlayers;
        public int ActivePlayerCount => _activePlayers.Count;
        public float MatchTimer => _matchTimer;
        public float MatchTimeLimit => matchTimeLimit;
        public bool IsMatchActive => _currentState == GameState.InMatch;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeSingleton();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            switch (_currentState)
            {
                case GameState.Countdown:
                    UpdateCountdown();
                    break;
                case GameState.InMatch:
                    UpdateMatch();
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _applicationIsQuitting = true;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
        #endregion

        #region Initialization
        private void InitializeSingleton()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Initialize()
        {
            _activePlayers.Clear();
            _eliminatedPlayers.Clear();
            _isInitialized = true;

            if (debugMode)
            {
                Debug.Log("[GameManager] Initialized successfully");
            }
        }
        #endregion

        #region State Management
        public void ChangeState(GameState newState)
        {
            if (_currentState == newState) return;

            GameState previousState = _currentState;
            _currentState = newState;

            OnStateExit(previousState);
            OnStateEnter(newState);

            OnStateChanged?.Invoke(previousState, newState);

            if (debugMode)
            {
                Debug.Log($"[GameManager] State changed: {previousState} -> {newState}");
            }
        }

        private void OnStateEnter(GameState state)
        {
            switch (state)
            {
                case GameState.Countdown:
                    StartCountdown();
                    break;
                case GameState.InMatch:
                    StartMatch();
                    break;
                case GameState.MatchEnd:
                    EndMatch();
                    break;
            }
        }

        private void OnStateExit(GameState state)
        {
            // Cleanup específico por estado si es necesario
        }
        #endregion

        #region Player Management
        public bool RegisterPlayer(PlayerController player)
        {
            if (player == null)
            {
                Debug.LogWarning("[GameManager] Cannot register null player");
                return false;
            }

            if (_activePlayers.Count >= maxPlayers)
            {
                Debug.LogWarning($"[GameManager] Max players ({maxPlayers}) reached");
                return false;
            }

            if (_activePlayers.Contains(player))
            {
                Debug.LogWarning($"[GameManager] Player {player.PlayerID} already registered");
                return false;
            }

            _activePlayers.Add(player);
            player.OnPlayerDefeated += HandlePlayerDefeated;

            OnPlayerRegistered?.Invoke(player);

            if (debugMode)
            {
                Debug.Log($"[GameManager] Player {player.PlayerID} registered. Total: {_activePlayers.Count}");
            }

            return true;
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (player == null) return;

            player.OnPlayerDefeated -= HandlePlayerDefeated;

            if (_activePlayers.Remove(player))
            {
                OnPlayerUnregistered?.Invoke(player);

                if (debugMode)
                {
                    Debug.Log($"[GameManager] Player {player.PlayerID} unregistered. Remaining: {_activePlayers.Count}");
                }

                // Verificar condición de victoria
                if (_currentState == GameState.InMatch)
                {
                    CheckWinCondition();
                }
            }
        }

        private void HandlePlayerDefeated(int playerId)
        {
            PlayerController player = _activePlayers.Find(p => p.PlayerID == playerId);

            if (player != null)
            {
                _activePlayers.Remove(player);
                _eliminatedPlayers.Add(player);

                OnPlayerEliminated?.Invoke(player);

                if (debugMode)
                {
                    Debug.Log($"[GameManager] Player {playerId} eliminated. Active: {_activePlayers.Count}");
                }

                CheckWinCondition();
            }
        }

        public Transform GetSpawnPoint(int playerIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[GameManager] No spawn points configured");
                return null;
            }

            return spawnPoints[playerIndex % spawnPoints.Length];
        }
        #endregion

        #region Match Flow
        private void StartCountdown()
        {
            _currentCountdown = countdownSeconds;
            OnCountdownTick?.Invoke(_currentCountdown);
        }

        private void UpdateCountdown()
        {
            // Usar InvokeRepeating o Timer en producción
            // Simplificado para demostración
            _currentCountdown--;
            OnCountdownTick?.Invoke(_currentCountdown);

            if (_currentCountdown <= 0)
            {
                ChangeState(GameState.InMatch);
            }
        }

        private void StartMatch()
        {
            _matchTimer = 0f;
            _eliminatedPlayers.Clear();

            OnMatchStart?.Invoke();

            if (debugMode)
            {
                Debug.Log($"[GameManager] Match started with {_activePlayers.Count} players");
            }
        }

        private void UpdateMatch()
        {
            _matchTimer += Time.deltaTime;

            // Verificar límite de tiempo
            if (currentMode == GameMode.TimedMatch && _matchTimer >= matchTimeLimit)
            {
                DetermineTimeoutWinner();
            }
        }

        private void EndMatch()
        {
            OnMatchEnd?.Invoke();

            if (debugMode)
            {
                Debug.Log($"[GameManager] Match ended. Duration: {_matchTimer:F1}s");
            }
        }

        private void CheckWinCondition()
        {
            switch (currentMode)
            {
                case GameMode.FreeForAll:
                case GameMode.LastStanding:
                    if (_activePlayers.Count <= 1)
                    {
                        DeclareWinner(_activePlayers.Count > 0 ? _activePlayers[0] : null);
                    }
                    break;

                case GameMode.Teams:
                    // Implementar lógica de equipos
                    break;
            }
        }

        private void DetermineTimeoutWinner()
        {
            // El jugador con mayor spin gana en timeout
            PlayerController winner = null;
            float highestSpin = 0f;

            foreach (var player in _activePlayers)
            {
                var controller = player.GetComponent<FakeBladeController>();
                if (controller != null && controller.CurrentSpinSpeed > highestSpin)
                {
                    highestSpin = controller.CurrentSpinSpeed;
                    winner = player;
                }
            }

            DeclareWinner(winner);
        }

        private void DeclareWinner(PlayerController winner)
        {
            OnMatchWinner?.Invoke(winner);
            ChangeState(GameState.MatchEnd);

            if (debugMode)
            {
                string winnerName = winner != null ? winner.PlayerName : "No one";
                Debug.Log($"[GameManager] Winner: {winnerName}!");
            }
        }
        #endregion

        #region Public Methods
        public void StartGame()
        {
            if (_activePlayers.Count < minPlayersToStart)
            {
                Debug.LogWarning($"[GameManager] Need at least {minPlayersToStart} players to start");
                return;
            }

            ChangeState(GameState.Countdown);
        }

        public void PauseGame()
        {
            if (_currentState == GameState.InMatch)
            {
                Time.timeScale = 0f;
                ChangeState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (_currentState == GameState.Paused)
            {
                Time.timeScale = 1f;
                ChangeState(GameState.InMatch);
            }
        }

        public void RestartMatch()
        {
            // Reset todos los jugadores
            foreach (var player in _eliminatedPlayers)
            {
                if (player != null)
                {
                    _activePlayers.Add(player);
                }
            }
            _eliminatedPlayers.Clear();

            // Reset FakeBlades
            foreach (var player in _activePlayers)
            {
                var controller = player.GetComponent<FakeBladeController>();
                controller?.ResetFakeBlade();
                player.gameObject.SetActive(true);
            }

            ChangeState(GameState.Countdown);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            ChangeState(GameState.MainMenu);
        }

        public void SetGameMode(GameMode mode)
        {
            currentMode = mode;
        }
        #endregion
    }
}