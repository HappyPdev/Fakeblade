using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Poderes especiales (GDD 5). Los otorga el Núcleo (Core) equipado.
    /// Los valores numéricos se serializan en los assets: no reordenar ni reutilizar números.
    /// Los datos y el comportamiento de cada uno están en su asset de Resources/SpecialAbilities;
    /// un poder sin asset todavía usa Spin Boost (con un aviso).
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

        /// <summary>Defensa casi al 100% (sustituye a Storm Breaker; antes se llamaba Shield).</summary>
        [InspectorName("Defensa")]
        Defense = 3,

        /// <summary>Modo cargado: empuja fuerte y su golpe fuerte lanza al rival (sustituye a Dash eléctrico; antes se llamaba Dash).</summary>
        [InspectorName("Rayos")]
        Lightning = 4,

        /// <summary>Los golpes queman (sustituye a Rastro de fuego).</summary>
        [InspectorName("Fuego")]
        Fire = 5,

        /// <summary>Los golpes congelan.</summary>
        [InspectorName("Hielo")]
        Ice = 6,

        /// <summary>Clon fantasma que molesta a los rivales. Quehaceres C5.</summary>
        [InspectorName("Fantasma")]
        Ghost = 7
    }
}
