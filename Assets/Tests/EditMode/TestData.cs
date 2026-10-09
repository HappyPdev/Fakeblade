using System.Collections.Generic;
using System.Linq;
using FakeBlade.Core;
using NUnit.Framework;
using UnityEditor;

namespace FakeBlade.Tests
{
    /// <summary>Tipo de golpe para estimar el daño (GDD 2.8).</summary>
    public enum HitKind { Quick, Charged, Dash }

    /// <summary>
    /// Datos reales del juego para los tests: catálogo, CombatConfig, base del prefab y presets ya calculados.
    /// Los tests usan los assets de verdad, así que fallan si un cambio de valores se sale de lo acordado.
    /// </summary>
    public static class TestData
    {
        public const string CatalogPath = "Assets/Settings/FakeBladeCatalog.asset";
        public const string PartsFolder = "Assets/Settings/ComponentsData";

        /// <summary>Tolerancia de los objetivos del GDD 2.7 (la misma que marca el banco de equilibrio).</summary>
        public const float TargetTolerance = 0.10f;

        public static FakeBladeCatalog Catalog
        {
            get
            {
                var catalog = AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>(CatalogPath);
                Assert.IsNotNull(catalog, $"No se encuentra el catálogo en {CatalogPath}");
                return catalog;
            }
        }

        public static CombatConfig Config
        {
            get
            {
                var cfg = Catalog.combatConfig;
                Assert.IsNotNull(cfg, "El catálogo no tiene CombatConfig");
                return cfg;
            }
        }

        /// <summary>Piezas de ComponentsData sin las de LEGACY (lo mismo que lee el exportador).</summary>
        public static List<(string path, FakeBladeComponentData part)> FolderParts() =>
            AssetDatabase.FindAssets("t:FakeBladeComponentData", new[] { PartsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Replace('\\', '/').Contains("/LEGACY/"))
                .Select(p => (p, AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(p)))
                .ToList();

        /// <summary>Estadísticas finales del preset de un arquetipo.</summary>
        public static BladeStatBlock Preset(BladeArchetype archetype)
        {
            var catalog = Catalog;
            foreach (BladePreset p in catalog.presets)
            {
                var s = FakeBladeStats.Calculate(catalog.GetBaseStats(), p.tip, p.body, p.blade, p.core);
                if (s.Archetype == archetype) return s;
            }
            Assert.Fail($"No hay preset de {archetype} en el catálogo");
            return default;
        }

        public static IEnumerable<BladeStatBlock> AllPresets()
        {
            var catalog = Catalog;
            return catalog.presets.Select(p => FakeBladeStats.Calculate(catalog.GetBaseStats(), p.tip, p.body, p.blade, p.core));
        }

        /// <summary>
        /// Daño (RPM) que hace un golpe que gana el choque, como en CollisionResolver.Resolve +
        /// FakeBladeController.ApplyDamage: golpe base × velocidad × ataque × carga × tipo de golpe, y en quien
        /// lo recibe defensa × multiplicador global × su rasgo de daño recibido. Sin combo, poderes, Golpe lento
        /// ni Espinas. Si cambia la fórmula del choque, hay que cambiarla también aquí.
        /// </summary>
        public static float EstimatedHit(CombatConfig cfg, BladeStatBlock attacker, BladeStatBlock target,
            float damageSpeed, HitKind kind, int chargeLevel = 0)
        {
            float hitType;
            switch (kind)
            {
                case HitKind.Quick:
                    hitType = cfg.attackHitDamageMultiplier * attacker.Traits.Multiplier(PartTraitType.QuickHitDamage);
                    chargeLevel = 0;
                    break;
                case HitKind.Charged:
                    hitType = cfg.attackHitDamageMultiplier * attacker.Traits.Multiplier(PartTraitType.ChargedHitDamage);
                    break;
                default:
                    hitType = cfg.dashHitDamageMultiplier * attacker.Traits.Multiplier(PartTraitType.DashHitDamage);
                    chargeLevel = 0;
                    break;
            }

            float dealt = cfg.hitBaseDamage * BladeFormulas.HitSpeedFactor(cfg, damageSpeed)
                          * BladeFormulas.AttackMultiplier(cfg, attacker.AttackPower)
                          * BladeFormulas.ChargeDamageMultiplier(cfg, chargeLevel) * hitType;
            return dealt * BladeFormulas.DamageTakenFactor(target.Defense) * cfg.damageMultiplier
                   * target.Traits.Multiplier(PartTraitType.DamageTaken);
        }

        /// <summary>Velocidad de choque en la que el golpe vale ×1 (mitad del rango de hitSpeedRange).</summary>
        public static float MidHitSpeed(CombatConfig cfg) => (cfg.hitSpeedRange.x + cfg.hitSpeedRange.y) * 0.5f;

        public static void AssertNear(float expected, float actual, string what) =>
            Assert.That(actual, Is.InRange(expected * (1f - TargetTolerance), expected * (1f + TargetTolerance)),
                $"{what}: sale {actual:0.###}, objetivo {expected:0.###} ±{TargetTolerance:P0}");
    }
}
