using FakeBlade.Core;
using NUnit.Framework;

namespace FakeBlade.Tests
{
    /// <summary>
    /// Sentido del duelo estimado del banco (C14, DuelEstimator): con las mismas peonzas empatan, y el
    /// desgaste, los costes y la curación cambian la duración hacia donde deben.
    /// </summary>
    public class DuelEstimatorTests
    {
        private const float SecondsPerHit = 4.2f;

        private static DuelEstimator.Side Plain() => new DuelEstimator.Side
        {
            MaxSpin = 400f, Decay = 0.5f, HitDamage = 20f, SelfDamagePerHit = 2f, CostPerHit = 8f,
            EnergyPerHit = 0.1f, SpecialDuration = 5f, HealPerSpecial = 75f
        };

        [Test]
        public void Espejo_Empata()
        {
            var r = DuelEstimator.Simulate(Plain(), Plain(), SecondsPerHit);
            Assert.AreEqual(-1, r.Winner);
            Assert.Greater(r.Seconds, 0f);
        }

        [Test]
        public void MasDesgaste_Pierde()
        {
            var worn = Plain();
            worn.Decay = 3f;
            var r = DuelEstimator.Simulate(worn, Plain(), SecondsPerHit);
            Assert.AreEqual(1, r.Winner, "la de más desgaste cae antes");
        }

        [Test]
        public void CostesYDesgaste_AcortanElDuelo()
        {
            var free = Plain();
            free.Decay = 0f;
            free.CostPerHit = 0f;
            float withCosts = DuelEstimator.Simulate(Plain(), Plain(), SecondsPerHit).Seconds;
            float withoutCosts = DuelEstimator.Simulate(free, free, SecondsPerHit).Seconds;
            Assert.Less(withCosts, withoutCosts);
        }

        [Test]
        public void LaCuracion_AlargaElDuelo_YGanaQuienSeCura()
        {
            var noHeal = Plain();
            noHeal.HealPerSpecial = 0f;
            float healed = DuelEstimator.Simulate(Plain(), Plain(), SecondsPerHit).Seconds;
            float plain = DuelEstimator.Simulate(noHeal, noHeal, SecondsPerHit).Seconds;
            Assert.Greater(healed, plain);
            Assert.AreEqual(0, DuelEstimator.Simulate(Plain(), noHeal, SecondsPerHit).Winner);
        }

        [Test]
        public void SinCostesNiCuracion_DuraLoQueLosGolpes()
        {
            // Sin costes, desgaste ni curación, el duelo dura lo que tarda en caer la primera
            var s = Plain();
            s.Decay = 0f; s.CostPerHit = 0f; s.SelfDamagePerHit = 0f; s.HealPerSpecial = 0f;
            float hits = UnityEngine.Mathf.Ceil(s.MaxSpin / s.HitDamage);
            Assert.AreEqual(hits * SecondsPerHit, DuelEstimator.Simulate(s, s, SecondsPerHit).Seconds, 0.1f);
        }
    }
}
