using UnityEngine;
using UnityEditor;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Utilidad de editor para crear todos los componentes preset de FakeBlade.
    /// 
    /// 4 slots (Tip, Body, Blade, Core) × 3 clases (Light, Medium, Heavy) = 12 componentes.
    /// 
    /// FILOSOFÍA DE DISEÑO:
    /// - Light: Rápido, ágil, menos resistente. Ideal para hit-and-run.
    /// - Medium: Equilibrado. Buen punto de partida.
    /// - Heavy: Lento pero tanque. Mucha inercia, mucho daño de choque.
    /// 
    /// Cada tipo de slot tiene su "especialidad":
    /// - Tip: Controla estabilidad (spinDecay) y velocidad de movimiento
    /// - Body: Controla peso e inercia
    /// - Blade: Controla ataque y defensa
    /// - Core: Controla spin máximo y habilidad especial
    /// </summary>
    public static class FakeBladeComponentPresets
    {
        private const string SAVE_PATH = "Assets/Prefabs/Components/";

        [MenuItem("FakeBlade/Create All Component Presets")]
        public static void CreateAllPresets()
        {
            // Asegurar que la carpeta existe
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Components"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Components");

            // === TIPS (Puntas) ===
            CreateComponent("Tip_Light_NeedlePoint", "Needle Point",
                "Punta ultra-fina. Mínima fricción, máxima velocidad. Estabilidad reducida.",
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

            // === BODIES (Cuerpos) ===
            CreateComponent("Body_Light_AeroShell", "Aero Shell",
                "Cuerpo ultraligero. Se mueve como el viento pero sale volando en colisiones.",
                ComponentSlot.Body, WeightClass.Light,
                maxSpin: -50, spinDecay: 0.5f, moveSpeed: 3f, weight: -0.4f,
                attack: -3f, defense: -5f, dash: 2f);

            CreateComponent("Body_Medium_StandardFrame", "Standard Frame",
                "Cuerpo estándar bien balanceado. Sin sorpresas.",
                ComponentSlot.Body, WeightClass.Medium,
                maxSpin: 0, spinDecay: 0, moveSpeed: 0, weight: 0.3f,
                attack: 0, defense: 5f, dash: 0);

            CreateComponent("Body_Heavy_IronFortress", "Iron Fortress",
                "Cuerpo macizo de hierro. Imparable una vez en movimiento. Cuesta arrancar.",
                ComponentSlot.Body, WeightClass.Heavy,
                maxSpin: 50, spinDecay: -0.3f, moveSpeed: -4f, weight: 1.2f,
                attack: 5f, defense: 15f, dash: -4f);

            // === BLADES (Discos de ataque) ===
            CreateComponent("Blade_Light_RazorEdge", "Razor Edge",
                "Disco afilado y ligero. Ataques rápidos pero poco knockback.",
                ComponentSlot.Blade, WeightClass.Light,
                maxSpin: 0, spinDecay: 0.3f, moveSpeed: 1f, weight: -0.1f,
                attack: 8f, defense: -5f, dash: 1f);

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

            // === CORES (Núcleos) ===
            CreateComponent("Core_Light_SpeedBoost", "Velocity Core",
                "Núcleo de velocidad. Habilidad especial: Dash instantáneo extra.",
                ComponentSlot.Core, WeightClass.Light,
                maxSpin: 100, spinDecay: 0.5f, moveSpeed: 2f, weight: -0.1f,
                attack: 0, defense: 0, dash: 5f,
                ability: SpecialAbilityType.Dash, abilityUses: 5);

            CreateComponent("Core_Medium_SpinBoost", "Endurance Core",
                "Núcleo de resistencia. Habilidad especial: Recupera spin.",
                ComponentSlot.Core, WeightClass.Medium,
                maxSpin: 200, spinDecay: -0.5f, moveSpeed: 0, weight: 0.1f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.SpinBoost, abilityUses: 3);

            CreateComponent("Core_Heavy_ShockWave", "Impact Core",
                "Núcleo de impacto. Habilidad especial: Onda de choque que empuja enemigos.",
                ComponentSlot.Core, WeightClass.Heavy,
                maxSpin: 50, spinDecay: 0, moveSpeed: -1f, weight: 0.4f,
                attack: 5f, defense: 5f, dash: 0,
                ability: SpecialAbilityType.ShockWave, abilityUses: 2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== FakeBlade: 12 Component Presets Created! ===");
            Debug.Log($"Location: {SAVE_PATH}");
            Debug.Log("Tip: Light (Needle Point), Medium (Flat Base), Heavy (Wide Ball)");
            Debug.Log("Body: Light (Aero Shell), Medium (Standard Frame), Heavy (Iron Fortress)");
            Debug.Log("Blade: Light (Razor Edge), Medium (Balanced Ring), Heavy (Crush Wheel)");
            Debug.Log("Core: Light (Velocity Core), Medium (Endurance Core), Heavy (Impact Core)");
        }

        private static void CreateComponent(
            string fileName, string displayName, string description,
            ComponentSlot slot, WeightClass weightClass,
            float maxSpin, float spinDecay, float moveSpeed, float weight,
            float attack, float defense, float dash,
            SpecialAbilityType ability = SpecialAbilityType.None, int abilityUses = 0)
        {
            string path = $"{SAVE_PATH}{fileName}.asset";

            // Verificar si ya existe
            var existing = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(path);
            if (existing != null)
            {
                Debug.Log($"[FakeBladePresets] Updating existing: {fileName}");
                // Actualizar valores via SerializedObject
                UpdateExistingAsset(existing, displayName, description, slot, weightClass,
                    maxSpin, spinDecay, moveSpeed, weight, attack, defense, dash, ability, abilityUses);
                return;
            }

            var component = ScriptableObject.CreateInstance<FakeBladeComponentData>();

            // Usar SerializedObject para setear campos privados
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
            so.FindProperty("specialAbility").enumValueIndex = (int)ability;
            so.FindProperty("specialAbilityUses").intValue = abilityUses;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(component, path);
            Debug.Log($"[FakeBladePresets] Created: {displayName} ({slot} - {weightClass})");
        }

        private static void UpdateExistingAsset(
            FakeBladeComponentData existing,
            string displayName, string description,
            ComponentSlot slot, WeightClass weightClass,
            float maxSpin, float spinDecay, float moveSpeed, float weight,
            float attack, float defense, float dash,
            SpecialAbilityType ability, int abilityUses)
        {
            var so = new SerializedObject(existing);
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
            so.FindProperty("specialAbility").enumValueIndex = (int)ability;
            so.FindProperty("specialAbilityUses").intValue = abilityUses;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
        }

        // === QUICK EQUIP PRESETS ===

        [MenuItem("FakeBlade/Quick Equip/All Light (Speed Build)")]
        public static void QuickEquipAllLight()
        {
            QuickEquipPreset("Light", "Tip_Light_NeedlePoint", "Body_Light_AeroShell",
                "Blade_Light_RazorEdge", "Core_Light_SpeedBoost");
        }

        [MenuItem("FakeBlade/Quick Equip/All Medium (Balanced Build)")]
        public static void QuickEquipAllMedium()
        {
            QuickEquipPreset("Medium", "Tip_Medium_FlatBase", "Body_Medium_StandardFrame",
                "Blade_Medium_BalancedRing", "Core_Medium_SpinBoost");
        }

        [MenuItem("FakeBlade/Quick Equip/All Heavy (Tank Build)")]
        public static void QuickEquipAllHeavy()
        {
            QuickEquipPreset("Heavy", "Tip_Heavy_WideBall", "Body_Heavy_IronFortress",
                "Blade_Heavy_CrushWheel", "Core_Heavy_ShockWave");
        }

        private static void QuickEquipPreset(string name, string tipFile, string bodyFile, string bladeFile, string coreFile)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[FakeBlade] Select a FakeBlade GameObject first!");
                return;
            }

            var stats = selected.GetComponent<FakeBladeStats>();
            if (stats == null)
            {
                Debug.LogWarning("[FakeBlade] Selected object has no FakeBladeStats component!");
                return;
            }

            var tip = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{SAVE_PATH}{tipFile}.asset");
            var body = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{SAVE_PATH}{bodyFile}.asset");
            var blade = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{SAVE_PATH}{bladeFile}.asset");
            var core = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{SAVE_PATH}{coreFile}.asset");

            if (tip != null) stats.EquipComponent(tip);
            if (body != null) stats.EquipComponent(body);
            if (blade != null) stats.EquipComponent(blade);
            if (core != null) stats.EquipComponent(core);

            EditorUtility.SetDirty(stats);
            Debug.Log($"[FakeBlade] Quick Equipped: {name} preset on {selected.name}");
            Debug.Log($"[FakeBlade] Final stats: {stats.GetStatsSummary()}");
        }
    }
}