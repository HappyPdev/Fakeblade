using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Utilidad de editor para crear las piezas preset de FakeBlade (GDD 3).
    ///
    /// Puntas, cuerpos y discos: una pieza por arquetipo (Ataque, Balanceada, Defensa, Agilidad), cada
    /// una con su modelo A-D. Núcleos: uno por poder.
    ///
    /// Archivos con nombre Tipo_Arquetipo_Nombre (Tip_Attack_StrikerPoint, Core_Defense_ShockWave...): se
    /// clasifican por arquetipo, no por peso, para poder añadir más adelante arquetipos mixtos. La clase de
    /// peso (Ligera, Media, Pesada) sigue siendo un dato de la pieza.
    ///
    /// Montando las piezas sale el arquetipo de la peonza (el que más se repite entre sus piezas).
    ///
    /// Solo crea las piezas que faltan: las que ya existen se ajustan a mano en el Inspector (estadísticas
    /// y rasgos) y este menú no las sobrescribe.
    /// </summary>
    public static class FakeBladeComponentPresets
    {
        public const string SAVE_PATH = "Assets/Settings/ComponentsData/";

        [MenuItem("FakeBlade/Create All Component Presets")]
        public static void CreateAllPresets()
        {
            FakeBladeAssetsMenu.EnsureFolder(SAVE_PATH.TrimEnd('/'));

            // Los rasgos (lista Rasgos) no se tocan aquí: se ponen en cada asset (GDD 3)

            // === PUNTAS ===
            CreateComponent("Tip_Agility_NeedlePoint", "Needle Point",
                "Punta ultrafina. Mínima fricción, máxima velocidad. Estabilidad reducida.",
                ComponentSlot.Tip, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: 0, spinDecay: 1f, moveSpeed: 4f, weight: -0.2f,
                attack: 0, defense: -3f, dash: 3f);

            CreateComponent("Tip_Balanced_FlatBase", "Flat Base",
                "Punta plana equilibrada. Buena estabilidad y velocidad decente.",
                ComponentSlot.Tip, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 50, spinDecay: -0.5f, moveSpeed: 1f, weight: 0f,
                attack: 0, defense: 0, dash: 0);

            CreateComponent("Tip_Defense_WideBall", "Wide Ball",
                "Punta esférica ancha. Máxima estabilidad, pero lenta.",
                ComponentSlot.Tip, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 100, spinDecay: -1.5f, moveSpeed: -3f, weight: 0.3f,
                attack: 0, defense: 5f, dash: -3f);

            // Valores provisionales hasta el pase de equilibrio (H9)
            CreateComponent("Tip_Attack_StrikerPoint", "Striker Point",
                "Punta de ataque. Agarra el suelo para embestir fuerte, pero se desgasta antes.",
                ComponentSlot.Tip, WeightClass.Medium, BladeArchetype.Attack,
                maxSpin: 0, spinDecay: 0.5f, moveSpeed: 2f, weight: 0f,
                attack: 2f, defense: 0, dash: 1f);

            // === CUERPOS ===
            CreateComponent("Body_Agility_AeroShell", "Aero Shell",
                "Cuerpo ultraligero. Se mueve como el viento pero sale volando en los choques.",
                ComponentSlot.Body, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: -50, spinDecay: 0.5f, moveSpeed: 3f, weight: -0.4f,
                attack: 0, defense: -5f, dash: 2f, charges: 1);

            CreateComponent("Body_Balanced_StandardFrame", "Standard Frame",
                "Cuerpo estándar bien balanceado. Sin sorpresas.",
                ComponentSlot.Body, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 0, spinDecay: 0, moveSpeed: 0, weight: 0.3f,
                attack: 0, defense: 5f, dash: 0);

            CreateComponent("Body_Defense_IronFortress", "Iron Fortress",
                "Cuerpo macizo de hierro. Imparable una vez en movimiento. Cuesta arrancar.",
                ComponentSlot.Body, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: -0.3f, moveSpeed: -4f, weight: 1.2f,
                attack: 0, defense: 15f, dash: -4f, charges: -1);

            CreateComponent("Body_Attack_AssaultFrame", "Assault Frame",
                "Cuerpo de asalto. Reparte el peso hacia delante para golpear más, a cambio de defensa.",
                ComponentSlot.Body, WeightClass.Medium, BladeArchetype.Attack,
                maxSpin: 0, spinDecay: 0.2f, moveSpeed: 1f, weight: 0.2f,
                attack: 2f, defense: 4f, dash: 1f);

            // === DISCOS ===
            CreateComponent("Blade_Attack_RazorEdge", "Razor Edge",
                "Disco afilado y ligero. Muchos ataques rápidos pero poco empuje.",
                ComponentSlot.Blade, WeightClass.Light, BladeArchetype.Attack,
                maxSpin: 0, spinDecay: 0.3f, moveSpeed: 1f, weight: -0.1f,
                attack: 3f, defense: 4f, dash: 1f, charges: 1);

            CreateComponent("Blade_Balanced_BalancedRing", "Balanced Ring",
                "Anillo equilibrado. Buen ataque y defensa decente.",
                ComponentSlot.Blade, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 30, spinDecay: 0, moveSpeed: 0, weight: 0.2f,
                attack: 9f, defense: 5f, dash: 0);

            CreateComponent("Blade_Defense_CrushWheel", "Crush Wheel",
                "Disco de demolición. Impactos devastadores. Muy pesado.",
                ComponentSlot.Blade, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: -30, spinDecay: 0.5f, moveSpeed: -2f, weight: 0.6f,
                attack: 0f, defense: 10f, dash: 2f);

            CreateComponent("Blade_Agility_GaleRing", "Gale Ring",
                "Anilla ligera y aerodinámica. Más velocidad y dash, poco aguante.",
                ComponentSlot.Blade, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: -20, spinDecay: 0.2f, moveSpeed: 2f, weight: -0.2f,
                attack: 6f, defense: 0, dash: 2f, charges: 1);

            // === NÚCLEOS (GDD 3) ===
            // Uno por poder. Todos +50 RPM máx. y una ventaja y un coste del mismo tamaño (2 puntos;
            // 1 punto = 25 RPM = 0,5 velocidad = 0,1 peso = 2 ataque = 3 defensa = 1 dash = 0,2 desgaste).
            // Fantasma tendrá el suyo con su poder (C5): Ligera, +2 dash, +0,4 desgaste.
            CreateComponent("Core_Balanced_SpinBoost", "Endurance Core",
                "Núcleo de resistencia. Poder: Spin Boost (recupera RPM).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 100, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: -4f, defense: 0, dash: 0,
                ability: SpecialAbilityType.SpinBoost);

            CreateComponent("Core_Defense_ShockWave", "Impact Core",
                "Núcleo de impacto. Poder: Onda de choque que empuja enemigos.",
                ComponentSlot.Core, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: 0, moveSpeed: -1f, weight: 0.2f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.ShockWave);

            CreateComponent("Core_Defense_Defense", "Fortress Core",
                "Núcleo defensivo. Poder: Defensa.",
                ComponentSlot.Core, WeightClass.Heavy, BladeArchetype.Defense,
                maxSpin: 50, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: 0, defense: 6f, dash: -2f,
                ability: SpecialAbilityType.Defense);

            CreateComponent("Core_Agility_Lightning", "Velocity Core",
                "Núcleo de velocidad. Poder: Rayos.",
                ComponentSlot.Core, WeightClass.Light, BladeArchetype.Agility,
                maxSpin: 50, spinDecay: 0, moveSpeed: 1f, weight: -0.2f,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.Lightning);

            CreateComponent("Core_Attack_Fire", "Blaze Core",
                "Núcleo ardiente. Poder: Fuego (sus golpes queman).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Attack,
                maxSpin: 50, spinDecay: 0, moveSpeed: 0, weight: 0,
                attack: 4f, defense: -6f, dash: 0,
                ability: SpecialAbilityType.Fire);

            CreateComponent("Core_Balanced_Ice", "Frost Core",
                "Núcleo helado. Poder: Hielo (sus golpes congelan).",
                ComponentSlot.Core, WeightClass.Medium, BladeArchetype.Balanced,
                maxSpin: 0, spinDecay: -0.4f, moveSpeed: 0, weight: 0,
                attack: 0, defense: 0, dash: 0,
                ability: SpecialAbilityType.Ice);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== FakeBlade: piezas que faltaban creadas en {SAVE_PATH} (las que ya existían no se tocan) ===");
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

            // Las piezas que ya existen no se tocan: sus valores se ajustan a mano en el Inspector (H8).
            // Estos valores solo sirven para crear las que falten.
            if (AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(path) != null) return;
            var component = ScriptableObject.CreateInstance<FakeBladeComponentData>();

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

            AssetDatabase.CreateAsset(component, path);
        }

        // === QUICK EQUIP === (una build por arquetipo, con sus piezas)

        [MenuItem("FakeBlade/Quick Equip/Ataque")]
        public static void QuickEquipAttack()
        {
            QuickEquipPreset("Attack", "Tip_Attack_StrikerPoint", "Body_Attack_AssaultFrame",
                "Blade_Attack_RazorEdge", "Core_Attack_Fire");
        }

        [MenuItem("FakeBlade/Quick Equip/Balanceada")]
        public static void QuickEquipBalanced()
        {
            QuickEquipPreset("Balanced", "Tip_Balanced_FlatBase", "Body_Balanced_StandardFrame",
                "Blade_Balanced_BalancedRing", "Core_Balanced_SpinBoost");
        }

        [MenuItem("FakeBlade/Quick Equip/Defensa")]
        public static void QuickEquipDefense()
        {
            QuickEquipPreset("Defense", "Tip_Defense_WideBall", "Body_Defense_IronFortress",
                "Blade_Defense_CrushWheel", "Core_Defense_Defense");
        }

        [MenuItem("FakeBlade/Quick Equip/Agilidad")]
        public static void QuickEquipAgility()
        {
            QuickEquipPreset("Agility", "Tip_Agility_NeedlePoint", "Body_Agility_AeroShell",
                "Blade_Agility_GaleRing", "Core_Agility_Lightning");
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
