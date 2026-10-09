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
        public void PesoNormalizado_VaDe0a1EntreLigeraYPesada()
        {
            Assert.AreEqual(0f, BladeFormulas.WeightNormalized(0.5f), 1e-4f);
            Assert.AreEqual(1f, BladeFormulas.WeightNormalized(3f), 1e-4f);
            Assert.AreEqual(0f, BladeFormulas.WeightNormalized(0.1f), 1e-4f, "por debajo de 0,5 no baja de 0");
            Assert.AreEqual(1f, BladeFormulas.WeightNormalized(5f), 1e-4f, "por encima de 3 no pasa de 1");
            Assert.AreEqual(0.5f, BladeFormulas.WeightNormalized(1.75f), 1e-4f);
        }

        [Test]
        public void VelocidadMaxima_DentroDeLimites_SubeConVelocidadYBajaConPeso()
        {
            for (float speed = 0f; speed <= 20f; speed += 1f)
            for (float wn = 0f; wn <= 1f; wn += 0.25f)
                Assert.That(BladeFormulas.MaxSpeed(_cfg, speed, wn), Is.InRange(3f, 25f));

            Assert.Greater(BladeFormulas.MaxSpeed(_cfg, 10f, 0.5f), BladeFormulas.MaxSpeed(_cfg, 6f, 0.5f));
            Assert.Greater(BladeFormulas.MaxSpeed(_cfg, 6f, 0f), BladeFormulas.MaxSpeed(_cfg, 6f, 1f));
        }

        [Test]
        public void Aceleracion_MasPesoAceleraMenos()
        {
            float previous = float.MaxValue;
            for (float weight = 0.5f; weight <= 4f; weight += 0.5f)
            {
                float accel = BladeFormulas.Acceleration(_cfg, 6f, weight);
                Assert.Less(accel, previous, $"peso {weight}");
                previous = accel;
            }
        }

        [Test]
        public void Frenado_MasPesoFrenaMenos()
        {
            Assert.Greater(BladeFormulas.StoppingRate(_cfg, 0.5f), BladeFormulas.StoppingRate(_cfg, 3f));
        }

        [Test]
        public void Impulso_LaLigeraSaleMasQueLaPesada()
        {
            Assert.AreEqual(1.15f, BladeFormulas.ImpulseFactor(0f), 1e-4f);
            Assert.AreEqual(0.85f, BladeFormulas.ImpulseFactor(1f), 1e-4f);
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
