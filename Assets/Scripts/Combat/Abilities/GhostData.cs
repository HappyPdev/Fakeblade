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

            // Al lado de su dueña (o de vuelta junto a ella, con sus golpes enteros)
            Vector3 side = Vector3.Cross(Vector3.up, Owner.Facing);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            Vector3 position = Owner.Position + side.normalized * Config.spawnOffset;
            clone.gameObject.SetActive(true);
            clone.SetPosition(position, Quaternion.identity);
            clone.ResetGhostHits();
            Puff(position, 1.5f);
        }

        public override void Tick(float dt) => Maintain(dt);

        // El clon sigue después de la activación, hasta que lo rompan
        public override bool IsLingering => CloneOut;
        public override void TickLingering(float dt) => Maintain(dt);
        public override void ClearLingering() => Vanish();

        private void Maintain(float dt)
        {
            FakeBladeController clone = Clone;
            if (clone == null || !clone.gameObject.activeSelf) return;
            if (clone.GhostHits >= Config.hitsToBreak || Owner.IsDestroyed)
            {
                Vanish();
                return;
            }

            // No se gasta: siempre con las RPM llenas (no pierde por desgaste ni por sus ataques)
            clone.AddSpin(clone.MaxSpinSpeed);

            // Humo de fantasma
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
            Puff(clone.Position, 2.5f);
            clone.gameObject.SetActive(false);
        }

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
