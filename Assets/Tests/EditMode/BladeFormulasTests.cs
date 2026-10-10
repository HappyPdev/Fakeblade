using FakeBlade.Core;
using NUnit.Framework;
using UnityEngine;

namespace FakeBlade.Tests
{
    /// <summary>
    /// Propiedades de las fórmulas de BladeFormulas (GDD 2.8) con el CombatConfig real. No fijan valores
    /// exactos: comprueban límites y sentido (más peso, menos aceleración...), que deben seguir valiendo
    /// aunque se ajusten los números.
    /// </summary>
    public class BladeFormulasTests
    {
        private CombatConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = TestData.Config;

        [Test]
        public void Peso_EnLaReferenciaVale1()
        {
            Assert.AreEqual(1f, BladeFormulas.WeightRatio(_cfg, _cfg.referenceWeight), 1e-4f);
            Assert.AreEqual(_cfg.referenceImpulse, BladeFormulas.ImpulseFactor(_cfg, _cfg.referenceWeight), 1e-4f);
            Assert.AreEqual(_cfg.referenceAcceleration, BladeFormulas.Acceleration(_cfg, 6f, _cfg.referenceWeight), 1e-3f);
        }

        [Test]
        public void VelocidadMaxima_DentroDeLimites_YSubeConLaVelocidad()
        {
            for (float speed = 0f; speed <= 20f; speed += 1f)
                Assert.That(BladeFormulas.MaxSpeed(_cfg, speed), Is.InRange(3f, 25f));
            Assert.Greater(BladeFormulas.MaxSpeed(_cfg, 10f), BladeFormulas.MaxSpeed(_cfg, 6f));
        }

        [Test]
        public void Aceleracion_MasPesoAceleraMenos_SinTopeArriba()
        {
            // C11: sin topes de peso, un jefe de peso 10 sigue siendo distinto de uno de 5, y se mueve
            float previous = float.MaxValue;
            for (float weight = 1f; weight <= 10f; weight += 0.5f)
            {
                float accel = BladeFormulas.Acceleration(_cfg, 6f, weight);
                Assert.Less(accel, previous, $"peso {weight}");
                Assert.Greater(accel, 0f, $"peso {weight}");
                previous = accel;
            }
        }

        [Test]
        public void Aceleracion_LaVelocidadSoloCuentaPorEncimaDe10()
        {
            Assert.AreEqual(BladeFormulas.Acceleration(_cfg, 6f, 3f), BladeFormulas.Acceleration(_cfg, 10f, 3f), 1e-4f);
            Assert.Greater(BladeFormulas.Acceleration(_cfg, 13f, 3f), BladeFormulas.Acceleration(_cfg, 10f, 3f));
        }

        [Test]
        public void Impulso_LaLigeraSaleMasQueLaPesada_SinTopeArriba()
        {
            Assert.Greater(BladeFormulas.ImpulseFactor(_cfg, 1.5f), BladeFormulas.ImpulseFactor(_cfg, 3f));
            Assert.Greater(BladeFormulas.ImpulseFactor(_cfg, 5f), BladeFormulas.ImpulseFactor(_cfg, 10f));
            Assert.Greater(BladeFormulas.ImpulseFactor(_cfg, 10f), 0f);
        }

        [Test]
        public void Agarre_SubeElGiroYElFrenado_Y0EsElDeCombatConfig()
        {
            Assert.AreEqual(_cfg.turnRate, BladeFormulas.TurnRate(_cfg, 0f), 1e-4f);
            Assert.AreEqual(_cfg.stoppingRate, BladeFormulas.StoppingRate(_cfg, 0f), 1e-4f);
            Assert.Greater(BladeFormulas.TurnRate(_cfg, 3f), BladeFormulas.TurnRate(_cfg, 0f));
            Assert.Less(BladeFormulas.TurnRate(_cfg, -3f), BladeFormulas.TurnRate(_cfg, 0f));
            Assert.Greater(BladeFormulas.StoppingRate(_cfg, 3f), BladeFormulas.StoppingRate(_cfg, -3f));
            Assert.Greater(BladeFormulas.TurnRate(_cfg, -50f), 0f, "con muy poco agarre sigue pudiendo girar");
            Assert.Greater(BladeFormulas.StoppingRate(_cfg, -50f), 0f, "con muy poco agarre sigue frenando algo");
        }

        [Test]
        public void Ataque_EnElDeReferenciaEsX1_YSubeConElAtaque()
        {
            Assert.AreEqual(1f, BladeFormulas.AttackMultiplier(_cfg, _cfg.referenceAttackPower), 1e-4f);
            Assert.Greater(BladeFormulas.AttackMultiplier(_cfg, 20f), BladeFormulas.AttackMultiplier(_cfg, 10f));
            Assert.Greater(BladeFormulas.AttackMultiplier(_cfg, 1f), 0f, "con el ataque mínimo sigue haciendo daño");
        }

        [Test]
        public void Defensa_SinDefensaRecibeTodo_YConLaMaximaRecibeAlgo()
        {
            Assert.AreEqual(1f, BladeFormulas.DamageTakenFactor(0f), 1e-4f);
            Assert.AreEqual(1f, BladeFormulas.KnockbackTakenFactor(0f), 1e-4f);
            Assert.Less(BladeFormulas.DamageTakenFactor(30f), BladeFormulas.DamageTakenFactor(10f));
            // 80 es el tope de defensa (FakeBladeStats.Calculate)
            Assert.Greater(BladeFormulas.DamageTakenFactor(80f), 0f);
            Assert.Greater(BladeFormulas.KnockbackTakenFactor(80f), 0f);
        }

        [Test]
        public void FactorDeVelocidadDelGolpe_DeCeroAlTope()
        {
            Vector2 range = _cfg.hitSpeedRange;
            Vector2 factor = _cfg.hitSpeedFactor;
            Assert.AreEqual(0f, BladeFormulas.HitSpeedFactor(_cfg, 0f), 1e-4f, "un roce parado no quita");
            Assert.AreEqual(factor.x, BladeFormulas.HitSpeedFactor(_cfg, range.x), 1e-4f);
            Assert.AreEqual(factor.y, BladeFormulas.HitSpeedFactor(_cfg, range.y), 1e-4f);
            Assert.AreEqual(factor.y, BladeFormulas.HitSpeedFactor(_cfg, range.y * 3f), 1e-4f, "no pasa del tope");

            float previous = -1f;
            for (float speed = 0f; speed <= range.y * 1.5f; speed += 0.5f)
            {
                float f = BladeFormulas.HitSpeedFactor(_cfg, speed);
                Assert.GreaterOrEqual(f, previous, $"velocidad {speed}");
                previous = f;
            }
        }
    }
}
