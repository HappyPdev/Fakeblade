using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador principal de la peonza (FakeBlade). Gestiona f�sica, movimiento y combate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FakeBladeController : MonoBehaviour
    {
        #region Constants
        private const float MIN_SPIN_THRESHOLD = 0.1f;
        private const float COLLISION_DAMAGE_MULTIPLIER = 0.1f;
        private const float SPECIAL_VERTICAL_FORCE = 3f;
        private const float SPECIAL_SPIN_BONUS = 100f;
        #endregion

        #region Events
        public event Action<float> OnSpinChanged;
        public event Action OnDashExecuted;
        public event Action OnSpecialExecuted;
        public event Action<FakeBladeController, float> OnCollisionDamage;
        #endregion

        #region Serialized Fields
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float dashForce = 15f;
        [SerializeField] private float dashCooldown = 2f;

        [Header("Rotation/Spin")]
        [SerializeField] private float maxSpinSpeed = 1000f;
        [SerializeField] private float spinDecay = 5f;

        [Header("Combat")]
        [SerializeField] private float attackPower = 10f;
        [SerializeField] private float weight = 1f;
        [SerializeField] private float minCollisionVelocity = 2f;

        [Header("Physics")]
        [SerializeField] private float drag = 0.5f;
        [SerializeField] private float angularDrag = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
        private Rigidbody rb;
        private PlayerController playerController;
        private FakeBladeStats stats;

        private float currentSpinSpeed;
        private float dashTimer;
        private bool canDash = true;
        private bool isDestroyed = false;
        #endregion

        #region Properties
        public float SpinSpeedPercentage => maxSpinSpeed > 0 ? currentSpinSpeed / maxSpinSpeed : 0f;
        public float CurrentSpinSpeed => currentSpinSpeed;
        public bool CanDash => canDash;
        public float Weight => weight;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CacheComponents();
            ConfigurePhysics();
        }

        private void Start()
        {
            ResetFakeBlade();
        }

        private void Update()
        {
            if (isDestroyed) return;

            UpdateSpin();
            UpdateDashCooldown();

            if (showDebugInfo)
            {
                DrawDebugInfo();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isDestroyed) return;

            HandleCollision(collision);
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            stats = GetComponent<FakeBladeStats>();

            if (rb == null)
            {
                Debug.LogError($"[FakeBladeController] Rigidbody missing on {gameObject.name}");
            }
        }

        private void ConfigurePhysics()
        {
            if (rb != null)
            {
                rb.mass = weight;
                rb.linearDamping = drag;
                rb.angularDamping = angularDrag;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }
        #endregion

        #region Spin System
        private void UpdateSpin()
        {
            // P�rdida natural de velocidad
            currentSpinSpeed = Mathf.Max(0f, currentSpinSpeed - spinDecay * Time.deltaTime);

            // Rotaci�n visual
            transform.Rotate(Vector3.up, currentSpinSpeed * Time.deltaTime, Space.Self);

            OnSpinChanged?.Invoke(SpinSpeedPercentage);

            // Check para eliminaci�n
            if (currentSpinSpeed <= MIN_SPIN_THRESHOLD)
            {
                HandleSpinOut();
            }
        }

        public void ReduceSpin(float amount)
        {
            if (isDestroyed) return;

            currentSpinSpeed = Mathf.Max(0f, currentSpinSpeed - amount);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Spin reduced by {amount}. Current: {currentSpinSpeed:F1}/{maxSpinSpeed}");
            }
        }

        private void HandleSpinOut()
        {
            if (isDestroyed) return;

            isDestroyed = true;

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] {gameObject.name} eliminated by spin-out!");
            }

            // Liberar constraints para ca�da dram�tica
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.None;
            }

            // Notificar al player controller
            if (playerController != null)
            {
                playerController.OnFakeBladeDestroyed();
            }

            // Desactivar despu�s de un peque�o delay
            Invoke(nameof(DeactivateFakeBlade), 1f);
        }

        private void DeactivateFakeBlade()
        {
            gameObject.SetActive(false);
        }
        #endregion

        #region Movement
        public void HandleMovement(Vector2 input)
        {
            if (isDestroyed || rb == null) return;

            // Convertir input 2D a movimiento 3D
            Vector3 movement = new Vector3(input.x, 0f, input.y).normalized * moveSpeed;

            // Aplicar velocidad manteniendo la Y actual
            rb.linearVelocity = new Vector3(movement.x, rb.linearVelocity.y, movement.z);
        }

        public void ExecuteDash()
        {
            if (!canDash || isDestroyed || rb == null) return;

            // Direcci�n del dash
            Vector3 dashDirection = rb.linearVelocity.normalized;

            // Si est� quieto, dash hacia adelante
            if (dashDirection.sqrMagnitude < 0.01f)
            {
                dashDirection = transform.forward;
            }

            rb.AddForce(dashDirection * dashForce, ForceMode.Impulse);

            canDash = false;
            dashTimer = dashCooldown;

            OnDashExecuted?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Dash executed! Direction: {dashDirection}");
            }
        }

        public void ExecuteSpecial()
        {
            if (isDestroyed || rb == null) return;

            // Impulso vertical + recuperaci�n de spin
            rb.AddForce(Vector3.up * SPECIAL_VERTICAL_FORCE, ForceMode.Impulse);
            currentSpinSpeed = Mathf.Min(maxSpinSpeed, currentSpinSpeed + SPECIAL_SPIN_BONUS);

            OnSpecialExecuted?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Special executed! Spin: {currentSpinSpeed:F1}");
            }
        }

        private void UpdateDashCooldown()
        {
            if (!canDash)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    canDash = true;
                }
            }
        }
        #endregion

        #region Combat
        private void HandleCollision(Collision collision)
        {
            // Filtrar colisiones de baja velocidad
            if (collision.relativeVelocity.magnitude < minCollisionVelocity)
                return;

            // Detectar colisi�n con otra FakeBlade
            FakeBladeController otherFakeBlade = collision.gameObject.GetComponent<FakeBladeController>();

            if (otherFakeBlade != null && !otherFakeBlade.isDestroyed)
            {
                ProcessFakeBladeCollision(otherFakeBlade, collision);
            }
        }

        private void ProcessFakeBladeCollision(FakeBladeController other, Collision collision)
        {
            // Calcular da�o basado en velocidad relativa y estad�sticas
            float relativeSpeed = collision.relativeVelocity.magnitude;
            float baseDamage = relativeSpeed * attackPower * COLLISION_DAMAGE_MULTIPLIER;

            // Factor de peso (el m�s pesado hace m�s da�o)
            float weightRatio = other.weight / weight;
            float damageToSelf = baseDamage * weightRatio;
            float damageToOther = baseDamage * (weight / other.weight);

            // Aplicar da�o
            ReduceSpin(damageToSelf);
            other.ReduceSpin(damageToOther);

            // Disparar eventos
            OnCollisionDamage?.Invoke(other, damageToOther);
            other.OnCollisionDamage?.Invoke(this, damageToSelf);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Collision! Speed: {relativeSpeed:F1}, Damage dealt: {damageToOther:F1}, received: {damageToSelf:F1}");
            }
        }
        #endregion

        #region Stats Application
        public void ApplyStats(FakeBladeStats newStats)
        {
            if (newStats == null)
            {
                Debug.LogWarning("[FakeBladeController] Attempted to apply null stats");
                return;
            }

            stats = newStats;
            moveSpeed = stats.MoveSpeed;
            maxSpinSpeed = stats.MaxSpin;
            attackPower = stats.AttackPower;
            weight = stats.Weight;
            dashForce = stats.DashForce;

            ConfigurePhysics();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Stats applied - Speed: {moveSpeed}, Spin: {maxSpinSpeed}, Attack: {attackPower}, Weight: {weight}");
            }
        }
        #endregion

        #region Reset
        public void ResetFakeBlade()
        {
            currentSpinSpeed = maxSpinSpeed;
            canDash = true;
            dashTimer = 0f;
            isDestroyed = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Reset complete. Spin: {currentSpinSpeed}");
            }
        }
        #endregion

        #region Debug
        private void DrawDebugInfo()
        {
            // L�nea de velocidad
            Debug.DrawRay(transform.position, rb.linearVelocity, Color.green);

            // C�rculo de rango de dash
            if (!canDash)
            {
                Debug.DrawRay(transform.position, Vector3.up * 2f, Color.red);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Visualizar spin con color
            Gizmos.color = Color.Lerp(Color.red, Color.green, SpinSpeedPercentage);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
        #endregion
    }
}