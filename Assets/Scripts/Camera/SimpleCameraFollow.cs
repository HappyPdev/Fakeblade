using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Cámara dinámica que encuadra a todas las peonzas vivas (GDD 6 / v2).
    /// Ángulo fijo (2026-10-12): nunca gira; mantiene la dirección de vista inicial y solo se desplaza y hace
    /// zoom según la dispersión, las dos cosas suavizadas por separado. El punto que sigue tira un poco hacia el
    /// centro de la arena para moverse menos. Antes miraba cada frame al centro exacto de las peonzas y "se
    /// movía un poco loca". El temblor de CameraShake se suma encima sin acumular deriva.
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
        [Tooltip("Segundos que tarda (aprox.) en alcanzar el punto que sigue. Más = más suave")]
        public float followSmoothTime = 0.4f;
        [Tooltip("Segundos que tarda (aprox.) en ajustar el zoom. Más = más suave")]
        public float zoomSmoothTime = 0.6f;
        [Tooltip("Cuánto tira el punto que sigue hacia el centro de la arena (0 = el centro de las peonzas, 1 = fijo en la arena)")]
        [Range(0f, 1f)] public float arenaCenterPull = 0.25f;
        [Tooltip("Offset vertical extra")]
        public float heightOffset = 2f;

        /// <summary>Altura sobre el suelo del punto al que mira (centro de la pantalla).</summary>
        private const float LookHeight = 0.5f;

        private readonly List<Transform> _targets = new List<Transform>(4);
        private Vector3 _viewDirection = new Vector3(0f, 1f, -0.5f).normalized;
        private bool _hasView;
        private Quaternion _rotation;
        private Vector3 _focus;
        private Vector3 _focusVelocity;
        private float _zoom;
        private float _zoomVelocity;
        private bool _hasFocus;
        private ArenaDefinition _arena;
        private Vector3 _basePosition;

        public void SetTargets(GameObject[] targets)
        {
            _targets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] != null) _targets.Add(targets[i].transform);
            }

            // La dirección de vista se toma una sola vez, de la posición inicial de la cámara en la escena
            if (!_hasView && TryGetBounds(out Vector3 center, out _))
            {
                Vector3 offset = transform.position - center;
                if (offset.sqrMagnitude > 0.01f) _viewDirection = offset.normalized;
                _hasView = true;
            }
        }

        private void LateUpdate()
        {
            if (!TryGetBounds(out Vector3 center, out float spread))
            {
                if (_hasFocus) transform.SetPositionAndRotation(_basePosition + CameraShake.CurrentOffset, _rotation);
                return;
            }

            if (_arena == null) _arena = FindAnyObjectByType<ArenaDefinition>();
            Vector3 targetFocus = _arena != null ? Vector3.Lerp(center, _arena.Center, arenaCenterPull) : center;
            float targetZoom = Mathf.Lerp(minZoom, maxZoom, spread / Mathf.Max(0.01f, zoomLimiter));

            if (!_hasFocus)
            {
                _focus = targetFocus;
                _zoom = targetZoom;
                _rotation = Quaternion.LookRotation(-CameraOffset(minZoom));
                _hasFocus = true;
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, targetFocus, ref _focusVelocity, followSmoothTime);
                _zoom = Mathf.SmoothDamp(_zoom, targetZoom, ref _zoomVelocity, zoomSmoothTime);
            }

            // Ángulo fijo: el punto que sigue queda siempre en el centro de la pantalla
            Vector3 lookPoint = _focus + Vector3.up * LookHeight;
            _basePosition = lookPoint - _rotation * Vector3.forward * CameraOffset(_zoom).magnitude;
            transform.SetPositionAndRotation(_basePosition + CameraShake.CurrentOffset, _rotation);
        }

        /// <summary>De lo que mira a la cámara, con un zoom dado (la misma geometría de antes: vista + altura extra).</summary>
        private Vector3 CameraOffset(float zoom) => _viewDirection * zoom + Vector3.up * (heightOffset - LookHeight);

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
