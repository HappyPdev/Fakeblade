using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Grabación de datos del sandbox (GDD 6.3, Debug → Guardar datos): cada sesión escribe un CSV
    /// para analizar el equilibrio. En el editor va a Logs/Sandbox/ (junto a Assets, fuera del
    /// repositorio); en una build, a la carpeta de datos del juego (persistentDataPath/SandboxLogs).
    ///
    /// Columnas (separadas por ';', decimales con punto):
    ///   t (s de juego desde que se cargó la escena) ; real (s reales) ; event ; player ; other ;
    ///   v1 ; v2 ; v3 ; v4 ; info
    /// Eventos: SESSION, CONFIG, PLAYER, CLASH, DAMAGE, ATTACK, DASH, SPECIAL_ON, SPECIAL_OFF,
    /// STATUS, KO, RESET y SNAPSHOT (uno por peonza y segundo). Qué es cada v1-v4: ver la guía
    /// del sandbox (Guía del Sandbox.md).
    /// </summary>
    public sealed class SandboxRecorder : MonoBehaviour
    {
        private const float SnapshotInterval = 1f;
        private const float MinDamage = 0.5f; // el roce continuo no llena el archivo
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Tracked
        {
            public PlayerController Player;
            public FakeBladeController Blade;
            public string Build;
            public Action<float, FakeBladeController> OnDamaged;
            public Action<int> OnAttack;
            public Action OnDash;
            public Action<SpecialAbilityType> OnSpecialOn;
            public Action<SpecialAbilityType> OnSpecialOff;
            public Action<StatusEffectType> OnStatus;
            public Action OnHealCut;
            public Action OnKO;
        }

        private readonly Dictionary<PlayerController, Tracked> _tracked = new Dictionary<PlayerController, Tracked>();
        private readonly List<PlayerController> _gone = new List<PlayerController>();
        private readonly StringBuilder _line = new StringBuilder(256);
        private StreamWriter _writer;
        private float _nextSnapshot;
        private string _config;
        private Func<PlayerController, bool> _isDummy;

        /// <summary>Archivo de la sesión actual (o de la última).</summary>
        public static string CurrentPath { get; private set; }

        public static string Folder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Sandbox"))
            : Path.Combine(Application.persistentDataPath, "SandboxLogs");

        public static SandboxRecorder Create(Transform parent, Func<PlayerController, bool> isDummy)
        {
            var go = new GameObject("[SandboxRecorder]");
            go.transform.SetParent(parent, false);
            var recorder = go.AddComponent<SandboxRecorder>();
            recorder._isDummy = isDummy;
            return recorder;
        }

        private void OnEnable() => CollisionResolver.OnClashResolved += HandleClash;

        private void OnDisable() => CollisionResolver.OnClashResolved -= HandleClash;

        private void OnDestroy()
        {
            foreach (Tracked t in _tracked.Values) Untrack(t);
            _tracked.Clear();
            Close();
        }

        private void Update()
        {
            if (!SandboxSettings.RecordData)
            {
                Close();
                return;
            }
            if (_writer == null) Open();

            Sync();

            string config = ConfigText();
            if (config != _config)
            {
                _config = config;
                Write("CONFIG", null, null, 0, 0, 0, 0, config);
            }

            if (Time.timeSinceLevelLoad >= _nextSnapshot && Time.timeScale > 0f)
            {
                _nextSnapshot = Time.timeSinceLevelLoad + SnapshotInterval;
                foreach (Tracked t in _tracked.Values) Snapshot(t);
                _writer.Flush();
            }
        }

        /// <summary>Apunta un evento del propio sandbox (p. ej. RESET).</summary>
        public void Note(string evt, string info)
        {
            if (_writer != null) Write(evt, null, null, 0, 0, 0, 0, info);
        }

        #region Archivo
        private void Open()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                CurrentPath = Path.Combine(Folder, $"sandbox_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
                _writer = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
                _writer.WriteLine("t;real;event;player;other;v1;v2;v3;v4;info");
                ArenaData arena = MatchSetup.Arena;
                Write("SESSION", null, null, 0, 0, 0, 0,
                    $"arena={(arena != null ? arena.name : "?")}|date={DateTime.Now:yyyy-MM-dd HH:mm:ss}|version={Application.version}");
                _config = null;
                foreach (Tracked t in _tracked.Values) t.Build = null; // vuelve a escribir las peonzas
                _nextSnapshot = 0f;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SandboxRecorder] No se puede escribir en {Folder}: {e.Message}");
                SandboxSettings.RecordData = false;
                _writer = null;
            }
        }

        private void Close()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        private void Write(string evt, PlayerController player, PlayerController other,
            float v1, float v2, float v3, float v4, string info)
        {
            if (_writer == null) return;
            _line.Clear();
            _line.Append(Time.timeSinceLevelLoad.ToString("0.000", Inv)).Append(';')
                .Append(Time.realtimeSinceStartup.ToString("0.000", Inv)).Append(';')
                .Append(evt).Append(';')
                .Append(Name(player)).Append(';')
                .Append(Name(other)).Append(';')
                .Append(v1.ToString("0.###", Inv)).Append(';')
                .Append(v2.ToString("0.###", Inv)).Append(';')
                .Append(v3.ToString("0.###", Inv)).Append(';')
                .Append(v4.ToString("0.###", Inv)).Append(';')
                .Append(info);
            _writer.WriteLine(_line);
        }

        private static string Name(PlayerController player) => player != null ? "J" + (player.PlayerID + 1) : "";

        private static string ConfigText()
        {
            CombatConfig c = CombatConfig.Active;
            return string.Format(Inv,
                "speed={0}|damageMultiplier={1}|chargedDamagePerLevel={2}|parryWindow={3}|quickAttackCost={4}|dashCost={5}|" +
                "damagePerImpactSpeed={6}|damagePerSpeedDiff={7}|knockbackBase={8}|specialEnergyMultiplier={9}|" +
                "speedSpread={10}|referenceMaxSpeed={11}|attackHitDamage={12}|dashHitDamage={13}|dashCooldown={14}",
                SandboxSettings.Speed, c.damageMultiplier, c.chargedDamagePerLevel, c.parryWindow, c.quickAttackSpinCostPct,
                c.dashSpinCostPct, c.damagePerImpactSpeed, c.damagePerSpeedDiff, c.knockbackBase, c.specialEnergyMultiplier,
                c.speedSpread, c.referenceMaxSpeed, c.attackHitDamageMultiplier, c.dashHitDamageMultiplier, c.dashCooldown);
        }
        #endregion

        #region Peonzas
        private void Sync()
        {
            GameManager gm = GameManager.Instance;
            _gone.Clear();
            foreach (PlayerController p in _tracked.Keys)
                if (p == null || gm == null || !Contains(gm.Players, p)) _gone.Add(p);
            foreach (PlayerController p in _gone)
            {
                Untrack(_tracked[p]);
                _tracked.Remove(p);
            }
            if (gm == null) return;

            foreach (PlayerController p in gm.Players)
            {
                if (p == null || p.Blade == null) continue;
                if (!_tracked.TryGetValue(p, out Tracked t))
                {
                    t = Track(p);
                    _tracked.Add(p, t);
                }
                // Peonza nueva o piezas cambiadas (Mi peonza): se apunta cómo es
                string build = BuildText(p);
                if (build != t.Build)
                {
                    t.Build = build;
                    FakeBladeStats s = p.Stats;
                    Write("PLAYER", p, null, s.MaxSpin, s.Weight, s.AttackPower, s.Defense, build);
                }
            }
        }

        private static bool Contains(IReadOnlyList<PlayerController> list, PlayerController p)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == p) return true;
            return false;
        }

        private string BuildText(PlayerController p)
        {
            FakeBladeStats s = p.Stats;
            return string.Format(Inv,
                "{0}|tip={1}|body={2}|blade={3}|core={4}|special={5}|archetype={6}|moveSpeed={7}|charges={8}|dashForce={9}|spinDecay={10}|traits={11}",
                _isDummy != null && _isDummy(p) ? "dummy" : "human",
                PartName(s.EquippedTip), PartName(s.EquippedBody), PartName(s.EquippedBlade), PartName(s.EquippedCore),
                s.SpecialAbility, s.Archetype, s.MoveSpeed, s.AttackCharges, s.DashForce, s.SpinDecay, s.Traits);
        }

        private static string PartName(FakeBladeComponentData part) => part != null ? part.ComponentName : "-";

        private Tracked Track(PlayerController player)
        {
            FakeBladeController blade = player.Blade;
            var t = new Tracked { Player = player, Blade = blade };
            t.OnDamaged = (amount, source) =>
            {
                if (amount < MinDamage) return;
                Write("DAMAGE", player, Owner(source), amount, blade.CurrentSpinSpeed, 0, 0,
                    blade.Status.HasStatus ? "status=" + blade.Status.Current : "");
            };
            t.OnAttack = level => Write("ATTACK", player, null, level, blade.SpinSpeedPercentage * 100f, 0, 0, "");
            t.OnDash = () => Write("DASH", player, null, 0, blade.SpinSpeedPercentage * 100f, 0, 0, "");
            t.OnSpecialOn = type => Write("SPECIAL_ON", player, null, 0, blade.SpinSpeedPercentage * 100f, 0, 0, "special=" + type);
            t.OnSpecialOff = type => Write("SPECIAL_OFF", player, null, 0, blade.SpinSpeedPercentage * 100f, 0, 0, "special=" + type);
            t.OnStatus = status => Write("STATUS", player, Owner(blade.Status.Source), 0, 0, 0, 0, "status=" + status);
            t.OnKO = () => Write("KO", player, Owner(blade.LastDamageSource), 0, 0, 0, 0, "");
            t.OnHealCut = () => Write("HEAL_CUT", player, null, 0, blade.SpinSpeedPercentage * 100f, 0, 0, "");

            blade.OnDamaged += t.OnDamaged;
            blade.OnAttackLaunched += t.OnAttack;
            blade.OnDashExecuted += t.OnDash;
            blade.OnSpecialActivated += t.OnSpecialOn;
            blade.OnSpecialEnded += t.OnSpecialOff;
            blade.OnStatusEffectChanged += t.OnStatus;
            blade.OnHealCut += t.OnHealCut;
            blade.OnSpinOut += t.OnKO;
            return t;
        }

        private static void Untrack(Tracked t)
        {
            if (t.Blade == null) return;
            t.Blade.OnDamaged -= t.OnDamaged;
            t.Blade.OnAttackLaunched -= t.OnAttack;
            t.Blade.OnDashExecuted -= t.OnDash;
            t.Blade.OnSpecialActivated -= t.OnSpecialOn;
            t.Blade.OnSpecialEnded -= t.OnSpecialOff;
            t.Blade.OnStatusEffectChanged -= t.OnStatus;
            t.Blade.OnHealCut -= t.OnHealCut;
            t.Blade.OnSpinOut -= t.OnKO;
        }

        private void Snapshot(Tracked t)
        {
            FakeBladeController blade = t.Blade;
            if (blade == null) return;
            Vector3 v = blade.Velocity;
            v.y = 0f;
            Write("SNAPSHOT", t.Player, null, blade.SpinSpeedPercentage * 100f, v.magnitude, blade.Special.Energy,
                blade.Attack.CurrentCharges,
                (blade.IsDestroyed ? "ko|" : "") + (blade.Status.HasStatus ? "status=" + blade.Status.Current : "") +
                (blade.Special.IsActive ? "|special=" + blade.Special.Type : ""));
        }

        private static PlayerController Owner(FakeBladeController blade) =>
            blade != null ? blade.GetComponent<PlayerController>() : null;
        #endregion

        #region Choques
        private void HandleClash(CollisionResolver.ClashReport r)
        {
            if (_writer == null) return;
            PlayerController a = Owner(r.A), b = Owner(r.B);
            Write("CLASH", a, b, r.ApproachA, r.ApproachB, r.DamageToA, r.DamageToB, string.Format(Inv,
                "kind={0}|knockA={1:0.##}|knockB={2:0.##}|chargeA={3}|chargeB={4}|dashA={5}|dashB={6}|spinA={7:0.#}|spinB={8:0.#}",
                r.Kind, r.KnockbackToA, r.KnockbackToB, r.ChargeA, r.ChargeB, r.DashA ? 1 : 0, r.DashB ? 1 : 0,
                r.A != null ? r.A.SpinSpeedPercentage * 100f : 0f, r.B != null ? r.B.SpinSpeedPercentage * 100f : 0f));
        }
        #endregion
    }
}
