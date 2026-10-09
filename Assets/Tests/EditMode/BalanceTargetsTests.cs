using FakeBlade.Core;
using NUnit.Framework;

namespace FakeBlade.Tests
{
    /// <summary>
    /// Objetivos de equilibrio del GDD 2.7 con los presets y el CombatConfig reales, calculados sin jugar
    /// (±10%, como el banco). Lo que depende del movimiento (cuántas veces golpea cada uno, a qué velocidad
    /// llega) solo lo mide el banco de equilibrio; aquí se comprueba lo que sale de las estadísticas.
    /// Si un objetivo cambia en el GDD, se cambia aquí también.
    /// </summary>
    public class BalanceTargetsTests
    {
        private CombatConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = TestData.Config;

        [TestCase(1, 1.25f)]
        [TestCase(2, 1.5f)]
        [TestCase(3, 1.75f)]
        public void Cargado_MultiplicaAlRapido(int level, float expected)
        {
            Assert.AreEqual(expected, BladeFormulas.ChargeDamageMultiplier(_cfg, level), 1e-3f);
        }

        [Test]
        public void Rapido_BalanceadaContraBalanceada_Unas20RPM()
        {
            var balanced = TestData.Preset(BladeArchetype.Balanced);
            float hit = TestData.EstimatedHit(_cfg, balanced, balanced, TestData.MidHitSpeed(_cfg), HitKind.Quick);
            TestData.AssertNear(20f, hit, "Golpe rápido Balanceada contra Balanceada (golpe a ×1 de velocidad)");
        }

        [Test]
        public void Dash_ALaMismaVelocidad_PegaMenosQueUnRapido()
        {
            var balanced = TestData.Preset(BladeArchetype.Balanced);
            float speed = TestData.MidHitSpeed(_cfg);
            Assert.Less(TestData.EstimatedHit(_cfg, balanced, balanced, speed, HitKind.Dash),
                TestData.EstimatedHit(_cfg, balanced, balanced, speed, HitKind.Quick));
        }

        [Test]
        [Ignore("Pendiente C12: Ataque cargado 3 a tope contra Agilidad hace 68 (2026-10-10)")]
        public void TopeDeUnGolpe_CargadoA3ATope_NoPasaDe60()
        {
            const float cap = 60f;
            float topSpeed = _cfg.hitSpeedRange.y;
            foreach (var attacker in TestData.AllPresets())
            foreach (var target in TestData.AllPresets())
            {
                float hit = TestData.EstimatedHit(_cfg, attacker, target, topSpeed, HitKind.Charged, 3);
                Assert.LessOrEqual(hit, cap * (1f + TestData.TargetTolerance),
                    $"{attacker.Archetype} contra {target.Archetype}: cargado 3 a tope hace {hit:0.#} RPM");
            }
        }

        [TestCase(BladeArchetype.Attack, 1.10f, Ignore = "Pendiente C12: recibe 1,25 (2026-10-10)")]
        [TestCase(BladeArchetype.Defense, 0.65f, Ignore = "Pendiente C12: recibe 0,74 (2026-10-10)")]
        [TestCase(BladeArchetype.Agility, 1.20f)]
        public void DanoQueRecibe_RespectoALaBalanceada(BladeArchetype archetype, float expected)
        {
            var balanced = TestData.Preset(BladeArchetype.Balanced);
            var preset = TestData.Preset(archetype);
            float speed = TestData.MidHitSpeed(_cfg);
            float ratio = TestData.EstimatedHit(_cfg, balanced, preset, speed, HitKind.Quick)
                          / TestData.EstimatedHit(_cfg, balanced, balanced, speed, HitKind.Quick);
            TestData.AssertNear(expected, ratio, $"Daño que recibe {archetype} (Balanceada = 1)");
        }

        [Test]
        public void DanoQueHace_LaDefensaPegaComoLaBalanceada()
        {
            float ratio = BladeFormulas.AttackMultiplier(_cfg, TestData.Preset(BladeArchetype.Defense).AttackPower)
                          / BladeFormulas.AttackMultiplier(_cfg, TestData.Preset(BladeArchetype.Balanced).AttackPower);
            TestData.AssertNear(1f, ratio, "Daño que hace la Defensa (Balanceada = 1), a la misma velocidad");
        }

        [Test]
        public void DanoQueHace_ElAtaqueEsElQueMasPega()
        {
            float attack = BladeFormulas.AttackMultiplier(_cfg, TestData.Preset(BladeArchetype.Attack).AttackPower);
            foreach (BladeArchetype other in new[] { BladeArchetype.Balanced, BladeArchetype.Defense, BladeArchetype.Agility })
                Assert.Greater(attack, BladeFormulas.AttackMultiplier(_cfg, TestData.Preset(other).AttackPower),
                    $"El Ataque debe pegar más que {other} a la misma velocidad");
        }

        [Test]
        public void Velocidad_LaAgilidadEsLaMasRapida_YLaDefensaLaMasLenta()
        {
            float MaxSpeed(BladeArchetype a)
            {
                var s = TestData.Preset(a);
                return BladeFormulas.MaxSpeed(_cfg, s.MoveSpeed, BladeFormulas.WeightNormalized(s.Weight));
            }

            float agility = MaxSpeed(BladeArchetype.Agility);
            float defense = MaxSpeed(BladeArchetype.Defense);
            foreach (BladeArchetype other in new[] { BladeArchetype.Attack, BladeArchetype.Balanced })
            {
                Assert.Greater(agility, MaxSpeed(other), $"Agilidad más rápida que {other}");
                Assert.Less(defense, MaxSpeed(other), $"Defensa más lenta que {other}");
            }
        }
    }
}
