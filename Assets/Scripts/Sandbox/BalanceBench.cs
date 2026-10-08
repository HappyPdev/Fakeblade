using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Banco de pruebas de equilibrio (C10, método en el GDD 11). Se lanza con Play en la escena Sandbox
    /// (menú FakeBlade → Banco de equilibrio). Con el primer jugador humano y el primer dummy:
    ///
    /// - Cada preset golpea a cada preset con ataque rápido, cargado 1-3 y dash, en tres situaciones:
    ///   PARADO (suelta desde quieta a 3,2 m), TOPE (sale a su velocidad máxima de movimiento) y TARDE
    ///   (suelta desde quieta a 5 m: el acelerón llega frenado).
    /// - Compara con la tabla de objetivos (GDD 2.7) y guarda un informe en Logs/Balance/ (.md y .csv).
    ///
    /// Durante el banco los dummies están quietos, los trucos apagados, la velocidad a x1 y no se graba
    /// la sesión del sandbox; al acabar se deja todo como estaba (piezas, control y ajustes).
    /// </summary>
    public class BalanceBench : MonoBehaviour
    {
        public enum Hit { Quick, Charged1, Charged2, Charged3, Dash }
        public enum Situation { Standing, TopSpeed, Late }

        // Objetivos (GDD 2.7): golpe de referencia, multiplicadores por arquetipo, tope y duración
        private const float TargetQuick = 20f;
        private const float MaxSingleHit = 60f;
        private const float TargetMirrorSeconds = 60f;
        /// <summary>Segundos entre golpes con daño en el registro del sandbox (2026-10-07: uno cada 4,2 s).</summary>
        private const float SecondsPerHit = 4.2f;
        private const float Tolerance = 0.1f;

        private static readonly Dictionary<string, Vector2> ArchetypeTargets = new Dictionary<string, Vector2>
        {
            // x = daño que hace, y = daño que recibe (Balanceada = 1)
            ["PRESET_ATTACK"] = new Vector2(1.25f, 1.10f),
            ["PRESET_DEFENSE"] = new Vector2(0.75f, 0.65f),
            ["PRESET_AGILITY"] = new Vector2(1.10f, 1.20f),
            ["PRESET_BALANCED"] = new Vector2(1f, 1f),
        };

        private const float StandingGap = 3.2f;
        /// <summary>A tope se empieza algo más lejos: mientras se espera a que salga el ataque ya va moviéndose.</summary>
        private const float TopSpeedGap = 4.5f;
        private const float LateGap = 5f;
        private const float QuickTapTime = 0.06f;

        private struct Result
        {
            public bool Hit;
            public int Level;
            public float Approach, Damage, DamageToAttacker, Knockback;
        }

        public static bool IsRunning { get; private set; }
        public static string LastReportPath { get; private set; }

        private FakeBladeCatalog _catalog;
        private PlayerController _attacker, _defender;
        private Rigidbody _rbA, _rbD;
        private readonly BenchInput _input = new BenchInput();
        private readonly Dictionary<string, Result> _results = new Dictionary<string, Result>();
        private CollisionResolver.ClashReport _last;
        private int _clashes;

        /// <summary>Empieza el banco en la partida en curso. Devuelve el motivo si no se puede.</summary>
        public static string Run(FakeBladeCatalog catalog)
        {
            if (IsRunning) return "El banco ya está en marcha.";
            if (catalog == null || catalog.presets.Count == 0) return "Sin catálogo o sin presets.";
            var gm = GameManager.Instance;
            if (gm == null || !MatchSetup.IsSandbox) return "Abre la escena Sandbox y dale a Play.";

            PlayerController attacker = null, defender = null;
            foreach (PlayerController p in gm.Players)
            {
                if (p == null) continue;
                if (attacker == null && p.IsHumanControlled) attacker = p;
                else if (defender == null && p.GetComponent<SandboxDummyBrain>() != null) defender = p;
            }
            if (attacker == null) return "No hay ningún jugador humano.";
            if (defender == null) return "No hay ningún dummy (añade uno en Rivales).";

            var bench = new GameObject("BalanceBench").AddComponent<BalanceBench>();
            bench._catalog = catalog;
            bench._attacker = attacker;
            bench._defender = defender;
            return null;
        }

        private IEnumerator Start()
        {
            IsRunning = true;
            _rbA = _attacker.GetComponent<Rigidbody>();
            _rbD = _defender.GetComponent<Rigidbody>();

            // Guardar lo que se toca para dejarlo igual al acabar
            var attackerParts = Parts(_attacker.Stats);
            var defenderParts = Parts(_defender.Stats);
            var behaviour = SandboxSettings.Behaviour;
            var cheats = new[] { SandboxSettings.InfiniteSpin, SandboxSettings.FullSpecial, SandboxSettings.InfiniteCharges,
                                 SandboxSettings.NoDashCooldown, SandboxSettings.Invulnerable };
            bool record = SandboxSettings.RecordData;
            float scale = GameTime.NormalScale;

            SandboxSettings.Behaviour = DummyBehaviour.Idle;
            SandboxSettings.InfiniteSpin = SandboxSettings.FullSpecial = SandboxSettings.InfiniteCharges =
                SandboxSettings.NoDashCooldown = SandboxSettings.Invulnerable = CheatTarget.Off;
            SandboxSettings.RecordData = false;
            GameTime.SetNormalScale(1f, true);
            _attacker.SetInputSource(_input);
            CollisionResolver.OnClashResolved += OnClash;

            float started = Time.realtimeSinceStartup;
            Debug.Log("[BalanceBench] Empieza el banco de pruebas de equilibrio");

            try
            {
                foreach (BladePreset attackerPreset in _catalog.presets)
                {
                    Equip(_attacker.Stats, attackerPreset);
                    foreach (BladePreset defenderPreset in _catalog.presets)
                    {
                        Equip(_defender.Stats, defenderPreset);
                        yield return null;
                        foreach (Hit hit in Enum.GetValues(typeof(Hit)))
                            foreach (Situation situation in Enum.GetValues(typeof(Situation)))
                                yield return Measure(attackerPreset, defenderPreset, hit, situation);
                    }
                }
            }
            finally
            {
                CollisionResolver.OnClashResolved -= OnClash;
                _input.Clear();
                _attacker.SetInputSource(null);
                Restore(_attacker.Stats, attackerParts);
                Restore(_defender.Stats, defenderParts);
                SandboxSettings.Behaviour = behaviour;
                SandboxSettings.InfiniteSpin = cheats[0];
                SandboxSettings.FullSpecial = cheats[1];
                SandboxSettings.InfiniteCharges = cheats[2];
                SandboxSettings.NoDashCooldown = cheats[3];
                SandboxSettings.Invulnerable = cheats[4];
                SandboxSettings.RecordData = record;
                GameTime.SetNormalScale(scale, true);
                IsRunning = false;
            }

            WriteReport(Time.realtimeSinceStartup - started);
            Destroy(gameObject);
        }

        private void OnClash(CollisionResolver.ClashReport report)
        {
            _clashes++;
            _last = report;
        }

        #region Medida
        private IEnumerator Measure(BladePreset attackerPreset, BladePreset defenderPreset, Hit hit, Situation situation)
        {
            float gap = situation == Situation.Late ? LateGap : situation == Situation.TopSpeed ? TopSpeedGap : StandingGap;
            // Siempre cerca del centro de la arena, de izquierda a derecha
            Vector3 attackerPos = new Vector3(-gap * 0.6f, _attacker.transform.position.y, 0f);
            Vector3 defenderPos = new Vector3(gap * 0.4f, _defender.transform.position.y, 0f);
            _input.Clear();
            for (int i = 0; i < 20; i++)
            {
                _attacker.Blade.SetPosition(attackerPos, Quaternion.identity);
                _defender.Blade.SetPosition(defenderPos, Quaternion.identity);
                _rbA.linearVelocity = Vector3.zero;
                _rbD.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
            }
            _attacker.Blade.Special.Reset(_attacker.Blade, _attacker.Stats.SpecialAbility);
            _attacker.Blade.Attack.RefillCharges();
            _attacker.Blade.ClearDashCooldown();
            _attacker.Blade.AddSpin(_attacker.Blade.MaxSpinSpeed);
            _defender.Blade.AddSpin(_defender.Blade.MaxSpinSpeed);

            int level = hit == Hit.Charged1 ? 1 : hit == Hit.Charged2 ? 2 : hit == Hit.Charged3 ? 3 : 0;
            if (hit != Hit.Dash)
            {
                // Mantener el botón en el sitio hasta el nivel pedido (sin moverse). El rápido es una
                // pulsación corta (más de un frame para que el input la lea, menos que el umbral de carga)
                _input.Held = true;
                float t0 = Time.time;
                while (Time.time - t0 < 3f)
                {
                    Pin(attackerPos);
                    yield return new WaitForFixedUpdate();
                    if (level == 0 ? Time.time - t0 >= QuickTapTime : _attacker.Blade.Attack.ChargeLevel >= level) break;
                }
            }

            // Soltar (o dash) y esperar a que salga de verdad: hasta entonces se queda quieta en su sitio o,
            // a tope, a su velocidad máxima, para que el acelerón empiece siempre igual
            int before = _clashes;
            _input.Move = Vector2.right;
            if (hit == Hit.Dash) _input.Dash = true;
            else _input.Held = false;

            bool launched = false;
            float t1 = Time.time;
            while (Time.time - t1 < 0.5f)
            {
                if (situation == Situation.TopSpeed) _rbA.linearVelocity = Vector3.right * _attacker.Blade.MaxMoveSpeed;
                else Pin(attackerPos);
                yield return new WaitForFixedUpdate();
                launched = hit == Hit.Dash ? _attacker.Blade.IsDashAttacking : _attacker.Blade.Attack.IsAttacking;
                if (launched) break;
            }

            float t2 = Time.time;
            while (launched && _clashes == before && Time.time - t2 < 1.5f) yield return new WaitForFixedUpdate();
            _input.Clear();

            var result = new Result();
            if (launched && _clashes > before)
            {
                bool attackerIsA = _last.A == _attacker.Blade;
                result.Hit = true;
                result.Level = attackerIsA ? _last.ChargeA : _last.ChargeB;
                result.Approach = attackerIsA ? _last.ApproachA : _last.ApproachB;
                result.Damage = attackerIsA ? _last.DamageToB : _last.DamageToA;
                result.DamageToAttacker = attackerIsA ? _last.DamageToA : _last.DamageToB;
                result.Knockback = attackerIsA ? _last.KnockbackToB : _last.KnockbackToA;
            }
            // Un cargado que no llega a su nivel (sin cargas suficientes) no cuenta
            if (level > 0 && result.Level != level) result.Hit = false;
            _results[Key(attackerPreset.nameKey, defenderPreset.nameKey, hit, situation)] = result;

            yield return new WaitForSeconds(0.6f);
        }

        private void Pin(Vector3 position)
        {
            _rbA.linearVelocity = Vector3.zero;
            _attacker.Blade.SetPosition(position, Quaternion.identity);
        }
        #endregion

        #region Informe
        private void WriteReport(float seconds)
        {
            var cfg = CombatConfig.Active;
            string folder = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Balance"))
                : Path.Combine(Application.persistentDataPath, "BalanceLogs");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string mdPath = Path.Combine(folder, $"bench_{stamp}.md");
            string csvPath = Path.Combine(folder, $"bench_{stamp}.csv");

            var presets = _catalog.presets;
            string balanced = "PRESET_BALANCED";
            var md = new StringBuilder();
            md.AppendLine($"# Banco de equilibrio — {DateTime.Now:yyyy-MM-dd HH:mm}");
            md.AppendLine();
            md.AppendLine($"Valores: daño global ×{F(cfg.damageMultiplier)}, cargado +{F(cfg.chargedDamagePerLevel * 100f)}%/nivel, " +
                          $"golpe con ataque ×{F(cfg.attackHitDamageMultiplier)}, con dash ×{F(cfg.dashHitDamageMultiplier)}, " +
                          $"golpe base {F(cfg.hitBaseDamage)} (×{F(cfg.hitSpeedFactor.x)} a {F(cfg.hitSpeedRange.x)} m/s, " +
                          $"×{F(cfg.hitSpeedFactor.y)} a {F(cfg.hitSpeedRange.y)} m/s). " +
                          $"Duración: {seconds:F0} s.");
            md.AppendLine();
            md.AppendLine($"Objetivos (GDD 2.7): rápido ≈ {TargetQuick:F0} en Balanceada contra Balanceada, cargado ×1,25 / ×1,5 / ×1,75, " +
                          $"ningún golpe por encima de {MaxSingleHit:F0}, espejo de Balanceada ≈ {TargetMirrorSeconds:F0} s " +
                          $"(con un golpe cada {F(SecondsPerHit)} s). P = parado, T = a tope, L = tarde.");
            md.AppendLine();

            // 1) Cada preset contra Balanceada
            md.AppendLine("## Golpes contra Balanceada");
            md.AppendLine();
            md.AppendLine("| Atacante | Rápido P / T / L | Cargado 1 T | Cargado 2 T | Cargado 3 P / T / L | Dash P / T / L |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (BladePreset a in presets)
            {
                md.AppendLine($"| {Name(a.nameKey)} | {Cells(a.nameKey, balanced, Hit.Quick, true)} | " +
                              $"{Cell(a.nameKey, balanced, Hit.Charged1, Situation.TopSpeed)} | {Cell(a.nameKey, balanced, Hit.Charged2, Situation.TopSpeed)} | " +
                              $"{Cells(a.nameKey, balanced, Hit.Charged3, true)} | {Cells(a.nameKey, balanced, Hit.Dash, true)} |");
            }
            md.AppendLine();

            // 2) Arquetipos: daño que hacen y que reciben respecto a Balanceada
            md.AppendLine("## Arquetipos respecto a Balanceada");
            md.AppendLine();
            md.AppendLine("| Preset | Hace (objetivo) | Recibe (objetivo) |");
            md.AppendLine("|---|---|---|");
            foreach (BladePreset p in presets)
            {
                ArchetypeTargets.TryGetValue(p.nameKey, out Vector2 target);
                float deals = Ratio(p.nameKey, balanced, balanced, balanced);
                float takes = Ratio(balanced, p.nameKey, balanced, balanced);
                md.AppendLine($"| {Name(p.nameKey)} | {Check(deals, target.x)} | {Check(takes, target.y)} |");
            }
            md.AppendLine();
            md.AppendLine("Hace = daño medio de sus golpes (P y T) contra Balanceada / el de Balanceada. Recibe = daño medio que le hace Balanceada / el que se hace a sí misma.");
            md.AppendLine();

            // 3) Duración estimada de cada duelo (columna = quien recibe)
            md.AppendLine("## Duración estimada de los duelos (s)");
            md.AppendLine();
            var header = new StringBuilder("| Atacante \\ Defensor |");
            var line = new StringBuilder("|---|");
            foreach (BladePreset d in presets) { header.Append($" {Name(d.nameKey)} |"); line.Append("---|"); }
            md.AppendLine(header.ToString());
            md.AppendLine(line.ToString());
            foreach (BladePreset a in presets)
            {
                var row = new StringBuilder($"| {Name(a.nameKey)} |");
                foreach (BladePreset d in presets)
                {
                    float hits = HitsToKo(a.nameKey, d.nameKey);
                    row.Append(hits > 0f ? $" {hits * SecondsPerHit:F0} ({hits:F0} golpes) |" : " — |");
                }
                md.AppendLine(row.ToString());
            }
            md.AppendLine();
            md.AppendLine($"Golpes hasta el K.O. con la mezcla media de golpes (P y T) × {F(SecondsPerHit)} s por golpe. Objetivo del espejo de Balanceada: {TargetMirrorSeconds:F0} s.");
            md.AppendLine();

            // 4) Golpes por encima del tope
            md.AppendLine($"## Golpes por encima de {MaxSingleHit:F0}");
            md.AppendLine();
            int over = 0;
            foreach (var pair in _results)
            {
                if (!pair.Value.Hit || pair.Value.Damage <= MaxSingleHit) continue;
                over++;
                md.AppendLine($"- {Describe(pair.Key)}: {pair.Value.Damage:F1} ({pair.Value.Approach:F1} m/s)");
            }
            if (over == 0) md.AppendLine("- Ninguno.");

            // CSV con todo
            var csv = new StringBuilder("attacker;defender;hit;situation;landed;level;approach;damage;damageToAttacker;knockback\n");
            foreach (var pair in _results)
            {
                Result r = pair.Value;
                csv.AppendLine($"{pair.Key};{(r.Hit ? 1 : 0)};{r.Level};{F2(r.Approach)};{F2(r.Damage)};{F2(r.DamageToAttacker)};{F2(r.Knockback)}");
            }

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
            LastReportPath = mdPath;
            Debug.Log($"[BalanceBench] Hecho en {seconds:F0} s. Informe: {mdPath}");
        }

        /// <summary>Daño medio de los golpes P y T de un atacante contra un defensor, sobre el de una pareja de referencia.</summary>
        private float Ratio(string attacker, string defender, string refAttacker, string refDefender)
        {
            float sum = 0f, refSum = 0f;
            foreach (Hit hit in Enum.GetValues(typeof(Hit)))
                foreach (Situation s in new[] { Situation.Standing, Situation.TopSpeed })
                {
                    if (!TryGet(attacker, defender, hit, s, out Result r) || !TryGet(refAttacker, refDefender, hit, s, out Result rr)) continue;
                    sum += r.Damage;
                    refSum += rr.Damage;
                }
            return refSum > 0f ? sum / refSum : 0f;
        }

        private float HitsToKo(string attacker, string defender)
        {
            float sum = 0f;
            int n = 0;
            foreach (Hit hit in Enum.GetValues(typeof(Hit)))
                foreach (Situation s in new[] { Situation.Standing, Situation.TopSpeed })
                    if (TryGet(attacker, defender, hit, s, out Result r)) { sum += r.Damage; n++; }
            if (n == 0 || sum <= 0f) return 0f;
            BladePreset preset = _catalog.presets.Find(p => p.nameKey == defender);
            float maxSpin = MaxSpinOf(preset);
            return maxSpin / (sum / n);
        }

        private float MaxSpinOf(BladePreset preset)
        {
            // Las RPM máximas salen de las piezas: se miden montándolas un instante en el defensor
            var saved = Parts(_defender.Stats);
            Equip(_defender.Stats, preset);
            float maxSpin = _defender.Stats.MaxSpin;
            Restore(_defender.Stats, saved);
            return maxSpin;
        }

        private bool TryGet(string attacker, string defender, Hit hit, Situation s, out Result r) =>
            _results.TryGetValue(Key(attacker, defender, hit, s), out r) && r.Hit;

        private string Cells(string attacker, string defender, Hit hit, bool allSituations) =>
            $"{Cell(attacker, defender, hit, Situation.Standing)} / {Cell(attacker, defender, hit, Situation.TopSpeed)} / {Cell(attacker, defender, hit, Situation.Late)}";

        private string Cell(string attacker, string defender, Hit hit, Situation s)
        {
            if (!_results.TryGetValue(Key(attacker, defender, hit, s), out Result r)) return "?";
            if (!r.Hit) return "—";
            string value = r.Damage.ToString("F0", CultureInfo.InvariantCulture);
            return r.Damage > MaxSingleHit ? $"**{value}**" : value;
        }

        private static string Check(float value, float target)
        {
            if (target <= 0f) return F(value);
            bool ok = Mathf.Abs(value - target) <= Tolerance * target;
            return $"{F(value)} ({F(target)}) {(ok ? "OK" : "✗")}";
        }

        private static string Key(string attacker, string defender, Hit hit, Situation s) => $"{attacker};{defender};{hit};{s}";

        private static string Describe(string key)
        {
            string[] p = key.Split(';');
            return $"{Name(p[0])} → {Name(p[1])}, {p[2]} {p[3]}";
        }

        private static string Name(string presetKey) => Loc.Get(presetKey);
        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string F2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        #endregion

        #region Piezas
        private static FakeBladeComponentData[] Parts(FakeBladeStats stats) =>
            new[] { stats.EquippedTip, stats.EquippedBody, stats.EquippedBlade, stats.EquippedCore };

        private static void Equip(FakeBladeStats stats, BladePreset preset)
        {
            foreach (var part in new[] { preset.tip, preset.body, preset.blade, preset.core })
                if (part != null) stats.EquipComponent(part);
        }

        private static void Restore(FakeBladeStats stats, FakeBladeComponentData[] parts)
        {
            foreach (var part in parts)
                if (part != null) stats.EquipComponent(part);
        }
        #endregion

        /// <summary>Mando virtual del banco: mantiene y suelta el ataque, se mueve y hace dash.</summary>
        private sealed class BenchInput : IBladeInputSource
        {
            public Vector2 Move;
            public bool Held;
            public bool Dash;

            public Vector2 MovementInput => Move;
            public bool AttackHeld => Held;

            public bool ConsumeDash()
            {
                bool dash = Dash;
                Dash = false;
                return dash;
            }

            public bool ConsumeSpecial() => false;
            public void ClearBuffers() => Dash = false;
            public void Vibrate(float lowFrequency, float highFrequency, float duration) { }

            public void Clear()
            {
                Move = Vector2.zero;
                Held = false;
                Dash = false;
            }
        }
    }
}
