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

1. `play_game` y espera ~2 s a que arranque la escena.
2. `execute_script` con el archivo del probe. Llama a `Execute()`, que crea el runner y vuelve enseguida.
3. Espera a que el log ponga `hecho`, con un bucle en bash que compruebe el archivo (como pausa sirve `timeout 1 tail -f /dev/null`). No hagas una espera larga en primer plano.
4. Lee el log y las PNG (`ScreenCapture.CaptureScreenshot(path, 2)`) con Read.
5. `stop_game` cuando termines, salvo que el usuario quiera seguir jugando.

## Controlar la peonza

- `PlayerController.Update` sobrescribe el movimiento y el ataque en cada frame con lo que lee de su fuente de input. Para manejarla, dale una fuente falsa (`IBladeInputSource`) con `SetInputSource(fake)`. **Devuélvela al terminar** con `SetInputSource(null)`.
- Las teclas simuladas (`InputSystem.QueueStateEvent`) solo llegan si la vista Game tiene el foco. Si no llegan, llama al método por reflexión.
- Quédate cerca del centro de la arena (|x|, |z| ≤ ~5 en Arena_00): más lejos, la peonza atraviesa la pared. Una carrera recta hasta el nivel de carga 3 no cabe: carga parado y pon la velocidad al soltar.

## Respetar el estado del usuario

- El usuario puede tocar el juego mientras corre la prueba (por ejemplo, cambiar el mando de J1 en Controles). Antes de cambiar algo (piezas, dispositivos, PlayerPrefs, CombatConfig), **lee el valor actual y al final restaura ese mismo valor**, no el de por defecto.
- Dispositivo por jugador: PlayerPrefs `FakeBlade.DevicePreference.P{n}` (0 = KB1, 1 = KB2, 2 o más = número de mando).

## Informar

Resume lo que mediste con números (daño, RPM, tiempos), junto con lo esperado según el GDD (2.7, 2.8 o la sección de la mecánica). Si algo hay que probarlo con mando real, márcalo con 🎮 para el usuario.
