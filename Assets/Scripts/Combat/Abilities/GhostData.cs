using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Fantasma (GDD 5, C5; decidido el 2026-10-13 y 2026-10-14). Crea un clon de sí misma que juega solo con la IA de
    /// Agilidad (nivel Normal) y las piezas básicas de la Agilidad. Sus golpes quitan el 10% del daño normal (y cuentan
    /// para su dueña), pero empujan y cortan la carga de ataque del rival. Ni hace parry ni se le puede hacer, no pierde
    /// RPM ni recibe estados y no usa especial. No tiene vida: aguanta 3 ataques y al tercero se deshace en humo.
    /// Dura hasta que lo rompan (o hasta que caiga su dueña); activarlo otra vez lo trae junto a ella con los 3 golpes.
    /// El clon se crea una vez, al empezar, y se esconde hasta que se usa (sin instanciar en combate, GDD 7.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Ghost", menuName = "FakeBlade/Specials/Fantasma")]
    public class GhostData : SpecialAbilityData
    {
        [Header("Clon")]
        [Tooltip("Prefab de peonza para el clon (el de los jugadores, del catálogo)")]
        public GameObject clonePrefab;
        [Tooltip("Piezas del clon: las básicas de la Agilidad (punta, cuerpo, disco y núcleo)")]
        public FakeBladeComponentData[] cloneParts = new FakeBladeComponentData[0];
        [Tooltip("Nivel de la IA del clon")]
        public AIDifficulty aiDifficulty = AIDifficulty.Normal;
        [Tooltip("Ataques que aguanta antes de deshacerse")]
        [Min(1)] public int hitsToBreak = 3;
        [Tooltip("Daño que hacen sus golpes (0,1 = el 10% del normal)")]
        [Range(0f, 1f)] public float damageMultiplier = 0.1f;

        [Header("Al salir (2026-10-27)")]
        [Tooltip("Segundos que atraviesa las peonzas al salir (sin empujar ni golpear; el suelo y el muro sí cuentan): sale de " +
                 "dentro de su dueña sin molestarla. Si al acabar sigue encima de alguna, espera a separarse (1,5 s como mucho). " +
                 "0 = aparece ya a un lado, chocando")]
        [Min(0f)] public float intangibleTime = 0.5f;
        [Tooltip("Velocidad con la que sale de su dueña hacia donde aparece (m/s)")]
        [Min(0f)] public float emergeSpeed = 7f;

        [Header("Dueña mientras el clon está fuera (2026-10-28)")]
        [Tooltip("Velocidad máxima de la dueña (1,15 = un 15% más)")]
        [Min(0f)] public float ownerMoveMultiplier = 1.15f;
        [Tooltip("Velocidad de recarga de sus cargas de ataque (1,3 = un 30% más deprisa)")]
        [Min(0f)] public float ownerRechargeMultiplier = 1.3f;
        [Tooltip("Cambios de sitio con el clon (pulsando Especial con el clon fuera) por cada vez que se saca. Gastados, Especial " +
                 "vuelve a sacarlo, con la energía llena")]
        [Min(0)] public int swapsPerClone = 1;

        [Header("Aspecto")]
        [Tooltip("Cuánto se aclara el color de su dueña (1 = blanco)")]
        [Range(0f, 1f)] public float paleness = 0.65f;
        [Tooltip("Distancia a su dueña al aparecer (m)")]
        public float spawnOffset = 1.6f;
        [Tooltip("Humo por segundo que suelta mientras está")]
        public float smokeRate = 22f;

        public override SpecialAbilityType Type => SpecialAbilityType.Ghost;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new GhostAbility(this, owner);
    }

    public sealed class GhostAbility : SpecialAbility<GhostData>
    {
        private float _smokeAcc;

        // El poder se crea al equipar el núcleo (al aparecer la peonza): el clon se crea ya, escondido
        public GhostAbility(GhostData data, FakeBladeController owner) : base(data, owner)
        {
            if (Application.isPlaying) EnsureClone();
        }

        private FakeBladeController Clone => Owner.GhostClone;
        private bool CloneOut => Clone != null && Clone.gameObject.activeSelf;

        public override void OnActivate()
        {
            FakeBladeController clone = EnsureClone();
            if (clone == null) return;

            // Al lado de su dueña (o de vuelta junto a ella, con sus golpes enteros), dentro de la arena
            Vector3 position = SpawnPoint();
            // Si se cayó de la arena o se quedó sin RPM (2026-10-27: pegada al muro salía fuera y después volvía quieto y
            // humeando), vuelve entero
            if (clone.IsDestroyed) clone.ResetFakeBlade();
            clone.gameObject.SetActive(true);
            clone.ResetGhostHits();
            _swapsLeft = Config.swapsPerClone;
            // Sale de dentro de su dueña (2026-10-27, pedido del usuario: al lado chocaba con ella), atravesando las peonzas un
            // momento, hacia donde aparece. Ignorar los choques después de activarlo: al desactivar un collider se olvida
            if (Config.intangibleTime > 0f)
            {
                SetIntangible(clone, true);
                _intangibleLeft = Config.intangibleTime;
                _intangibleExtra = MaxIntangibleExtra;
                clone.SetPosition(Owner.Position, Quaternion.identity);
                Vector3 away = position - Owner.Position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) clone.ApplyKnockback(away.normalized * Config.emergeSpeed);
            }
            else clone.SetPosition(position, Quaternion.identity);
            // Con efectos propios (VfxLibrary → Ghost, 2026-10-27): la bocanada que se desdobla hacia el clon, que aparece
            // desde invisible; si no, la nube de antes
            clone.RestartGhostVisuals();
            if (!BladeSpecialFx.PlayCloneAppear(Owner, position)) Puff(position, 1.5f);
        }

        public override void Tick(float dt) => Maintain(dt);

        // El clon sigue después de la activación, hasta que lo rompan
        public override bool IsLingering => CloneOut;

        // Mientras el clon está fuera, la dueña va más rápida y recarga antes (2026-10-28, aceptado por el usuario)
        public override float MoveSpeedMultiplier => CloneOut ? Config.ownerMoveMultiplier : 1f;
        public override float AttackRechargeMultiplier => CloneOut ? Config.ownerRechargeMultiplier : 1f;

        // Especial con el clon fuera: cambia de sitio con él (uno por clon; 2026-10-28)
        public override bool OnPressedWhileActive() => TrySwap();
        public override bool OnPressedWhileLingering() => TrySwap();

        /// <summary>Le queda algún cambio de sitio con el clon y ahora se puede (el clon fuera y ya separado de ella). Para la IA.</summary>
        public bool CanSwap => _swapsLeft > 0 && CloneOut && !_intangible && !Clone.IsDestroyed;

        private int _swapsLeft;

        /// <summary>
        /// Cambia de sitio con el clon (pedido del usuario, 2026-10-28): cada uno sigue con su velocidad, y una explosión de humo
        /// en los dos sitios (Swap Burst).
        /// </summary>
        private bool TrySwap()
        {
            if (!CanSwap) return false;
            _swapsLeft--;
            FakeBladeController clone = Clone;
            Vector3 ownerAt = Owner.Position, cloneAt = clone.Position;
            Vector3 ownerVelocity = Owner.Velocity, cloneVelocity = clone.Velocity;
            Owner.SetPosition(cloneAt, Quaternion.identity);
            clone.SetPosition(ownerAt, Quaternion.identity);
            Owner.SetVelocity(ownerVelocity);
            clone.SetVelocity(cloneVelocity);
            if (!BladeSpecialFx.PlayCloneSwap(Owner, ownerAt, cloneAt))
            {
                Puff(ownerAt, 1.5f);
                Puff(cloneAt, 1.5f);
            }
            return true;
        }
        public override void TickLingering(float dt) => Maintain(dt);
        public override void ClearLingering() => Vanish();

        private void Maintain(float dt)
        {
            FakeBladeController clone = Clone;
            if (clone == null || !clone.gameObject.activeSelf) return;
            // Roto, caída su dueña o caído él (fuera de la arena): se deshace
            if (clone.GhostHits >= Config.hitsToBreak || Owner.IsDestroyed || clone.IsDestroyed)
            {
                Vanish();
                return;
            }

            // No se gasta: siempre con las RPM llenas (no pierde por desgaste ni por sus ataques)
            clone.AddSpin(clone.MaxSpinSpeed);
            if (_intangible) UpdateIntangible(clone, dt);

            // Humo de fantasma (con efectos propios en la biblioteca, el clon lleva los suyos: BladeSpecialFx)
            if (HasOwnEffects) return;
            _smokeAcc += Config.smokeRate * dt;
            Color smoke = new Color(1f, 1f, 1f, 0.45f);
            while (_smokeAcc >= 1f)
            {
                _smokeAcc -= 1f;
                Vector3 p = clone.Position + Random.insideUnitSphere * 0.4f;
                VfxSystem.EmitParticle(VfxType.LowSpinSmoke, p, Vector3.up * 0.4f, smoke, 0.25f);
            }
        }

        private void Vanish()
        {
            FakeBladeController clone = Clone;
            if (clone == null || !clone.gameObject.activeSelf) return;
            // Con efectos propios: humo que sigue la inercia que llevaba el clon al recibir el golpe
            if (!BladeSpecialFx.PlayCloneVanish(Owner, clone)) Puff(clone.Position, 2.5f);
            if (_intangible) SetIntangible(clone, false);
            clone.gameObject.SetActive(false);
        }

        /// <summary>Lo más que espera, tras Intangible Time, a separarse de la peonza que tenga encima (s).</summary>
        private const float MaxIntangibleExtra = 1.5f;
        /// <summary>Distancia entre centros a la que ya no se tocan (dos esferas de 0,6 m y algo de margen).</summary>
        private const float ClearDistance = 1.3f;
        private bool _intangible;
        private float _intangibleLeft, _intangibleExtra;

        /// <summary>Al acabar Intangible Time, vuelve a chocar en cuanto no tenga ninguna peonza encima (o pasado el margen).</summary>
        private void UpdateIntangible(FakeBladeController clone, float dt)
        {
            if (_intangibleLeft > 0f)
            {
                _intangibleLeft -= dt;
                return;
            }
            _intangibleExtra -= dt;
            if (_intangibleExtra > 0f && Overlapping(clone)) return;
            SetIntangible(clone, false);
        }

        /// <summary>El clon atraviesa (o deja de atravesar) a todas las peonzas de la partida y a sus clones.</summary>
        private void SetIntangible(FakeBladeController clone, bool on)
        {
            _intangible = on;
            Collider mine = clone.GetComponent<Collider>();
            GameManager gm = GameManager.Instance;
            if (mine == null || gm == null) return;
            var players = gm.Players;
            for (int i = 0; i < players.Count; i++)
            {
                FakeBladeController blade = players[i] != null ? players[i].Blade : null;
                if (blade == null) continue;
                Ignore(mine, blade, on);
                if (blade.GhostClone != null && blade.GhostClone != clone) Ignore(mine, blade.GhostClone, on);
            }
        }

        private static void Ignore(Collider mine, FakeBladeController other, bool ignore)
        {
            Collider collider = other.GetComponent<Collider>();
            if (collider != null && collider != mine) Physics.IgnoreCollision(mine, collider, ignore);
        }

        /// <summary>¿Tiene encima alguna peonza en juego (o un clon fuera)?</summary>
        private static bool Overlapping(FakeBladeController clone)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return false;
            var players = gm.Players;
            for (int i = 0; i < players.Count; i++)
            {
                FakeBladeController blade = players[i] != null ? players[i].Blade : null;
                if (blade == null) continue;
                if (Touching(clone, blade)) return true;
                FakeBladeController other = blade.GhostClone;
                if (other != null && other != clone && other.gameObject.activeSelf && Touching(clone, other)) return true;
            }
            return false;
        }

        private static bool Touching(FakeBladeController a, FakeBladeController b) =>
            !b.IsDestroyed && b.gameObject.activeSelf && FlatDistance(a.Position, b.Position) < ClearDistance * ClearDistance;

        /// <summary>
        /// Dónde aparece el clon: a Spawn Offset de su dueña, hacia un lado, el que quede más cerca del centro de la arena, y
        /// nunca más allá del muro (menos su radio y un margen).
        /// </summary>
        private Vector3 SpawnPoint()
        {
            const float wallMargin = 0.9f;
            Vector3 side = Vector3.Cross(Vector3.up, Owner.Facing);
            side.y = 0f;
            side = side.sqrMagnitude < 0.001f ? Vector3.right : side.normalized;
            Vector3 right = Owner.Position + side * Config.spawnOffset;
            Vector3 left = Owner.Position - side * Config.spawnOffset;
            ArenaDefinition arena = ArenaDefinition.Current;
            if (arena == null) return right;

            Vector3 center = arena.Center;
            Vector3 position = FlatDistance(right, center) <= FlatDistance(left, center) ? right : left;
            Vector3 offset = position - center;
            offset.y = 0f;
            float limit = Mathf.Max(0f, arena.InnerRadius - wallMargin);
            if (offset.magnitude > limit)
            {
                Vector3 inside = center + offset.normalized * limit;
                position = new Vector3(inside.x, position.y, inside.z);
            }
            return position;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static bool HasOwnEffects =>
            VfxSystem.Library != null && VfxSystem.Library.GetSpecialEffects(SpecialAbilityType.Ghost) is SpecialVfxStyle style &&
            style.cloneEffects;

        private void Puff(Vector3 position, float intensity)
        {
            VfxSystem.Play(VfxType.LowSpinSmoke, position, Vector3.up, Color.white, intensity);
            VfxSystem.Play(VfxType.SpinOut, position, Vector3.up, Data.color, intensity * 0.5f);
        }

        /// <summary>Crea el clon la primera vez (al equipar el núcleo, antes del combate) y lo deja escondido.</summary>
        private FakeBladeController EnsureClone()
        {
            if (Owner.GhostClone != null) return Owner.GhostClone;
            if (Config.clonePrefab == null) return null;

            GameObject go = Object.Instantiate(Config.clonePrefab, Owner.Position, Quaternion.identity);
            go.name = Owner.name + "_Fantasma";
            var player = go.GetComponent<PlayerController>();
            var blade = go.GetComponent<FakeBladeController>();
            var stats = go.GetComponent<FakeBladeStats>();
            if (player == null || blade == null) { Object.Destroy(go); return null; }

            player.SetRegisterInMatch(false);
            PlayerController ownerPlayer = Owner.Owner;
            Color baseColor = ownerPlayer != null ? ownerPlayer.PlayerColor : Color.white;
            player.SetPlayerColor(Color.Lerp(baseColor, Color.white, Config.paleness));
            if (ownerPlayer != null) player.SetTeamID(ownerPlayer.TeamID);
            if (stats != null)
                foreach (FakeBladeComponentData part in Config.cloneParts)
                    if (part != null) stats.EquipComponent(part);

            blade.MakeGhost(Owner, Config.damageMultiplier);
            AIBrain ai = AIProfiles.Ensure(go, AIProfiles.ForStyle(AIStyle.Agility));
            ai.Difficulty = Config.aiDifficulty;
            player.SetInputSource(ai);

            go.SetActive(false);
            Owner.GhostClone = blade;
            return blade;
        }
    }
}
