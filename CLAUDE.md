# FakeBlade

Juego de combate de peonzas (tipo Beyblade) para 2-4 jugadores locales con teclado y mandos. Unity 6 (URP), Input System, Cinemachine. El editor se controla desde Claude con **Coplay MCP**.

## Idioma y estilo

- Hablar con el usuario **en español**. El GDD, los comentarios del código y los commits también van en español.
- Los textos de la UI van por `Loc` (`Assets/Scripts/Core/Loc.cs`, tabla clave → [es, en]). Nunca escribir textos visibles a mano en el código.
- Commits: título en español con los códigos de tarea entre paréntesis, por ejemplo `... (C10)`. En el cuerpo, una lista de guiones con lo que cambia en el juego y la línea `GDD: ...` cuando se toca el documento. Mirar `git log` para copiar el tono.

## Documentos de diseño (fuente de verdad)

- `JUEGO DE PEONZAS v.2.md`: la versión nueva del diseño. **Si contradice al GDD, gana v2** y se quita del GDD el texto que la contradice.
- `FakeBlade GDD.md`: el documento único y consolidado. Secciones clave:
  - 2.7 Objetivos de equilibrio y 2.8 Fórmulas (copia en código: `BladeFormulas`).
  - 7.2 Arquitectura.
  - **11. Registro de decisiones**: cada decisión es una fila con su fecha.
  - **12. Pendiente de definir**: lo abierto, con una propuesta por defecto entre paréntesis.
  - **13. Quehaceres**: el backlog, por fases (A-I) con casillas. 🎮 marca lo que hay que probar con mandos reales.
- Si algo no está en los documentos o es ambiguo, **preguntar al usuario** (AskUserQuestion) antes de implementarlo. Después, apuntarlo en el GDD en la misma sesión: en su sección, más una fila en §11. Al terminar una tarea, marcarla en §13.
- El usuario prefiere añadir funciones poco a poco y probarlas con mando en sesiones posteriores.
- `Estadísticas de las piezas.md/.html` se generan desde el editor; no se editan a mano. `Guía del Sandbox.md` explica el panel del Sandbox y su CSV.

## Código

- Ensamblados: `FakeBlade.Runtime` (`Assets/Scripts/`) y `FakeBlade.Editor` (`Assets/Scripts/Editor/`). Namespaces: `FakeBlade.Core`, `FakeBlade.Core.Editor` y `FakeBlade.UI`.
- Carpetas: `Core` (GameManager, PlayerController, CombatConfig, catálogo, Loc...), `FakeBlade` (peonza: FakeBladeController, FakeBladeStats, BladeFormulas, modelo y pintura), `Combat` (ataque, choques, poderes, estados), `Input`, `AI`, `Sandbox`, `UI`, `VFX`.
- Datos en ScriptableObjects: `FakeBladeComponentData` (piezas), `CombatConfig`, `MatchRules`, `HUDTheme`, `SpecialAbilityData` (en `Resources/SpecialAbilities`) y `FakeBladeCatalog` (`Assets/Settings/FakeBladeCatalog.asset`). Añadir contenido es añadir entradas al catálogo.
- Las piezas se llaman `Tipo_Arquetipo_Nombre` (por ejemplo `Tip_Attack_StrikerPoint`). Los arquetipos son Ataque, Balanceada, Defensa y Agilidad.
- **`BladeFormulas` y el JavaScript de `ComponentsStatsHtml` (Editor) son la misma fórmula escrita dos veces**: si se cambia una, hay que cambiar la otra y el GDD 2.8.
- Rendimiento: cero asignaciones (GC) por frame en gameplay, HUD y efectos; partículas con emisores compartidos (`Emit()`), sin instanciar nada en combate. Mantenerlo (GDD 7.3).
- Cada peonza recibe órdenes de un `IBladeInputSource` (jugador, dummy o IA). En las pruebas se cambia con `PlayerController.SetInputSource(...)`.

## Herramientas del editor (menú FakeBlade)

- **Banco de equilibrio**: se lanza con Play en la escena Sandbox, tarda unos 7 minutos y deja el informe en `Logs/Balance/`.
- **Export ComponentsData Stats**: genera `Estadísticas de las piezas.html/.md` en la raíz.
- **Import ComponentsData Stats**: aplica el `fakeblade-piezas.json` descargado del HTML (por GUID, con vista previa y deshacer).
- Setup Specials, Setup VFX y los menús de assets crean solo lo que falta. Las piezas existentes las ajusta el usuario a mano en el Inspector.
- Blender (solo está instalada la 5.0): `Tools/Blender/export_blade_parts.py` y `export_arena.py`. Probarlos en modo headless sobre una copia en el scratchpad.

## Reglas de trabajo con Unity

- **Antes de editar scripts**: `get_unity_editor_state`. Si está en Play, pararlo primero, avisando al usuario, porque puede estar probando. Editar en Play recompila y deja nulos los campos creados por código.
- Después de editar: `check_compile_errors`. Puede necesitar un reintento mientras Unity recarga.
- Antes de un banco de pruebas o un commit, comprobar si hay piezas o assets editados sin guardar (`EditorUtility.IsDirty`) y avisar al usuario.
- Para probar en Play, usar la skill `probar-en-play`. Para tocar el equilibrio, la skill `equilibrio`. Para los tests de EditMode (`Assets/Tests/EditMode`, ensamblado `FakeBlade.Tests.EditMode`), la skill `ejecutar-tests`.
- Las escenas `BattleArena` y `Sandbox` se pueden abrir y jugar directamente (se crea un MatchSetup por defecto). Las otras escenas son `MainMenu` y `Assembly`.

## Git

- Ramas: `desarrollo` para el trabajo diario (todos los commits van aquí) y `main` estable, con lo probado. Se hace merge de `desarrollo` a `main` (avance rápido) cuando el usuario lo aprueba, normalmente al cerrar una fase o tras probar con mando. `Legacy` guarda el `main` antiguo (2026-02) y no se toca.
- Para hacer push, el usuario puede tener que lanzar los comandos él: su consola es cmd o PowerShell 5.1, que no admite `&&` (dárselos línea a línea).
- **No commitear nunca** los `.blend` / `.blend1` (modelos en curso del usuario), salvo que lo pida.
- El árbol de trabajo suele tener cambios del usuario o de otra sesión. **Solo se commitean los cambios propios**. Si un archivo mezcla cambios de los dos (por ejemplo el GDD): hacer stage del archivo entero y quitar los bloques ajenos con `git apply --cached -R --unidiff-zero`. Hacer stage bloque a bloque descolocó texto una vez.
- Commitear solo cuando el usuario lo pida o lo apruebe.

## Entorno (Windows)

- No hay Python: para scripts usar awk, bash o C# (execute_script de Coplay).
- En Git Bash, `tar` toma `C:` como un host remoto (usar `cat archivo | tar ...`). `perl s|||` se rompe con `||` dentro del código: usar `#` como delimitador.
- Los archivos temporales y los scripts de prueba van al scratchpad de la sesión, nunca a `Assets/`.

## Sesiones

- `/inicio-sesion`: prepara la sesión (estado, pendientes y siguiente tarea).
- `/fin-sesion`: deja todo listo para la próxima (GDD, commit, traspaso).
- El traspaso entre sesiones (dónde se quedó el trabajo y qué sigue) vive en la memoria de Claude (`fakeblade-session-handoff`), no en el repo.
