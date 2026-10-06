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
            ["OUT"] = new[] { "ELIMINADO", "OUT" },
            ["RESPAWN"] = new[] { "REAPARECE", "RESPAWN" },
            ["TIME_UP"] = new[] { "¡TIEMPO!", "TIME!" },
            ["PLAYER_BADGE"] = new[] { "J{0}", "P{0}" },
            ["PLAYER_NAME"] = new[] { "Jugador {0}", "Player {0}" },
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
            ["SANDBOX_PLAYER"] = new[] { "JUGADOR", "PLAYER" },
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
            ["DEVICE_KB_LEFT"] = new[] { "TECLADO J1", "KEYBOARD P1" },
            ["DEVICE_KB_RIGHT"] = new[] { "TECLADO J2", "KEYBOARD P2" },
            ["DEVICE_PAD"] = new[] { "MANDO {0}", "GAMEPAD {0}" },
            ["DEVICE_PAD_ANY"] = new[] { "MANDO", "GAMEPAD" },

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
            ["ARENA_00"] = new[] { "ARENA CLÁSICA", "CLASSIC ARENA" },
            ["ARENA_TEST"] = new[] { "ARENA DE PRUEBAS", "TEST ARENA" },
        };
    }
}
