---
name: probar-en-play
description: Cómo comprobar una mecánica de FakeBlade en Play mode desde Claude con Coplay MCP. Un script "probe" con un runner (corrutina) que mueve la peonza, mide, escribe un log y hace capturas. Úsala siempre que haya que verificar gameplay, física, daño, UI en combate o input en el juego real.
---

# Probar en Play (Coplay)

## Antes de empezar

1. `get_unity_editor_state`. Si el usuario tiene Play en marcha, **pregúntale** antes de pararlo o de usarlo: puede estar probando él.
2. Termina **todas** las ediciones de scripts y espera a `check_compile_errors` = 0 **antes** de entrar en Play. Editar en Play recompila y deja nulos los campos creados por código.
3. Abre la escena: `Sandbox` para mecánicas y dummies; `BattleArena` para un combate normal. Las dos crean su MatchSetup por defecto.
4. **Dile al usuario qué va a hacer la prueba antes de lanzarla**: suele mirar la vista Game mientras corre.

## El patrón

Copia `ProbeTemplate.cs` (en esta carpeta) al **scratchpad**, nunca a `Assets/`. Cambia `NAME` y la ruta del log, y escribe la prueba en `Run()`. Luego:

1. `play_game` y espera unos 6 s a que arranque la escena (y la cuenta atrás): `for i in 1 2 3 4 5 6; do ping -n 2 127.0.0.1 >/dev/null; done`.
2. `execute_script` con el archivo del probe. Llama a `Execute()`, que crea el runner y vuelve enseguida.
3. Espera a que el log tenga la **línea exacta** `hecho`:
   ```bash
   for i in $(seq 1 200); do grep -qx "hecho" "$LOG" 2>/dev/null && break; ping -n 2 127.0.0.1 >/dev/null; done; cat "$LOG"
   ```
   - `grep -qx`, no `grep -q`: una línea como «daño **hecho**» también coincide y el bucle se corta antes de tiempo (2026-10-11: me hizo creer que el juego iba lento).
   - En este Git Bash, `timeout 1 tail -f /dev/null` no sirve como pausa; `ping -n 2 127.0.0.1` tarda 1 s.
   - Ajusta el número de vueltas a lo que dure la prueba y pon el `timeout` del Bash en consonancia (máximo 600000 ms).
4. Lee el log y las PNG (`ScreenCapture.CaptureScreenshot(path, 2)`) con Read.
5. `stop_game` cuando termines, salvo que el usuario quiera seguir jugando.

## Controlar la peonza

- `PlayerController.Update` sobrescribe el movimiento y el ataque en cada frame con lo que lee de su fuente de input. Para manejarla, dale una fuente falsa (`IBladeInputSource`) con `SetInputSource(fake)`. **Devuélvela al terminar** con `SetInputSource(null)`.
- Las teclas simuladas (`InputSystem.QueueStateEvent`) solo llegan si la vista Game tiene el foco. Si no llegan, llama al método por reflexión.
- Quédate cerca del centro de la arena (|x|, |z| ≤ ~5; el radio interior es 7 m): más lejos, la peonza choca con la pared. Una carrera recta hasta el nivel de carga 3 no cabe: carga parado y pon la velocidad al soltar.
- **Dummies e IA**: `SandboxSettings.Behaviour` (`DummyBehaviour.AI` para la IA), `SandboxSettings.AIDifficulty`, `SandboxSettings.AIProfile` (int: 0 = según su peonza; i = `AIProfiles.All[i - 1]`) y, por reflexión, los métodos privados `SetDummyCount(int)`, `SetDummyPreset(int)` (índice del preset en el catálogo: Ataque 0, Defensa 1, Agilidad 2, Balanceada 3) y `ApplyDummyBrains()` de `SandboxController`. Los comportamientos son subclases: busca la IA con `GetComponent<AIBrain>()`, su clase con `GetType()` y su perfil (asset) con `.Profile`. Para poner un perfil concreto: `AIProfiles.Ensure(go, perfil)`. Los valores de nivel y perfil son assets de `Resources/AI` que la IA lee en vivo: en un probe se pueden cambiar, pero se quedan guardados, así que restaura los originales al terminar. Para que J1 juegue con IA: `AddComponent<AIBrain>()` y `SetInputSource(ai)`; al terminar, destrúyelo y llama a `SetInputSource(null)`.
- **Trucos y valores**: `SandboxSettings.InfiniteSpin = CheatTarget.All` mantiene vivas a las dos peonzas en pruebas largas (los eventos `OnDamaged` siguen contando el daño). En el Sandbox, `CombatConfig.Active` es una copia: se puede tocar en la prueba sin cambiar el asset (compruébalo comparándola con el asset cargado).
- Eventos útiles para contar: `OnAttackLaunched(nivel)`, `OnDashExecuted`, `OnSpecialActivated`, `OnDamaged(cantidad, fuente)` de cada peonza y `CollisionResolver.OnParry(quien para, atacante, doble)`. Para seguir la física, `yield return new WaitForFixedUpdate()` paso a paso.

## Respetar el estado del usuario

- El usuario puede tocar el juego mientras corre la prueba (por ejemplo, cambiar el mando de J1 en Controles). Antes de cambiar algo (piezas, dispositivos, PlayerPrefs, CombatConfig), **lee el valor actual y al final restaura ese mismo valor**, no el de por defecto.
- Dispositivo por jugador: PlayerPrefs `FakeBlade.DevicePreference.P{n}` (0 = KB1, 1 = KB2, 2 o más = número de mando).

## Medir bien

- **Compara con las mismas piezas.** Para comparar niveles de IA o comportamientos, pon el mismo preset a las dos peonzas: J1 suele tener las piezas que eligió el usuario (2026-10-11: J1 con el preset Ataque contra un dummy Balanceado parecía que la IA perdía).
- **Las muestras cortas tienen mucho ruido.** Con 20 s por prueba, el parry del Experto salió entre 3 y 9 de 13 en ejecuciones seguidas. Usa 45 s o más, o repite, antes de sacar conclusiones.
- **Si el resultado no cuadra, traza.** Antes de cambiar código, registra frame a frame (o paso de física) los valores internos (por reflexión) alrededor del momento que falla. Así salieron hoy la IA sin cargas para el parry y el ataque desviado al rodear.
- La primera prueba nada más entrar en Play puede salir rara (escena recién cargada): si un número no cuadra, repítela.

## Informar

Resume lo que mediste con números (daño, RPM, tiempos), junto con lo esperado según el GDD (2.7, 2.8 o la sección de la mecánica). Si algo hay que probarlo con mando real, márcalo con 🎮 para el usuario.
