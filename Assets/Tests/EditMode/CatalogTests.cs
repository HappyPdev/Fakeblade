using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FakeBlade.Core;
using NUnit.Framework;
using UnityEngine;

namespace FakeBlade.Tests
{
    /// <summary>
    /// Que el contenido esté completo y bien enlazado: catálogo, piezas, presets, poderes y textos ES/EN.
    /// Son errores que si no solo se ven jugando (una pieza sin modelo, un preset con un hueco, un texto
    /// que sale como su clave).
    /// </summary>
    public class CatalogTests
    {
        private static readonly ComponentSlot[] Slots =
            { ComponentSlot.Tip, ComponentSlot.Body, ComponentSlot.Blade, ComponentSlot.Core };

        [Test]
        public void Catalogo_TieneSusAjustes()
        {
            var catalog = TestData.Catalog;
            Assert.IsNotNull(catalog.combatConfig, "combatConfig");
            Assert.IsNotNull(catalog.uiTheme, "uiTheme");
            Assert.IsNotNull(catalog.playerPrefab, "playerPrefab");
            Assert.IsNotNull(catalog.playerPrefab.GetComponent<FakeBladeStats>(), "el prefab de jugador no tiene FakeBladeStats");
            Assert.IsNotEmpty(catalog.arenas, "arenas");
            Assert.IsTrue(catalog.arenas.All(a => a != null), "hay una arena vacía en el catálogo");
        }

        [Test]
        public void Piezas_CadaListaTieneSoloPiezasDeSuTipo()
        {
            var catalog = TestData.Catalog;
            foreach (ComponentSlot slot in Slots)
            {
                List<FakeBladeComponentData> parts = catalog.GetParts(slot);
                Assert.IsNotEmpty(parts, $"No hay piezas de {slot}");
                foreach (var part in parts)
                {
                    Assert.IsNotNull(part, $"Hueco vacío en la lista de {slot}");
                    Assert.AreEqual(slot, part.ComponentType, $"{part.name} está en la lista de {slot}");
                }
            }
        }

        [Test]
        public void Piezas_TodasLasDeComponentsDataEstanEnElCatalogo()
        {
            var catalog = TestData.Catalog;
            var inCatalog = new HashSet<FakeBladeComponentData>(Slots.SelectMany(catalog.GetParts));
            var missing = TestData.FolderParts().Where(p => !inCatalog.Contains(p.part)).Select(p => p.path).ToList();
            Assert.IsEmpty(missing, "Piezas en ComponentsData que no salen en el juego (faltan en el catálogo)");
        }

        [Test]
        public void Piezas_NombreTipoArquetipoNombre_YCoincideConLaPieza()
        {
            foreach (var (path, part) in TestData.FolderParts())
            {
                string[] bits = part.name.Split('_');
                Assert.GreaterOrEqual(bits.Length, 3, $"{path}: el nombre debe ser Tipo_Arquetipo_Nombre");
                Assert.AreEqual(part.ComponentType.ToString(), bits[0], $"{path}: el tipo del nombre no es el de la pieza");
                Assert.AreEqual(part.Archetype.ToString(), bits[1], $"{path}: el arquetipo del nombre no es el de la pieza");
            }
        }

        [Test]
        public void Piezas_TodasTienenModeloYNombre()
        {
            foreach (var (path, part) in TestData.FolderParts())
            {
                Assert.IsNotNull(part.Model, $"{path} no tiene modelo");
                Assert.IsFalse(string.IsNullOrWhiteSpace(part.ComponentName), $"{path} no tiene nombre");
            }
        }

        [Test]
        public void Nucleos_TienenPoderConSuAsset_YElRestoNinguno()
        {
            var assets = Resources.LoadAll<SpecialAbilityData>("SpecialAbilities").Select(d => d.Type).ToHashSet();
            foreach (var (path, part) in TestData.FolderParts())
            {
                if (part.ComponentType == ComponentSlot.Core)
                {
                    Assert.AreNotEqual(SpecialAbilityType.None, part.SpecialAbility, $"{path}: núcleo sin poder");
                    Assert.IsTrue(assets.Contains(part.SpecialAbility),
                        $"{path}: el poder {part.SpecialAbility} no tiene asset en Resources/SpecialAbilities");
                }
                else
                {
                    Assert.AreEqual(SpecialAbilityType.None, part.SpecialAbility, $"{path}: solo los núcleos dan poder");
                }
            }
        }

        [Test]
        public void Presets_UnoPorArquetipo_CompletosYConSuNombre()
        {
            var catalog = TestData.Catalog;
            var expected = new Dictionary<string, BladeArchetype>
            {
                ["PRESET_ATTACK"] = BladeArchetype.Attack,
                ["PRESET_BALANCED"] = BladeArchetype.Balanced,
                ["PRESET_DEFENSE"] = BladeArchetype.Defense,
                ["PRESET_AGILITY"] = BladeArchetype.Agility
            };
            CollectionAssert.AreEquivalent(expected.Keys, catalog.presets.Select(p => p.nameKey));

            foreach (BladePreset p in catalog.presets)
            {
                var parts = new[] { p.tip, p.body, p.blade, p.core };
                for (int i = 0; i < parts.Length; i++)
                {
                    Assert.IsNotNull(parts[i], $"{p.nameKey}: falta la pieza de {Slots[i]}");
                    Assert.AreEqual(Slots[i], parts[i].ComponentType, $"{p.nameKey}: {parts[i].name} en el hueco de {Slots[i]}");
                }
                var stats = FakeBladeStats.Calculate(catalog.GetBaseStats(), p.tip, p.body, p.blade, p.core);
                Assert.AreEqual(expected[p.nameKey], stats.Archetype, $"{p.nameKey}: sus piezas dan otro arquetipo");
                Assert.AreNotEqual(p.nameKey, Loc.Get(p.nameKey, Language.Spanish), $"{p.nameKey} no está en Loc");
            }
        }

        [Test]
        public void Textos_TodosEnEspanolEIngles_ConLosMismosHuecos()
        {
            var field = typeof(Loc).GetField("Table", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Loc.Table no existe (¿se ha renombrado?)");
            var table = (Dictionary<string, string[]>)field.GetValue(null);
            var holes = new Regex(@"\{\d+\}");

            foreach (var entry in table)
            {
                Assert.AreEqual(2, entry.Value.Length, $"{entry.Key}: debe tener ES y EN");
                Assert.IsFalse(string.IsNullOrEmpty(entry.Value[0]), $"{entry.Key}: falta el texto en español");
                Assert.IsFalse(string.IsNullOrEmpty(entry.Value[1]), $"{entry.Key}: falta el texto en inglés");
                var es = holes.Matches(entry.Value[0]).Select(m => m.Value).OrderBy(v => v);
                var en = holes.Matches(entry.Value[1]).Select(m => m.Value).OrderBy(v => v);
                CollectionAssert.AreEqual(es, en, $"{entry.Key}: ES y EN no tienen los mismos {{0}}, {{1}}...");
            }
        }
    }
}
