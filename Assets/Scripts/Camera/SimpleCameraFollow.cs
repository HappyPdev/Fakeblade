using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Cámara dinámica que encuadra a todas las peonzas vivas (GDD 6 / v2).
    /// Mantiene la dirección de vista inicial y ajusta la distancia según la dispersión.
    /// El temblor de CameraShake se suma encima sin acumular deriva.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("=== CAMERA SETTINGS ===")]
        [Tooltip("Distancia mínima (peonzas juntas)")]
        public float minZoom = 10f;
        [Tooltip("Distancia máxima (peonzas separadas)")]
        public float maxZoom = 22f;
        [Tooltip("Separación a partir de la cual se usa la distancia máxima")]
        public float zoomLimiter = 12f;
        [Tooltip("Velocidad de suavizado")]
        public float smoothSpeed = 5f;
        [Tooltip("Offset vertical extra")]
        public float heightOffset = 2f;

        private readonly List<Transform> _targets = new List<Transform>(4);
        private Vector3 _viewDirection = new Vector3(0f, 1f, -0.5f).normalized;
        private Vector3 _basePosition;
        private bool _hasBase;

        public void SetTargets(GameObject[] targets)
        {
            _targets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] != null) _targets.Add(targets[i].transform);
            }

            _basePosition = transform.position;
            _hasBase = true;

            if (TryGetBounds(out Vector3 center, out _))
            {
                Vector3 offset = _basePosition - center;
                if (offset.sqrMagnitude > 0.01f) _viewDirection = offset.normalized;
            }
        }

        private void LateUpdate()
        {
            if (!_hasBase)
            {
                _basePosition = transform.position;
                _hasBase = true;
            }

            if (TryGetBounds(out Vector3 center, out float spread))
            {
                float zoom = Mathf.Lerp(minZoom, maxZoom, spread / Mathf.Max(0.01f, zoomLimiter));
                Vector3 target = center + _viewDirection * zoom + Vector3.up * heightOffset;
                _basePosition = Vector3.Lerp(_basePosition, target, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));

                transform.position = _basePosition + CameraShake.CurrentOffset;
                transform.LookAt(center + Vector3.up * 0.5f);
            }
            else
            {
                transform.position = _basePosition + CameraShake.CurrentOffset;
            }
        }

        /// <summary>Centro de las peonzas activas y separación máxima (diámetro).</summary>
        private bool TryGetBounds(out Vector3 center, out float spread)
        {
            center = Vector3.zero;
            spread = 0f;
            int count = 0;

            for (int i = 0; i < _targets.Count; i++)
            {
                Transform t = _targets[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                center += t.position;
                count++;
            }

            if (count == 0) return false;
            center /= count;

            float maxSqr = 0f;
            for (int i = 0; i < _targets.Count; i++)
            {
                Transform t = _targets[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                float sqr = (t.position - center).sqrMagnitude;
                if (sqr > maxSqr) maxSqr = sqr;
            }

            spread = Mathf.Sqrt(maxSqr) * 2f;
            return true;
        }
    }
}
