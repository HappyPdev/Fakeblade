using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Utilidad de editor para crear las piezas preset de FakeBlade (GDD 3).
    ///
    /// Puntas, cuerpos y discos: una pieza por clase (Ligera, Media, Pesada). Núcleos: uno por poder.
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
                ComponentSlot.Tip, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: 0, spinDecay: 1f, moveSpeed: 4f, weight: -0.2f,
                attack: 0, defense: -5f, dash: 3f);

            CreateComponent("Tip_Medium_FlatBase", "Flat Base",
                "Punta plana equilibrada. Buena estabilidad y velocidad decente.",
                ComponentSlot.Tip, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 50, spinDecay: -0.5f, moveSpeed: 1f, weight: 0f,
                attack: 0, defense: 0, dash: 0);

            CreateComponent("Tip_Heavy_WideBall", "Wide Ball",
                "Punta esférica ancha. Máxima estabilidad, pero lenta.",
                ComponentSlot.Tip, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 100, spinDecay: -1.5f, moveSpeed: -3f, weight: 0.3f,
                attack: 0, defense: 5f, dash: -3f);

            // === CUERPOS ===
            CreateComponent("Body_Light_AeroShell", "Aero Shell",
                "Cuerpo ultraligero. Se mueve como el viento pero sale volando en los choques.",
                ComponentSlot.Body, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: -50, spinDecay: 0.5f, moveSpeed: 3f, weight: -0.4f,
                attack: -3f, defense: -5f, dash: 2f, charges: 1);

            CreateComponent("Body_Medium_StandardFrame", "Standard Frame",
                "Cuerpo estándar bien balanceado. Sin sorpresas.",
                ComponentSlot.Body, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 0, spinDecay: 0, moveSpeed: 0, weight: 0.3f,
                attack: 0, defense: 5f, dash: 0);

            CreateComponent("Body_Heavy_IronFortress", "Iron Fortress",
                "Cuerpo macizo de hierro. Imparable una vez en movimiento. Cuesta arrancar.",
                ComponentSlot.Body, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: -0.3f, moveSpeed: -4f, weight: 1.2f,
                attack: 5f, defense: 15f, dash: -4f, charges: -1);

            // === DISCOS ===
            CreateComponent("Blade_Light_RazorEdge", "Razor Edge",
                "Disco afilado y ligero. Muchos ataques rápidos pero poco empuje.",
                ComponentSlot.Blade, WeightClass.Light, BladeArchetype.Attack,
                maxSpin: 0, spinDecay: 0.3f, moveSpeed: 1f, weight: -0.1f,
                attack: 8f, defense: -5f, dash: 1f, charges: 1);

            CreateComponent("Blade_Medium_BalancedRing", "Balanced Ring",
                "Anillo equilibrado. Buen ataque y defensa decente.",
                ComponentSlot.Blade, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 30, spinDecay: 0, moveSpeed: 0, weight: 0.2f,
                attack: 5f, defense: 5f, dash: 0);

            CreateComponent("Blade_Heavy_CrushWheel", "Crush Wheel",
                "Disco de demolición. Impactos devastadores. Muy pesado.",
                ComponentSlot.Blade, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: -30, spinDecay: 0.5f, moveSpeed: -2f, weight: 0.6f,
                attack: 15f, defense: 10f, dash: 2f);

            // === NÚCLEOS (GDD 3) ===
            // Uno por poder. Todos +50 RPM máx. y una ventaja y un coste del mismo tamaño (2 puntos;
            // 1 punto = 25 RPM = 0,5 velocidad = 0,1 peso = 2 ataque = 3 defensa = 1 dash = 0,2 desgaste).
            // Fantasma tendrá el suyo con su poder (C5): Ligera, +2 dash, +0,4 desgaste.
            CreateComponent("Core_Medium_SpinBoost", "Endurance Core",
                "Núcleo de resistencia. Poder: Spin Boost (recupera RPM).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 100, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: -4f, defense: 0, dash: 0,
                ability: SpecialAbilityType.SpinBoost);

            CreateComponent("Core_Heavy_ShockWave", "Impact Core",
                "Núcleo de impacto. Poder: Onda de choque que empuja enemigos.",
                ComponentSlot.Core, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: 0, moveSpeed: -1f, weight: 0.2f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.ShockWave);

            CreateComponent("Core_Heavy_Defense", "Fortress Core",
                "Núcleo defensivo. Poder: Defensa.",
                ComponentSlot.Core, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: 0, defense: 6f, dash: -2f,
                ability: SpecialAbilityType.Defense);

            CreateComponent("Core_Light_Lightning", "Velocity Core",
                "Núcleo de velocidad. Poder: Rayos.",
                ComponentSlot.Core, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: 50, spinDecay: 0, moveSpeed: 1f, weight: -0.2f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.Lightning);

            CreateComponent("Core_Medium_Fire", "Blaze Core",
                "Núcleo ardiente. Poder: Fuego (sus golpes queman).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Attack,
                maxSpin: 50, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: 4f, defense: -6f, dash: 0,
                ability: SpecialAbilityType.Fire);

            CreateComponent("Core_Medium_Ice", "Frost Core",
                "Núcleo helado. Poder: Hielo (sus golpes congelan).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 0, spinDecay: -0.4f, moveSpeed: 0, weight: 0,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.Ice);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== FakeBlade: piezas preset creadas/actualizadas en {SAVE_PATH} ===");
        }

        /// <summary>Piezas exportadas desde Blender (Tools/Blender/export_blade_parts.py).</summary>
        public const string MODELS_PATH = "Assets/3D Models/Bayblade 01/Parts/";

        /// <summary>
        /// Modelo de una pieza: el del tipo de su arquetipo (A Ataque, B Balanceada, C Defensa,
        /// D Agilidad, GDD 3). Todos los núcleos usan el genérico. Null si no se ha exportado.
        /// </summary>
        private static GameObject LoadModel(ComponentSlot slot, BladeArchetype archetype)
        {
            string type;
            switch (archetype)
            {
                case BladeArchetype.Attack: type = "A"; break;
                case BladeArchetype.Defense: type = "C"; break;
                case BladeArchetype.Agility: type = "D"; break;
                default: type = "B"; break;
            }

            string file;
            switch (slot)
            {
                case ComponentSlot.Tip: file = $"Punta/Punta_Type_{type}"; break;
                case ComponentSlot.Body: file = $"Body/Body_Type_{type}"; break;
                case ComponentSlot.Blade: file = $"Rings/Ring_Type_{type}"; break;
                default: file = "Nucleo/Nucleo_Generico"; break;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS_PATH}{file}.fbx");
        }

        private static void CreateComponent(
            string fileName, string displayName, string description,
            ComponentSlot slot, WeightClass weightClass, BladeArchetype archetype,
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
            so.FindProperty("archetype").enumValueIndex = (int)archetype;
            so.FindProperty("model").objectReferenceValue = LoadModel(slot, archetype);
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
                "Blade_Light_RazorEdge", "Core_Light_Lightning");
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
                "Blade_Heavy_CrushWheel", "Core_Heavy_Defense");
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
