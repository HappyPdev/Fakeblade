using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Componente que almacena las estadísticas de una FakeBlade.
    /// Se calculará a partir de las partes modulares.
    /// </summary>
    public class FakeBladeStats : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Movement Stats")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float dashForce = 15f;

        [Header("Spin Stats")]
        [SerializeField] private float maxSpin = 1000f;
        [SerializeField] private float spinResistance = 10f;

        [Header("Combat Stats")]
        [SerializeField] private float attackPower = 10f;
        [SerializeField] private float weight = 1f;
        [SerializeField] private float defense = 5f;
        #endregion

        #region Properties
        public float MoveSpeed => moveSpeed;
        public float DashForce => dashForce;
        public float MaxSpin => maxSpin;
        public float SpinResistance => spinResistance;
        public float AttackPower => attackPower;
        public float Weight => weight;
        public float Defense => defense;
        #endregion

        #region Public Methods
        /// <summary>
        /// Recalcula las stats basándose en las partes equipadas
        /// </summary>
        public void RecalculateStats(/* Aquí irán las partes modulares */)
        {
            // TODO: Implementar cuando tengamos el sistema modular
            Debug.Log("[FakeBladeStats] Stats recalculated");
        }

        public void SetStats(float speed, float dash, float spin, float attack, float mass)
        {
            moveSpeed = speed;
            dashForce = dash;
            maxSpin = spin;
            attackPower = attack;
            weight = mass;
        }
        #endregion
    }
}