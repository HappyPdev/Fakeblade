using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Telemetría de las partidas (C19, GDD 6.6): cada partida local (no el sandbox) deja un CSV con todo lo que
    /// sirve para analizar el equilibrio: quién jugaba (humano o IA, nivel, piezas, poder), la arena, las reglas,
    /// el resultado, los sucesos de combate y un resumen por jugador. Anónima; se apaga en Opciones.
    ///
    /// Durante la partida solo guarda números en un búfer reservado de antemano (cero GC por frame) y escribe el
    /// archivo al terminar, al reiniciarla o al salir a medias (resultado «abandoned»).
    ///
    /// Carpeta: en el editor, Logs/Telemetria/; en una build, Telemetria/ junto al ejecutable (si no se puede
    /// escribir ahí, persistentDataPath/Telemetria). En WebGL no se graba.
    ///
    /// Formato (separado por ';', decimales con punto), las mismas columnas que el CSV del sandbox sin «real»:
    ///   t (s de partida, sin pausas) ; event ; player ; other ; v1 ; v2 ; v3 ; v4 ; info
    /// Qué es cada v1-v4 por evento: ver GDD 6.6.
    /// </summary>
    public sealed class MatchTelemetry : MonoBehaviour
    {
        public const string FilePrefix = "partida_";
        private const int Capacity = 32768;
        private const float SnapshotInterval = 1f;
        private const float MinDamage = 0.5f; // el roce continuo no llena el archivo
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private enum Kind : byte
        {
            Clash, Damage, Attack, Dash, SpecialOn, SpecialOff, Status, HealCut, Parry, Block, Wall,
            KO, Eliminated, Respawn, Snapshot
        }

        private struct Entry
        {
            public float T;
            public Kind Kind;
            public sbyte Player, Other;
            /// <summary>Código según el evento: tipo de choque, poder, estado...</summary>
            public byte Code, Code2;
            public float V1, V2, V3, V4;
        }

        /// <summary>Jugador seguido: sus datos fijos, sus contadores y los delegados de sus eventos.</summary>
        private sealed class Side
        {
            public int Index;
            public PlayerController Player;
            public FakeBladeController Blade;
            public string Header;
            public bool IsCpu;
            /// <summary>Último estado visto en partida: al reiniciarla, las peonzas ya están reseteadas al escribir.</summary>
            public float LastSpin;
            public int LastLives, LastScore;

            public readonly int[] Attacks = new int[4];
            public int Dashes, Specials, Clashes, HitsLanded, HitsReceived, EvenClashes, AttackHits, DashHits;
            public int Parries, Parried, DoubleParries, Blocks, Blocked, WallHits, HealCuts;
            public int StatusInflicted, StatusReceived, KOs, Deaths, SelfKOs, Snapshots;
            public float DamageDealt, DamageTakenRival, DamageTakenWall, ClashDamageDealt, ClashDamageTaken;
            public float Healed, SpecialTime, SpecialStart = -1f, AliveTime, EliminatedAt = -1f;
            public float SpeedSum, CenterDistanceSum;

            public Action<float, FakeBladeController> OnDamaged;
            public Action<float> OnHealed;
            public Action<int> OnAttack;
            public Action OnDash;
            public Action<SpecialAbilityType> OnSpecialOn;
            public Action<SpecialAbilityType> OnSpecialOff;
            public Action<StatusEffectType> OnStatus;
            public Action OnHealCut;
            public Action<float> OnWall;

            public void ResetCounters()
            {
                Array.Clear(Attacks, 0, Attacks.Length);
                Dashes = Specials = Clashes = HitsLanded = HitsReceived = EvenClashes = AttackHits = DashHits = 0;
                Parries = Parried = DoubleParries = Blocks = Blocked = WallHits = HealCuts = 0;
                StatusInflicted = StatusReceived = KOs = Deaths = SelfKOs = Snapshots = 0;
                DamageDealt = DamageTakenRival = DamageTakenWall = ClashDamageDealt = ClashDamageTaken = 0f;
                Healed = SpecialTime = AliveTime = SpeedSum = CenterDistanceSum = 0f;
                SpecialStart = EliminatedAt = -1f;
            }
        }

        // Búfer compartido entre partidas y escenas: se reserva una sola vez
        private static Entry[] _entries;
        private int _count;
        private int _dropped;

        private readonly List<Side> _sides = new List<Side>(4);
        private GameManager _gm;
        private FakeBladeCatalog _catalog;
        private ArenaDefinition _arena;
        private bool _recording;
        private bool _started;
        private float _lastMatchTime;
        private DateTime _startDate;
        private float _nextSnapshot;
        private static int _matchNumber;
        private static string _sessionId;

        /// <summary>Último archivo escrito (para avisos y pruebas).</summary>
        public static string LastPath { get; private set; }

        /// <summary>Carpeta de la telemetría en el editor (la que lee el informe).</summary>
        public static string EditorFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Telemetria"));

        private static string BuildFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Telemetria"));
        private static string FallbackFolder => Path.Combine(Application.persistentDataPath, "Telemetria");

        public static MatchTelemetry Create(Transform parent, FakeBladeCatalog catalog, ArenaDefinition arena)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return null;
#else
            var go = new GameObject("[MatchTelemetry]");
            go.transform.SetParent(parent, false);
            var telemetry = go.AddComponent<MatchTelemetry>();
            telemetry._catalog = catalog;
            telemetry._arena = arena;
            return telemetry;
#endif
        }

        #region Ciclo de vida
        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null)
            {
                enabled = false;
                return;
            }
            if (_entries == null) _entries = new Entry[Capacity];
            if (_sessionId == null) _sessionId = Guid.NewGuid().ToString("N").Substring(0, 6);

            _gm.OnMatchPrepared += HandleMatchPrepared;
            _gm.OnStateChanged += HandleStateChanged;
            _gm.OnPlayerKO += HandleKO;
            _gm.OnPlayerEliminated += HandleEliminated;
            _gm.OnPlayerRespawned += HandleRespawned;
            _gm.OnMatchEnded += HandleMatchEnded;
            CollisionResolver.OnClashResolved += HandleClash;
            CollisionResolver.OnParry += HandleParry;
            CollisionResolver.OnBlock += HandleBlock;
        }

        private void OnDestroy()
        {
            // Salir al menú o a la selección a media partida
            if (_recording && _started) Finish(null);
            StopRecording();

            if (_gm != null)
            {
                _gm.OnMatchPrepared -= HandleMatchPrepared;
                _gm.OnStateChanged -= HandleStateChanged;
                _gm.OnPlayerKO -= HandleKO;
                _gm.OnPlayerEliminated -= HandleEliminated;
                _gm.OnPlayerRespawned -= HandleRespawned;
                _gm.OnMatchEnded -= HandleMatchEnded;
            }
            CollisionResolver.OnClashResolved -= HandleClash;
            CollisionResolver.OnParry -= HandleParry;
            CollisionResolver.OnBlock -= HandleBlock;
        }

        private void OnApplicationQuit()
        {
            if (_recording && _started) Finish(null);
            StopRecording();
        }

        private void Update()
        {
            if (!_recording || _gm.CurrentState != GameManager.GameState.InMatch) return;

            _lastMatchTime = _gm.MatchTime;
            float dt = Time.deltaTime;
            for (int i = 0; i < _sides.Count; i++)
            {
                Side s = _sides[i];
                if (s.Blade != null && !s.Blade.IsDestroyed && !s.Player.IsEliminated) s.AliveTime += dt;
                s.LastSpin = s.Player.SpinPercentage;
                s.LastLives = s.Player.Lives;
                s.LastScore = s.Player.Score;
            }

            if (_gm.MatchTime < _nextSnapshot) return;
            _nextSnapshot = _gm.MatchTime + SnapshotInterval;
            Vector3 center = _arena != null ? _arena.Center : Vector3.zero;
            for (int i = 0; i < _sides.Count; i++) Snapshot(_sides[i], center);
        }
        #endregion

        #region Partida
        private void HandleMatchPrepared(IReadOnlyList<PlayerController> players)
        {
            // Reiniciar a media partida: la anterior se guarda como abandonada
            if (_recording && _started) Finish(null);
            StopRecording();

            if (!SettingsService.Current.telemetry || _gm.Rules == null || _gm.Rules.IsSandbox) return;

            _count = 0;
            _dropped = 0;
            _started = false;
            _nextSnapshot = 0f;
            _lastMatchTime = 0f;
            _startDate = DateTime.Now;
            for (int i = 0; i < players.Count && i < sbyte.MaxValue; i++)
            {
                if (players[i] == null || players[i].Blade == null) continue;
                _sides.Add(Track(players[i], _sides.Count));
            }
            _recording = _sides.Count > 0;
        }

        private void HandleStateChanged(GameManager.GameState previous, GameManager.GameState state)
        {
            if (!_recording || state != GameManager.GameState.InMatch || _started) return;
            _started = true;
            _matchNumber++;
        }

        private void HandleMatchEnded(MatchResult result)
        {
            if (!_recording) return;
            Finish(result);
            StopRecording();
        }

        private void StopRecording()
        {
            for (int i = 0; i < _sides.Count; i++) Untrack(_sides[i]);
            _sides.Clear();
            _recording = false;
            _started = false;
        }
        #endregion

        #region Peonzas
        private Side Track(PlayerController player, int index)
        {
            FakeBladeController blade = player.Blade;
            var s = new Side
            {
                Index = index, Player = player, Blade = blade, IsCpu = player.IsCpu, LastSpin = player.SpinPercentage,
                LastLives = player.Lives, LastScore = player.Score
            };
            s.ResetCounters();
            s.Header = PlayerText(player);

            s.OnDamaged = (amount, source) =>
            {
                Side from = SideOf(source);
                if (from == s) return; // sus propios costes no son daño
                if (from != null)
                {
                    s.DamageTakenRival += amount;
                    from.DamageDealt += amount;
                }
                else s.DamageTakenWall += amount;
                if (amount >= MinDamage)
                    Add(Kind.Damage, s, from, (byte)(from != null ? 1 : 0), amount, blade.SpinSpeedPercentage * 100f, 0f, 0f);
            };
            s.OnHealed = amount => s.Healed += amount;
            s.OnAttack = level =>
            {
                s.Attacks[Mathf.Clamp(level, 0, 3)]++;
                Add(Kind.Attack, s, null, (byte)Mathf.Clamp(level, 0, 3), level, blade.SpinSpeedPercentage * 100f, Speed(blade), 0f);
            };
            s.OnDash = () =>
            {
                s.Dashes++;
                Add(Kind.Dash, s, null, 0, 0f, blade.SpinSpeedPercentage * 100f, Speed(blade), 0f);
            };
            s.OnSpecialOn = type =>
            {
                s.Specials++;
                s.SpecialStart = _gm.MatchTime;
                Add(Kind.SpecialOn, s, null, (byte)type, 0f, blade.SpinSpeedPercentage * 100f, blade.Attack.CurrentCharges, 0f);
            };
            s.OnSpecialOff = type =>
            {
                float duration = 0f;
                if (s.SpecialStart >= 0f)
                {
                    duration = _gm.MatchTime - s.SpecialStart;
                    s.SpecialTime += duration;
                    s.SpecialStart = -1f;
                }
                Add(Kind.SpecialOff, s, null, (byte)type, duration, blade.SpinSpeedPercentage * 100f, 0f, 0f);
            };
            s.OnStatus = status =>
            {
                if (status == StatusEffectType.None) return;
                Side from = SideOf(blade.Status.Source);
                s.StatusReceived++;
                if (from != null && from != s) from.StatusInflicted++;
                Add(Kind.Status, s, from, (byte)status, 0f, blade.SpinSpeedPercentage * 100f, 0f, 0f);
            };
            s.OnHealCut = () =>
            {
                s.HealCuts++;
                Add(Kind.HealCut, s, null, 0, 0f, blade.SpinSpeedPercentage * 100f, 0f, 0f);
            };
            s.OnWall = intensity =>
            {
                s.WallHits++;
                Add(Kind.Wall, s, null, 0, intensity * 15f, blade.SpinSpeedPercentage * 100f, 0f, 0f);
            };

            blade.OnDamaged += s.OnDamaged;
            blade.OnHealed += s.OnHealed;
            blade.OnAttackLaunched += s.OnAttack;
            blade.OnDashExecuted += s.OnDash;
            blade.OnSpecialActivated += s.OnSpecialOn;
            blade.OnSpecialEnded += s.OnSpecialOff;
            blade.OnStatusEffectChanged += s.OnStatus;
            blade.OnHealCut += s.OnHealCut;
            blade.OnWallHit += s.OnWall;
            return s;
        }

        private static void Untrack(Side s)
        {
            if (s.Blade == null) return;
            s.Blade.OnDamaged -= s.OnDamaged;
            s.Blade.OnHealed -= s.OnHealed;
            s.Blade.OnAttackLaunched -= s.OnAttack;
            s.Blade.OnDashExecuted -= s.OnDash;
            s.Blade.OnSpecialActivated -= s.OnSpecialOn;
            s.Blade.OnSpecialEnded -= s.OnSpecialOff;
            s.Blade.OnStatusEffectChanged -= s.OnStatus;
            s.Blade.OnHealCut -= s.OnHealCut;
            s.Blade.OnWallHit -= s.OnWall;
        }

        /// <summary>Jugador de una peonza (el clon de Fantasma cuenta para su dueña). Null si no se sigue.</summary>
        private Side SideOf(FakeBladeController blade)
        {
            if (blade == null) return null;
            if (blade.IsGhost) blade = blade.GhostOwner;
            if (blade == null) return null;
            for (int i = 0; i < _sides.Count; i++)
                if (_sides[i].Blade == blade) return _sides[i];
            return null;
        }

        private Side SideOf(PlayerController player)
        {
            if (player == null) return null;
            for (int i = 0; i < _sides.Count; i++)
                if (_sides[i].Player == player) return _sides[i];
            return null;
        }

        private static float Speed(FakeBladeController blade)
        {
            Vector3 v = blade.Velocity;
            v.y = 0f;
            return v.magnitude;
        }

        private void Snapshot(Side s, Vector3 center)
        {
            FakeBladeController blade = s.Blade;
            if (blade == null || blade.IsDestroyed || s.Player.IsEliminated) return;
            Vector3 offset = blade.Position - center;
            offset.y = 0f;
            float speed = Speed(blade);
            float distance = offset.magnitude;
            s.Snapshots++;
            s.SpeedSum += speed;
            s.CenterDistanceSum += distance;
            Add(Kind.Snapshot, s, null, (byte)(blade.Special.IsActive ? 1 : 0), blade.SpinSpeedPercentage * 100f, speed,
                blade.Special.Energy, distance);
        }
        #endregion

        #region Combate
        private void HandleClash(CollisionResolver.ClashReport r)
        {
            if (!_recording) return;
            Side a = SideOf(r.A), b = SideOf(r.B);
            if (a == null || b == null || a == b) return;

            a.Clashes++;
            b.Clashes++;
            a.ClashDamageTaken += r.DamageToA;
            b.ClashDamageTaken += r.DamageToB;
            a.ClashDamageDealt += r.DamageToB;
            b.ClashDamageDealt += r.DamageToA;

            if (r.Kind == CollisionResolver.ClashKind.Normal || r.Kind == CollisionResolver.ClashKind.Launched)
            {
                // Gana el golpe quien hace más daño del que recibe
                Side winner = r.DamageToB > r.DamageToA ? a : b;
                Side loser = winner == a ? b : a;
                winner.HitsLanded++;
                loser.HitsReceived++;
                bool winnerIsA = winner == a;
                if (winnerIsA ? r.DashA : r.DashB) winner.DashHits++;
                else if (winner.Blade != null && winner.Blade.IsAttacking) winner.AttackHits++;
            }
            else if (r.Kind == CollisionResolver.ClashKind.Even)
            {
                a.EvenClashes++;
                b.EvenClashes++;
            }

            // v1/v2: velocidad de cada una hacia la otra; v3/v4: daño que recibe cada una.
            // Código: tipo de choque y dash de cada una; código 2: nivel de carga de cada una
            byte flags = (byte)((int)r.Kind | (r.DashA ? 16 : 0) | (r.DashB ? 32 : 0));
            byte charges = (byte)(Mathf.Clamp(r.ChargeA, 0, 15) | (Mathf.Clamp(r.ChargeB, 0, 15) << 4));
            Add(Kind.Clash, a, b, flags, r.ApproachA, r.ApproachB, r.DamageToA, r.DamageToB, charges);
        }

        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool isDouble)
        {
            if (!_recording) return;
            Side p = SideOf(parrier), a = SideOf(attacker);
            if (p == null || a == null) return;
            if (isDouble)
            {
                p.DoubleParries++;
                a.DoubleParries++;
            }
            else
            {
                p.Parries++;
                a.Parried++;
            }
            Add(Kind.Parry, p, a, (byte)(isDouble ? 1 : 0), parrier.SpinSpeedPercentage * 100f,
                attacker.SpinSpeedPercentage * 100f, 0f, 0f);
        }

        private void HandleBlock(FakeBladeController blocker, FakeBladeController attacker)
        {
            if (!_recording) return;
            Side b = SideOf(blocker), a = SideOf(attacker);
            if (b == null || a == null) return;
            b.Blocks++;
            a.Blocked++;
            Add(Kind.Block, b, a, (byte)attacker.ChargeLevel, blocker.SpinSpeedPercentage * 100f,
                attacker.SpinSpeedPercentage * 100f, 0f, 0f);
        }

        private void HandleKO(PlayerController victim, PlayerController killer)
        {
            if (!_recording) return;
            Side v = SideOf(victim), k = SideOf(killer);
            if (v == null) return;
            v.Deaths++;
            if (k != null && k != v) k.KOs++;
            else v.SelfKOs++;

            // Código: 1 si el último daño vino de un rival (aunque sea fuera del tiempo que da el punto de K.O.)
            Side last = SideOf(victim.Blade != null ? victim.Blade.LastDamageSource : null);
            Add(Kind.KO, v, k, (byte)(last != null ? 1 : 0), v.Deaths, victim.Lives, 0f, 0f);
        }

        private void HandleEliminated(PlayerController player)
        {
            if (!_recording) return;
            Side s = SideOf(player);
            if (s == null) return;
            s.EliminatedAt = _gm.MatchTime;
            Add(Kind.Eliminated, s, null, 0, 0f, 0f, 0f, 0f);
        }

        private void HandleRespawned(PlayerController player)
        {
            if (!_recording) return;
            Side s = SideOf(player);
            if (s != null) Add(Kind.Respawn, s, null, 0, 0f, 0f, 0f, 0f);
        }

        private void Add(Kind kind, Side player, Side other, byte code, float v1, float v2, float v3, float v4, byte code2 = 0)
        {
            if (!_recording) return;
            if (_count >= Capacity)
            {
                _dropped++;
                return;
            }
            _entries[_count++] = new Entry
            {
                T = _gm.MatchTime, Kind = kind, Code = code, Code2 = code2,
                Player = (sbyte)(player != null ? player.Index : -1), Other = (sbyte)(other != null ? other.Index : -1),
                V1 = v1, V2 = v2, V3 = v3, V4 = v4
            };
        }
        #endregion

        #region Archivo
        /// <summary>Escribe el archivo de la partida. result = null: se ha dejado a medias.</summary>
        private void Finish(MatchResult result)
        {
            if (!_recording || !_started) return;
            // A medias: el último tiempo visto en partida (al reiniciar, el GameManager ya lo ha puesto a 0)
            float duration = result != null ? result.Duration : _lastMatchTime;
            for (int i = 0; i < _sides.Count; i++)
            {
                Side s = _sides[i];
                if (result != null)
                {
                    // Partida terminada: nada se ha reseteado todavía, vale el estado actual
                    s.LastSpin = s.Player.SpinPercentage;
                    s.LastLives = s.Player.Lives;
                    s.LastScore = s.Player.Score;
                }
                if (s.SpecialStart >= 0f)
                {
                    s.SpecialTime += duration - s.SpecialStart;
                    s.SpecialStart = -1f;
                }
            }

            var sb = new StringBuilder(64 * (_count + 32));
            sb.Append("t;event;player;other;v1;v2;v3;v4;info\n");
            WriteHeader(sb);
            for (int i = 0; i < _count; i++) WriteEntry(sb, ref _entries[i]);
            WriteEnd(sb, result, duration);

            string file = $"{FilePrefix}{_startDate:yyyy-MM-dd_HH-mm-ss}_{_sessionId}.csv";
            LastPath = TryWrite(Application.isEditor ? EditorFolder : BuildFolder, file, sb)
                       ?? (Application.isEditor ? null : TryWrite(FallbackFolder, file, sb));
            if (LastPath != null) Debug.Log($"[Telemetría] Partida guardada en {LastPath}");
            _started = false;
        }

        private static string TryWrite(string folder, string file, StringBuilder sb)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, file);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetría] No se puede escribir en {folder}: {e.Message}");
                return null;
            }
        }

        private void WriteHeader(StringBuilder sb)
        {
            MatchRules rules = _gm.Rules;
            ArenaData arena = MatchSetup.Arena;
            int humans = 0, ais = 0;
            for (int i = 0; i < _sides.Count; i++)
                if (_sides[i].IsCpu) ais++;
                else humans++;

            Row(sb, 0f, "MATCH", null, null, _sides.Count, humans, ais, _matchNumber, string.Format(Inv,
                "date={0:yyyy-MM-dd HH:mm:ss}|version={1}|platform={2}|session={3}|machine={4}|mode={5}|win={6}|lives={7}|" +
                "timeLimit={8}|points={9}|teams={10}|friendlyFire={11}|arena={12}",
                _startDate, Application.version, Application.isEditor ? "editor" : Application.platform.ToString(), _sessionId,
                MachineId(), rules.displayNameKey, rules.winCondition, rules.StartingLives, rules.timeLimit, rules.pointsToWin,
                rules.teams ? 1 : 0, rules.friendlyFire ? 1 : 0, arena != null ? arena.name : "-"));

            CombatConfig c = CombatConfig.Active;
            Row(sb, 0f, "CONFIG", null, null, 0, 0, 0, 0, string.Format(Inv,
                "damageMultiplier={0}|hitBaseDamage={1}|hitSpeedFactor={2}-{3}|chargedDamagePerLevel={4}|parryWindow={5}|" +
                "quickAttackCost={6}|dashCost={7}|dashCooldown={8}|attackHitDamage={9}|dashHitDamage={10}|" +
                "specialEnergyPerSecond={11}|specialEnergyMultiplier={12}|damagePerImpactSpeed={13}|knockbackBase={14}",
                c.damageMultiplier, c.hitBaseDamage, c.hitSpeedFactor.x, c.hitSpeedFactor.y, c.chargedDamagePerLevel,
                c.parryWindow, c.quickAttackSpinCostPct, c.dashSpinCostPct, c.dashCooldown, c.attackHitDamageMultiplier,
                c.dashHitDamageMultiplier, c.specialEnergyPerSecond, c.specialEnergyMultiplier, c.damagePerImpactSpeed,
                c.knockbackBase));

            for (int i = 0; i < _sides.Count; i++)
            {
                Side s = _sides[i];
                FakeBladeStats st = s.Player.Stats;
                Row(sb, 0f, "PLAYER", s, null, st.MaxSpin, st.Weight, st.AttackPower, st.Defense, s.Header);
            }
        }

        /// <summary>Quién juega y con qué peonza (se apunta al empezar, antes de que nada cambie).</summary>
        private string PlayerText(PlayerController p)
        {
            FakeBladeStats st = p.Stats;
            PlayerSetup setup = FindSetup(p.PlayerID);
            string control = "human", level = "-", profile = "-", device = "-";
            if (p.IsCpu)
            {
                control = "ai";
                var brain = p.GetComponent<AIBrain>();
                if (brain != null)
                {
                    level = brain.Difficulty.ToString();
                    profile = brain.Profile != null ? brain.Profile.name : "-";
                }
            }
            else if (setup != null) device = setup.Device.ToString();

            string preset = "custom";
            if (setup != null && setup.PresetIndex >= 0 && _catalog != null)
            {
                BladePreset bp = _catalog.GetPreset(setup.PresetIndex);
                // Solo cuenta como preset si no se ha cambiado ninguna pieza
                if (bp != null && bp.tip == st.EquippedTip && bp.body == st.EquippedBody && bp.blade == st.EquippedBlade &&
                    bp.core == st.EquippedCore)
                    preset = bp.nameKey;
            }

            SpecialAbilityData special = p.Blade.Special.Data;
            return string.Format(Inv,
                "control={0}|level={1}|profile={2}|device={3}|team={4}|preset={5}|archetype={6}|tip={7}|body={8}|blade={9}|" +
                "core={10}|special={11}|energyRequired={12}|moveSpeed={13}|charges={14}|dashForce={15}|spinDecay={16}|traits={17}",
                control, level, profile, device, p.TeamID, preset, st.Archetype, PartName(st.EquippedTip),
                PartName(st.EquippedBody), PartName(st.EquippedBlade), PartName(st.EquippedCore), st.SpecialAbility,
                special != null ? special.energyRequired : 0f, st.MoveSpeed, st.AttackCharges, st.DashForce, st.SpinDecay,
                st.Traits);
        }

        private static PlayerSetup FindSetup(int playerIndex)
        {
            for (int i = 0; i < MatchSetup.Players.Count; i++)
                if (MatchSetup.Players[i].PlayerIndex == playerIndex) return MatchSetup.Players[i];
            return null;
        }

        private static string PartName(FakeBladeComponentData part) => part != null ? part.name : "-";

        /// <summary>Identificador anónimo del ordenador (para separar grupos de jugadores al juntar archivos).</summary>
        private static string MachineId()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier) return "-";
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * 16777619;
                return hash.ToString("x8");
            }
        }

        private void WriteEntry(StringBuilder sb, ref Entry e)
        {
            Side p = e.Player >= 0 ? _sides[e.Player] : null;
            Side o = e.Other >= 0 ? _sides[e.Other] : null;
            switch (e.Kind)
            {
                case Kind.Clash:
                    Row(sb, e.T, "CLASH", p, o, e.V1, e.V2, e.V3, e.V4, string.Format(Inv,
                        "kind={0}|dashA={1}|dashB={2}|chargeA={3}|chargeB={4}", (CollisionResolver.ClashKind)(e.Code & 15),
                        (e.Code & 16) != 0 ? 1 : 0, (e.Code & 32) != 0 ? 1 : 0, e.Code2 & 15, e.Code2 >> 4));
                    break;
                case Kind.Damage:
                    Row(sb, e.T, "DAMAGE", p, o, e.V1, e.V2, 0f, 0f, e.Code == 1 ? "from=rival" : "from=wall");
                    break;
                case Kind.Attack:
                    Row(sb, e.T, "ATTACK", p, null, e.V1, e.V2, e.V3, 0f, "");
                    break;
                case Kind.Dash:
                    Row(sb, e.T, "DASH", p, null, 0f, e.V2, e.V3, 0f, "");
                    break;
                case Kind.SpecialOn:
                    Row(sb, e.T, "SPECIAL_ON", p, null, 0f, e.V2, e.V3, 0f, "special=" + (SpecialAbilityType)e.Code);
                    break;
                case Kind.SpecialOff:
                    Row(sb, e.T, "SPECIAL_OFF", p, null, e.V1, e.V2, 0f, 0f, "special=" + (SpecialAbilityType)e.Code);
                    break;
                case Kind.Status:
                    Row(sb, e.T, "STATUS", p, o, 0f, e.V2, 0f, 0f, "status=" + (StatusEffectType)e.Code);
                    break;
                case Kind.HealCut:
                    Row(sb, e.T, "HEAL_CUT", p, null, 0f, e.V2, 0f, 0f, "");
                    break;
                case Kind.Parry:
                    Row(sb, e.T, "PARRY", p, o, e.V1, e.V2, 0f, 0f, e.Code == 1 ? "double=1" : "");
                    break;
                case Kind.Block:
                    Row(sb, e.T, "BLOCK", p, o, e.V1, e.V2, 0f, 0f, "charge=" + e.Code);
                    break;
                case Kind.Wall:
                    Row(sb, e.T, "WALL", p, null, e.V1, e.V2, 0f, 0f, "");
                    break;
                case Kind.KO:
                    Row(sb, e.T, "KO", p, o, e.V1, e.V2, 0f, 0f, o == null ? "self=1" : "");
                    break;
                case Kind.Eliminated:
                    Row(sb, e.T, "ELIMINATED", p, null, 0f, 0f, 0f, 0f, "");
                    break;
                case Kind.Respawn:
                    Row(sb, e.T, "RESPAWN", p, null, 0f, 0f, 0f, 0f, "");
                    break;
                case Kind.Snapshot:
                    Row(sb, e.T, "SNAPSHOT", p, null, e.V1, e.V2, e.V3, e.V4, e.Code == 1 ? "special=1" : "");
                    break;
            }
        }

        private void WriteEnd(StringBuilder sb, MatchResult result, float duration)
        {
            string winners = "";
            if (result != null)
                for (int i = 0; i < result.Winners.Count; i++)
                    winners += (winners.Length > 0 ? "," : "") + Name(result.Winners[i]);

            string end = result == null ? "abandoned" : result.EndedByTime ? "time" : "condition";
            Row(sb, duration, "END", null, null, duration, result != null && result.IsDraw ? 1 : 0,
                result != null ? result.WinningTeam : -1, _dropped, string.Format(Inv, "end={0}|winners={1}", end, winners));

            float minutes = Mathf.Max(duration / 60f, 0.0001f);
            for (int i = 0; i < _sides.Count; i++)
            {
                Side s = _sides[i];
                PlayerController p = s.Player;
                bool winner = result != null && result.Winners.Contains(p);
                Row(sb, duration, "SUMMARY", s, null, winner ? 1 : 0, s.DamageDealt / minutes, s.DamageTakenRival / minutes,
                    s.AliveTime, string.Format(Inv,
                        "winner={0}|finalSpin={1:0.#}|lives={2}|score={3}|eliminatedAt={4:0.##}|kos={5}|deaths={6}|selfKOs={7}|" +
                        "damageDealt={8:0.#}|damageTakenRival={9:0.#}|damageTakenWall={10:0.#}|clashDamageDealt={11:0.#}|" +
                        "clashDamageTaken={12:0.#}|clashes={13}|hitsLanded={14}|hitsReceived={15}|evenClashes={16}|" +
                        "attackHits={17}|dashHits={18}|attacks0={19}|attacks1={20}|attacks2={21}|attacks3={22}|dashes={23}|" +
                        "parries={24}|parried={25}|doubleParries={26}|blocks={27}|blocked={28}|specials={29}|" +
                        "specialTime={30:0.#}|healed={31:0.#}|healCuts={32}|statusInflicted={33}|statusReceived={34}|" +
                        "wallHits={35}|avgSpeed={36:0.##}|avgCenterDistance={37:0.##}|aliveTime={38:0.#}",
                        winner ? 1 : 0, s.LastSpin * 100f, s.LastLives, s.LastScore, s.EliminatedAt, s.KOs, s.Deaths, s.SelfKOs,
                        s.DamageDealt, s.DamageTakenRival, s.DamageTakenWall, s.ClashDamageDealt, s.ClashDamageTaken,
                        s.Clashes, s.HitsLanded, s.HitsReceived, s.EvenClashes, s.AttackHits, s.DashHits,
                        s.Attacks[0], s.Attacks[1], s.Attacks[2], s.Attacks[3], s.Dashes, s.Parries, s.Parried,
                        s.DoubleParries, s.Blocks, s.Blocked, s.Specials, s.SpecialTime, s.Healed, s.HealCuts,
                        s.StatusInflicted, s.StatusReceived, s.WallHits,
                        s.Snapshots > 0 ? s.SpeedSum / s.Snapshots : 0f, s.Snapshots > 0 ? s.CenterDistanceSum / s.Snapshots : 0f,
                        s.AliveTime));
            }
        }

        private static void Row(StringBuilder sb, float t, string evt, Side player, Side other,
            float v1, float v2, float v3, float v4, string info)
        {
            sb.Append(t.ToString("0.000", Inv)).Append(';')
                .Append(evt).Append(';')
                .Append(player != null ? Name(player.Player) : "").Append(';')
                .Append(other != null ? Name(other.Player) : "").Append(';')
                .Append(v1.ToString("0.###", Inv)).Append(';')
                .Append(v2.ToString("0.###", Inv)).Append(';')
                .Append(v3.ToString("0.###", Inv)).Append(';')
                .Append(v4.ToString("0.###", Inv)).Append(';')
                .Append(info).Append('\n');
        }

        private static string Name(PlayerController player) => player != null ? "J" + (player.PlayerID + 1) : "";
        #endregion
    }
}
