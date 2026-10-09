---
name: equilibrio
description: Flujo para cambiar el equilibrio de FakeBlade (estadísticas de piezas, fórmulas de daño y movimiento, CombatConfig, rasgos). Cubre los objetivos del GDD, el banco de equilibrio, la exportación e importación de estadísticas y cómo mantener sincronizadas las fórmulas. Úsala antes de tocar BladeFormulas, CombatConfig, valores de piezas o tareas C* de equilibrio.
---

# Equilibrio

## Principios (decididos por el usuario)

- **El usuario ajusta las piezas a mano** (en el Inspector o en el HTML de estadísticas). No cambies los valores de las piezas por tu cuenta: propón los cambios con números y pregunta antes.
- Los pesos son casi todos positivos a propósito (con pesos negativos las peonzas iban demasiado rápidas). Cada arquetipo se distingue por su velocidad.
- La Defensa hace el mismo daño que la Balanceada a propósito: es lenta y golpea menos.
- El daño por golpe que el usuario quiere ver en juego: 20-25 normal, 30-40 cargado y 13-18 con dash. La masa solo afecta al empuje.
- Los objetivos están en el GDD **2.7** y las fórmulas en el **2.8**. Lee las dos secciones antes de proponer nada.

## Fuentes que deben decir lo mismo

| Qué | Dónde |
|---|---|
| Fórmulas (C#) | `Assets/Scripts/FakeBlade/BladeFormulas.cs` |
| Fórmulas (JS del HTML) | `Assets/Scripts/Editor/ComponentsStatsHtml.cs` |
| Documento | `FakeBlade GDD.md` §2.8 |
| Ajustes globales | asset de `CombatConfig` |
| Tests (si existen) | `Assets/Tests/EditMode/` |

Si cambias una fórmula, cambia las tres primeras en el mismo paso y ejecuta los tests.

## Herramientas

1. **Ver los números**: menú *FakeBlade → Export ComponentsData Stats*. Regenera `Estadísticas de las piezas.md/.html`. Para analizar, lee el `.md`: es más corto.
2. **Banco**: Play en la escena `Sandbox` y menú *FakeBlade → Banco de equilibrio* (con `execute_script`: `EditorApplication.ExecuteMenuItem("FakeBlade/Banco de equilibrio")`). Tarda unos 7 minutos. **No lo esperes en primer plano**: comprueba en segundo plano si aparece un `bench_*.md` nuevo en `Logs/Balance/` y lee ese informe. Compara con la tabla de objetivos (2.7).
3. **Aplicar cambios del usuario**: el usuario descarga `fakeblade-piezas.json` del HTML y se aplica con *FakeBlade → Import ComponentsData Stats* (por GUID, con vista previa y deshacer). Luego se vuelve a exportar.

## Antes del banco o de un commit

Comprueba `EditorUtility.IsDirty` en las piezas y en `CombatConfig`. Si hay ajustes del usuario sin guardar, avísale: el banco usaría esos valores y el commit no los llevaría.

## Al terminar

- GDD 2.7 / 2.8 actualizados si cambió un objetivo o una fórmula. Fila en §11 con cada decisión de equilibrio del usuario.
- En el resumen: tabla antes y después de las métricas que cambiaron (daño, tiempo de combate, velocidad), por arquetipo.
