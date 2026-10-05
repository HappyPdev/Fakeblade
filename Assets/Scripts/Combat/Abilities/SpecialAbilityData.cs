using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Datos de un poder especial (GDD 5). Un asset por poder en Resources/SpecialAbilities.
    /// Aquí están los valores comunes a todos los poderes; cada subclase añade los suyos y crea
    /// su comportamiento (<see cref="SpecialAbility"/>).
    ///
    /// Añadir un poder nuevo: un valor en <see cref="SpecialAbilityType"/>, una subclase de estos
    /// datos con su clase de comportamiento y su asset (menú FakeBlade/Setup Specials).
    /// </summary>
    public abstract class SpecialAbilityData : ScriptableObject
    {
        [Header("Identidad")]
        public string nameEs = "";
        public string nameEn = "";
        [Tooltip("Color del poder: aura, partículas y esfera del HUD")]
        public Color color = Color.white;
        [Tooltip("Icono pixel del poder (HUD y selección de peonzas). Opcional")]
        public Sprite icon;

        [Header("Común (GDD 5)")]
        [Tooltip("Energía necesaria para llenar la esfera. 1 = la barra estándar; más = tarda más en cargarse")]
        [Min(0.1f)] public float energyRequired = 1f;
        [Tooltip("Segundos que dura activo (la esfera se vacía en este tiempo)")]
        [Min(0.1f)] public float duration = 5f;
        [Tooltip("Intensidad del estallido de partículas al activarlo")]
        [Min(0.1f)] public float activationBurst = 1f;

        /// <summary>Poder al que corresponden estos datos (lo fija cada subclase).</summary>
        public abstract SpecialAbilityType Type { get; }

        /// <summary>Nombre en el idioma actual.</summary>
        public string DisplayName
        {
            get
            {
                string localized = Loc.Current == Language.English ? nameEn : nameEs;
                return string.IsNullOrEmpty(localized) ? name : localized;
            }
        }

        /// <summary>Crea el comportamiento de este poder para una peonza concreta.</summary>
        public abstract SpecialAbility CreateRuntime(FakeBladeController owner);
    }
}
