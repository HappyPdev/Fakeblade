using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Arena seleccionable (GDD 4): nombre, prefab y vista previa.</summary>
    [CreateAssetMenu(fileName = "ArenaData", menuName = "FakeBlade/Arena Data")]
    public class ArenaData : ScriptableObject
    {
        [Tooltip("Clave de localización o nombre directo si no existe la clave")]
        public string nameKey = "ARENA_00";
        [Tooltip("Prefab con ArenaDefinition en la raíz")]
        public GameObject prefab;
        public Sprite preview;

        public string DisplayName => Loc.Get(nameKey);
    }
}
