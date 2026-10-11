using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Lo que la IA sabe de la partida en este frame (GDD 6.4): rival más cercano, si viene un ataque
    /// y dónde está la pared. Lee los datos internos del juego; lo que el nivel ignora se queda en
    /// "no lo sabe" (TargetCharges = -1, TargetCharging = false, pared en el infinito).
    /// Clase C# sin asignaciones por frame.
    /// </summary>
    public sealed class AIPerception
    {
        /// <summary>Radio por defecto del collider de la peonza (GDD 2.1, esfera de 0,6 m), si no se puede leer.</summary>
        private const float DefaultRadius = 0.6f;
        /// <summary>Velocidad hacia la IA a partir de la que un ataque del rival "viene a por ella".</summary>
        private const float IncomingSpeed = 2f;
        /// <summary>Distancia entre bordes a la que un ataque del rival cuenta como amenaza.</summary>
        private const float ThreatGap = 4f;
        /// <summary>Por debajo de esta velocidad (m/s) el rival cuenta como quieto.</summary>
        private const float IdleSpeed = 1.5f;

        /// <summary>Radio real del collider (esfera × escala), leído una vez.</summary>
        public float BladeRadius { get; private set; }

        public FakeBladeController Target { get; private set; }
        /// <summary>Hacia el rival, en el plano y normalizado.</summary>
        public Vector3 ToTarget { get; private set; }
        /// <summary>Distancia entre centros.</summary>
        public float Distance { get; private set; }
        /// <summary>Distancia entre bordes.</summary>
        public float Gap { get; private set; }
        public Vector3 TargetVelocity { get; private set; }
        /// <summary>Velocidad a la que se acercan (positiva) o se alejan (negativa).</summary>
        public float ClosingSpeed { get; private set; }
        /// <summary>Segundos hasta chocar si siguen así (infinito si no se acercan).</summary>
        public float TimeToImpact { get; private set; }

        public bool TargetAttacking { get; private set; }
        /// <summary>El rival está cargando un ataque (solo si el nivel lo lee).</summary>
        public bool TargetCharging { get; private set; }
        /// <summary>El rival ataca (botón o dash) y viene hacia la IA.</summary>
        public bool IncomingAttack { get; private set; }
        /// <summary>Cargas de ataque del rival (-1 si el nivel no lo lee).</summary>
        public int TargetCharges { get; private set; }
        /// <summary>Vida del rival (fracción de sus RPM máximas; se ve en el HUD).</summary>
        public float TargetSpin { get; private set; }
        /// <summary>El rival está casi quieto, sin atacar ni cargar.</summary>
        public bool TargetIdle { get; private set; }
        /// <summary>Del borde del rival a la pared (infinito sin arena).</summary>
        public float TargetWallDistance { get; private set; }
        /// <summary>Del centro de la arena hacia el rival, en el plano y normalizado.</summary>
        public Vector3 TargetOutward { get; private set; }

        /// <summary>Hacia el centro de la arena, en el plano y normalizado.</summary>
        public Vector3 ToCenter { get; private set; }
        /// <summary>Del borde de la peonza a la pared (infinito sin arena o si el nivel no la mira).</summary>
        public float WallDistance { get; private set; }
        /// <summary>Velocidad hacia la pared (negativa si se aleja de ella).</summary>
        public float OutwardSpeed { get; private set; }

        public void Refresh(FakeBladeController self, ArenaDefinition arena, AILevel level)
        {
            if (BladeRadius <= 0f)
            {
                BladeRadius = self.TryGetComponent(out SphereCollider sphere)
                    ? sphere.radius * Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.z)
                    : DefaultRadius;
            }

            Vector3 position = self.Position;
            Vector3 velocity = self.Velocity;
            velocity.y = 0f;

            // Pared: las arenas son circulares (GDD 4), basta con la distancia al centro
            WallDistance = float.PositiveInfinity;
            OutwardSpeed = 0f;
            ToCenter = Vector3.zero;
            if (arena != null)
            {
                Vector3 fromCenter = position - arena.Center;
                fromCenter.y = 0f;
                float radius = fromCenter.magnitude;
                Vector3 outward = radius > 0.01f ? fromCenter / radius : Vector3.zero;
                ToCenter = -outward;
                if (level.watchesWalls)
                {
                    WallDistance = arena.InnerRadius - radius - BladeRadius;
                    OutwardSpeed = Vector3.Dot(velocity, outward);
                }
            }

            Target = self.FindNearestEnemy();
            if (Target == null)
            {
                Distance = Gap = TimeToImpact = float.PositiveInfinity;
                ClosingSpeed = 0f;
                ToTarget = TargetVelocity = Vector3.zero;
                TargetAttacking = TargetCharging = IncomingAttack = TargetIdle = false;
                TargetCharges = -1;
                TargetSpin = 0f;
                TargetWallDistance = float.PositiveInfinity;
                TargetOutward = Vector3.zero;
                return;
            }

            Vector3 offset = Target.Position - position;
            offset.y = 0f;
            Distance = offset.magnitude;
            ToTarget = Distance > 0.01f ? offset / Distance : Vector3.forward;
            Gap = Mathf.Max(0f, Distance - 2f * BladeRadius);

            Vector3 targetVelocity = Target.Velocity;
            targetVelocity.y = 0f;
            TargetVelocity = targetVelocity;
            ClosingSpeed = Vector3.Dot(velocity - targetVelocity, ToTarget);
            TimeToImpact = ClosingSpeed > 0.1f ? Gap / ClosingSpeed : float.PositiveInfinity;

            TargetAttacking = Target.IsAttacking;
            TargetCharging = level.readsCharging && Target.Attack.IsCharging;
            IncomingAttack = TargetAttacking && Gap < ThreatGap && Vector3.Dot(targetVelocity, -ToTarget) > IncomingSpeed;
            TargetCharges = level.readsResources ? Target.Attack.CurrentCharges : -1;
            TargetSpin = Target.SpinSpeedPercentage;
            TargetIdle = targetVelocity.sqrMagnitude < IdleSpeed * IdleSpeed && !TargetAttacking && !Target.Attack.IsCharging;

            TargetWallDistance = float.PositiveInfinity;
            TargetOutward = Vector3.zero;
            if (arena != null)
            {
                Vector3 fromCenter = Target.Position - arena.Center;
                fromCenter.y = 0f;
                float radius = fromCenter.magnitude;
                TargetOutward = radius > 0.01f ? fromCenter / radius : Vector3.zero;
                TargetWallDistance = arena.InnerRadius - radius - BladeRadius;
            }
        }
    }
}
