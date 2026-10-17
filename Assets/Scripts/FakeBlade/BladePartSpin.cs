using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Giro de cada pieza a su velocidad (O1, GDD 7.6): la peonza entera gira con su pivote y cada pieza gira además
    /// respecto a él, para que su velocidad final sea la de la peonza × su multiplicador. Así se lee el dibujo de un
    /// disco que, girando muy rápido, parecería quieto o al revés (efecto rueda de carro). Solo visual.
    ///
    /// Multiplicador = el de su tipo de pieza en CombatConfig (Giro visual) × el de la pieza (Inspector de la pieza).
    /// Los dos se leen cada frame: se pueden ajustar en Play. Va en el contenedor de los modelos (BladeModel); lo mueven
    /// FakeBladeController (partida) y BladePreviewStage (selección), sin GC.
    /// </summary>
    public sealed class BladePartSpin : MonoBehaviour
    {
        private const int Slots = 4;
        private readonly Transform[] _parts = new Transform[Slots];
        private readonly Quaternion[] _baseRotations = new Quaternion[Slots];
        private readonly FakeBladeComponentData[] _data = new FakeBladeComponentData[Slots];
        private readonly float[] _angles = new float[Slots];

        /// <summary>Multiplica el giro del disco (congelada: más lento, K8). Lo pone FakeBladeController cada frame.</summary>
        public float DiscSpin { get; set; } = 1f;

        private void Awake()
        {
            // En partida, la peonza la recoge para moverla (en la selección la recoge la vista previa)
            var owner = GetComponentInParent<FakeBladeController>(true);
            if (owner != null) owner.PartSpin = this;
        }

        public void Set(ComponentSlot slot, Transform part, FakeBladeComponentData data)
        {
            int i = (int)slot;
            _parts[i] = part;
            _baseRotations[i] = part.localRotation; // la del FBX, que se conserva
            _data[i] = data;
            _angles[i] = 0f;
        }

        /// <summary>degreesPerSecond = velocidad de giro visual de la peonza entera.</summary>
        public void Tick(float degreesPerSecond, float dt)
        {
            CombatConfig cfg = CombatConfig.Active;
            for (int i = 0; i < Slots; i++)
            {
                Transform part = _parts[i];
                if (part == null) continue;
                float multiplier = cfg.SlotVisualSpin((ComponentSlot)i) * (_data[i] != null ? _data[i].VisualSpinMultiplier : 1f);
                if (i == (int)ComponentSlot.Blade) multiplier *= DiscSpin;
                // Lo que gira de más o de menos respecto a la peonza
                _angles[i] = (_angles[i] + degreesPerSecond * (multiplier - 1f) * dt) % 360f;
                part.localRotation = Quaternion.AngleAxis(_angles[i], Vector3.up) * _baseRotations[i];
            }
        }
    }
}
