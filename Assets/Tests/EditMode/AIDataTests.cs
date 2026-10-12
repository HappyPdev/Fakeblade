using System.Linq;
using FakeBlade.Core;
using NUnit.Framework;
using UnityEngine;

namespace FakeBlade.Tests
{
    /// <summary>
    /// Que los assets de la IA (GDD 6.4, D2) estén completos: un nivel por dificultad, un perfil por arquetipo,
    /// cada perfil con una clase de IA y su nombre en Loc. Sin ellos la IA usa los valores por defecto del código
    /// sin avisar, y los ajustes hechos en el Inspector no se notarían.
    /// </summary>
    public class AIDataTests
    {
        [Test]
        public void Niveles_UnAssetPorDificultad()
        {
            AILevel[] levels = Resources.LoadAll<AILevel>(AILevel.ResourcesFolder);
            foreach (AIDifficulty difficulty in System.Enum.GetValues(typeof(AIDifficulty)))
            {
                int count = levels.Count(l => l.difficulty == difficulty);
                Assert.AreEqual(1, count, $"Nivel {difficulty}: {count} assets en Resources/{AILevel.ResourcesFolder} " +
                                          "(usa FakeBlade → Setup AI)");
            }
        }

        [Test]
        public void Imposible_SinFallosHumanos()
        {
            // D9: el Imposible es el Experto mejorado y sin ningún fallo humano
            AILevel level = AILevel.Get(AIDifficulty.Impossible);
            Assert.AreEqual(0f, level.perceptionDelay, "perceptionDelay");
            Assert.AreEqual(0f, level.predictionError, "predictionError");
            Assert.AreEqual(0f, level.lapseInterval, "lapseInterval");
            Assert.AreEqual(0f, level.steerWobbleDegrees, "steerWobbleDegrees");
            Assert.AreEqual(0f, level.overshootRate, "overshootRate");
        }

        [Test]
        public void Perfiles_SegunPeonzaHayUnAssetDeCadaArquetipo()
        {
            AIProfiles.ClearCache();
            foreach (BladeArchetype archetype in System.Enum.GetValues(typeof(BladeArchetype)))
            {
                AIProfile profile = AIProfiles.ForArchetype(archetype);
                Assert.AreEqual((int)archetype, (int)profile.style, $"{archetype} juega el perfil {profile.name}");
                Assert.AreNotEqual(HideFlags.DontSave, profile.hideFlags,
                    $"{archetype} no tiene asset en Resources/{AIProfile.ResourcesFolder} (usa FakeBlade → Setup AI)");
            }
        }

        [Test]
        public void Perfiles_TienenClaseDeIAYNombre()
        {
            foreach (AIProfile profile in Resources.LoadAll<AIProfile>(AIProfile.ResourcesFolder))
            {
                System.Type type = AIProfiles.BrainType(profile.style);
                Assert.IsTrue(typeof(AIBrain).IsAssignableFrom(type), $"{profile.name}: {type} no es una IA");
                Assert.AreNotEqual(profile.nameKey, Loc.Get(profile.nameKey, Language.Spanish),
                    $"{profile.name}: {profile.nameKey} no está en Loc");
            }
        }
    }
}
