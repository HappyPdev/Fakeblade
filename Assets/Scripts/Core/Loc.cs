using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    public enum Language
    {
        Spanish = 0,
        English = 1
    }

    /// <summary>
    /// Localización ligera ES/EN (GDD 9). Sin dependencias de paquetes:
    /// una tabla clave → [es, en]. Para añadir textos, añadir una entrada a la tabla.
    /// El idioma se guarda en PlayerPrefs.
    /// </summary>
    public static class Loc
    {
        private const string PrefsKey = "fakeblade.language";

        public static event Action OnLanguageChanged;

        private static bool _initialized;
        private static Language _current;

        public static Language Current
        {
            get
            {
                EnsureInitialized();
                return _current;
            }
            set
            {
                EnsureInitialized();
                if (_current == value) return;
                _current = value;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                OnLanguageChanged?.Invoke();
            }
        }

        public static void Toggle() => Current = Current == Language.Spanish ? Language.English : Language.Spanish;

        public static string Get(string key)
        {
            EnsureInitialized();
            if (key != null && Table.TryGetValue(key, out var entry))
                return entry[(int)_current];
            return key;
        }

        /// <summary>Texto en un idioma concreto, sin cambiar el del juego (p. ej. documentos exportados).</summary>
        public static string Get(string key, Language language)
        {
            EnsureInitialized();
            if (key != null && Table.TryGetValue(key, out var entry))
                return entry[(int)language];
            return key;
        }

        public static string Format(string key, object arg0) => string.Format(Get(key), arg0);
        public static string Format(string key, object arg0, object arg1) => string.Format(Get(key), arg0, arg1);

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Language fallback = Application.systemLanguage == SystemLanguage.Spanish
                ? Language.Spanish
                : Language.English;
            _current = (Language)PlayerPrefs.GetInt(PrefsKey, (int)fallback);
        }

        // [0] = Español, [1] = English
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // Combate
            ["FIGHT"] = new[] { "¡LANZAMIENTO!", "LET IT RIP!" },
            ["KO"] = new[] { "K.O.", "K.O." },
            ["PARRY"] = new[] { "¡PARRY!", "PARRY!" },
            ["BLOCK"] = new[] { "¡BLOQUEO!", "BLOCKED!" },
            ["OUT"] = new[] { "ELIMINADO", "OUT" },
            ["RESPAWN"] = new[] { "REAPARECE", "RESPAWN" },
            ["TIME_UP"] = new[] { "¡TIEMPO!", "TIME!" },
            ["PLAYER_BADGE"] = new[] { "J{0}", "P{0}" },
            ["PLAYER_NAME"] = new[] { "Jugador {0}", "Player {0}" },
            ["CPU_BADGE"] = new[] { "IA", "AI" },
            ["CPU_NAME"] = new[] { "IA {0}", "AI {0}" },
            ["TEAM_NAME"] = new[] { "Equipo {0}", "Team {0}" },
            ["POINTS_SHORT"] = new[] { "PT", "PT" },

            // Resultado
            ["WINS"] = new[] { "¡{0} GANA!", "{0} WINS!" },
            ["DRAW"] = new[] { "¡EMPATE!", "DRAW!" },

            // Menús
            ["PAUSED"] = new[] { "PAUSA", "PAUSED" },
            ["RESUME"] = new[] { "REANUDAR", "RESUME" },
            ["RESTART"] = new[] { "REINICIAR", "RESTART" },
            ["REMATCH"] = new[] { "REVANCHA", "REMATCH" },
            ["MAIN_MENU"] = new[] { "MENÚ PRINCIPAL", "MAIN MENU" },
            ["LANGUAGE"] = new[] { "IDIOMA: ESPAÑOL", "LANGUAGE: ENGLISH" },
            ["CONFIRM_EXIT"] = new[] { "¿SALIR AL MENÚ PRINCIPAL?", "EXIT TO MAIN MENU?" },
            ["YES"] = new[] { "SÍ", "YES" },
            ["NO"] = new[] { "NO", "NO" },

            ["CHANGE_BLADES"] = new[] { "CAMBIAR PEONZAS", "CHANGE BLADES" },
            ["BACK"] = new[] { "VOLVER", "BACK" },

            // Modos
            ["MODE_LAST_STANDING"] = new[] { "ÚLTIMO EN PIE", "LAST STANDING" },
            ["MODE_STOCKS"] = new[] { "VIDAS", "STOCKS" },
            ["MODE_POINTS"] = new[] { "TODOS CONTRA TODOS", "FREE FOR ALL" },
            ["MODE_TEAMS"] = new[] { "POR EQUIPOS", "TEAMS" },
            ["MODE_SANDBOX"] = new[] { "SANDBOX", "SANDBOX" },

            // Menú principal
            ["SUBTITLE"] = new[] { "BATALLA DE PEONZAS", "SPINNING TOP BATTLES" },
            ["PLAY"] = new[] { "JUGAR", "PLAY" },
            ["LEARN_TO_PLAY"] = new[] { "APRENDE A JUGAR", "HOW TO PLAY" },
            ["OPTIONS"] = new[] { "OPCIONES", "OPTIONS" },
            ["CONTROLS"] = new[] { "CONTROLES", "CONTROLS" },
            ["INFO"] = new[] { "INFORMACIÓN", "CREDITS" },
            ["QUIT"] = new[] { "SALIR", "QUIT" },
            ["CONFIRM_QUIT"] = new[] { "¿SALIR DEL JUEGO?", "QUIT THE GAME?" },
            ["MENU_HINT"] = new[] { "ENTER / A: ACEPTAR   ·   ESC / B: ATRÁS", "ENTER / A: ACCEPT   ·   ESC / B: BACK" },

            // Opciones
            ["OPT_RESOLUTION"] = new[] { "RESOLUCIÓN", "RESOLUTION" },
            ["OPT_FULLSCREEN"] = new[] { "PANTALLA COMPLETA", "FULLSCREEN" },
            ["OPT_QUALITY"] = new[] { "CALIDAD", "QUALITY" },
            ["OPT_SHADOWS"] = new[] { "SOMBRAS", "SHADOWS" },
            ["OPT_AA"] = new[] { "ANTIALIASING", "ANTIALIASING" },
            ["OPT_BLOOM"] = new[] { "BLOOM", "BLOOM" },
            ["OPT_PARTICLES"] = new[] { "PARTÍCULAS", "PARTICLES" },
            ["OPT_VSYNC"] = new[] { "VSYNC", "VSYNC" },
            ["OPT_FPS"] = new[] { "MOSTRAR FPS", "SHOW FPS" },
            ["OPT_MASTER"] = new[] { "VOLUMEN GENERAL", "MASTER VOLUME" },
            ["OPT_MUSIC"] = new[] { "MÚSICA", "MUSIC" },
            ["OPT_SFX"] = new[] { "EFECTOS", "SOUND FX" },
            ["OPT_LANGUAGE"] = new[] { "IDIOMA", "LANGUAGE" },
            ["OPT_VIBRATION"] = new[] { "VIBRACIÓN MANDO", "GAMEPAD RUMBLE" },
            ["OPT_SHAKE"] = new[] { "SACUDIDA CÁMARA", "CAMERA SHAKE" },
            ["OPT_CORE_IMAGE"] = new[] { "ICONO DEL NÚCLEO", "CORE ICON" },
            ["OPT_TELEMETRY"] = new[] { "GUARDAR DATOS DE PARTIDA", "SAVE MATCH DATA" },
            ["OPT_PIXELATION"] = new[] { "PIXELADO", "PIXELATION" },
            ["OPT_DAMAGE_NUMBERS"] = new[] { "NÚMEROS DE DAÑO", "DAMAGE NUMBERS" },
            ["PIX_SOFT"] = new[] { "SUAVE", "SOFT" },
            ["PIX_MEDIUM"] = new[] { "MEDIO", "MEDIUM" },
            ["PIX_STRONG"] = new[] { "FUERTE", "STRONG" },
            ["OPT_BLADE_COLORS"] = new[] { "COLORES DE PEONZA", "BLADE COLORS" },
            ["BLADE_COLORS"] = new[] { "COLORES DE PEONZA", "BLADE COLORS" },
            ["BLADE_COLORS_PALETTE"] = new[] { "COLOR", "COLOR" },
            ["BLADE_COLORS_RESET"] = new[] { "RESTAURAR COLORES", "RESET COLORS" },
            ["OFF"] = new[] { "NO", "OFF" },
            ["SHADOWS_HARD"] = new[] { "DURAS", "HARD" },
            ["SHADOWS_SOFT"] = new[] { "SUAVES", "SOFT" },
            ["LEVEL_LOW"] = new[] { "BAJAS", "LOW" },
            ["LEVEL_MEDIUM"] = new[] { "MEDIAS", "MEDIUM" },
            ["LEVEL_HIGH"] = new[] { "ALTAS", "HIGH" },
            ["QUALITY_MOBILE"] = new[] { "BAJA", "LOW" },
            ["QUALITY_PC"] = new[] { "ALTA", "HIGH" },

            // Controles
            ["CTRL_DEVICES"] = new[] { "DISPOSITIVO DE CADA JUGADOR", "EACH PLAYER'S DEVICE" },
            ["CTRL_SCHEME"] = new[] { "ESQUEMA", "SCHEME" },
            ["CTRL_HINT"] = new[] { "ELIGE UNA ACCIÓN Y PULSA LA TECLA NUEVA", "PICK AN ACTION AND PRESS THE NEW KEY" },
            ["CTRL_PRESS_KEY"] = new[] { "PULSA UNA TECLA... (ESC: CANCELAR)", "PRESS A KEY... (ESC: CANCEL)" },
            ["CTRL_PRESS_BUTTON"] = new[] { "PULSA UN BOTÓN... (START: CANCELAR)", "PRESS A BUTTON... (START: CANCEL)" },
            ["CTRL_RESET"] = new[] { "RESTABLECER", "RESET DEFAULTS" },
            ["ACTION_UP"] = new[] { "ARRIBA", "UP" },
            ["ACTION_DOWN"] = new[] { "ABAJO", "DOWN" },
            ["ACTION_LEFT"] = new[] { "IZQUIERDA", "LEFT" },
            ["ACTION_RIGHT"] = new[] { "DERECHA", "RIGHT" },
            ["ACTION_ATTACK"] = new[] { "ATAQUE", "ATTACK" },
            ["ACTION_ATTACKALT"] = new[] { "ATAQUE (ALT.)", "ATTACK (ALT)" },
            ["ACTION_DASH"] = new[] { "DASH", "DASH" },
            ["ACTION_DASHALT"] = new[] { "DASH (ALT.)", "DASH (ALT)" },
            ["ACTION_SPECIAL"] = new[] { "ESPECIAL", "SPECIAL" },
            ["ACTION_SPECIALALT"] = new[] { "ESPECIAL (ALT.)", "SPECIAL (ALT)" },
            ["ACTION_SANDBOXPANEL"] = new[] { "PANEL SANDBOX", "SANDBOX PANEL" },

            // Sandbox (GDD 6.3)
            ["SANDBOX_TITLE"] = new[] { "SANDBOX", "SANDBOX" },
            ["SANDBOX_RIVALS"] = new[] { "RIVALES", "RIVALS" },
            ["SANDBOX_BEHAVIOUR"] = new[] { "DUMMIES", "DUMMIES" },
            ["SANDBOX_INTERVAL"] = new[] { "CADA", "EVERY" },
            ["SANDBOX_NONE"] = new[] { "NINGUNO", "NONE" },
            ["SANDBOX_SECTION_RIVALS"] = new[] { "RIVALES", "RIVALS" },
            ["SANDBOX_SECTION_BLADE"] = new[] { "MI PEONZA", "MY BLADE" },
            ["SANDBOX_SECTION_CHEATS"] = new[] { "TRUCOS", "CHEATS" },
            ["SANDBOX_RESET"] = new[] { "REINICIAR TODO", "RESET ALL" },
            ["CHEAT_SPIN"] = new[] { "RPM INFINITAS", "INFINITE RPM" },
            ["CHEAT_SPECIAL"] = new[] { "ESPECIAL LLENO", "FULL SPECIAL" },
            ["CHEAT_CHARGES"] = new[] { "CARGAS INFINITAS", "INFINITE CHARGES" },
            ["CHEAT_DASH"] = new[] { "DASH SIN ESPERA", "NO DASH COOLDOWN" },
            ["CHEAT_INVULNERABLE"] = new[] { "INVULNERABLE", "INVULNERABLE" },
            ["CHEAT_PLAYERS"] = new[] { "JUGADORES", "PLAYERS" },
            ["CHEAT_DUMMIES"] = new[] { "DUMMIES", "DUMMIES" },
            ["CHEAT_ALL"] = new[] { "TODOS", "ALL" },
            ["SANDBOX_SECTION_DEBUG"] = new[] { "DEBUG", "DEBUG" },
            ["SANDBOX_SECTION_TUNING"] = new[] { "AJUSTES", "TUNING" },
            ["SANDBOX_SECTION_EFFECTS"] = new[] { "EFECTOS", "EFFECTS" },
            ["SANDBOX_IMPACT"] = new[] { "REPETIR IMPACTO", "REPEAT IMPACT" },
            ["SANDBOX_IMPACT_HINT"] = new[] { "SE REPITE JUNTO A TU PEONZA AL CERRAR EL PANEL", "REPEATS NEXT TO YOUR TOP WHEN THE PANEL CLOSES" },
            ["IMPACT_HIT"] = new[] { "GOLPE {0}", "HIT {0}" },
            ["IMPACT_CHARGED"] = new[] { "CARGADO {0}", "CHARGED {0}" },
            ["IMPACT_PARRY"] = new[] { "PARRY", "PARRY" },
            ["IMPACT_BLOCK"] = new[] { "BLOQUEO", "BLOCK" },
            ["SANDBOX_TRAIL"] = new[] { "RASTRO", "TRAIL" },
            ["TRAIL_BOTH"] = new[] { "CINTA Y PARTÍCULAS", "RIBBON AND PARTICLES" },
            ["TRAIL_RIBBON"] = new[] { "SOLO CINTA", "RIBBON ONLY" },
            ["TRAIL_PARTICLES"] = new[] { "SOLO PARTÍCULAS", "PARTICLES ONLY" },
            ["SANDBOX_SPEED"] = new[] { "VELOCIDAD", "SPEED" },
            ["SPEED_PAUSE"] = new[] { "PAUSA", "PAUSE" },
            ["SANDBOX_STEP_HINT"] = new[] { "PAUSA · . / R3: AVANZAR 1 FRAME", "PAUSED · . / R3: STEP 1 FRAME" },
            ["DEBUG_PARRY"] = new[] { "VENTANA DE PARRY", "PARRY WINDOW" },
            ["DEBUG_VELOCITY"] = new[] { "VELOCIDADES", "VELOCITIES" },
            ["DEBUG_DAMAGE"] = new[] { "NÚMEROS DE DAÑO", "DAMAGE NUMBERS" },
            ["DEBUG_STATUS"] = new[] { "ESTADOS", "STATUS" },
            ["DEBUG_FPS"] = new[] { "FPS", "FPS" },
            ["DEBUG_LOG"] = new[] { "REGISTRO", "EVENT LOG" },
            ["DEBUG_RECORD"] = new[] { "GUARDAR DATOS", "SAVE DATA" },
            ["TUNE_GLOBAL_DAMAGE"] = new[] { "DAÑO GLOBAL", "GLOBAL DAMAGE" },
            ["TUNE_CHARGE_DAMAGE"] = new[] { "DAÑO POR CARGA", "DAMAGE PER CHARGE" },
            ["TUNE_ARENA"] = new[] { "ARENA", "ARENA" },
            ["TUNE_PARRY"] = new[] { "VENTANA PARRY", "PARRY WINDOW" },
            ["TUNE_ATTACK_COST"] = new[] { "COSTE ATAQUE", "ATTACK COST" },
            ["TUNE_DASH_COST"] = new[] { "COSTE DASH", "DASH COST" },
            ["TUNE_DAMAGE"] = new[] { "DAÑO CHOQUE", "CLASH DAMAGE" },
            ["TUNE_KNOCKBACK"] = new[] { "EMPUJE", "KNOCKBACK" },
            ["TUNE_ENERGY"] = new[] { "ENERGÍA ESPECIAL", "SPECIAL ENERGY" },
            ["TUNE_ATTACK_HIT"] = new[] { "DAÑO ATAQUE", "ATTACK DAMAGE" },
            ["TUNE_DASH_HIT"] = new[] { "DAÑO DASH", "DASH DAMAGE" },
            ["TUNE_RESET"] = new[] { "RESTAURAR VALORES", "RESET VALUES" },
            ["LOG_CLASH"] = new[] { "CHOQUE {0} -{1} · {2} -{3}", "CLASH {0} -{1} · {2} -{3}" },
            ["LOG_PARRY"] = new[] { "{0} HACE PARRY A {1}", "{0} PARRIES {1}" },
            ["LOG_DOUBLE_PARRY"] = new[] { "DOBLE PARRY {0} · {1}", "DOUBLE PARRY {0} · {1}" },
            ["LOG_BLOCK"] = new[] { "{0} BLOQUEA A {1}", "{0} BLOCKS {1}" },
            ["LOG_SPECIAL"] = new[] { "{0} ACTIVA {1}", "{0} USES {1}" },
            ["LOG_STATUS"] = new[] { "{0}: {1}", "{0}: {1}" },
            ["LOG_KO"] = new[] { "{0} K.O.", "{0} K.O." },
            ["LOG_HEAL_CUT"] = new[] { "{0}: CURACIÓN CORTADA", "{0}: HEAL CUT" },
            ["STATUS_BURNING"] = new[] { "QUEMADA", "BURNING" },
            ["STATUS_FROZEN"] = new[] { "CONGELADA", "FROZEN" },
            ["STATUS_LAUNCHED"] = new[] { "LANZADA", "LAUNCHED" },
            ["STATUS_INVULNERABLE"] = new[] { "INVULNERABLE", "INVULNERABLE" },
            ["SANDBOX_MAX_HINT"] = new[] { "MÁXIMO 4 PEONZAS", "4 TOPS MAX" },
            ["SANDBOX_HINT"] = new[] { "{0} · PANEL", "{0} · PANEL" },
            ["DUMMY_NAME"] = new[] { "DUMMY {0}", "DUMMY {0}" },
            ["DUMMY_IDLE"] = new[] { "QUIETOS", "IDLE" },
            ["DUMMY_MOVE"] = new[] { "SE MUEVEN", "MOVE" },
            ["DUMMY_ATTACK"] = new[] { "ATACAN", "ATTACK" },
            ["DUMMY_DASH"] = new[] { "DASH HACIA TI", "DASH AT YOU" },
            ["DUMMY_SPECIAL"] = new[] { "ESPECIAL", "SPECIAL" },
            ["DUMMY_AI"] = new[] { "IA", "AI" },
            ["SANDBOX_AI_LEVEL"] = new[] { "NIVEL IA", "AI LEVEL" },
            ["AI_EASY"] = new[] { "FÁCIL", "EASY" },
            ["AI_NORMAL"] = new[] { "NORMAL", "NORMAL" },
            ["AI_HARD"] = new[] { "DIFÍCIL", "HARD" },
            ["AI_EXPERT"] = new[] { "EXPERTO", "EXPERT" },
            ["AI_IMPOSSIBLE"] = new[] { "IMPOSIBLE", "IMPOSSIBLE" },
            ["SANDBOX_AI_PROFILE"] = new[] { "PERFIL IA", "AI STYLE" },
            ["AI_PROFILE_AUTO"] = new[] { "SEGÚN PEONZA", "FROM TOP" },
            ["SANDBOX_DUMMY_BLADE"] = new[] { "SU PEONZA", "THEIR TOP" },
            ["SANDBOX_DUMMY_CORE"] = new[] { "SU NÚCLEO", "THEIR CORE" },
            ["DUMMY_CORE_PRESET"] = new[] { "EL DE SU PEONZA", "TOP'S OWN" },
            ["DEVICE_KB_LEFT"] = new[] { "TECLADO J1", "KEYBOARD P1" },
            ["DEVICE_KB_RIGHT"] = new[] { "TECLADO J2", "KEYBOARD P2" },
            ["DEVICE_PAD"] = new[] { "MANDO {0}", "GAMEPAD {0}" },
            ["DEVICE_PAD_ANY"] = new[] { "MANDO", "GAMEPAD" },

            // Rasgos de las piezas (H8): TRAIT_ + nombre del PartTraitType en mayúsculas
            ["TRAIT_SPECIALENERGYQUICKHIT"] = new[] { "ENERGÍA (RÁPIDO)", "ENERGY (QUICK)" },
            ["TRAIT_SPECIALENERGYDASHHIT"] = new[] { "ENERGÍA (DASH)", "ENERGY (DASH)" },
            ["TRAIT_SPECIALENERGYCHARGEDHIT"] = new[] { "ENERGÍA (CARGADO)", "ENERGY (CHARGED)" },
            ["TRAIT_SPECIALENERGYPARRY"] = new[] { "ENERGÍA (PARRY)", "ENERGY (PARRY)" },
            ["TRAIT_SPECIALENERGYALL"] = new[] { "ENERGÍA DEL ESPECIAL", "SPECIAL ENERGY" },
            ["TRAIT_DASHCOOLDOWN"] = new[] { "ESPERA DEL DASH", "DASH COOLDOWN" },
            ["TRAIT_DASHCOST"] = new[] { "COSTE DEL DASH", "DASH COST" },
            ["TRAIT_ATTACKRECHARGETIME"] = new[] { "RECARGA DE ATAQUES", "ATTACK RECHARGE" },
            ["TRAIT_ATTACKCOST"] = new[] { "COSTE DE ATAQUES", "ATTACK COST" },
            ["TRAIT_CHARGETIME"] = new[] { "TIEMPO DE CARGA", "CHARGE TIME" },
            ["TRAIT_QUICKHITDAMAGE"] = new[] { "DAÑO DEL RÁPIDO", "QUICK DAMAGE" },
            ["TRAIT_CHARGEDHITDAMAGE"] = new[] { "DAÑO DEL CARGADO", "CHARGED DAMAGE" },
            ["TRAIT_DASHHITDAMAGE"] = new[] { "DAÑO DEL DASH", "DASH DAMAGE" },
            ["TRAIT_KNOCKBACKDEALT"] = new[] { "EMPUJE QUE HACES", "KNOCKBACK DEALT" },
            ["TRAIT_STATUSDURATIONDEALT"] = new[] { "DURACIÓN DE TUS ESTADOS", "STATUS DURATION" },
            ["TRAIT_DAMAGETAKEN"] = new[] { "DAÑO RECIBIDO", "DAMAGE TAKEN" },
            ["TRAIT_KNOCKBACKTAKEN"] = new[] { "EMPUJE RECIBIDO", "KNOCKBACK TAKEN" },
            ["TRAIT_WALLDAMAGETAKEN"] = new[] { "DAÑO DE PAREDES", "WALL DAMAGE" },
            ["TRAIT_THORNS"] = new[] { "ESPINAS", "THORNS" },
            ["TRAIT_STATUSDURATIONTAKEN"] = new[] { "ESTADOS QUE SUFRES", "STATUS TAKEN" },
            ["TRAIT_SLOWHITDAMAGE"] = new[] { "GOLPE LENTO", "SLOW HIT" },
            ["TRAIT_PARRYWINDOW"] = new[] { "VENTANA DE PARRY", "PARRY WINDOW" },
            ["TRAIT_TURNRATE"] = new[] { "GIRO", "TURNING" },
            ["TRAIT_ACCELERATION"] = new[] { "ACELERACIÓN", "ACCELERATION" },
            ["TRAIT_SPECIALDURATION"] = new[] { "DURACIÓN DEL PODER", "POWER DURATION" },
            ["TRAIT_HEALAMOUNT"] = new[] { "CURACIÓN", "HEALING" },

            // Información
            ["INFO_EMPTY"] = new[] { "SIN DATOS DE CRÉDITOS", "NO CREDITS DATA" },
            ["INFO_CREATED_BY"] = new[] { "UN JUEGO DE", "A GAME BY" },

            // Selección de peonzas
            ["LOBBY_TITLE"] = new[] { "SELECCIÓN DE PEONZAS", "BLADE SELECT" },
            ["LOBBY_JOIN"] = new[] { "MANTÉN A / ESPACIO / CTRL DER.\nPARA UNIRTE", "HOLD A / SPACE / RIGHT CTRL\nTO JOIN" },
            ["LOBBY_PRESET"] = new[] { "PRESET", "PRESET" },
            ["LOBBY_COLOR"] = new[] { "COLOR", "COLOR" },
            ["LOBBY_TEAM"] = new[] { "EQUIPO", "TEAM" },
            ["LOBBY_CONFIRM"] = new[] { "CONFIRMAR", "CONFIRM" },
            ["LOBBY_READY"] = new[] { "¡LISTO!", "READY!" },
            ["LOBBY_HINT"] = new[] { "ATAQUE: LISTO · MANTÉN ATRÁS: SALIR", "ATTACK: READY · HOLD BACK: LEAVE" },
            ["LOBBY_PAD_LOST"] = new[] { "MANDO DESCONECTADO", "CONTROLLER DISCONNECTED" },
            ["LOBBY_PAD_LOST_HINT"] = new[] { "MANTÉN A EN UN MANDO PARA SEGUIR CON ESTA COLUMNA", "HOLD A ON A CONTROLLER TO TAKE OVER THIS COLUMN" },
            ["LOBBY_HINT_CPU"] = new[] { "{0}: AÑADIR IA · {1}: COLUMNA", "{0}: ADD AI · {1}: COLUMN" },
            ["LOBBY_EDITING_CPU"] = new[] { "EDITANDO {0}\n{1}: CAMBIAR DE COLUMNA", "EDITING {0}\n{1}: SWITCH COLUMN" },
            ["LOBBY_CPU_HINT_IDLE"] = new[] { "SIEMPRE LISTA\nSE EDITA DESDE TU COLUMNA", "ALWAYS READY\nEDIT IT FROM YOUR COLUMN" },
            ["LOBBY_CPU_HINT_EDIT"] = new[] { "ATAQUE: VOLVER\nMANTÉN ATRÁS: QUITAR IA", "ATTACK: BACK\nHOLD BACK: REMOVE AI" },
            ["LOBBY_AI_LEVEL"] = new[] { "NIVEL IA", "AI LEVEL" },
            ["LOBBY_SPECIAL_LINE"] = new[] { "{0} · {1} CARGAS", "{0} · {1} CHARGES" },
            ["COLOR_N"] = new[] { "COLOR {0}", "COLOR {0}" },
            ["SLOT_TIP"] = new[] { "PUNTA", "TIP" },
            ["SLOT_BODY"] = new[] { "CUERPO", "BODY" },
            ["SLOT_BLADE"] = new[] { "DISCO", "BLADE" },
            ["SLOT_CORE"] = new[] { "NÚCLEO", "CORE" },
            ["PRESET_ATTACK"] = new[] { "ATAQUE", "ATTACK" },
            ["PRESET_DEFENSE"] = new[] { "DEFENSA", "DEFENSE" },
            ["PRESET_AGILITY"] = new[] { "AGILIDAD", "AGILITY" },
            ["PRESET_BALANCED"] = new[] { "BALANCEADA", "BALANCED" },
            ["PRESET_CUSTOM"] = new[] { "PERSONALIZADA", "CUSTOM" },
            ["PRESET_RANDOM"] = new[] { "ALEATORIO", "RANDOM" },
            ["ARCHETYPE_BALANCED"] = new[] { "BALANCEADA", "BALANCED" },
            ["ARCHETYPE_ATTACK"] = new[] { "ATAQUE", "ATTACK" },
            ["ARCHETYPE_DEFENSE"] = new[] { "DEFENSA", "DEFENSE" },
            ["ARCHETYPE_AGILITY"] = new[] { "AGILIDAD", "AGILITY" },

            // Aprende a jugar (GDD 6.7): estilo de juego de cada arquetipo y qué hace cada poder
            ["LOBBY_ARCHETYPE"] = new[] { "ARQUETIPO", "ARCHETYPE" },
            ["LOBBY_POWER"] = new[] { "PODER", "POWER" },
            ["ARCHETYPE_DESC_ATTACK"] = new[] {
                "PEGA FUERTE Y EMPUJA MUCHO, PERO AGUANTA MENOS. BUSCA AL RIVAL, CARGA TUS ATAQUES Y NO LE DEJES RESPIRAR.",
                "HITS HARD AND PUSHES A LOT, BUT TAKES MORE DAMAGE. CHASE YOUR RIVAL, CHARGE YOUR ATTACKS AND KEEP UP THE PRESSURE." },
            ["ARCHETYPE_DESC_DEFENSE"] = new[] {
                "PESADA Y CON MUCHAS RPM: AGUANTA GOLPES Y CUESTA EMPUJARLA, PERO ES LENTA. ESPERA, BLOQUEA Y CASTIGA AL QUE LLEGUE.",
                "HEAVY WITH LOTS OF RPM: SOAKS HITS AND IS HARD TO PUSH, BUT SLOW. WAIT, BLOCK AND PUNISH WHOEVER COMES IN." },
            ["ARCHETYPE_DESC_AGILITY"] = new[] {
                "LIGERA Y LA MÁS RÁPIDA, PERO AGUANTA POCO Y LA EMPUJAN FÁCIL. ENTRA, GOLPEA Y SAL: ESQUIVA CON EL DASH Y BUSCA LOS PARRYS.",
                "LIGHT AND THE FASTEST, BUT FRAGILE AND EASY TO PUSH. HIT AND RUN: DODGE WITH THE DASH AND GO FOR PARRIES." },
            ["ARCHETYPE_DESC_BALANCED"] = new[] {
                "UN POCO DE TODO, SIN PUNTOS DÉBILES. LA MEJOR PARA EMPEZAR: ATACA O DEFIENDE SEGÚN LO QUE HAGA EL RIVAL.",
                "A BIT OF EVERYTHING, NO WEAK SPOTS. THE BEST ONE TO START WITH: ATTACK OR DEFEND DEPENDING ON YOUR RIVAL." },
            // Pausa del tutorial (GDD 6.7, P8) y ventana Cambiar peonza
            ["TUT_REPEAT_STEP"] = new[] { "REPETIR PASO", "REPEAT STEP" },
            ["TUT_SKIP_STEP"] = new[] { "SALTAR PASO", "SKIP STEP" },
            ["TUT_REPEAT_ALL"] = new[] { "REPETIR TUTORIAL", "RESTART TUTORIAL" },
            ["TUT_QUIT"] = new[] { "SALIR DEL TUTORIAL", "QUIT TUTORIAL" },
            ["TUT_CONFIRM_QUIT"] = new[] { "¿SALIR DEL TUTORIAL?", "QUIT THE TUTORIAL?" },
            ["TUT_CONFIRM_RESTART"] = new[] { "¿REPETIR TODO EL TUTORIAL?", "RESTART THE WHOLE TUTORIAL?" },
            ["CHANGE_BLADE"] = new[] { "CAMBIAR PEONZA", "CHANGE BLADE" },
            ["CHANGE_BLADE_HINT"] = new[] { "ATAQUE: LISTO · ATRÁS: CANCELAR", "ATTACK: READY · BACK: CANCEL" },
            ["APPLY"] = new[] { "APLICAR", "APPLY" },
            // Pasos del tutorial (GDD 6.7): ventana de indicaciones
            ["TUT_STEP_N"] = new[] { "PASO {0} DE {1}", "STEP {0} OF {1}" },
            ["TUT_WELL_DONE"] = new[] { "¡BIEN!", "WELL DONE!" },
            ["TUT_HOLD"] = new[] { "MANTÉN {0}", "HOLD {0}" },
            ["TUT_MOVE_STICK"] = new[] { "STICK IZQUIERDO", "LEFT STICK" },
            ["TUT_MOVE_ARROWS"] = new[] { "FLECHAS", "ARROW KEYS" },
            ["TUT_MOVE_TITLE"] = new[] { "MOVERSE", "MOVE" },
            ["TUT_MOVE_BODY"] = new[] {
                "MUEVE TU PEONZA POR LA ARENA. LA BARRA DE TU PANEL SON TUS RPM: TU VIDA. BAJA SOLA, CON LOS GOLPES, CON LAS PAREDES Y AL ATACAR O HACER DASH. A 0, CAES.",
                "MOVE YOUR TOP AROUND THE ARENA. THE BAR ON YOUR PANEL IS YOUR RPM: YOUR LIFE. IT DROPS BY ITSELF, WITH HITS, WALLS, ATTACKS AND DASHES. AT 0, YOU'RE OUT." },
            ["TUT_DASH_TITLE"] = new[] { "DASH", "DASH" },
            ["TUT_DASH_BODY"] = new[] {
                "UN ACELERÓN PARA ESQUIVAR O COLOCARTE. GASTA RPM (VIDA) Y TIENE RECARGA: ES LA LÍNEA AMARILLA DE TU PANEL.",
                "A BURST TO DODGE OR REPOSITION. IT COSTS RPM (LIFE) AND HAS A COOLDOWN: THE YELLOW LINE ON YOUR PANEL." },
            ["TUT_ATTACK_TITLE"] = new[] { "ATAQUE RÁPIDO", "QUICK ATTACK" },
            ["TUT_ATTACK_BODY"] = new[] {
                "GOLPEA AL DUMMY. EN UN CHOQUE GANA QUIEN LLEGA MÁS RÁPIDO: MÁS VELOCIDAD, MÁS DAÑO, Y MÁS PESO, MÁS EMPUJE. SIN DIRECCIÓN VAS AL RIVAL MÁS CERCANO. CADA ATAQUE GASTA UNA CARGA (LOS CUADRADOS), QUE VUELVE SI NO ATACAS.",
                "HIT THE DUMMY. IN A CLASH THE FASTER TOP WINS: MORE SPEED, MORE DAMAGE, AND MORE WEIGHT, MORE PUSH. WITH NO DIRECTION YOU GO FOR THE CLOSEST RIVAL. EACH ATTACK USES A CHARGE (THE SQUARES), WHICH COMES BACK IF YOU DON'T ATTACK." },
            ["TUT_CHARGED_TITLE"] = new[] { "ATAQUE CARGADO", "CHARGED ATTACK" },
            ["TUT_CHARGED_BODY"] = new[] {
                "MANTÉN ATAQUE PARA CARGARLO. CADA NIVEL GASTA UNA CARGA Y DA MÁS DAÑO, ALCANCE Y EMPUJE. EN EL MÁXIMO SALE SOLO.",
                "HOLD ATTACK TO CHARGE IT. EACH LEVEL USES A CHARGE AND GIVES MORE DAMAGE, REACH AND PUSH. AT THE MAX IT FIRES BY ITSELF." },
            ["TUT_WALLS_TITLE"] = new[] { "PAREDES Y DASH", "WALLS AND DASH" },
            ["TUT_WALLS_BODY"] = new[] {
                "EMPUJA AL DUMMY CONTRA LA PARED: EL GOLPE LE QUITA RPM (Y A TI, SI CHOCAS FUERTE). EL DASH TAMBIÉN GOLPEA: SI GANA EL CHOQUE, RECUPERAS LA MITAD DE LO QUE COSTÓ.",
                "PUSH THE DUMMY INTO THE WALL: THE IMPACT TAKES ITS RPM (AND YOURS, IF YOU CRASH HARD). THE DASH HITS TOO: IF IT WINS THE CLASH, YOU GET HALF ITS COST BACK." },
            ["TUT_PARRY_TITLE"] = new[] { "PARRY", "PARRY" },
            ["TUT_PARRY_BODY"] = new[] {
                "ACÉRCATE AL DUMMY: TE ATACA CADA SEGUNDO Y AVISA CON UN ANILLO NARANJA. ATACA JUSTO CUANDO VIENE: SU ATAQUE NO TE HACE NADA Y SE QUEDA ATURDIDO. EL ANILLO CIAN MARCA TU MOMENTO DE PARRY.",
                "GET CLOSE TO THE DUMMY: IT ATTACKS EVERY SECOND AND WARNS WITH AN ORANGE RING. ATTACK RIGHT AS IT COMES: ITS ATTACK DOES NOTHING TO YOU AND IT GETS STUNNED. THE CYAN RING SHOWS YOUR PARRY WINDOW." },
            ["TUT_BLOCK_TITLE"] = new[] { "BLOQUEO", "BLOCK" },
            ["TUT_BLOCK_BODY"] = new[] {
                "AHORA CARGA SUS ATAQUES: ESPERA A QUE LO SUELTE Y ATACA JUSTO CUANDO VIENE. CONTRA UN CARGADO FUERTE, EL PARRY ES UN BLOQUEO: RECIBES LA MITAD DEL DAÑO Y DEL EMPUJE.",
                "NOW IT CHARGES ITS ATTACKS: WAIT FOR THE RELEASE AND ATTACK RIGHT AS IT COMES. AGAINST A STRONG CHARGED ATTACK, A PARRY BECOMES A BLOCK: YOU TAKE HALF THE DAMAGE AND PUSH." },
            ["TUT_SPECIAL_TITLE"] = new[] { "ESPECIAL", "SPECIAL" },
            ["TUT_SPECIAL_BODY"] = new[] {
                "LA ESFERA DE TU PANEL SE LLENA CON EL TIEMPO Y CON GOLPES, PARRYS Y BLOQUEOS. LLENA, ACTÍVALA: CURA UN 25% Y RELLENA LAS CARGAS, PERO UN GOLPE ENEMIGO CORTA LA CURACIÓN. ESTA VEZ TE LA DAMOS LLENA.",
                "THE SPHERE ON YOUR PANEL FILLS OVER TIME AND WITH HITS, PARRIES AND BLOCKS. WHEN FULL, ACTIVATE IT: IT HEALS 25% AND REFILLS YOUR CHARGES, BUT AN ENEMY HIT CUTS THE HEALING. THIS TIME IT'S ALREADY FULL." },
            ["TUT_FIGHT_TITLE"] = new[] { "COMBATE", "FIGHT" },
            ["TUT_FIGHT_BODY"] = new[] {
                "EL DUMMY AHORA ES UNA IA FÁCIL. ¡USA TODO LO QUE HAS APRENDIDO Y GÁNALE!",
                "THE DUMMY IS NOW AN EASY AI. USE EVERYTHING YOU'VE LEARNED AND BEAT IT!" },
            ["TUT_DONE_TITLE"] = new[] { "¡TUTORIAL COMPLETADO!", "TUTORIAL COMPLETE!" },
            ["TUT_DONE_BODY"] = new[] {
                "YA SABES JUGAR. SIGUE PRACTICANDO LO QUE QUIERAS; EN LA PAUSA PUEDES CAMBIAR DE PEONZA, REPETIR EL TUTORIAL O SALIR.",
                "YOU KNOW HOW TO PLAY. KEEP PRACTICING AS MUCH AS YOU LIKE; IN THE PAUSE MENU YOU CAN CHANGE YOUR BLADE, RESTART THE TUTORIAL OR QUIT." },

            ["POWER_DESC_SPINBOOST"] = new[] {
                "DURANTE 5 S VA MÁS RÁPIDA, RECARGA ANTES, NO SE DESGASTA Y SE CURA HASTA QUE LA GOLPEAN. PARA PRESIONAR O RECUPERARTE.",
                "FOR 5 S IT'S FASTER, RECHARGES SOONER, DOESN'T WEAR DOWN AND HEALS UNTIL HIT. TO PRESS OR TO RECOVER." },
            ["POWER_DESC_SHOCKWAVE"] = new[] {
                "DOS ONDAS QUE EMPUJAN Y DAÑAN A LAS PEONZAS CERCANAS (LA SEGUNDA, PULSANDO OTRA VEZ). CUANDO TE RODEEN O CONTRA LA PARED.",
                "TWO WAVES THAT PUSH AND DAMAGE NEARBY TOPS (PRESS AGAIN FOR THE SECOND). WHEN SURROUNDED OR NEAR A WALL." },
            ["POWER_DESC_DEFENSE"] = new[] {
                "CASI NO PIERDE RPM NI LA EMPUJAN Y RECARGA ANTES, PERO VA MÁS LENTA. PARA AGUANTAR UN ATAQUE FUERTE O CON POCA VIDA.",
                "TAKES ALMOST NO DAMAGE OR PUSH AND RECHARGES FASTER, BUT MOVES SLOWER. TO TANK A BIG ATTACK OR WHEN LOW ON LIFE." },
            ["POWER_DESC_LIGHTNING"] = new[] {
                "EMPUJA FUERTE AL CHOCAR, Y SU PRIMER CHOQUE FUERTE LANZA AL RIVAL CON MÁS DAÑO. PARA ESTRELLARLO CONTRA LA PARED.",
                "PUSHES HARD ON CONTACT, AND ITS FIRST STRONG HIT LAUNCHES THE RIVAL WITH EXTRA DAMAGE. TO SMASH THEM INTO THE WALL." },
            ["POWER_DESC_FIRE"] = new[] {
                "TUS GOLPES QUEMAN: EL RIVAL PIERDE RPM DURANTE 3 S. ENCADENA GOLPES PARA QUE NO SE APAGUE.",
                "YOUR HITS BURN: THE RIVAL LOSES RPM FOR 3 S. CHAIN HITS TO KEEP THE FIRE GOING." },
            ["POWER_DESC_ICE"] = new[] {
                "TUS GOLPES CONGELAN: EL RIVAL VA MÁS LENTO Y RECARGA PEOR, Y TÚ VAS MÁS RÁPIDA. TAMBIÉN APAGA TU QUEMADURA.",
                "YOUR HITS FREEZE: THE RIVAL GETS SLOWER AND RECHARGES WORSE, AND YOU GET FASTER. IT ALSO PUTS OUT YOUR BURN." },
            ["POWER_DESC_GHOST"] = new[] {
                "CREA UN CLON QUE MOLESTA A LOS RIVALES Y CORTA SUS CARGAS; AGUANTA 3 GOLPES. PARA DISTRAER Y ATACAR POR OTRO LADO.",
                "CREATES A CLONE THAT HARASSES RIVALS AND BREAKS THEIR CHARGES; IT TAKES 3 HITS. TO DISTRACT AND STRIKE FROM ANOTHER SIDE." },
            ["POWER_DESC_SPIKES"] = new[] {
                "QUIEN TE ATACA SE LLEVA BUENA PARTE DEL DAÑO, Y TÚ RECIBES MENOS Y PEGAS MÁS. CUANDO EL RIVAL VAYA A POR TI.",
                "WHOEVER ATTACKS YOU TAKES MUCH OF THE DAMAGE BACK; YOU TAKE LESS AND HIT HARDER. WHEN THE RIVAL COMES FOR YOU." },
            ["POWER_DESC_BOUNCE"] = new[] {
                "QUIEN TE EMBISTE SALE DESPEDIDO Y TÚ NO PIERDES RPM EN ESE CHOQUE. CERCA DE LA PARED, PARA QUE EL RIVAL SE ESTRELLE.",
                "WHOEVER RAMS YOU GETS FLUNG AWAY AND YOU LOSE NO RPM. NEAR A WALL, SO THE RIVAL CRASHES INTO IT." },
            ["POWER_DESC_SHIELDS"] = new[] {
                "TRES ESCUDOS GIRAN A TU ALREDEDOR: CADA UNO PARA UN CHOQUE Y DESPIDE AL RIVAL. DURAN HASTA ROMPERSE; OJO CON LOS HUECOS.",
                "THREE SHIELDS SPIN AROUND YOU: EACH ONE STOPS A HIT AND BOUNCES THE RIVAL. THEY LAST UNTIL BROKEN; MIND THE GAPS." },
            ["POWER_DESC_TORNADO"] = new[] {
                "ATRAE A LAS PEONZAS CERCANAS Y LUEGO LAS EXPULSA CON UN EMPUJÓN. CON VARIOS RIVALES CERCA O PARA LANZARLOS A LA PARED.",
                "PULLS NEARBY TOPS IN AND THEN BLASTS THEM AWAY. WITH SEVERAL RIVALS CLOSE OR TO THROW THEM AT THE WALL." },
            ["POWER_DESC_LEAP"] = new[] {
                "SALTA HACIA DONDE APUNTES Y AL CAER HACE UNA ONDA PEQUEÑA Y FUERTE. PARA CAER ENCIMA DEL RIVAL O ESCAPAR.",
                "JUMPS WHERE YOU AIM AND LANDS WITH A SMALL, STRONG WAVE. TO LAND ON YOUR RIVAL OR TO ESCAPE." },
            ["POWER_DESC_WATERTRAIL"] = new[] {
                "DEJA UN RASTRO DE AGUA QUE HACE RESBALAR A LOS RIVALES; TÚ VAS MÁS RÁPIDA SOBRE ÉL. DA VUELTAS ALREDEDOR DEL RIVAL.",
                "LEAVES A WATER TRAIL THAT MAKES RIVALS SLIP; YOU GO FASTER ON IT. CIRCLE AROUND YOUR RIVAL." },
            ["POWER_DESC_RAM"] = new[] {
                "DOS EMBESTIDAS MUY FUERTES QUE NO ADMITEN PARRY (LA SEGUNDA, PULSANDO OTRA VEZ). APUNTA BIEN: GOLPEAN EN UN ÁREA AMPLIA.",
                "TWO VERY STRONG CHARGES THAT CAN'T BE PARRIED (PRESS AGAIN FOR THE SECOND). AIM WELL: THEY HIT A WIDE AREA." },
            // Los nombres de los poderes están en su asset (SpecialAbilityData.nameEs / nameEn)
            ["STAT_ATTACK"] = new[] { "ATAQUE", "ATTACK" },
            ["STAT_DEFENSE"] = new[] { "DEFENSA", "DEFENSE" },
            ["STAT_SPEED"] = new[] { "VELOCIDAD", "SPEED" },
            ["STAT_RPM"] = new[] { "RPM", "RPM" },
            ["STAT_WEIGHT"] = new[] { "PESO", "WEIGHT" },

            // Parámetros de partida
            ["SET_TITLE"] = new[] { "PARTIDA", "MATCH" },
            ["SET_MODE"] = new[] { "MODO", "MODE" },
            ["SET_LIVES"] = new[] { "VIDAS", "STOCKS" },
            ["SET_TIME"] = new[] { "TIEMPO", "TIME" },
            ["SET_POINTS"] = new[] { "PUNTOS", "POINTS" },
            ["SET_FRIENDLY_FIRE"] = new[] { "FUEGO AMIGO", "FRIENDLY FIRE" },
            ["SET_ARENA"] = new[] { "ESCENARIO", "STAGE" },
            ["SET_START"] = new[] { "¡EMPEZAR!", "START!" },
            ["SET_HINT"] = new[] { "SOLO J1 · ATRÁS: VOLVER A LAS PEONZAS", "P1 ONLY · BACK: RETURN TO BLADES" },
            ["SET_NO_LIMIT"] = new[] { "SIN LÍMITE", "NO LIMIT" },
            ["SET_NO_TARGET"] = new[] { "SIN OBJETIVO", "NO TARGET" },
            ["SET_TEAMS_INVALID"] = new[] { "CADA EQUIPO NECESITA AL MENOS UN JUGADOR", "EACH TEAM NEEDS AT LEAST ONE PLAYER" },

            // Arenas
            ["ARENA_BASIC_2"] = new[] { "ARENA BÁSICA", "BASIC ARENA" },
            ["ARENA_V2"] = new[] { "ARENA PLANA", "FLAT ARENA" },
            ["ARENA_00"] = new[] { "ARENA CLÁSICA", "CLASSIC ARENA" },
            ["ARENA_TEST"] = new[] { "ARENA DE PRUEBAS", "TEST ARENA" },
        };
    }
}
