---
name: fin-sesion
description: Cierra una sesión de trabajo en FakeBlade y deja todo preparado para la siguiente. Verifica, actualiza el GDD, propone el commit con solo los cambios propios y escribe el traspaso en la memoria. Úsala cuando el usuario escriba /fin-sesion o diga que termina por hoy.
disable-model-invocation: true
---

# Fin de sesión

Objetivo: que la próxima sesión empiece sin tener que redescubrir nada, con el trabajo verificado y commiteado.

## 1. Verificar

- Coplay: `get_unity_editor_state`. Si está en Play por una prueba mía, para el Play. Si es del usuario, pregúntale.
- `check_compile_errors`: debe dar 0 errores. Si hay alguno causado por mis cambios, arréglalo antes de seguir.
- Assets sin guardar: con `execute_script`, comprueba `EditorUtility.IsDirty` en las piezas (`FakeBladeComponentData`), en `CombatConfig` y en la escena abierta. Si hay cambios sin guardar, avisa al usuario: pueden ser ajustes suyos a mano.
- Ejecuta los tests de EditMode con la skill `ejecutar-tests` (ojo con la escena sin guardar) e informa del resultado. Si falla alguno, no se commitea sin decírselo al usuario.
- Si se tocó `BladeFormulas`, comprueba que el JavaScript de `ComponentsStatsHtml` y el GDD 2.8 dicen lo mismo.

## 2. Actualizar el GDD (solo con lo que pasó en esta sesión)

- §13 Quehaceres: marca las casillas terminadas. Si algo se probó con mando, indícalo. Apunta las tareas nuevas que hayan salido en su fase, con su código.
- §11 Registro de decisiones: una fila con la fecha de hoy por cada decisión que tomó el usuario.
- §12 Pendiente de definir: lo que quedó abierto, con la propuesta por defecto entre paréntesis.
- La sección de diseño correspondiente: el texto que describe cómo funciona ahora.

## 3. Commit (con la aprobación del usuario)

- `git status` y `git diff`: separa mis cambios de los ajenos (mira la lista de ajenos del traspaso; los `.blend` nunca se incluyen).
- Prepara el mensaje con el estilo del repo (mira `git log -5`): título en español con los códigos de tarea, lista de cambios y la línea `GDD: ...`.
- Enseña al usuario la lista de archivos y el mensaje, y **espera su aprobación** antes de commitear.
- En los archivos con cambios mezclados: haz stage del archivo entero y después `git apply --cached -R --unidiff-zero` con los bloques ajenos. Comprueba con `git diff --cached` que solo queda lo mío.
- Termina el mensaje con la línea de coautoría que indique el sistema.
- El commit va en `desarrollo`. Después, haz push de `desarrollo`.
- **Merge a `main`**: si en la sesión se cerró una fase o algo quedó probado con mando, pregunta al usuario si se pasa a `main` (`git push origin desarrollo:main`, avance rápido; nunca forzado).

## 4. Traspaso a la próxima sesión (memoria)

Reescribe `fakeblade-session-handoff.md` en la carpeta de memoria; no le añadas debajo. Mantén su frontmatter y actualiza `description` y `modified`. Contenido:

- **Estado**: fecha de hoy, rama, último commit (hash corto) y si queda trabajo propio sin commitear.
- **Qué se hizo**: 3-6 viñetas con lo que importa para seguir. No repitas lo que ya dicen el commit o el GDD: enlaza a ellos.
- **Siguiente**: las 2-3 próximas tareas de §13, en orden, y qué hay que preguntar al usuario antes de empezar cada una.
- **Ajenos en el árbol de trabajo**: la lista actual de archivos que no son míos.
- **Lecciones**: solo si hoy salió algo nuevo que haga falta recordar. Si es una norma estable del proyecto, va a `CLAUDE.md`, no aquí (proponle el cambio al usuario).

Actualiza la línea de `MEMORY.md` que apunta al traspaso. Si alguna otra memoria quedó desfasada (por ejemplo, los rasgos con condición ya se implementaron), corrígela o bórrala.

## 5. Resumen al usuario

En 4-6 líneas: qué quedó hecho, el commit (o qué queda sin commitear y por qué), qué toca en la próxima sesión y si hay que probar algo con mando (🎮) antes.
