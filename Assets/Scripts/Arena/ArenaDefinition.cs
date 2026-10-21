using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Raíz de un prefab de arena (GDD 4). Define el radio jugable y genera los
    /// puntos de aparición en círculo, proyectados sobre el suelo (vale para suelos en cuenco).
    /// </summary>
    public class ArenaDefinition : MonoBehaviour
    {
        [Tooltip("Radio interior jugable (hasta el muro), en metros de mundo")]
        [SerializeField] private float innerRadius = 6f;
        [Tooltip("Distancia de los spawns al centro, como fracción del radio interior")]
        [SerializeField][Range(0.1f, 0.9f)] private float spawnRadiusPercent = 0.5f;
        [Tooltip("Altura de aparición sobre el suelo")]
        [SerializeField] private float spawnHeight = 0.6f;
        [SerializeField] private float spawnAngleOffset = 90f;
        [Tooltip("Spawns fijos opcionales. Si no hay suficientes, se generan en círculo")]
        [SerializeField] private Transform[] fixedSpawnPoints;

        public Vector3 Center => transform.position;
        public float InnerRadius => innerRadius;

        /// <summary>La arena de la escena (la última activada), o null. Para los efectos que no deben salir de ella.</summary>
        public static ArenaDefinition Current { get; private set; }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Ancho máximo (m) de algo redondo centrado en point para no pasar del muro (sin arena, sin límite). Para la marca y
        /// la onda del Salto junto a la pared (2026-10-21).
        /// </summary>
        public static float MaxWidthAt(Vector3 point)
        {
            ArenaDefinition arena = Current;
            if (arena == null) return float.MaxValue;
            Vector3 offset = point - arena.Center;
            offset.y = 0f;
            return Mathf.Max(1.5f, 2f * (arena.innerRadius - offset.magnitude));
        }

        /// <summary>Crea (o reutiliza) count puntos de aparición repartidos en círculo.</summary>
        public Transform[] CreateSpawnPoints(int count)
        {
            count = Mathf.Max(1, count);
            if (fixedSpawnPoints != null && fixedSpawnPoints.Length >= count)
            {
                var result = new Transform[count];
                System.Array.Copy(fixedSpawnPoints, result, count);
                return result;
            }

            Physics.SyncTransforms();

            var parent = new GameObject("SpawnPoints").transform;
            parent.SetParent(transform, true);

            var points = new Transform[count];
            float radius = innerRadius * spawnRadiusPercent;
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 360f / count + spawnAngleOffset) * Mathf.Deg2Rad;
                Vector3 pos = Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                var point = new GameObject($"SpawnPoint_{i}").transform;
                point.SetParent(parent, true);
                point.position = ProjectToFloor(pos);
                points[i] = point;
            }
            return points;
        }

        /// <summary>Punto aleatorio dentro de la arena (sobre el suelo).</summary>
        public Vector3 RandomPoint(float radiusPercent = 0.6f)
        {
            Vector2 p = Random.insideUnitCircle * innerRadius * radiusPercent;
            return ProjectToFloor(Center + new Vector3(p.x, 0f, p.y));
        }

        public Vector3 ProjectToFloor(Vector3 position)
        {
            Vector3 origin = new Vector3(position.x, Center.y + 20f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * spawnHeight;
            return new Vector3(position.x, Center.y + spawnHeight, position.z);
        }

#if UNITY_EDITOR
        public void EditorConfigure(float radius, float height)
        {
            innerRadius = radius;
            spawnHeight = height;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            DrawCircle(Center, innerRadius);
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.8f);
            DrawCircle(Center, innerRadius * spawnRadiusPercent);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                Vector3 next = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
