using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Poderes especiales (GDD 5). Los otorga el Núcleo (Core) equipado.
    /// Los valores numéricos se serializan en los assets: no reordenar.
    /// </summary>
    public enum SpecialAbilityType
    {
        /// <summary>Sin poder (se usa Spin Boost por defecto).</summary>
        None = 0,

        /// <summary>Recupera RPM progresivamente mientras está activo.</summary>
        [InspectorName("Spin Boost")]
        SpinBoost = 1,

        /// <summary>Onda de choque que empuja y quita RPM a los enemigos cercanos.</summary>
        [InspectorName("Onda de choque")]
        ShockWave = 2,

        /// <summary>Storm Breaker: menos daño, gran resistencia al empuje y ataques rápidos como cargados.</summary>
        [InspectorName("Storm Breaker (escudo)")]
        Shield = 3,

        /// <summary>Dash eléctrico: dashes más largos casi sin cooldown y carga de ataque más rápida.</summary>
        [InspectorName("Dash eléctrico")]
        Dash = 4
    }
}
