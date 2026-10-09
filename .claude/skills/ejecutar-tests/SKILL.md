---
name: ejecutar-tests
description: Ejecuta los tests de EditMode de FakeBlade (Assets/Tests/EditMode) desde Claude con Coplay y lee el resultado. Úsala después de tocar BladeFormulas, CollisionResolver, CombatConfig, piezas, presets, el catálogo o Loc, y en /fin-sesion.
---

# Ejecutar los tests de EditMode

## Antes de lanzarlos

1. `get_unity_editor_state`: no puede estar en Play, y `check_compile_errors` debe dar 0 errores.
2. **Mira si la escena abierta tiene cambios sin guardar.** Lo indica el `*` en el título de la ventana de Unity, o `EditorSceneManager.GetActiveScene().isDirty` desde `execute_script`. Si los tiene, el Test Runner abre la ventana **"Scene(s) Have Been Modified"** y Unity se queda bloqueado hasta que alguien la conteste: Coplay deja de responder (timeouts de 60 s). Antes de lanzar los tests, pregunta al usuario si quiere guardar la escena. No la guardes ni la descartes por tu cuenta.
   - Para ver si esa ventana está abierta: con PowerShell, enumera las ventanas visibles del proceso de Unity (`EnumWindows` + `GetWindowThreadProcessId`).
   - Ojo: la escena `Sandbox` puede quedar marcada como modificada justo después de una recarga de scripts, aunque un momento antes no lo estuviera. Por eso `RunTests.cs` lo vuelve a comprobar en el mismo instante de lanzar los tests y, si la escena tiene cambios, no lanza nada y lo dice. No hagas `AssetDatabase.Refresh` en el mismo paso que el lanzamiento.

## Lanzarlos

1. Copia `RunTests.cs` (en esta carpeta) al scratchpad y cambia `<SCRATCHPAD>` por su ruta absoluta.
2. `execute_script` con ese archivo. Vuelve enseguida con `started`; aunque `execute_script` diga timeout, los tests pueden estar corriendo.
3. Espera en segundo plano a que `tests.txt` contenga `hecho` (bucle con `timeout 1 tail -f /dev/null`). Tardan unos segundos.
4. Lee `tests.txt`: primero el resumen (pasan, fallan) y después cada test con el mensaje de lo que falló.

## Interpretar

- **BalanceTargetsTests** son los objetivos del GDD 2.7 (±10 %). Si falla uno después de un ajuste que el usuario sí quería, el error está en el objetivo: pregúntale si se cambia el GDD 2.7 (y el test). No toques los valores de las piezas por tu cuenta (ver la skill `equilibrio`).
- **BladeFormulasTests** y **CatalogTests**: si fallan, normalmente es un error de verdad (una fórmula que cambió de sentido, una pieza sin modelo, un texto sin traducir).
- `TestData.EstimatedHit` repite la fórmula del choque de `CollisionResolver.Resolve`. Si se cambia esa fórmula, hay que cambiarla también ahí.
