using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Utilidad de editor para crear las piezas preset de FakeBlade (GDD 3).
    ///
    /// 4 slots (Punta, Cuerpo, Disco, Núcleo) × 3 clases (Ligera, Media, Pesada) = 12 piezas.
    ///
    /// - Ligera: rápida, ágil, menos resistente y con más cargas de ataque.
    /// - Media: equilibrada.
    /// - Pesada: lenta pero tanque, con menos cargas de ataque.
    ///
    /// Montando las piezas salen los arquetipos: Ataque, Defensa, Agilidad o Balanceada.
    /// </summary>
    public static class FakeBladeComponentPresets
    {
        public const string SAVE_PATH = "Assets/Settings/ComponentsData/";

        [MenuItem("FakeBlade/Create All Component Presets")]
        public static void CreateAllPresets()
        {
            FakeBladeAssetsMenu.EnsureFolder(SAVE_PATH.TrimEnd('/'));

            // === PUNTAS ===
            CreateComponent("Tip_Light_NeedlePoint", "Needle Point",
                "Punta ultrafina. Mínima fricción, máxima velocidad. Estabilidad reducida.",
                ComponentSlot.Tip, WeightClass.Light,
                maxSpin: 0, spinDecay: 1f, moveSpeed: 4f, weight: -0.2f,
                attack: 0, defense: -5f, dash: 3f);

            CreateComponent("Tip_Medium_FlatBase", "Flat Base",
                "Punta plana equilibrada. Buena estabilidad y velocidad decente.",
                ComponentSlot.Tip, WeightClass.Medium,
                maxSpin: 50, spinDecay: -0.5f, moveSpeed: 1f, weight: 0f,
                attack: 0, defense: 0, dash: 0);

            CreateComponent("Tip_Heavy_WideBall", "Wide Ball",
                "Punta esférica ancha. Máxima estabilidad, pero lenta.",
                ComponentSlot.Tip, WeightClass.Heavy,
                maxSpin: 100, spinDecay: -1.5f, moveSpeed: -3f, weight: 0.3f,
                attack: 0, defense: 5f, dash: -3f);

            // === CUERPOS ===
            CreateComponent("Body_Light_AeroShell", "Aero Shell",
                "Cuerpo ultraligero. Se mueve como el viento pero sale volando en los choques.",
                ComponentSlot.Body, WeightClass.Light,
                maxSpin: -50, spinDecay: 0.5f, moveSpeed: 3f, weight: -0.4f,
                attack: -3f, defense: -5f, dash: 2f, charges: 1);

            CreateComponent("Body_Medium_StandardFrame", "Standard Frame",
                "Cuerpo estándar bien balanceado. Sin sorpresas.",
                ComponentSlot.Body, WeightClass.Medium,
                maxSpin: 0, spinDecay: 0, moveSpeed: 0, weight: 0.3f,
                attack: 0, defense: 5f, dash: 0);

            CreateComponent("Body_Heavy_IronFortress", "Iron Fortress",
                "Cuerpo macizo de hierro. Imparable una vez en movimiento. Cuesta arrancar.",
                ComponentSlot.Body, WeightClass.Heavy,
                maxSpin: 50, spinDecay: -0.3f, moveSpeed: -4f, weight: 1.2f,
                attack: 5f, defense: 15f, dash: -4f, charges: -1);

            // === DISCOS ===
            CreateComponent("Blade_Light_RazorEdge", "Razor Edge",
                "Disco afilado y ligero. Muchos ataques rápidos pero poco empuje.",
                ComponentSlot.Blade, WeightClass.Light,
                maxSpin: 0, spinDecay: 0.3f, moveSpeed: 1f, weight: -0.1f,
                attack: 8f, defense: -5f, dash: 1f, charges: 1);

            CreateComponent("Blade_Medium_BalancedRing", "Balanced Ring",
                "Anillo equilibrado. Buen ataque y defensa decente.",
                ComponentSlot.Blade, WeightClass.Medium,
                maxSpin: 30, spinDecay: 0, moveSpeed: 0, weight: 0.2f,
                attack: 5f, defense: 5f, dash: 0);

            CreateComponent("Blade_Heavy_CrushWheel", "Crush Wheel",
                "Disco de demolición. Impactos devastadores. Muy pesado.",
                ComponentSlot.Blade, WeightClass.Heavy,
                maxSpin: -30, spinDecay: 0.5f, moveSpeed: -2f, weight: 0.6f,
                attack: 15f, defense: 10f, dash: 2f);

            // === NÚCLEOS ===
            CreateComponent("Core_Light_ElectricDash", "Velocity Core",
                "Núcleo de velocidad. Poder: Dash eléctrico.",
                ComponentSlot.Core, WeightClass.Light,
                maxSpin: 100, spinDecay: 0.5f, moveSpeed: 2f, weight: -0.1f,
                attack: 0, defense: 0, dash: 5f,
                ability: SpecialAbilityType.Dash);

            CreateComponent("Core_Medium_SpinBoost", "Endurance Core",
                "Núcleo de resistencia. Poder: Spin Boost (recupera RPM).",
                ComponentSlot.Core, WeightClass.Medium,
                maxSpin: 200, spinDecay: -0.5f, moveSpeed: 0, weight: 0.1f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.SpinBoost);

            CreateComponent("Core_Heavy_StormBreaker", "Fortress Core",
                "Núcleo defensivo. Poder: Storm Breaker (resistencia masiva al empuje).",
                ComponentSlot.Core, WeightClass.Heavy,
                maxSpin: 50, spinDecay: 0, moveSpeed: -1f, weight: 0.4f,
                attack: 5f, defense: 5f, dash: 0,
                ability: SpecialAbilityType.Shield);

            CreateComponent("Core_Heavy_ShockWave", "Impact Core",
                "Núcleo de impacto. Poder: Onda de choque que empuja enemigos.",
                ComponentSlot.Core, WeightClass.Heavy,
                maxSpin: 50, spinDecay: 0, moveSpeed: -1f, weight: 0.4f,
                attack: 5f, defense: 5f, dash: 0,
                ability: SpecialAbilityType.ShockWave);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== FakeBlade: piezas preset creadas/actualizadas en {SAVE_PATH} ===");
        }

        private static void CreateComponent(
            string fileName, string displayName, string description,
            ComponentSlot slot, WeightClass weightClass,
            float maxSpin, float spinDecay, float moveSpeed, float weight,
            float attack, float defense, float dash, int charges = 0,
            SpecialAbilityType ability = SpecialAbilityType.None)
        {
            string path = $"{SAVE_PATH}{fileName}.asset";

            var component = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(path);
            bool isNew = component == null;
            if (isNew) component = ScriptableObject.CreateInstance<FakeBladeComponentData>();

            // SerializedObject para escribir los campos privados
            var so = new SerializedObject(component);
            so.FindProperty("componentName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("componentType").enumValueIndex = (int)slot;
            so.FindProperty("weightClass").enumValueIndex = (int)weightClass;
            so.FindProperty("maxSpinModifier").floatValue = maxSpin;
            so.FindProperty("spinDecayModifier").floatValue = spinDecay;
            so.FindProperty("moveSpeedModifier").floatValue = moveSpeed;
            so.FindProperty("weightModifier").floatValue = weight;
            so.FindProperty("attackPowerModifier").floatValue = attack;
            so.FindProperty("defenseModifier").floatValue = defense;
            so.FindProperty("dashForceModifier").floatValue = dash;
            so.FindProperty("attackChargesModifier").intValue = charges;
            so.FindProperty("specialAbility").intValue = (int)ability;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew) AssetDatabase.CreateAsset(component, path);
            else EditorUtility.SetDirty(component);
        }

        // === QUICK EQUIP ===

        [MenuItem("FakeBlade/Quick Equip/All Light (Agilidad)")]
        public static void QuickEquipAllLight()
        {
            QuickEquipPreset("Light", "Tip_Light_NeedlePoint", "Body_Light_AeroShell",
                "Blade_Light_RazorEdge", "Core_Light_ElectricDash");
        }

        [MenuItem("FakeBlade/Quick Equip/All Medium (Balanceada)")]
        public static void QuickEquipAllMedium()
        {
            QuickEquipPreset("Medium", "Tip_Medium_FlatBase", "Body_Medium_StandardFrame",
                "Blade_Medium_BalancedRing", "Core_Medium_SpinBoost");
        }

        [MenuItem("FakeBlade/Quick Equip/All Heavy (Defensa)")]
        public static void QuickEquipAllHeavy()
        {
            QuickEquipPreset("Heavy", "Tip_Heavy_WideBall", "Body_Heavy_IronFortress",
                "Blade_Heavy_CrushWheel", "Core_Heavy_StormBreaker");
        }

        [MenuItem("FakeBlade/Quick Equip/Attack Build (Ataque)")]
        public static void QuickEquipAttack()
        {
            QuickEquipPreset("Attack", "Tip_Medium_FlatBase", "Body_Light_AeroShell",
                "Blade_Heavy_CrushWheel", "Core_Heavy_ShockWave");
        }

        private static void QuickEquipPreset(string presetName, string tipFile, string bodyFile, string bladeFile, string coreFile)
        {
            var selected = Selection.activeGameObject;
            if (selected == null || !selected.TryGetComponent(out FakeBladeStats stats))
            {
                Debug.LogWarning("[FakeBlade] Selecciona un GameObject con FakeBladeStats.");
                return;
            }

            Undo.RecordObject(stats, $"Quick Equip {presetName}");
            Equip(stats, tipFile);
            Equip(stats, bodyFile);
            Equip(stats, bladeFile);
            Equip(stats, coreFile);
            EditorUtility.SetDirty(stats);

            Debug.Log($"[FakeBlade] {presetName} en {selected.name}: {stats.GetStatsSummary()}");
        }

        private static void Equip(FakeBladeStats stats, string fileName)
        {
            var data = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{SAVE_PATH}{fileName}.asset");
            if (data != null) stats.EquipComponent(data);
            else Debug.LogWarning($"[FakeBlade] No existe {fileName}. Usa 'FakeBlade/Create All Component Presets'.");
        }
    }
}
