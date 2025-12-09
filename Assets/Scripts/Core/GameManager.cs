using UnityEngine;
using System.Collections.Generic;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Gestor principal del juego. Controla el flujo de estados y jugadores activos.
    /// Patrón Singleton para acceso global.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        public static GameManager Instance { get; private set; }
        #endregion

        #region Events
        public static event Action<GameState> OnGameStateChanged;
        public static event Action<PlayerController> OnPlayerEliminated;
        public static event Action<PlayerController> OnMatchWinner;
        public static event Action OnMatchStarted;
        public static event Action OnMatchEnded;
        #endregion

        #region Serialized Fields
        [Header("Game Settings")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float matchDuration = 180f;
        [SerializeField] private bool enableTimeLimit = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private GameState currentState = GameState.MainMenu;
        private readonly List<PlayerController> activePlayers = new List<PlayerController>();
        private readonly List<PlayerController> eliminatedPlayers = new List<PlayerController>();
        private float matchTimer;
        #endregion

        #region Properties
        public GameState CurrentState => currentState;
        public int ActivePlayerCount => activePlayers.Count;
        public float MatchTimeRemaining => matchTimer;
        public IReadOnlyList<PlayerController> ActivePlayers => activePlayers.AsReadOnly();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeSingleton();
        }

        private void Update()
        {
            if (currentState == GameState.InMatch)
            {
                UpdateMatch();
            }
        }

        private void OnDestroy()
        {
            // Limpiar eventos para evitar memory leaks
            OnGameStateChanged = null;
            OnPlayerEliminated = null;
            OnMatchWinner = null;
            OnMatchStarted = null;
            OnMatchEnded = null;
        }
        #endregion

        #region Initialization
        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (debugMode)
                Debug.Log("[GameManager] Initialized");
        }
        #endregion

        #region Player Management
        public bool RegisterPlayer(PlayerController player)
        {
            if (player == null)
            {
                Debug.LogError("[GameManager] Attempted to register null player");
                return false;
            }

            if (activePlayers.Count >= maxPlayers)
            {
                Debug.LogWarning($"[GameManager] Max players ({maxPlayers}) reached");
                return false;
            }

            if (activePlayers.Contains(player))
            {
                Debug.LogWarning($"[GameManager] Player {player.PlayerID} already registered");
                return false;
            }

            activePlayers.Add(player);

            if (debugMode)
                Debug.Log($"[GameManager] Player {player.PlayerID} registered. Total: {activePlayers.Count}");

            return true;
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (activePlayers.Remove(player))
            {
                eliminatedPlayers.Add(player);
                OnPlayerEliminated?.Invoke(player);

                if (debugMode)
                    Debug.Log($"[GameManager] Player {player.PlayerID} unregistered");
            }
        }
        #endregion

        #region Match Control
        public void StartMatch()
        {
            if (activePlayers.Count < 2)
            {
                Debug.LogWarning("[GameManager] Need at least 2 players to start match");
                return;
            }

            ChangeState(GameState.InMatch);
            matchTimer = matchDuration;
            eliminatedPlayers.Clear();

            // Reset all players
            foreach (var player in activePlayers)
            {
                var fakeBladeController = player.GetComponent<FakeBladeController>();
                if (fakeBladeController != null)
                {
                    fakeBladeController.ResetFakeBlade();
                }
            }

            OnMatchStarted?.Invoke();

            if (debugMode)
                Debug.Log($"[GameManager] Match started with {activePlayers.Count} players");
        }

        private void UpdateMatch()
        {
            if (enableTimeLimit)
            {
                matchTimer -= Time.deltaTime;
                if (matchTimer <= 0f)
                {
                    EndMatchByTime();
                }
            }

            CheckVictoryConditions();
        }

        private void CheckVictoryConditions()
        {
            int playersAlive = 0;
            PlayerController lastStanding = null;

            foreach (var player in activePlayers)
            {
                if (player.IsAlive)
                {
                    playersAlive++;
                    lastStanding = player;
                }
            }

            // Victoria por último en pie
            if (playersAlive == 1 && activePlayers.Count > 1)
            {
                EndMatch(lastStanding);
            }
            // Empate (todos eliminados simultáneamente)
            else if (playersAlive == 0)
            {
                EndMatch(null);
            }
        }

        private void EndMatch(PlayerController winner)
        {
            ChangeState(GameState.PostMatch);

            if (winner != null)
            {
                OnMatchWinner?.Invoke(winner);
                if (debugMode)
                    Debug.Log($"[GameManager] Match won by Player {winner.PlayerID}");
            }
            else
            {
                if (debugMode)
                    Debug.Log("[GameManager] Match ended in a draw");
            }

            OnMatchEnded?.Invoke();
        }

        private void EndMatchByTime()
        {
            // Buscar al jugador con más spin restante
            PlayerController winner = null;
            float highestSpin = 0f;

            foreach (var player in activePlayers)
            {
                if (!player.IsAlive) continue;

                var fakeBladeController = player.GetComponent<FakeBladeController>();
                if (fakeBladeController != null)
                {
                    float currentSpin = fakeBladeController.SpinSpeedPercentage;
                    if (currentSpin > highestSpin)
                    {
                        highestSpin = currentSpin;
                        winner = player;
                    }
                }
            }

            EndMatch(winner);
        }
        #endregion

        #region State Management
        private void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnGameStateChanged?.Invoke(newState);

            if (debugMode)
                Debug.Log($"[GameManager] State changed to: {newState}");
        }

        public void SetState(GameState newState)
        {
            ChangeState(newState);
        }
        #endregion

        #region Enums
        public enum GameState
        {
            MainMenu,
            Assembly,      // Selección de partes
            PreMatch,      // Countdown
            InMatch,       // Combate activo
            PostMatch      // Resultados
        }
        #endregion
    }
}