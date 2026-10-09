---
name: inicio-sesion
description: Prepara una sesión de trabajo en FakeBlade. Lee el traspaso de la sesión anterior, revisa git, Unity y el backlog del GDD, y propone la siguiente tarea. Úsala cuando el usuario escriba /inicio-sesion o diga que empieza a trabajar.
disable-model-invocation: true
argument-hint: "[tarea opcional, p. ej. C13]"
---

# Inicio de sesión

Objetivo: en pocos minutos y sin tocar nada, saber dónde estamos y acordar con el usuario qué hacer. **Esta skill no edita archivos ni el editor**: solo lee y resume.

## 1. Recoger el estado (en paralelo)

- **Traspaso**: lee el archivo de memoria `fakeblade-session-handoff.md` y los que enlace que vengan al caso (rasgos con condición, modelos de piezas...).
- **Git**: `git status --short`, `git log --oneline -8` y `git diff --stat`. Clasifica los cambios sin commitear en:
  - *del usuario o de otra sesión* (los que el traspaso marca como ajenos, los `.blend`, cosas que no reconoces): no se tocan ni se commitean;
  - *pendientes de una sesión anterior mía*: avisa, porque deberían haberse commiteado en el fin de sesión.
- **Unity** (Coplay): `get_unity_editor_state` (¿está en Play?, escena abierta) y `check_compile_errors`. Si Coplay no responde, dilo y sigue: el usuario puede tener Unity cerrado.
- **Backlog**: en `FakeBlade GDD.md`, busca la línea de `13. # Quehaceres` y lee esa sección. Lee también `12. # Pendiente de definir` por encima. No leas el GDD entero.
- **Tests** (si existe `Assets/Tests/`): apunta que hay que ejecutarlos antes de cambiar fórmulas.

## 2. Elegir la siguiente tarea

- Si el usuario pasó una tarea como argumento (`$ARGUMENTS`), céntrate en ella: localiza su entrada en §13 y las secciones del GDD que le afectan.
- Si no, propón **una** tarea recomendada y 1-2 alternativas, a partir de: lo que el traspaso dice que va después, las casillas sin marcar de §13 y lo que está bloqueado por §12.
- Para la tarea recomendada, comprueba si depende de algo sin decidir en §12 o en los documentos. Si es así, prepara las preguntas para el usuario, cada una con una propuesta por defecto.

## 3. Informe al usuario (corto, en español)

```
**Dónde lo dejamos**: <1-2 frases: último commit y qué se hizo>
**Estado**: <Unity: editor/Play, errores de compilación | Git: N cambios propios pendientes, N ajenos (no se tocan)>
**Avisos**: <solo si hay: errores, cambios propios sin commitear, Play activo, assets sin guardar>
**Propuesta**: <tarea> — <por qué ahora> — <qué archivos o sistemas toca>
**Alternativas**: <1-2>
**Preguntas antes de empezar**: <solo si hacen falta, con su propuesta por defecto>
```

Después, espera a que el usuario elija. No empieces a implementar en este mismo turno.
