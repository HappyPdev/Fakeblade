# FakeBlade

# Game Design Document

Juego arcade de duelos de peonzas que recrea la serie de dibujos de Beyblade, para hasta 4 jugadores en local.

> **Documento consolidado.** La fuente prioritaria es *"JUEGO DE PEONZAS v.2"*: si algo de este documento contradice al v2, manda el v2 y se ha eliminado de aquí.
> Las decisiones tomadas durante el desarrollo están en la sección **11. Registro de decisiones**, lo que falta por decidir en **12. Pendiente de definir** y la lista de trabajo en **13. Quehaceres**.
> Última actualización: 2026-10-05.

1. # Visión General

## 1.1. Concepto

FakeBlade es un juego arcade de combate en 3D entre jugadores que controlan peonzas (beyblades) en batallas dinámicas dentro de arenas circulares. Combina la física de choque entre peonzas con habilidades especiales, inspirado en la serie Beyblade.

- Mecánicas simples, ágiles y fáciles de aprender.
- Estilo arcade: partidas rápidas, controles directos y acción inmediata.
- Multijugador local para 2-4 jugadores simultáneos.

## 1.2. Pilares del Diseño

- Mecánicas simples de movimiento que simulan un poco la física de choques y movimiento, sin depender de ella al 100%.
- Estrategia en función de la peonza: cada peonza se monta con piezas intercambiables (ver sección 3).
- Estilo retro con shaders pixelados y efectos de partículas.
- Multijugador competitivo o por equipos.

## 1.3 Plataformas y Audiencia

Plataformas: PC (Windows) y navegador con WebGL. Más adelante, **Linux** y **Android**. Lo que implica cada una (API gráficas, render, controles y limitaciones) está en **7.5**.
Audiencia: a partir de 8 años.

2. # Mecánicas Core

## 2.1 Sistema de Física de la peonza

- **Velocidad angular (RPM):** cada peonza tiene unas revoluciones máximas distintas según sus piezas, sobre una base de **400 RPM** (antes 350, subida para que los combates duren más).
- **Masa e inercia:** dependen de las piezas montadas.
- **Fricción dinámica:** superficie + colisiones.
- **Giroscopio:** efecto de estabilización. Internamente, la peonza se mantiene estable sola hasta que se le termina la "estamina" (RPM).
- **Precisión:** bamboleo cuando pierde velocidad. Por debajo del **30% de RPM** la peonza se bambolea: su inclinación da vueltas (precesión) y crece hasta 12° cerca de 0 RPM. Además echa **humo** y **chispas sueltas**, cada vez más cuanto menos RPM le quedan. Por ahora el bamboleo es solo visual y no afecta al control. Se ajusta en `CombatConfig` → `lowSpinThreshold`, `lowSpinWobbleAngle` y `lowSpinWobbleFrequency`.
- **Estela y chispas:** al moverse deja una estela de píxeles de su color (más grande y opaca durante ataques y dash). Además, mientras dura el acelerón de un ataque o un dash deja una **estela continua** de su color, para que el impulso se note aunque ya fuera rápida. Si va rápido con muchas RPM, la punta saca chispas contra el suelo.
- **Giro visual:** la velocidad a la que se ve girar la peonza no es proporcional a las RPM. Sigue una curva de forma logarítmica: se mantiene casi al máximo durante casi toda la vida y solo se frena de golpe cuando las RPM están muy cerca de 0. Por defecto gira al 90% con un 8% de RPM y al 94% con un 10%. Solo por debajo del 5% se nota que frena. El punto de frenado se ajusta en `CombatConfig` → `visualSpinKnee`.
- **Control:** **una estadística, un efecto** (C11 y C13, 2026-10-10). La **velocidad** da la velocidad máxima; el **peso**, la aceleración y los acelerones del ataque y del dash (una sola vez, con curvas sin topes); el **agarre**, el giro (cuánto derrapa al cambiar de dirección) y el frenado al soltar el stick. Las velocidades máximas se acercan a una de referencia (`speedSpread` 0,5 y `referenceMaxSpeed` 15,5) para que ninguna sea incómoda. La masa física (`minPhysicalMass` 0,6 como mínimo) solo cuenta en los choques. Fórmulas y valores en 2.8.
- **Movimiento:** se aplica una fuerza en la dirección indicada con el mando. Al hacer un ataque o un dash, la peonza acelera en la dirección en la que avanza. Si no se está moviendo, lo hace hacia el enemigo más cercano.

## 2.2 Controles

Cada jugador controla su peonza con mando o con teclado. Las acciones son: **movimiento, ataque (rápido o cargado), dash y habilidad especial**, más **pausa**.

Controles por defecto (reasignables desde el menú Controles):

| Acción | Teclado J1 | Teclado J2 | Mando |
|---|---|---|---|
| Movimiento | W A S D | Flechas | Stick izquierdo / Cruceta |
| Ataque (mantener = cargado) | Espacio | Ctrl derecho / Num 0 | A (Sur) / RB |
| Dash | Shift izquierdo | Shift derecho / Num 1 | B (Este) / RT |
| Especial | E | Enter / Num 2 | Y (Norte) / LT |
| Pausa | Esc | Esc | Start |
| Panel del sandbox (solo en el sandbox) | Tab | — | Select |

Hasta **2 jugadores pueden compartir el teclado** (J1 con WASD y J2 con las flechas). El resto usa mando.

**Vibración del mando** (se apaga en Opciones → Juego): pulso al lanzar un ataque (más fuerte cuanto más cargado), zumbido suave mientras se carga que sube con el nivel y un pulso al subir cada nivel, pulso fuerte en el dash, golpes dados (motor agudo) y recibidos (motor grave) según el daño, golpes contra la pared, K.O. y un aviso grave cuando un golpe corta la curación del poder. Un aviso flojo no corta a uno más fuerte que aún dura.

## 2.3 Sistema de Ataque

- **Ataque rápido:** al pulsar el botón, la peonza hace una pequeña aceleración en la dirección del joystick. Gana menos velocidad que un dash o un ataque cargado, pero durante el ataque tiene una **bonificación de masa**. Si no hay dirección marcada, ataca hacia el enemigo más cercano.
- **Cargas de ataque:** cada peonza tiene un número de cargas que se gastan al atacar y se recuperan con el tiempo, **solo sin atacar**: cada ataque (rápido, cargado o parry) pone a 0 la carga que se estaba recargando, la recarga no corre mientras se carga un ataque y vuelve a contar **0,5 s después** de terminar el ataque (`attackRechargeDelay`, 2026-10-11). Quien ataca sin parar apenas recarga. La media es **3 cargas**. Las piezas ligeras dan más cargas y las pesadas menos. Se muestran como puntos debajo de la barra de vida.
- **Ataques rápidos seguidos (combos):** pulsando repetidamente se encadenan varios ataques, cada uno con su coste en cargas y RPM. Los golpes consecutivos acumulan una pequeña bonificación de daño y de empuje.
- **Ataque cargado:** al mantener pulsado el botón de ataque, sube el nivel de carga. Cada nivel da **más alcance** (acelerón +60%) y **más empuje** (+40%), y **+25% de daño** sobre el de un ataque rápido: el daño se calcula como el de un ataque rápido (sin la velocidad extra de la carga, quitada **en proporción**: al soltarlo se guarda qué parte de su velocidad tendría un rápido lanzado igual, y al chocar cuenta esa parte de la velocidad real) y se multiplica por 1 + 0,25 × nivel (nivel 1 = ×1,25, nivel 2 = ×1,5, nivel 3 = ×1,75; antes +10% por nivel, que jugando apenas se notaba, y al principio llegaba a ×3,7 porque velocidad y masa se multiplicaban). Se ajusta con `chargedDamagePerLevel` en `CombatConfig`. El nivel máximo de carga depende de las cargas restantes, y el ataque gasta tantas cargas como niveles se hayan cargado. Si se mantiene en el máximo un tiempo, el ataque se lanza solo. **Mientras se carga, la peonza se mueve a su velocidad normal** (sin el freno al 60%; probado con mando el 2026-10-07: no se echa de menos, porque para apuntar el cargado ya hay que usar el movimiento o soltarlo un instante para el autoapuntado).
- **Efecto de la carga:** mientras se carga, unas partículas convergen hacia la peonza. En cada nivel son más, más grandes y pasan del color del jugador a un blanco dorado, y sale un anillo en el suelo. Al llegar al máximo hay un destello de estrellas y salen llamas de la base hasta que se lanza.
- **Daño de los ataques:** un golpe con ataque (rápido o cargado) hace un **20% más** que un choque sin atacar (`attackHitDamageMultiplier` 1,2), para que atacar compense frente al dash.
- **Coste:** los ataques cuestan RPM, igual que el dash.
- **Cooldown:** entre ataques cargados hay un tiempo de enfriamiento.

## 2.4 Sistema de Dash

El dash es un acelerón **mayor que el de los ataques** que se hace con un botón propio. Sirve para dos cosas:

- **Esquivar o reposicionarse:** para salir de un ataque o buscar una posición mejor contra el objetivo.
- **Atacar:** si durante el dash la peonza choca contra otra, **cuenta como un ataque** y tiene la misma prioridad que un ataque, aplicando la regla de velocidad del sistema de choque (2.5).

Cuesta **8% de las RPM máximas** y tiene **1,8 s** de espera (antes 6% y 1,5 s). **No tiene bonificación de masa ni armadura**, y sus golpes hacen un **25% menos de daño** (`dashHitDamageMultiplier` 0,75): llega mucho más rápido que un ataque y el daño sale de la velocidad, así que sin esa rebaja era el mejor golpe para todos los arquetipos. Ahora sirve para moverse, esquivar y rematar; el golpe principal es el ataque. Las piezas pesadas (Defensa) alargan su espera (ver rasgos en 3).

**Dash acertado:** si durante la ventana del dash la peonza **gana un choque contra un enemigo** (llega más rápida, según la regla de 2.5), recupera el **50% de las RPM que le costó el dash**. Solo una vez por dash. Si pierde el choque, choca con un compañero o no toca a nadie, no recupera nada. La fracción se ajusta en `CombatConfig` → `dashHitRefundFraction`.

**Dirección (sin autoapuntado con dirección):** el dash y los ataques van exactamente hacia donde apunta el jugador. Solo si no pulsa ninguna dirección van al enemigo más cercano (ver 2.1).

## 2.5 Sistema de Choque

Cuando dos peonzas chocan, se comparan sus velocidades en el momento del choque (la componente de la velocidad con la que cada una va hacia la otra):

- **La más lenta** recibe el **golpe** y sale empujada según la relación de masas (incluida la bonificación de masa del ataque; la masa no cambia el daño). El golpe tiene un **daño base** (`hitBaseDamage` 50) que la velocidad de choque solo mueve entre **×0,75** (8 m/s) y **×1,25** (24 m/s o más), y por debajo de 8 m/s baja hasta 0 (los roces no quitan). Así el daño se equilibra con el ataque de las piezas y no se dispara a toda velocidad (decidido el 2026-10-07 con el banco de pruebas; antes el daño crecía en línea recta con la velocidad y la diferencia de velocidad).
- **La más rápida** recibe solo una **fracción del daño** (por defecto un 25%, ajustable). Así cualquier choque tiene algún coste.
- **Impacto directo:** se transmite la energía entre las peonzas.
- **Impacto oblicuo:** desvía la trayectoria y hace perder velocidad. Hace menos daño porque la velocidad de aproximación es menor.
- **Defensa:** sincronizar bien los ataques para llegar con más velocidad que el rival es la forma de defenderse ("timing").
- **Daño global:** todo el daño de golpes, paredes y roce se multiplica por `damageMultiplier` (**0,5**; antes 0,8), para que un golpe normal quite 20-30 RPM y los combates sean más largos. La quemadura va aparte, por porcentaje.
- **Peso y ataque:** la **masa solo cuenta para el empuje**, no para el daño (ni el peso de las piezas ni el bonus de masa del ataque), para que sea más sencillo equilibrar las estadísticas. En el empuje, la relación de masas está limitada a ×0,8-×1,3 (`massRatioRange`). El daño depende del golpe base, de la velocidad (±25%), del ataque (las diferencias de ataque de las piezas cuentan un 60%, `attackSpread`), de la defensa, de la carga y del tipo de golpe (ataque ×1,2, dash ×0,5).
- **Daños de referencia:** los mide el banco de pruebas (2.7). Último informe, contra Balanceada, rápido parado / a tope y cargado 3 parado / a tope: Balanceada 17 / 25 y 27 / 44, Ataque 25 / 35 y 40 / 63, Defensa 18 / 29, Agilidad 30 / 38 y 44 / 67.
- **Paredes:** chocar contra el borde hace el daño de un **choque parejo contra una peonza** a esa velocidad: velocidad perpendicular × `damagePerImpactSpeed` × `wallDamageScale` (1); antes era la mitad. Solo cuenta la velocidad perpendicular (rozarla no quita RPM) y desde 3 m/s. Un solo golpe de pared por peonza cada **0,1 s** (`wallHitCooldown`): al dar en la unión de dos tramos llegaban dos a la vez; si el segundo es más fuerte, solo se suma la diferencia. El suelo nunca quita RPM.
- **Peonzas pegadas:** si dos peonzas siguen en contacto **0,3 s** después de un choque (sin velocidad no hay choque nuevo), se separan con un empuje de **7 m/s** (la más pesada empuja más; la resistencia al empuje lo frena como mucho a la mitad) y reciben el daño de un choque parejo a 5 m/s, con su chispazo. No da energía ni corta curaciones. El roce continuo (8 RPM/s) sigue aparte. Valores en `CombatConfig` → `stuckRepel*` y `stuckImpactSpeed`.

### Parry

Un ataque **rápido** lanzado justo antes de recibir un ataque enemigo lo bloquea por completo si es un rápido, un dash o un cargado de nivel 1; contra un cargado de nivel 2 o más solo **lo bloquea a medias** (ver *Bloqueo*).

- **Ventana:** los primeros **0,12 s** desde que se lanza el ataque rápido (al soltar el botón). Solo vale el ataque rápido: ni el cargado ni el dash hacen parry.
- **Condición:** la otra peonza tiene que venir atacando (ataque rápido, cargado o dash). El parry tiene prioridad sobre la regla de la más rápida.
- **Resultado:**
  - Quien hace el parry **no recibe daño**.
  - El atacante recibe solo **su fracción del impacto** (la misma que recibe normalmente la peonza más rápida).
  - El atacante **sale despedido**, su ataque o dash queda cortado y queda **aturdido 0,3 s** (sin control, ataques ni dash) para que el rebote se note.
  - Quien hace el parry frena su embestida y casi no se mueve.
- **Recompensa para quien hace el parry:** gana un **12%** de la energía del especial (antes 20%; 2026-10-12). **A quien le hacen parry también gana un 6%** (`parriedEnergy`): contra un rival que lo para todo no se cargaba nunca el especial, y así atacar compensa el riesgo. **Ya no recupera la carga gastada** (2026-10-11): el parry cuesta su carga como cualquier ataque y corta la recarga igual que ellos; si no, salía gratis y la IA Experta tenía parry infinito.
- **Aviso:** partículas especiales (estallido blanco y cian), texto "¡PARRY!" en pantalla y una pausa muy breve del juego (hit-stop de 0,08 s).
- **Doble parry:** si las dos peonzas están en su ventana a la vez, se anulan. Ninguna recibe daño ni recompensa, y las dos rebotan aturdidas.
- **Bloqueo** *(2026-10-12)*: un parry contra un **cargado de nivel 2 o más** cuenta como parry, pero no refleja: quien para recibe **la mitad del daño y del empuje** que recibiría quieta, y gana la mitad de la energía del parry (6%). El atacante no recibe daño, **gana la energía de su golpe como si hubiera entrado** (cargado 2: 22%) y queda **aturdido un instante**, más cuanto más ligero: `0,15 × (2,5 ÷ peso)` s, entre 0,08 y 0,25 (Agilidad 0,19 s, Ataque 0,17 s, Defensa y Balanceada 0,12 s), para dar impacto al bloqueo. Rebota como la ganadora de un choque. Aviso «¡BLOQUEO!». Así gastar muchas cargas en un golpe compensa el riesgo (con la recarga lenta, C16, juntarlas cuesta) y la Defensa tiene un punto débil contra los cargados fuertes. Ajustes en `CombatConfig` → Parry (`parryBlockMinLevel`, `blockDamageFraction`, `blockKnockbackFraction`, `blockEnergyFraction`, `blockStagger`, `blockStaggerMin`, `blockStaggerMax`). Medido en el Sandbox (Ataque contra Defensa): cargado 2, 24,6 de daño sin parar y 12,5 bloqueando; cargado 3, 30 y 15,1; el empuje, de 15,6 a 7,2-7,8. Contra un rápido o un cargado 1, parry completo como antes.
- **Piezas:** cada pieza suma o resta ventana de parry (`parryWindowModifier`): las de agilidad **+0,03 s**, las balanceadas 0, las de ataque **−0,01 s** y las de defensa **−0,03 s**. Se suman todas las piezas montadas; la ventana nunca baja de 0,02 s. *(Pendiente de asignar en los assets de las piezas.)*
- **Ajustes:** todo se configura en `CombatConfig` → Parry.

## 2.6 Gestión de Energía

- **Vida o resistencia (RPM):** la vida de la peonza son sus revoluciones por minuto. Cuantas más RPM, más energía para seguir luchando. Las RPM bajan poco a poco solas (desgaste) y al recibir golpes.
- **Masa:** resistencia a perder velocidad y a ser empujada.
- **Ataque:** fuerza de impacto en las colisiones, que depende de su velocidad al chocar (la masa solo cuenta para el empuje, ver 2.5).
- **Defensa:** resistencia al daño y al empuje.
- **Eliminación:** una peonza queda eliminada **únicamente cuando sus RPM llegan a 0**. No hay ring-out: los bordes de la arena impiden salir.

## 2.7 Objetivos de equilibrio (C10)

Lo que tiene que salir, escrito antes de tocar valores (método en el registro de decisiones). El banco de pruebas lo mide y marca lo que se sale (±10%).

| Objetivo | Valor |
|---|---|
| Duración, Balanceada contra Balanceada | Se aceptan duelos más largos (**~90-120 s**): con ~20 por golpe, 60 s pediría un golpe cada ~2 s. Se mide con el duelo completo del banco (C14). Es una referencia del tiempo de juego de una partida (enfoque de partidas rápidas; se gana quedando el último en pie), a calibrar con jugadores reales o con una IA que juegue bien |
| Dash | ≈ 75% de un ataque rápido |
| Ataque rápido, Balanceada contra Balanceada | ≈ **20 RPM** |
| Cargado 1 / 2 / 3 | ×1,25 / ×1,5 / ×1,75 del rápido (≈ 25 / 30 / 35) |
| Tope de un solo golpe (cualquier arquetipo, a tope, cargado 3) | **60 RPM** (~10% de una Balanceada) |

| Arquetipo (Balanceada = 1) | Daño que hace | Daño que recibe | Medido el 2026-10-09 (hace / recibe) |
|---|---|---|---|
| Ataque | 1,25 | 1,10 | 1,39 / 1,25: pega y recibe de más; bajar un poco su ataque (C12) |
| Defensa | **≈1** (antes 0,75) | 0,65 (aguanta ~1,5 veces más) | 1,01 / 0,74 |
| Agilidad | 1,10 (y golpea más a menudo) | 1,20 | 1 / 1,32 |
| Balanceada | 1 | 1 | referencia |

**Defensa pega como la Balanceada** (decidido el 2026-10-09 jugando con mando): al ser más lenta le cuesta más llegar a golpear; si además hiciera mucho menos daño, sería inviable. Lo que la distingue es lo que aguanta. Daños de un golpe medidos ese día con los valores puestos a mano: rápido 13-20 (parado / a tope), cargado 1-2 25-44, dash 10-16; en partida, 20-25 los normales, 30-40 los cargados y 13-18 el dash, que es lo buscado.

**Banco de pruebas:** menú *FakeBlade → Banco de equilibrio* con Play en la escena Sandbox (necesita un dummy). Cada preset golpea a cada preset con rápido, cargado 1-3 y dash, desde parado (P), saliendo a su velocidad máxima (T) y llegando tarde (L, a 5 m). Mientras dura (~7 min) los dummies están quietos, sin trucos, a velocidad ×1 y sin grabar la sesión; al acabar todo vuelve a como estaba. Informe en `Logs/Balance/` (.md con las tablas y .csv con cada golpe).

**Duelos completos** *(C14, 2026-10-10)*: con los golpes medidos, el banco simula cada pareja de presets a la vez (`DuelEstimator`): las dos se golpean cada 4,2 s con su daño medio (P y T) y el rebote de sus golpes; por cada golpe que entra gastan 0,7 ataques (1,5 cargas de media, 2% de sus RPM máximas por carga) y 0,1 dashes (8%, la mitad aciertan y devuelven la mitad); pierden su desgaste por segundo; y al llenar el especial (energía por tipo de golpe, como en el juego) se curan el 25% de sus RPM máximas (más la regeneración de Spin Boost), con un 25% de curaciones cortadas. El ritmo de uso sale de los registros del sandbox (2026-10-07 a 2026-10-10). No cuenta los demás efectos de los poderes, las paredes ni el parry. El informe da la duración, quién gana y con cuántas RPM, y deja la tabla de antes (solo golpes) para comparar.

**Tests de los objetivos** *(2026-10-10)*: `BalanceTargetsTests` (en `Assets/Tests/EditMode`) comprueba esta tabla en unos segundos, sin jugar, con los presets y el `CombatConfig` reales y el mismo ±10% que el banco. Mide lo que sale de las estadísticas: cargado ×1,25 / ×1,5 / ×1,75, rápido de Balanceada ≈ 20 con el golpe a ×1 de velocidad, tope de 60, daño que recibe cada arquetipo, que la Defensa pega como la Balanceada, que el Ataque es el que más pega y que la Agilidad es la más rápida y la Defensa la más lenta. Lo que depende del movimiento (cuántas veces golpea cada una, a qué velocidad llega) sigue midiéndolo solo el banco. El primer resultado coincide con el quinto informe del banco (Ataque recibe 1,25 y Defensa 0,74). **Si cambia un objetivo de esta tabla, se cambia también en el test.**

## 2.8 Fórmulas del combate y del movimiento

Cómo se convierten las estadísticas en valores del juego. Están en el código en `BladeFormulas` (las usan la peonza, el exportador de estadísticas y los tests, así que nunca se desajustan) y en `CollisionResolver`, que monta con ellas el daño de cada choque. Desde el 2026-10-10 el factor de velocidad del golpe (`HitSpeedFactor`) y el multiplicador del cargado (`ChargeDamageMultiplier`) también están en `BladeFormulas`. La combinación completa del golpe está repetida en los tests (`TestData.EstimatedHit`): si cambia la fórmula del choque, se cambia también ahí. Valores actuales de `CombatConfig`. **Las estadísticas actuales de cada pieza y preset, con lo que sale en el juego, están en "Estadísticas de las piezas.html"** (para leer en el navegador: colores por arquetipo, lo que ayuda en verde y lo que perjudica en rojo, gráficas de los presets respecto a la Balanceada y de lo que aporta cada pieza, y una tabla de todas las piezas que se ordena y filtra) y en "Estadísticas de las piezas.md". Los dos se generan con *FakeBlade → Export ComponentsData Stats*. **Editar desde el HTML:** en *Editar y exportar* se cambian los valores de las piezas (y el % de sus rasgos) y todo se recalcula al momento; *Descargar cambios* guarda `fakeblade-piezas.json`, y *FakeBlade → Import ComponentsData Stats* lo aplica a las piezas (enseña los cambios antes, avisa si una pieza cambió en Unity después de exportar, se deshace con Ctrl+Z y vuelve a exportar).

**Qué toca cada estadística**

| Estadística | Qué cambia |
|---|---|
| RPM | La vida. Los costes son un % de ella: ataque 2% por carga gastada, dash 8%, curación común 25%. |
| Desgaste | RPM que se pierden solas por segundo. |
| Velocidad | Solo la velocidad máxima (0,35 m/s por punto tras igualar, pese lo que pese). A la aceleración solo le afecta por encima de 10. |
| Peso | Aceleración, acelerones del ataque y del dash, y empuje. **No toca el daño, la velocidad máxima ni el giro.** |
| Ataque | Daño que hace: cada punto, +4% (ataque 15 = ×1). |
| Defensa | Daño recibido −1% por punto y empuje recibido −0,5% por punto. |
| Dash | Velocidad del acelerón del dash (× el factor de peso). |
| Cargas | Ataques guardados y nivel máximo del cargado. |
| Parry | Segundos que se suman a la ventana de 0,12 s (tope 0,4 s). |
| Agarre | Giro y frenado al soltar el stick: cada punto, ±10%. Solo lo modifican las puntas (goma agarra y gira cerrado; bola desliza). |

**Movimiento** (C11 y C13, 2026-10-10): **una estadística, un efecto.** El peso entra como `r = 2,5 ÷ peso` (peso de referencia 2,5, `referenceWeight`; el peso cuenta como mínimo 0,6): r > 1 en las ligeras y r < 1 en las pesadas. Cada efecto del peso es `valor en la referencia × r^exponente`: **sin topes** (un jefe de peso 10 sigue siendo distinto de uno de 5 y se mueve) y nunca llega a 0 ni a negativo. Antes era una recta entre peso 0,5 y 3 cortada en los extremos: por encima de 3 el peso dejaba de contar, y la aceleración y el frenado se dividían además entre la masa (el peso contaba dos veces). El agarre entra como `g = 1 + 0,1 × agarre` (`gripPerPoint`, mínimo ×0,25).

| Valor | Fórmula | Qué estadística |
|---|---|---|
| Velocidad máxima | `15,5 + 0,5 × (10 + velocidad × 0,7 − 15,5)`, entre 3 y 25 m/s | Velocidad (el peso no la toca) |
| Aceleración | `24,4 × max(velocidad × 0,1; 1) × r^1,34`, entre 1 y 120 m/s², igual para cualquier masa | Peso (y la velocidad, por encima de 10) |
| Acelerón del dash / del ataque | `dash (o 9 el ataque) × 0,91 × r^0,28` | Peso |
| Giro | `8 × g` correcciones de la velocidad lateral por segundo (entre 2 y 50); menos = derrapa más | Agarre |
| Frenado al soltar el stick | `0,8 × g` por segundo | Agarre |
| Masa física | `max(0,6; peso)` | Peso: solo separación y empuje de la física de Unity |

Los valores (`referenceAcceleration` 24,4, `accelerationWeightExponent` 1,34, `referenceImpulse` 0,91, `impulseWeightExponent` 0,28, `turnRate` 8, `stoppingRate` 0,8, `maxVelocity` 10, `speedPerPoint` 0,7) se ajustaron para que **los presets se muevan como antes del cambio**. Con las piezas del 2026-10-10: aceleración Agilidad 42,8 · Ataque 29 · Balanceada y Defensa 17,5 m/s² (antes 42,6 / 28,9 / 17,5); velocidad máxima 17,3 / 15,7 / 15,4 / 15 m/s (antes 17,4 / 16 / 15,3 / 15,1); acelerón ×0,97 / ×0,94 / ×0,85. Cambian a propósito el giro (antes 14-18 correcciones por segundo según el peso, casi instantáneo; ahora 8 con agarre 0, para que se note el derrape) y el frenado (antes 0,38-1,26 según el peso; ahora 0,8 con agarre 0). Lo que distingue a cada peonza en el giro es el agarre de su punta. **Compensar peso con velocidad:** el peso ya no cambia la velocidad máxima; en la aceleración, la velocidad solo ayuda por encima de 10 puntos.

**Daño de un golpe** (lo recibe la peonza más lenta del choque):

`42 × f(velocidad) × ofensa × carga × tipo de golpe`

- **f(velocidad):** por debajo de 8 m/s, `0,75 × v / 8` (los roces casi no quitan); de 8 a 24 m/s, de ×0,75 a ×1,25; por encima, ×1,25. La velocidad es la de cierre, sin la parte que pone la carga (se quita en proporción).
- **Ofensa:** `1 + 0,6 × (ataque / 15 − 1)` = `0,4 + 0,04 × ataque`; ×(1 + 0,1 por golpe encadenado, máximo 3) si está atacando.
- **Carga:** `1 + 0,25 × nivel` (nivel 3 = ×1,75).
- **Tipo de golpe:** ataque ×1,2, dash ×0,75, sin atacar ×1; y los rasgos de daño por tipo y Golpe lento.

**Lo que se quita de verdad:** `golpe × (1 − defensa / 100) × 0,5 (daño global)` × poder Defensa × rasgo Daño recibido.

- **La peonza más rápida** recibe solo `velocidad de cierre × 0,25 × ofensa del rival` (con la misma defensa y el mismo daño global).
- **Parry:** quien lo hace no recibe nada; el atacante, lo mismo que la más rápida. **Espinas:** el atacante recibe el % del rasgo del daño que ha hecho.
- **Paredes:** `velocidad perpendicular × (1 − defensa / 100) × 0,5`, desde 3 m/s y un golpe cada 0,1 s. **Roce:** 8 RPM/s. **Quemadura:** 1,5% de las RPM máximas cada 0,5 s, sin defensa.

**Empuje:** `(5 + 0,4 × diferencia de velocidad) × empuje del ataque × relación de masas`, tope 18 m/s; quien lo recibe lo multiplica por `(1 − defensa × 0,005)` y sus rasgos, y el ganador del choque se lleva el 35%. Empuje del ataque: +40% por nivel de carga, combo y rasgos. Relación de masas: masa efectiva de cada una (el peso, ×1,5 si está atacando), limitada a ×0,8-×1,3.

**Pérdida de control tras un empuje** *(2026-10-11)*: un empuje de 4 m/s o más deja la peonza sin control `0,1 + 0,015 × empuje` segundos (12 m/s ≈ 0,28 s; 18 m/s ≈ 0,37 s): el stick no acelera ni frena, y el tope de velocidad se abre mientras dura. Después el control vuelve poco a poco en 0,15 s. Puede seguir atacando y haciendo dash. La **lanzada** de Rayos quita el control toda su duración (0,5 s). Antes, el stick a contrapelo cancelaba el empuje casi al momento. Medido en el sandbox (empuje de 12 m/s con el stick en contra): recorre 2,5 m (antes 0,6 m); con el stick suelto, 7,2 m (antes 6,0). Lanzada de Rayos de 15 m/s con el stick en contra: 5,1 m. Valores en `CombatConfig` → `knockbackControl*`.

3. # Peonzas Modulares

Cada peonza se compone de **piezas intercambiables** que modifican sus estadísticas. Los jugadores montan su peonza antes de cada combate.

| Pieza | Afecta sobre todo a |
|---|---|
| Punta (Tip) | Estabilidad (desgaste de RPM), velocidad de movimiento y **agarre** (giro y frenado; solo lo modifican las puntas) |
| Cuerpo (Body) | Peso e inercia |
| Disco (Blade) | Ataque y defensa |
| Núcleo (Core) | RPM máximas y **habilidad especial** |

Estadísticas que modifican las piezas: velocidad de rotación (RPM máx. y desgaste), peso/inercia, potencia de ataque, resistencia/defensa, velocidad de movimiento, fuerza de dash, número de cargas de ataque, ventana de parry y agarre. El agarre está en todas las piezas por comodidad (el mismo formato de datos), pero solo lo modifican las puntas: el resto lo deja a 0 (2026-10-10).

Cada pieza tiene una clase de peso (Ligera / Media / Pesada) y **un arquetipo** (Ataque, Balanceada, Defensa o Agilidad: los mismos tipos A, B, C y D de los modelos). El arquetipo dice a qué estilo pertenece la pieza y se muestra en la selección para elegir sin confusiones. El número de piezas no importa: más adelante habrá piezas con cualidades concretas (por ejemplo, dos características equilibradas y otras muy débiles), y cada una llevará su arquetipo.

| Pieza | Arquetipo |
|---|---|
| Needle Point, Aero Shell, Velocity Core (Rayos), Tide Core (Rastro de agua), Phantom Core (Fantasma) | Agilidad |
| Flat Base, Standard Frame, Balanced Ring, Endurance Core (Spin Boost), Frost Core (Hielo), Aegis Core (Escudo), Cyclone Core (Tornado) | Balanceada |
| Wide Ball, Iron Fortress, Crush Wheel, Fortress Core (Defensa), Impact Core (Onda de choque), Spike Core (Pinchos), Rebound Core (Rebote) | Defensa |
| Razor Edge, Blaze Core (Fuego), Meteor Core (Salto), Rhino Core (Embestida) | Ataque |

**Arquetipos.** Los arquetipos no son peonzas fijas: son el **resultado de combinar las piezas**. Según las estadísticas finales, la peonza encaja en uno:

- **Ataque:** más cargas de ataque, masa media y velocidad media.
- **Defensa:** menos cargas, más masa y velocidad media.
- **Agilidad:** cargas medias, masa baja y velocidad alta.
- **Balanceada:** todo medio.

**Rasgos de las piezas** *(implementados; primeras piezas con rasgos el 2026-10-07)*. Además de sus estadísticas, una pieza puede tener **rasgos**: efectos propios sobre el combate en porcentaje, que hacen piezas con personalidad y con contrapartidas. Por ejemplo:

- Un disco que da **+30% de energía del especial con los ataques cargados**.
- Una punta con **dash con un 25% menos de espera pero un 15% más caro**.
- Un cuerpo que **recarga los ataques más rápido** pero **los encarece**.

Se ponen en el asset de la pieza (lista *Rasgos* en el Inspector, en tanto por uno: 0,25 = +25%, −0,1 = −10%) y los de todas las piezas montadas se suman. Casi todos multiplican un valor (1 + suma, nunca por debajo de ×0,1); Espinas y Golpe lento suman directamente. El menú *Create All Component Presets* ya no sobrescribe las piezas que existen, así que estadísticas y rasgos se ajustan a mano sin miedo. Tipos disponibles (`PartTraitType`; los 16 nuevos del 2026-10-08 probados uno a uno con un probe en el sandbox, 17 comprobaciones bien):

| Grupo | Rasgo (nombre en el juego) | Qué cambia |
|---|---|---|
| Especial | Energía (rápido / dash / cargado / parry) y Energía del especial | Energía que se gana con cada tipo de golpe acertado, o con todos |
| Ataques | Recarga de ataques, Coste de ataques, Tiempo de carga | Lo que tarda en volver cada carga, las RPM que cuestan y lo que tarda en subir cada nivel del cargado |
| Dash | Espera del dash, Coste del dash | Tiempo entre dashes y RPM que cuesta |
| Golpes que das | Daño del rápido, Daño del cargado, Daño del dash | Daño de cada tipo de golpe |
| | Empuje que haces | Empuje al golpear |
| | Duración de tus estados | Lo que duran la quemadura, la congelación y la lanzada que provocas |
| | Golpe lento | Daño extra cuando llegas despacio: entero hasta 8 m/s, nada desde 16 m/s (ayuda a las peonzas lentas) |
| Golpes que recibes | Daño recibido | Daño de otras peonzas (golpes, roce y lanzadas; no paredes ni estados) |
| | Empuje recibido, Daño de paredes | Empuje que recibes y daño de las paredes |
| | Espinas | Quien te golpea atacando recibe ese % del daño que te ha hecho (daño fijo, sin defensa) |
| | Estados que sufres | Lo que te duran la quemadura, la congelación y la lanzada |
| Manejo y poder | Ventana de parry | Ventana de parry (sobre la de las estadísticas) |
| | Giro, Aceleración | Lo rápido que cambia de dirección y que acelera (la velocidad máxima no cambia) |
| | Duración del poder, Curación | Lo que dura el poder activo y lo que curan los poderes (el 25% común y Spin Boost) |

Para mostrarlos en la selección ya hay nombre de cada rasgo en español e inglés (`TRAIT_*` en `Loc`) y un texto listo para la UI (`PartTraits.Label`: "DAÑO DEL RÁPIDO +15%").

**Rasgos con condición (para más adelante):** dan personalidad pero necesitan reglas propias.

- **Último aliento:** por debajo del 30% de RPM, más daño (remontadas).
- **Firme al cargar:** mientras carga recibe menos daño y empuje.
- **Primer golpe:** el primer golpe tras unos segundos sin chocar hace más daño (premia entrar y salir).
- *(Añadir aquí las ideas del usuario y nuevas propuestas antes de implementarlos.)*

Rasgos actuales (equilibrio del 2026-10-07):

| Pieza | Rasgo | Por qué |
|---|---|---|
| Wide Ball, Iron Fortress, Crush Wheel (pesadas) | Espera del dash **+20%** cada una (las tres: +60%, 2,9 s) | La Defensa aguanta y contraataca; no debe vivir del dash. |
| Needle Point | Coste de los ataques **−25%** | La Agilidad ataca mucho y barato. |
| Aero Shell | Recarga de los ataques **−20%** | Ídem: más ataques rápidos, en vez de dashes. |

**Ataque de las piezas** (mismo ajuste): Crush Wheel 15 → **6**, Iron Fortress 5 → **0** (la Defensa pegaba más que el Ataque), Razor Edge 8 → **12** (el disco de Ataque es el que más pega) y Aero Shell −3 → **0**. *(Cambiado otra vez en C10, el 2026-10-07: ver el registro en Quehaceres C10.)*

**Núcleos** (uno por poder). Deben notarse, pero sin decidir la partida: todos dan **+50 RPM máximas** y, además, una **ventaja y un coste del mismo tamaño** (2 puntos), a juego con su poder. Los poderes más fuertes ya pagan con la energía que necesitan. 1 punto = 25 RPM = 0,5 de velocidad = 0,1 de peso = 2 de ataque = 3 de defensa = 1 de dash = 0,2 de desgaste.

| Núcleo | Poder | Clase | Ventaja | Coste |
|---|---|---|---|---|
| Endurance Core | Spin Boost | Media | +50 RPM (total +100) | −4 ataque |
| Impact Core | Onda de choque | Pesada | +0,2 peso | −1 velocidad |
| Fortress Core | Defensa | Pesada | +6 defensa | −2 dash |
| Velocity Core | Rayos | Ligera | +1 velocidad | −0,2 peso |
| Blaze Core | Fuego | Media | +4 ataque | −6 defensa |
| Frost Core | Hielo | Media | −0,4 desgaste | −50 RPM (total 0) |
| Spike Core | Pinchos | Pesada | +2 ataque y +3 defensa | −1 velocidad (provisional, 2026-10-13) |
| Rebound Core | Rebote | Pesada | +0,2 peso | −1 velocidad (provisional, 2026-10-13) |
| Aegis Core | Escudo | Media | +25 RPM (total +75) y +3 defensa | −4 ataque (provisional, 2026-10-13) |
| Cyclone Core | Tornado | Media | +2 dash | +0,4 desgaste (provisional, 2026-10-13) |
| Meteor Core | Salto | Media | +2 ataque y +0,1 peso | −3 defensa y −1 dash (provisional, 2026-10-13) |
| Tide Core | Rastro de agua | Ligera | +0,5 velocidad y +1 dash | −6 defensa (provisional, 2026-10-14) |
| Phantom Core | Fantasma | Ligera | +2 dash | +0,4 desgaste (hecho con C5, 2026-10-14) |
| Rhino Core | Embestida | Pesada | +0,2 peso | −1 velocidad (provisional, 2026-10-14) |

Valores en `FakeBlade/Create All Component Presets` (`FakeBladeComponentPresets`).

**Colores de la peonza** *(implementado; falta conectar los modelos nuevos)*. El color que elige el jugador es el protagonista y cada pieza lleva el suyo:

- Por defecto: **disco (anillas) del color elegido, cuerpo blanco, punta negra y núcleo en un tono más profundo del color elegido** (por ejemplo, con rojo: anillas rojas, cuerpo blanco, punta negra y núcleo rojo oscuro).
- Cada color de la paleta se puede personalizar en **Opciones → Colores de peonza** (ver 9.2.3) y se guarda.
- **Núcleo:** un modelo genérico para todos los poderes, que solo cambia de color. Lleva el tono de la paleta y además **brilla con el color de su poder según la carga de la esfera**: apagado al empezar, sube poco a poco hasta un 70%, late suave con la esfera llena y late fuerte mientras el poder está activo. En la selección de peonzas brilla como con la esfera llena, para que se vea el poder.
- **Requisitos del modelo** (para Blender): cada pieza es un objeto separado y su nombre (o el de su padre) dice qué es: `Tip`/`Punta`, `Body`/`Cuerpo`, `Ring`/`Blade`/`Disco`/`Anilla` y `Core`/`Nucleo`. Lo que no se reconoce se pinta como disco. El núcleo usa el material `BladeCoreMaterial` (URP Lit con emisión activada). Se pinta con `BladePaint`.

**Modelos de las piezas** (en `Assets/3D Models/Bayblade 01/bayblade_01.blend`, colección `BayBlade 01`). Cada pieza (Body, Rings, Punta) tiene **4 tipos, uno por arquetipo**: **A = Ataque, B = Balanceada, C = Defensa, D = Agilidad** (más un tipo 0 de base). El núcleo es uno genérico (`Nucleo Generico`). Todas las piezas comparten el origen (0, 0, 0) y están a la escala del juego (1 unidad = 1 m): puestas en el mismo punto quedan montadas.

- **En el juego:** cada pieza del catálogo apunta a su modelo (campo `model`; por defecto, el del tipo de su arquetipo) y `BladeModel` monta la peonza con las piezas equipadas dentro del pivote de giro, ocultando el modelo antiguo del prefab. Los ajustes (escala, material del núcleo) están en el prefab, en `FakeBladeController` → Model Settings.

- **Exportar a Unity:** script `Tools/Blender/export_blade_parts.py` (en Blender: Scripting → abrir → Run Script). Crea un FBX por pieza en `Parts/<Colección>/<Nombre>.fbx`, junto al .blend, con la rotación y la escala aplicadas (también las escalas negativas) y los ejes de Unity: en Unity llegan con rotación 0 y escala 1, sin ajustar nada. No toca el .blend. Los objetos sueltos de `BayBlade 01` (la arena) no se exportan.

El resto de valores se irán probando y equilibrando. El sistema es modular y ampliable: añadir una pieza nueva es crear un asset de datos.

4. # Diseño de Arenas

Al principio habrá un solo tipo de arena. En las opciones antes del duelo se podrá elegir escenario, para que el diseño sea modular y ampliable.

Las arenas serán circulares, con un **borde que impide salir** y una fricción normal que no afecta al movimiento. **Por ahora el suelo es plano**: la física es más predecible y más fácil de equilibrar. Los cuencos se dejan para arenas futuras (Quehaceres I2).

Arenas actuales (cada una es un prefab con sus puntos de aparición y un asset de datos con nombre y vista previa, que aparece en la selección de escenario):

- **Arena básica** (`Arena_Basic_2`, **por defecto** desde el 2026-10-11): suelo totalmente plano y paredes totalmente verticales, modelada por el usuario en dos FBX (`Assets/3D Models/Arenas/Basic 2/BattleArena Suelo 2.fbx` y `BattleArena Paredes 2.fbx`). Escalada a 7 m de radio interior (×0,34) y 2,5 m de pared; suelo con BoxCollider, pared con MeshCollider y los materiales del proyecto (`SueloMaterial 00` y `ParedMaterial Cristal 00`).
- **Arena plana** (`Arena_V2_Plana`): objeto `Arena V2 Plana` del .blend (colección `Escenario`), suelo plano con malla en cuadrícula relajada y pared cilíndrica. Colisión: el suelo es un **BoxCollider** (una losa, más estable que una malla) y la pared un MeshCollider. *Su FBX aún no está en el proyecto: hasta que se exporte no entra en el catálogo.*
- **Arena clásica** (`Arena 00.fbx`): la primera, con forma de cuenco.
- **Arena de pruebas**: suelo y paredes de cubos, generada por código.

**Exportar una arena a Unity:** script `Tools/Blender/export_arena.py` (en Blender: Scripting → abrir → Run Script; cambia `ARENA_OBJECT` para otra arena). Crea `Assets/3D Models/Arenas/<Nombre>.fbx` con dos mallas: `<Nombre> Suelo` (las caras que miran hacia arriba a la altura más baja) y `<Nombre> Paredes` (el resto), centradas y con el suelo a altura 0. Después, *FakeBlade → Setup Arenas* (solo las arenas, sin tocar escenas; también lo hace *Setup Menus & Scenes*) monta el prefab: mide el radio interior, lo escala a 7 m, pone los colliders (losa si la cara de arriba del suelo es plana, medida con raycasts), los materiales físicos y de render, y lo añade al catálogo. Una arena puede venir en un FBX o en varios (suelo y paredes por separado); las mallas con "pared" o "wall" en el nombre son muro. Para añadir otra arena: sus rutas y su clave de nombre en `FakeBladeProjectSetup.BuildArenas` y el nombre en `Loc`. La primera del catálogo es la de por defecto (partida, Sandbox y fondo del menú). Setup Arenas no se ejecuta si hay una escena abierta con cambios sin guardar.

Cuando todo funcione, se añadirán **hazards**: suelos con distintas fricciones y efectos (hielo que desliza, suelos que aceleran) y zonas que afectan a las estadísticas. También powerups en el escenario que mejoren los ataques o recuperen algo de RPM.

**Escenarios más variados** *(idea del usuario, 2026-10-12; Quehaceres I4 e I5)*:

- **Obstáculos diversos, rampas y elementos con los que interactúan las peonzas** (por ejemplo, rebotadores, barras que giran, paredes que se mueven o trampolines). Cada arena con su personalidad.
- **Coleccionables** que aparecen por el escenario: pequeñas curas, escudos temporales, vida temporal (RPM por encima del máximo durante un rato), recarga del especial, etc. Que se puedan activar o quitar en las reglas de partida (6.2). Sin instanciar nada en combate (un pool, 7.3).

5. # Sistema de Poderes

El poder especial lo define el **Núcleo** montado: para cambiar de especial se cambia el núcleo de la peonza. Habrá un núcleo por poder.

- **Carga:** el poder se carga con cada **golpe acertado** (ganar el choque) según el **tipo de golpe**, no según el daño: ataque rápido 8%, dash **6%** (antes 10%: el dash ya no es la vía para cargarlo), **ataque cargado 12% + 5% por nivel** (nivel 3 = 27%: gastar cargas se recompensa), ganar un choque sin atacar 3%, parry 20%; en un choque parejo, la mitad. Todo × `specialEnergyMultiplier` (**1,2**; antes 1,5, con el que salía cada 20-30 s), para que el especial llegue hacia la mitad del combate sin repetirse demasiado (efectivo: rápido ~10%, dash ~7%, cargado nivel 3 ≈ 32%). Golpear a una Defensa activa también cuenta; golpear a un aliado, no. También se carga al recoger powerups del escenario. **Además se carga sola con el tiempo** (2026-10-13, idea del usuario): a la vez que pierde RPM por desgaste, gana **4% de la barra por segundo** (`specialEnergyPerSecond` = 0,04, no lo multiplica `specialEnergyMultiplier`; sí el rasgo *Energía del especial*), así que solo con el tiempo se llena en 25 s. Medido con el banco (dos IA Difíciles, duelos de ~27 s): sin la carga por tiempo, el 63% de las peonzas no llegaba a activarlo; con 0,04, el primero llega a los **14 s (53% del duelo)**, una vez por peonza y duelo, y ninguna se queda sin él. **Cada poder necesita una cantidad distinta de energía** para llenarse: los más fuertes tardan más (columna *Energía* de la tabla; 1 = la barra actual). Mientras el poder está activo **no se gana energía**: al terminar, la barra empieza de 0. Valores en `CombatConfig` → `specialEnergy*`.
- **Activación:** cuando la carga está llena, se pulsa Especial. Mientras está activo, la carga se va vaciando con el tiempo. Todos duran **5 s**. Cuando se vacía, el poder termina. Excepción: Rayos dura hasta 6 s o hasta que da su golpe fuerte (ver tabla).
- **Nombres:** simples, en español e inglés: Defensa / Defense, Fuego / Fire, Hielo / Ice, Rayos / Lightning, Fantasma / Ghost. Spin Boost y Onda de choque mantienen los suyos.

**Efecto común a todos los poderes (al activarse)** *(implementado)*:

- Recupera el **25% de las RPM máximas** (mismo valor para todos, ajustable en `CombatConfig`), **poco a poco en 2 s** (`specialHealTime`), no al momento.
- **Rellena todas las cargas de ataque** disponibles en ese momento.

**Curaciones de los poderes:** todas se aplican con el tiempo (la del efecto común y la de Spin Boost). Si mientras tanto la peonza recibe un **golpe de ataque enemigo** que le quita RPM (ataque rápido, cargado o dash; también en un parry), **se corta lo que quede de curación** hasta el próximo poder: sale un humo, vibra el mando y el sandbox lo apunta ("CURACIÓN CORTADA"). **No la cortan** las paredes, el roce, las peonzas pegadas, la quemadura ni el daño de poderes, ni los choques en los que el rival no atacaba.

Después, durante el tiempo activo, cada poder tiene su efecto propio.

Poderes (un núcleo por poder: 14 a 2026-10-14; Gancho, para el futuro):

| Poder | Estado | Energía | Efecto propio |
|---|---|---|---|
| Spin Boost | Rehecho en F1 (2026-10-13): «Acelerón» | 1 | Durante 5 s la peonza lo hace todo mejor: **+25% de velocidad máxima y de aceleración**, recarga de ataques **×1,5** y **sin desgaste** de RPM. Además cura **2% de las RPM máximas por segundo** hasta que la golpea un ataque enemigo (antes solo curaba, 4%/s, y un golpe lo dejaba en nada). Valores en el asset `SpinBoost`. |
| Onda de choque | Rehecho en F1 (2026-10-13): 3 ondas a mano | 1 | **Tres ondas**: la primera sale al activarlo y las otras dos al **pulsar Especial otra vez** mientras dura; con la tercera, el poder termina. Cada onda llega a 5 m, quita el **6% de las RPM máximas** del rival y lo empuja a 14 m/s en el centro (la mitad en el borde) y lo deja **lanzado 0,4 s**, así que si choca contra la pared recibe daño de pared, como con Rayos. Antes era una sola onda al activarlo y 5 s de aura sin efecto. Valores en el asset `ShockWave`. |
| Defensa *(sustituye a Storm Breaker)* | Implementado (C1) | 1,2 | Defensa casi al 100%: no pierde RPM por golpes, paredes, quemadura ni desgaste, y el empuje que recibe es casi nulo (**10%**, también el del choque físico). Además, sus ataques se recargan **1,5 veces más rápido**, pero se mueve un **15% más lento**. El dash solo cuesta el **10% de lo normal**, pero su impulso (alcance) baja un **40%**. Atacar sí cuesta RPM. Valores en el asset `Defense`. |
| Fuego *(sustituye a Rastro de fuego)* | Implementado (C2) | 1 | Sus golpes **queman** al enemigo: 1,5% de sus RPM máximas cada 0,5 s durante 3 s (9% en total). Un golpe nuevo reinicia la duración; no se acumula. Cuenta como golpe **cualquier choque en el que le quite RPM al rival**: lo gane o no, también si es el rival quien la embiste, y en un parry. El roce continuo no quema. Valores en el asset `Fire`. |
| Hielo | Implementado (C3) | 1 | Aura de hielo y humo blanco de frío. A los enemigos que golpea los **congela** durante 3 s: −35% de velocidad de movimiento y recarga de ataques a la mitad. Un golpe nuevo reinicia la duración; no se acumula. Cuenta como golpe lo mismo que en Fuego: cualquier choque en el que le quite RPM al rival. Valores en el asset `Ice`. |
| Pinchos *(Fase L, L3)* | Implementado (2026-10-13) | 1 | Unos pinchos giran alrededor de la peonza (solo se ven). Quien la golpea **atacando** (rápido, cargado o dash) recibe el **70%** del daño que le ha hecho, como el rasgo Espinas (daño fijo, sin defensa; cuenta como golpe suyo para el punto de K.O.). Además recibe un **20% menos** de daño y hace un **10% más**. Medido: un rápido que le quitaba 14,3 le quita 11,5 y el atacante recibe 8,9 (antes 0,8). Valores en el asset `Spikes`. |
| Rebote *(Fase L, L2)* | Implementado (2026-10-13) | 1 | Un «parry pasivo» que convive con Defensa: mientras dura, cualquier peonza que **la embista** (que vaya hacia ella a 1 m/s o más: ataque, dash o empujón) sale **despedida** con el empuje entero **×1,8** (sin la fracción reducida de quien gana el choque) y recibe el daño normal del choque, y ella **no pierde RPM** en ese choque. Sus propios ataques hacen y cuestan lo de siempre. Los parrys y las peonzas lanzadas siguen sus reglas. Medido: un rápido que le quitaba 14,3 le quita 0,1 (roce) y el atacante sale a 13,6 m/s. Valores en el asset `Bounce`. |
| Escudo *(Fase L, L6)* | Implementado (2026-10-13) | **1,2** | **Tres escudos de 60º** giran alrededor a **una vuelta por segundo** y cubren la mitad del contorno. Si una peonza la embiste (ataque, dash o empujón, desde 1 m/s) y entra **por un escudo**, el choque se para: **ninguna de las dos pierde RPM**, la que embiste sale despedida con el empuje entero **×1,8** y ese escudo **se rompe**. Si entra **por un hueco**, es un choque normal. **Duración indefinida** (2026-10-13, tras probarlo con mando): la fase activa dura 0,5 s y los escudos **se quedan hasta romperse**; mientras, la esfera se vuelve a cargar, y activarlo otra vez **rellena los tres** (con el efecto común). Necesita **1,2** de energía (2026-10-15, tras probarlo con mando: con 0,6 se llenaba con dos golpes cargados y con 0,8 seguía siendo muy fuerte): es un poder sin ventaja de daño y difícil de controlar a voluntad. Medido: 8 de 16 ataques parados (la mitad) y el poder termina con el tercero roto. Valores en el asset `Shields`. |
| Tornado *(Fase L, L1)* | Implementado (2026-10-13) | 1 | Durante **1,5 s** atrae hacia sí a las peonzas enemigas a **6 m** o menos (40 m/s² en el centro y la mitad en el borde: una peonza acelera unos 24, así que huyendo a tope **no escapa, solo se frena**). Mientras atrae es **inamovible**: no recibe empuje de los choques y su masa física se multiplica por 50 (los choques físicos casi no la mueven; su propio movimiento sí). Después las **expulsa** (26 m/s en el centro y la mitad en el borde, más la velocidad con la que venían atraídas, que se anula) y quedan **lanzadas 0,4 s** (daño de pared si chocan), sin daño directo. El empujón **se carga mientras atrae**: pulsando Especial antes sale en ese momento, más flojo (desde el 50%); si no se hace nada, sale al acabar con la fuerza completa. Ajustado tras probarlo con mando (2026-10-13): más rápido (1,5 s en vez de 2), atracción y empujón más fuertes e inamovible. Medido: pega a un dummy de 5 m en 1 s y lo despide a 19 m/s (soltado a 0,5 s: 11,5); un ataque contra ella la mueve 0,01 m. Valores en el asset `Tornado`. |
| Salto *(Fase L, L5)* | Implementado (2026-10-13) | 1 | Primero **se carga 0,3 s**: se frena hasta quedarse quieta mientras se apunta con el stick (la dirección se toma al despegar). **Ni en la carga ni en el aire puede atacar ni hacer dash** (2026-10-14: si no, se cambiaba la dirección en el aire); al empezar y al acabar se corta la carga del ataque. Después salta **unos 1,9 m** de alto (2026-10-15, tras probarlo con mando: el doble otra vez, para exagerarlo; antes 0,9 m) y **5 m** hacia el stick (sin stick, hacia el rival más cercano), con **0,54 s en el aire** (2026-10-15: cae con **6 veces la gravedad**; con 0,95 s era muy lento y costaba acertar, y con 0,64 s todavía se pedía más rápido). Con la carga, 0,86 s en total. **En el aire:** si choca con otras peonzas las empuja **sin daño para nadie** (su propio salto casi no se desvía: 20% del empuje) y, como cancela la carga de ataque, **no se le puede hacer parry ni bloquearla**. **Al caer** (el impacto), una onda pequeña y fuerte: **10%** de las RPM en el centro (la mitad en el borde de **3 m**), empuje de **22 m/s** y las deja **lanzadas 0,4 s** (daño de pared si chocan). Un salto por activación: al caer, el poder termina. Medido: cayendo sobre un dummy le quita un 13,4% (con la pared) y lo despide a 25 m/s; en el aire, 0 de daño. Valores en el asset `Leap` (altura y distancia nominales 1,1 y 5,3: la fricción del aire las recorta a 0,9 2,2 y 5,3 nominales dan 1,87 y 4,8 m reales; gravedad en el aire ×6, campo *Gravity Scale*; tiempo en el aire = 2·√(2·altura / (9,81·gravedad))). |
| Rastro de agua *(Fase L, L4)* | Implementado (2026-10-14) | 1 | Durante **4 s** va dejando **agua** por donde pasa (un trozo cada 0,4 m, de 0,8 m de radio); cada trozo **se seca a los 3 s**, también después de terminar el poder. Las rivales que la pisan quedan **Mojadas 2 s** (pisarla otra vez lo renueva): **casi sin agarre** (giro y frenado ×0,1, por debajo del mínimo de las puntas), derrapan. Su dueña **patina a favor**: no resbala y va un **20% más rápida** sobre su agua. Sin instanciar nada: los trozos son una lista fija de puntos. Medido: girando 90º, seca cambia de dirección en 0,5 s y se desvía 0,5 m; mojada, a los 0,5 s aún va 22º desviada y ha derrapado 1,9 m. Valores en el asset `WaterTrail`. |
| Rayos *(sustituye a Dash eléctrico)* | Implementado (C4) | 0,8 | Entra en modo cargado hasta 6 s, con sus partículas. Solo cuenta **su velocidad hacia el rival**: si la embisten estando quieta, es un choque normal. **Choques pequeños** (de 1 a 6 m/s): empujan al enemigo x1,8, sin daño extra ni lanzada, y no gastan el poder. **Choque fuerte** (6 m/s o más): golpe con +20% de daño y empuje x2,5, deja al rival **lanzado** 0,5 s (sin control mientras vuela, 2026-10-11) y gasta el poder, con un destello. Contra una peonza congelada (o invulnerable) es un choque normal y el poder no se gasta. Valores en el asset `Lightning`. |
| Fantasma | Implementado (C5, 2026-10-14) | 1,3 | Crea **un clon** de sí misma que juega solo con la **IA de Agilidad** (nivel Normal) y las **piezas básicas de la Agilidad**. Sus golpes quitan el **10%** del daño normal (cuentan para su dueña en el punto de K.O.), pero empujan y **cortan la carga de ataque** del rival: sirve para molestar. **Ni hace parry ni se le puede hacer**, no pierde RPM, no recibe estados y no usa especial. **No tiene vida:** aguanta **3 ataques** (rápido, cargado o dash; los empujones no cuentan) y al tercero se deshace en humo. **Dura hasta que lo rompan** o hasta que cae su dueña; activarlo otra vez lo trae junto a ella con los 3 golpes. No cuenta como jugador (sin panel ni vidas) y es aliado de su dueña y de sus aliados. Se crea al equipar el núcleo y se esconde hasta que se usa (sin instanciar en combate). Medido: 0,8 de daño por golpe; una IA Difícil lo rompe con 3 ataques en 6 s. |
| Embestida *(nuevo, L8)* | Implementado (2026-10-14) | 1 | **Dos embestidas** (ajustable en el asset: eran tres, pero si la primera entraba encadenaba las tres y destrozaba): la primera al activarlo y la otra pulsando Especial otra vez; hay **6 s** para usarla (si no, se pierde) y con la última el poder termina. Cada una empieza con una **carga de 0,3 s** (se frena hasta quedarse quieta mientras se apunta; la dirección se toma al salir) y sale como una arrancada a **velocidad media** (13 m/s durante 0,45 s, menos que un dash) hacia el stick, con mucha fuerza: golpea en un **área más amplia** que un choque (a 1,8 m entre centros, frente a ~1,2 m de un contacto), así que cuesta más esquivarla, y a cada rival (una vez por embestida) lo **empuja fuerte** (24 m/s), le quita un **6%** de las RPM y lo deja **lanzado 0,4 s** (daño de pared si choca). **No se puede bloquear ni hacer parry**; mientras carga o embiste no puede atacar ni hacer dash, y el golpe la frena. Medido: sale a los 0,3 s, golpea a 1,75-1,8 m y despide a 22 m/s (8% con la pared); 0 parrys contra un dummy que ataca sin parar. Valores en el asset `Ram`. |

**Poderes con varios usos** (F1): si un poder lo admite, pulsar Especial con el poder activo lo usa otra vez (hoy, las ondas de Onda de choque). La IA vuelve a pulsar cuando el rival está a su alcance (`SpecialAbility.UseRange`, 6.4).

**La IA y los poderes** (D11): cuándo activa la IA cada poder, qué hace mientras dura y cómo reacciona al del rival está en 6.4 (*IA y especiales*).

**Reparto por arquetipo** (decidido el 2026-10-13, 3-4 núcleos por arquetipo con los poderes de la Fase L):

| Arquetipo | Poderes |
|---|---|
| Ataque | Fuego, **Onda de choque** (pasa de Defensa a Ataque), Salto, Embestida y, en el futuro, Gancho |
| Balanceada | Spin Boost, Hielo, Tornado, Escudo |
| Defensa | Defensa, Pinchos, Rebote |
| Agilidad | Rayos, Fantasma, Rastro de agua |

**Banco de poderes** (probe del 2026-10-13, IA Difícil contra IA Difícil con las mismas piezas Balanceadas; la peonza A usa el poder y la B nunca; 8 duelos por poder, hasta bajar del 10% de RPM o 60 s):

- **El especial casi no llega a usarse**: un duelo dura unos 27 s y, cargándolo jugando, A lo activó **0-0,8 veces por duelo** (Defensa, ninguna). El objetivo era que llegara hacia la mitad del combate: por eso ahora también se carga con el tiempo (ver *Carga*, arriba).
- Con la esfera llena al empezar (una activación por duelo), daño de A a B (control sin poder: 156 de daño hecho y 133 recibido; las victorias tienen un ruido de ±2 en 8 duelos): **Fuego 197** (el más fuerte), Rayos 171, Defensa 151 hecho pero solo **121 recibido**, Hielo 141, Spin Boost 144 y Onda de choque 147, estos dos como el control. Por eso se rehicieron en F1.

**Estados alterados** (implementado el sistema; los aplicarán Fuego, Hielo y Rayos):

- **Quemadura** (Fuego): pierde RPM por tic durante unos segundos. Es **daño fijo**: no lo reduce la defensa de las piezas (sí un poder defensivo activo).
- **Congelación** (Hielo): menos velocidad de movimiento y recarga de ataques más lenta durante unos segundos.
- **Lanzada** (Rayos y Onda de choque): durante 0,5 s tras el golpe fuerte de Rayos (0,4 s tras una onda; sigue las mismas reglas entre estados: una onda no lanza a una peonza congelada y apaga la quemadura), la peonza **no tiene prioridad por velocidad**. Si choca contra otra peonza, recibe el daño como la más lenta, y la otra recibe un choque parejo. Si choca contra una **pared**, la pared la golpea como una peonza parada con la fuerza del usuario de Rayos (daño base + diferencia de velocidad; en la prueba, a 30 m/s: 13,8% de las RPM frente al 1,1% de un choque normal). **Todo** ese daño (el de la lanzada y el de la peonza contra la que choca, como en los bolos) cuenta como golpe del usuario de Rayos para el punto de K.O. Así se premia empujar enemigos contra paredes o contra otros enemigos.
- **Mojada** (Rastro de agua, 2026-10-14): resbala (casi sin agarre) mientras dura. **Va aparte del estado principal**: se puede estar mojada a la vez que congelada o lanzada; solo una regla con los demás: **mojarse apaga la quemadura** y una peonza mojada **no se puede quemar**. Se ve con gotas que caen de la peonza y un icono de gota (si no tiene otro estado, que tiene prioridad en el icono). Le afectan los rasgos de duración de los estados.
- Las peonzas **invulnerables** (recién reaparecidas) no reciben estados.

**Reglas entre estados:**

- **Solo un estado a la vez.** Si llega uno nuevo que no está bloqueado, **sustituye** al anterior (por ejemplo, el fuego derrite el hielo).
- **Triángulo de bloqueos:** Fuego > Hielo > Rayos > Fuego.
  - Una peonza **en llamas no se puede congelar**.
  - Una peonza **congelada no puede ser afectada por el rayo**. Si Rayos la golpea, es un choque normal (sin empuje extra ni lanzada) y **el poder no se gasta**: puede buscar a otro rival.
  - Una peonza **lanzada no se puede quemar**, y el golpe fuerte de Rayos **apaga** una quemadura que ya tuviera.

**Cómo se ven los estados** *(implementado)*: salen partículas pixel del color del estado desde la parte llena de la barra de RPM del panel del jugador (brasas que suben, copos lentos o chispas rápidas). Sobre la peonza hay partículas (llamas; escarcha y vaho frío; rayos y chispas) y un **icono pixel del estado encima de ella** (llama, copo o rayo con contorno oscuro), que mira siempre a la cámara y parpadea en el último 30% de su duración. Colores en `VfxLibrary` → Estados alterados.

**Aura de cada poder** (mientras está activo, con el color del poder):

| Poder | Aura |
|---|---|
| Spin Boost | Doble espiral de cruces verdes que sube alrededor de la peonza (hecho). |
| Onda de choque | Ondas naranjas que se expanden por el suelo cada 0,4 s, más chispas a ras de suelo (hecho). |
| Defensa | Anillo protector azul que gira alrededor del cuerpo, más destellos en una cúpula (hecho, heredado de Storm Breaker). |
| Rayos | Rayos amarillos que chisporrotean alrededor, más chispas que saltan (hecho, heredado de Dash eléctrico). Golpe fuerte: destello con rayos blancos y amarillos, estrellas, sacudida de cámara y un pequeño parón (hecho). |
| Fuego | Corona de llamas rojas y amarillas alrededor de la peonza, más brasas que saltan (hecho). Las peonzas quemadas llevan sus llamitas y el icono de llama (estado Quemadura). |
| Hielo | Cristales que flotan alrededor de la peonza y humo blanco de frío que se arrastra por el suelo (hecho). Las peonzas congeladas llevan su escarcha, vaho frío y el icono de copo (estado Congelación). |
| Pinchos | Seis puntas moradas (destellos en estrella) que giran en círculo a la altura del disco, más alguna chispa que salta (hecho, con el emisor de los ataques: sin efectos nuevos). |
| Rebote | Un anillo rosa a la altura del disco que late cada 0,25 s y destellos que saltan hacia fuera; cada rebote, un estallido de estrellas y un anillo en el punto de choque (hecho, con emisores que ya había). |
| Escudo | Un arco dorado de píxeles por cada escudo que queda, en su ángulo real (el mismo con el que se decide si para el choque): se ve por dónde se puede entrar. Al romperse uno, un estallido de estrellas y chispas en su sitio (hecho). Se siguen viendo mientras queden escudos, aunque el poder ya no esté activo. |
| Tornado | Rayas turquesa que giran en espiral hacia la peonza desde todo su alcance (se ve hasta dónde atrae), más cuanto más se carga el empujón, y un remolino de destellos pegado a ella; el empujón, un estallido y una onda grande (hecho). |
| Salto | En la carga, partículas rosas que se juntan hacia ella, cada vez más; mientras vuela, un rastro de destellos y rayas rosas que caen; al caer, un estallido, una onda grande en el suelo, chispas, sacudida de cámara y un pequeño parón (hecho). |
| Rastro de agua | El agua: gotas azules a ras de suelo sobre cada trozo mojado (más tenues al secarse) y alguna onda pequeña; se ve hasta que se seca, aunque el poder haya terminado. Las mojadas, gotas que caen y el icono de gota (hecho). |
| Fantasma | El clon, del color de su dueña muy aclarado (casi blanco) y soltando humo; al aparecer y al deshacerse, una nube de humo. La transparencia de verdad queda para el aspecto (K) (hecho). |
| Embestida | Mientras embiste, rayas verde lima que quedan atrás y chispas delante; entre embestidas, un destello girando por cada una que queda; al golpear, un estallido, chispas, sacudida y un pequeño parón (hecho). |

**Poderes futuros** *(ideas del usuario, 2026-10-12; para otras sesiones, Fase L)*. Cada uno necesitará su asset, su núcleo en el catálogo, su aura y su prueba en el Sandbox, como los de la Fase C. Las dudas de cada uno están en 12.

| Poder | Efecto (idea) | Pensado para | Notas |
|---|---|---|---|
| **Tornado** *(hecho, L1)* | Atrae hacia sí a todas las peonzas que estén en un área y después las **expulsa** hacia fuera con un empuje de área. | — | **Convive con Onda de choque** (decidido el 2026-10-12): cada uno con su recarga y sus usos. |
| **Rebote** *(hecho, L2)* | Parecido a Rayos, pero solo empuja a **quien le ataca**: el que choca con ella sale empujado en dirección contraria. Quien lo activa **no pierde vida** en los choques que provocan los demás. | — | **Convive con Defensa** (decidido el 2026-10-12). Se parece más a Rayos que a Defensa, pero al revés: una especie de **parry pasivo con rayo** (Rayos empuja al que ella embiste; Rebote, al que la embiste a ella). Sus propios ataques sí dañan y cuestan como siempre. |
| **Pinchos** *(hecho, L3)* | Pinchos que giran alrededor de la peonza (para que todos vean que está activo). Sube un poco su daño y **refleja el 60-80% del daño recibido** al que la ataca (por equilibrar). | Defensa (aguantar golpes y devolverlos) | El daño reflejado cuenta como golpe suyo para el punto de K.O. |
| **Rastro de agua** *(hecho, L4)* | Durante unos segundos deja un rastro de líquido por donde pasa. Las otras peonzas que lo pisan **resbalan mucho**: su agarre baja a −10 durante unos segundos (C13), así que derrapan y pierden el control. | — | Ojo: hoy el agarre tiene un mínimo de ×0,25 (2.8, `GripMultiplier`), al que ya se llega con −7,5; con −10 el giro y el frenado quedan en un cuarto. Si no resbala lo bastante, que el rastro baje ese mínimo mientras dura. El rastro, sin instanciar nada en combate (7.3). |
| **Salto** *(hecho, L5)* | Salta hacia delante y, al caer, hace una **onda de choque**: daño de área y un empuje muy fuerte en un área pequeña. | — | La peonza en el aire: decidir si puede chocar mientras vuela y cómo se ve la sombra para saber dónde cae. |
| **Gancho** | Engancha a la peonza enemiga con la que choca y la **arrastra** con la suya. | — | **Por decidir:** puede ser contraproducente (arrastrarla también frena a la que engancha). Falta ver qué gana quien la usa (llevarla contra una pared, contra otra peonza...) y cómo se suelta. |
| **Escudo** *(hecho, L6)* | Genera **3 escudos** que giran alrededor de la peonza: paran los ataques de los rivales y los expulsan. Un rival que choca contra un escudo lo destruye y quedan menos girando. | — | Como giran, dejan huecos: un golpe que entra por un hueco llega a la peonza. Se termina al romperse los 3 o al acabarse el tiempo. |

6. # Sistema de Multijugador

Multijugador local para PC (Windows y, más adelante, Linux) y WebGL, con 2-4 jugadores usando mandos (y hasta 2 en teclado). Los huecos se pueden completar con **rivales CPU** (ver 6.4): el máximo de 4 cuenta jugadores humanos y CPU juntos.

**Escalable a 8:** en el futuro se quiere llegar a **8 jugadores**. Por ahora el máximo es 4, pero el código debe prepararse para que ese número sea un único valor configurable (lobby, HUD, colores, puntos de aparición, equipos...).

- **Asignación de controles flexible:** cada jugador se une en la pantalla de selección **manteniendo pulsado** el botón de ataque de su dispositivo (ver 9.2). El orden en que se unen fija el número de jugador (J1, J2...) y el dispositivo que usa cada uno.
- **Cámara dinámica** que encuadra a todos los jugadores vivos en todo momento. **Ángulo fijo** (2026-10-12): nunca gira; solo se desplaza y hace zoom, con seguimiento suave (`followSmoothTime` 0,4 s, `zoomSmoothTime` 0,6 s), y el punto que sigue tira un 25% hacia el centro de la arena (`arenaCenterPull`). Antes miraba cada frame al centro exacto de las peonzas y se movía «un poco loca».
- Si el proyecto funciona, se estudiará portarlo a móvil con partidas en la misma red local.

## 6.1 Modos de juego

- **Último en pie:** sin reaparición. Gana la última peonza (o equipo) con RPM. Sirve tanto para duelos 1vs1 como para batallas de 2-4 jugadores.
- **Todos contra todos (por puntos):** partida con tiempo límite. Cada eliminación da un punto al último jugador que golpeó a la peonza eliminada, y la peonza eliminada **reaparece**. Si una peonza se queda sin RPM **sin que nadie la haya golpeado** en los últimos segundos (desgaste o paredes), **pierde 1 punto** (autoeliminación). Gana quien tenga más puntos al acabar el tiempo o quien llegue antes a los puntos objetivo.
- **Por equipos (2vs2):** los equipos se enfrentan con las mismas reglas. El **fuego amigo es configurable**. Cada jugador elige su equipo en su columna de la pantalla de selección.
- **Sandbox (campo de pruebas):** sustituye al modo Práctica. Tiene **escena propia** y se usa tanto para practicar como de **entorno de debug**. Ver 6.3. *(Implementado en su primera versión, fase B.)*
- **Contra la IA:** la arquitectura separa quién controla la peonza (jugador, dummy o IA). La IA se usará primero en el sandbox y después como rival CPU en partidas normales. Ver 6.4.

**Modos futuros** *(ideas del usuario, 2026-10-12; para otras sesiones, Fase M; dudas en 12)*:

- **Carreras:** las peonzas compiten en un circuito.
- **Pilla pilla de fuego:** una peonza está **en llamas** y tiene que pegárselas a otra tocándola. Cuando se acaba el tiempo, **pierde** quien tenga el fuego. El fuego **no quita vida**: solo importa no tenerlo al final.
- **Virus:** como el pilla pilla, pero quien pega el fuego **no lo pierde**: el tocado se une al bando de los infectados. El fuego de este modo es **verde** y **no quita vida**. Al acabarse el tiempo, **ganan los que no estén infectados**.
- **Desafíos:** rivales CPU con nombre, cada uno con su peonza, su nivel y su perfil de IA (6.4). Más adelante, una **campaña** con personajes.

## 6.2 Reglas personalizables

Como en Super Smash Bros, cada modo tiene reglas por defecto y el jugador puede personalizarlas:

- **Vidas por jugador** (reaparición al perder una vida).
- **Límite de tiempo** (por defecto **sin límite**, salvo en el modo por puntos).
- **Puntos objetivo.**
- **Fuego amigo** (activado o desactivado).

**Desempates** cuando se acaba el tiempo: gana quien tenga más puntos o vidas. Si siguen empatados, gana quien tenga más % de RPM.

## 6.3 Sandbox (campo de pruebas y debug)

Escena propia (`Sandbox`) para probar mecánicas sin salir de la partida: parrys, especiales, piezas, etc. Todo se puede cambiar **en ejecución**.

- **Cómo se entra:** por JUGAR, como cualquier partida. Los jugadores se unen y eligen peonza en la selección, y **Sandbox** es un modo más (sustituye a Práctica): con 1 jugador es el único modo; con 2-4 se puede elegir. Dentro del sandbox se cambia de piezas en Mi peonza (E1).
- **Jugadores:** pueden entrar **hasta 4 jugadores humanos**, que se unen manteniendo pulsado ataque en la selección de peonzas. Sirve, por ejemplo, para practicar parrys entre dos personas con mando. **Máximo 4 peonzas en total** (humanos + dummies), como las CPU en partidas normales; se ampliará con F2.
- **Panel del sandbox:** se abre con un **botón propio** (Select/Back en mando, Tab en teclado; reasignable), que **pausa** el juego. Lo puede navegar **cualquiera** (mando, teclado o ratón), como el menú de pausa. Al cerrarlo, el juego sigue al momento (sin cuenta atrás) con los cambios aplicados.
- **Primera versión del panel:**
  - **Rivales:** ninguno o de 1 a 3 dummies (hasta completar 4 peonzas). La IA se añade aquí en la fase D.
  - **Comportamiento de los dummies:** un selector para todos a la vez (**Quieto, Moverse, Atacar, Dash hacia ti, Especial**) y otro de **cada cuántos segundos** (0,5 / 1 / 2 / 3). Con *Atacar* lanzan un ataque rápido hacia el jugador más cercano (para practicar parry).
- **Panel por secciones** *(implementado, E1-E7)*: el panel principal tiene **Rivales**, **Mi peonza**, **Trucos**, **Debug**, **Ajustes**, la **Velocidad** y **Reiniciar todo**; cada sección abre su subpanel y Atrás vuelve al principal (desde el principal, cierra). El botón del sandbox cierra el panel desde cualquier sección.
  - **Mi peonza:** un selector de jugador (J1-J4, solo si hay más de uno) y punta, cuerpo, disco y núcleo de ese jugador, con su arquetipo debajo y el núcleo con el color de su poder. El cambio es **en caliente**: modelo, estadísticas, cargas y poder (al cambiar de núcleo, el poder es el nuevo y la esfera empieza vacía; el HUD cambia de color). Se mantiene al reiniciar la partida.
  - **Trucos:** RPM infinitas, especial lleno, cargas infinitas, dash sin espera e invulnerable. Cada uno se aplica a **nadie, los jugadores, los dummies o todos**. Se mantienen durante la sesión.
  - **Reiniciar todo:** todas las peonzas vuelven a su punto de salida con RPM y cargas llenas, energía a 0 y sin estados alterados; el panel se cierra y se sigue jugando.
  - **Debug** *(E4, E5)*: un Sí/No para cada cosa, todo apagado al empezar: **ventana de parry** (anillo cian alrededor de la peonza mientras su ataque puede hacer parry), **velocidades** (flecha con lo que recorrerá en 0,25 s), **números de daño** (lo que pierde cada peonza, flotando; blanco, naranja desde 10 y rojo desde 30), **estados** (estado alterado o invulnerabilidad con el tiempo que le queda, bajo la peonza), **FPS** y **registro** (últimos 6 eventos abajo a la izquierda, con fondo: choques con lo que pierde cada uno en una sola línea, parrys, especiales, estados y K.O.; cada jugador con su color; se desvanecen a los 6 s). Usa tiempo real: se lee igual a cámara lenta.
  - **Guardar datos** *(en Debug, activado por defecto)*: cada sesión de sandbox escribe un CSV en `Logs/Sandbox/` (en la carpeta del proyecto, fuera del repositorio; en una build, en la carpeta de datos del juego) con las peonzas de cada jugador, la configuración de combate, todos los choques (velocidades, daño y empuje de cada una, nivel de carga, dash), daños, ataques, dashes, especiales, estados, K.O. y una foto por segundo de RPM, velocidad, energía y cargas. Sirve para analizar el equilibrio. Cómo leerlo: **Guía del Sandbox.md**.
  - **Velocidad** *(E6, en el panel principal)*: x1, x0,5, x0,25 o PAUSA. Con PAUSA el juego se congela al cerrar el panel y **«.» o el clic del stick derecho avanzan un frame** (la ayuda de abajo lo recuerda). La pausa normal, la cuenta atrás y el hit-stop vuelven a esta velocidad (`GameTime`).
  - **Ajustes** *(E7)*: **arena** (al cerrar el panel se recarga el sandbox en ella, con todo lo demás igual) y **valores de combate**: daño global, daño por nivel de carga, ventana de parry, coste del ataque rápido, coste del dash, daño por velocidad de choque, empuje base y energía del especial por daño, cada uno x0,5 / x0,75 / x1 / x1,25 / x1,5 / x2 sobre su valor. Son **temporales**: el sandbox juega con una copia de `CombatConfig`, el asset no se toca y **al salir del sandbox se pierden** (también la velocidad). "Restaurar valores" vuelve a x1.
- **Más adelante** (se irá ampliando):
  - Otros valores de `CombatConfig` en Ajustes, si hacen falta.

## 6.4 IA rival

La IA controla la peonza con la misma interfaz que un jugador (`IBladeInputSource`), así que no hace trampas: pulsa los mismos botones.

**Niveles de dificultad (5):** Fácil, Normal, Difícil, Experto e **Imposible** (2026-10-12, para quien busque un reto muy difícil). Cambian el tiempo de reacción, la puntería, la probabilidad de hacer parry, el uso del dash para esquivar y cómo usa el especial.

| Nivel | Reacción | Parry | Especial |
|---|---|---|---|
| Fácil | Lenta (~0,5 s) | Nunca | Al azar |
| Normal | Media (~0,3 s) | A veces | Cuando está lleno |
| Difícil | Rápida (~0,18 s) | A menudo | En buen momento |
| Experto | Muy rápida (~0,1 s) | Casi siempre que puede | Óptimo |
| Imposible | Muy rápida (~0,08 s), sin fallos | Casi siempre (85%) | Óptimo |

*(Valores orientativos, por ajustar.)*

**Comportamiento según el arquetipo de su peonza:**

- **Agilidad:** agresiva. Entra y sale con dash y **busca parrys** (ataque rápido justo cuando el rival lanza el suyo).
- **Ataque:** presión constante, combos y ataques cargados; usa el especial en cuanto lo tiene.
- **Defensa:** aguanta en el centro y contraataca cuando el rival gasta sus cargas o queda aturdido. Busca empujar al rival contra las paredes.
- **Balanceada:** mezcla de los anteriores según la situación.

**Rivales CPU en partidas normales:** se podrán añadir CPU a las partidas, hasta completar el máximo de jugadores (4 ahora; 8 en el futuro).

**Cómo está hecha (D1, 2026-10-11):**

- **`AIBrain`** (`Assets/Scripts/AI/`) es la clase base y el perfil Balanceado. Los perfiles por arquetipo (D3) heredan de ella y sobrescriben sus decisiones (ver *Perfiles por arquetipo* más abajo). `SimpleAIBrain` se queda solo para el fondo del menú hasta D8.
- **Percepción (`AIPerception`):** la IA **puede leer todos los datos internos del juego**; cada nivel decide cuáles ignora. Hoy: ver al rival cargar (para reaccionar antes), predecir su movimiento al apuntar, leer sus cargas de ataque y vigilar la pared. Sin asignaciones por frame.
- **Prioridades:** (1) salir de la pared si va hacia ella; (2) defenderse de un ataque que llega, con parry o esquiva con dash, mirado cada frame y con el retraso de reacción del nivel; (3) atacar si el rival está a tiro: el cargado se acerca mientras carga y el rápido embiste y se suelta a 3 m; (4) acercarse rodeando al rival. Sin atacar, **guarda unos 2,8 m de distancia** (salvo en Fácil), porque pegada al rival un ataque llega antes de poder reaccionar. Los niveles que hacen parry a menudo **se guardan una carga** para él mientras el rival pueda atacar.
- **Niveles (`AILevel`):** un asset por nivel (D2, ver *Datos de la IA* más abajo). Valores por defecto:

| Nivel | Reacción | Error de puntería | Parry (error de timing) | Esquiva | Lee | Capa táctica | Especial |
|---|---|---|---|---|---|---|---|
| Fácil | 0,5 s | ±25° | 0 | 0 | nada (ni la pared; no guarda distancia) | no | al azar |
| Normal | 0,3 s | ±14° | 25% (±0,08 s) | 20% | cargas del rival viniendo, pared | no | lleno |
| Difícil | 0,18 s | ±7° | 45% (±0,05 s) | 35% | además predice el movimiento y lee las cargas del rival | sí | lleno y con el rival cerca |
| Experto | 0,1 s | ±2° | 70% (±0,03 s) | 50% | igual que Difícil | sí | lleno y con el rival cerca |
| Imposible | 0,08 s | 0° | 85% (±0,02 s) | 60% | igual que Difícil | sí | lleno y con el rival cerca |

  El parry es la probabilidad de intentarlo cuando llega un ataque y tiene carga; el error de timing lo lanza antes o después y a veces falla (2026-10-11: con 90% y timing perfecto el Experto era imbatible).
- **Durante su ataque** sigue empujando el stick hacia el rival: si volvía a rodearlo, el golpe se desviaba y avanzaba la mitad (medido: 1,3-2,1 m frente a 2,5-3,2 m), y «atacaba al aire».
- **Capa táctica (Difícil y Experto, 2026-10-11):** una **agresividad** de 0 a 1 (0,5 = neutral) que se recalcula en cada decisión:
  - va **por delante en vida** → menos (no arriesga y guarda carga para el parry); por detrás → más (−0,8 × diferencia de vida);
  - **parrys que le ha hecho el rival** en la partida: tasa estimada partiendo de un 30%; si el rival los hace a menudo, menos; si casi nunca, más;
  - **rival quieto** (ni se mueve ni ataca) → +0,25;
  - **rival a menos de 1,5 m de la pared** y la IA empujando hacia ella → +0,2 (empotrarlo).
  
  Con agresividad 0,65 o más **no se guarda carga** para el parry, salvo que el rival la haya atacado en los últimos 2 s. Además, la agresividad sube o baja la probabilidad de atacar (×0,7-×1,2) y la de cargar (×0,5-×2). Si quiere un cargado y aún no tiene las cargas, **espera** a juntarlas en vez de gastar la que tiene. Contra un rival quieto, antes de un golpe fuerte **coge carrerilla**: se aleja hasta 5 m y entra en línea recta, cargando mientras corre o embistiendo (la velocidad del choque suma daño).
  Medido contra un J1 quieto (30 s): Difícil 7 rápidos y 4 cargados, Experto 6 y 3, con 10-13 carrerillas y unos 20 de daño medio por golpe (máximo 28-30). Antes no cargaba nunca (0-1) y fallaba muchos golpes. Con J1 atacando cada 1,5 s y cargas infinitas, el parry queda en un 21-26%, porque la IA se queda sin cargas (recarga solo sin atacar y parry sin devolución, C16).
- **En el Sandbox:** Rivales → Dummies → «IA», con las filas «Nivel IA» (Fácil, Normal, Difícil, Experto e Imposible), «Perfil IA» (Según peonza y después todos los perfiles de `Resources/AI/Profiles` por orden: hoy Balanceada, Ataque, Defensa y Agilidad; un perfil nuevo sale aquí solo) , «Su peonza» (el preset de los dummies, también fuera de «IA») y «Su núcleo» (el de su peonza o cualquier núcleo del catálogo, por el nombre de su poder: para probar cada poder contra la IA; 2026-10-15). Se aplica al momento y a todos los dummies a la vez.

**Perfiles por arquetipo (D3, 2026-10-12):**

- **Cómo se elige:** por defecto, el perfil del arquetipo de su peonza (`FakeBladeStats.Archetype`); en el Sandbox también a mano. `AIProfiles.Ensure` deja en la peonza la clase del comportamiento del perfil (`AIBrain` para Balanceada, `AttackAIBrain`, `DefenseAIBrain`, `AgilityAIBrain`) con ese perfil. Las IA de las partidas normales (D7, 9.2.1) usan siempre el de su arquetipo: el que más se repite entre sus piezas y, con empate (piezas muy mezcladas), Balanceada.
- **Estilo y habilidad:** el nivel da la habilidad base y el perfil la multiplica, así que un Experto sigue siendo mejor que un Normal con cualquier perfil. Además, cada perfil cambia dónde se coloca, cuándo y cómo ataca y cuándo usa el especial.
- **Niveles:** desde Normal se juega el perfil entero; en Fácil solo se nota su tendencia (dónde se coloca y cuánto ataca o carga), sin carrerillas, dashes de entrada o salida ni contraataques (`AILevel.fullProfile`).

| Perfil | Parry | Esquiva | Reacción | Estilo |
|---|---|---|---|---|
| Balanceada | ×1 | ×1 | ×1 | La IA de D1: guarda 2,8 m, rodea y ataca según la capa táctica. |
| Ataque | ×0,7 | ×0,8 | ×1 | Presión: guarda solo 2,2 m, ataca ×1,25 y carga ×1,6, no guarda carga para el parry y espera a juntar cargas para el cargado (también en Normal). Coge **carrerilla** para cada cargado (y en el 40% de los rápidos) siempre que tenga sitio, no solo contra un rival quieto. Usa el especial en cuanto lo tiene. Agresividad +0,15. |
| Defensa | ×1,1 | ×0,5 | ×1 | **Domina el centro:** se coloca a 1 m del centro, del lado del rival, y se queda quieta. Ataca poco (×0,5) salvo con hueco: el rival acaba de atacar (0,8 s), está aturdido o sin cargas (×1,6), o lo puede empotrar contra la pared (×1,4); como está en el centro, sus golpes lo empujan hacia fuera. Solo deja el centro para contraatacar o empotrar a un rival a menos de 4 m, si el rival no se acerca en 4 s o con agresividad 0,9 o más. Carga ×1,2. Agresividad −0,1. |
| Agilidad | ×1,3 | ×1,3 | ×0,9 | **Rodea** al rival a 3,6 m (cambia de sentido si va hacia la pared), entra con dash en el 30% de los rápidos y, al acabar su ataque, sale con dash de lado en el 30% (golpe y fuera). Busca parrys: se guarda una carga mientras el rival pueda atacar. Casi no carga (×0,5) y se acerca con dash el doble. |

  La carrerilla ahora se abre de lado hacia el centro cuando la IA está cerca de la pared (antes, con el rival más al centro, se metía contra la pared); lo nota también la Balanceada.
  Medido en el Sandbox (40 s por perfil, dummy con el preset de su arquetipo en «Según peonza» contra J1 con una IA Normal Balanceada y RPM infinitas):

| Difícil | Dist. al rival | Dist. al centro | Cargados | Carrerillas | Dashes | Parrys | Daño hecho / recibido |
|---|---|---|---|---|---|---|---|
| Ataque | 2,72 | 3,92 | 5 | 7 | 0 | 4 | 287 / 218 |
| Defensa | 2,57 | 3,75 (2,74 en otra) | 2 | 1 | 0 | 7 | 182 / 120 |
| Agilidad | 3,29 | 4,04 | 5 | 2 | 8 | 8 | 285 / 129 |
| Balanceada | 2,93 | 3,81 | 0 | 1 | 1 | 8 | 178 / 152 |

  La Defensa sujeta el centro la mitad del tiempo; el resto sale a contraatacar (36%) o a empotrar (21%), porque la IA Normal ataca mucho. En Normal, el Ataque hace 2 cargados y 4 carrerillas en 40 s (las cargas escasean por la recarga lenta, C16). En Fácil, la Defensa se queda a 2,3 m del centro y ninguna coge carrerilla ni usa el dash; el Ataque Fácil gana a la Balanceada Normal (591 / 353), sobre todo porque el preset de Ataque pega más.

  **Probado con mando (2026-10-12):** los perfiles se notan («sí noto los tipos de arquetipo con distintos comportamientos»), pero la IA «lee demasiado bien mis movimientos; se nota que es todo demasiado calculado». De ahí los fallos humanos.

**Fallos humanos (2026-10-12):** para que no lea los movimientos del rival de forma perfecta. Todos van por nivel: frecuentes en Fácil, casi nunca en Difícil y Experto (el Experto también los tiene, pocos).

- **Ve con retraso:** la posición y la velocidad del rival con las que se mueve y apunta le llegan con un retraso, así que las fintas y los cambios bruscos de dirección la engañan y llega a donde estaba el rival. Los ataques que le vienen se ven al momento: ya tienen su tiempo de reacción y su error de parry (`AIPerception`, que guarda las últimas posiciones del rival).
- **Predicción con error:** al apuntar adelantado (Difícil y Experto), cada ataque calcula de más o de menos.
- **Despistes:** cada cierto tiempo (al azar, entre ×0,6 y ×1,4 del intervalo), un rato en que no decide ni se defiende y el stick se queda como estaba.
- **Pulso imperfecto:** el stick deriva despacio a un lado y a otro al moverse (ruido suave), no va en línea perfecta.
- **Pasarse:** rodeando al rival o llegando a él, a veces sigue recta 0,5-0,9 s: abre el círculo y puede rozar la pared, o se pasa de frenada.

| Nivel | Retraso al ver | Error de predicción | Despiste (cada / dura) | Deriva del stick | Pasarse (por segundo) |
|---|---|---|---|---|---|
| Fácil | 0,35 s | (no predice) | ~5 s / 0,6 s | ±20° | 15% |
| Normal | 0,25 s | (no predice) | ~8 s / 0,5 s | ±12° | 8% |
| Difícil | 0,15 s | ±35% | ~14 s / 0,4 s | ±6° | 2% |
| Experto | 0,08 s | ±15% | ~22 s / 0,3 s | ±3° | 1% |
| Imposible | 0 | 0 | nunca | 0 | 0 |

  Medido (40 s por nivel, Balanceada contra una IA Normal): despistes 8 / 5 / 3 / 1 y veces que se pasa 4 / 2 / 0 / 0 (Fácil → Experto); choques con la pared 12 / 4 / 6 / 3; la distancia al rival que ve se aparta de la real 0,89 / 0,68 / 0,50 / 0,23 m de media. El parry no se rompe: contra un rápido cada 1,5 s, el Experto para el 31-34% con fallos y el 24-38% sin ellos (lo mismo dentro del ruido).
  **Imposible** (2026-10-12): el Experto mejorado y sin ningún fallo humano (tablas de arriba). Medido contra una IA Normal en 40 s: 0 despistes, ve al rival sin error, 8 parrys y el doble de daño hecho que recibido (135 / 67).
- **Contra cargados fuertes** (2026-10-12): como el parry contra un cargado de nivel 2 o más solo bloquea (2.5), la IA que ve cargar al rival intenta antes esquivarlo con dash (con su probabilidad de esquiva) y, si no, hace el parry igualmente.

**IA y especiales (D11, primera parte el 2026-10-14 y segunda el 2026-10-15):** la IA usa bien su poder y lee el del rival. Código en `AIBrain.Specials.cs` (parte de `AIBrain`); cada poder expone a la IA lo que necesita (carga, vuelo, punto de caída, ondas que quedan, escudos puestos...).

- **Qué lee cada nivel** (`AILevel.readsSpecials`): Fácil, ningún poder del rival; Normal, solo lo que se ve venir (la carga de la Embestida y del Salto y las ondas de Onda de choque); desde Difícil, todos. Reacciona con su tiempo de reacción y, para esquivar, con `specialDodgeChance` × la esquiva del perfil: Normal 40%, Difícil 70%, Experto 85% e Imposible 95%.
- **Poder del rival:**

| Poder del rival | Qué hace la IA |
|---|---|
| Embestida | No admite parry: esquiva de lado con dash cuando a la carga le faltan 0,1 s (antes, el rival aún puede reapuntar) o en cuanto embiste hacia ella, y mientras se aparta de lado. |
| Salto | En la carga (aún no se sabe adónde) se aparta de lado; en el aire sale de la onda del punto donde va a caer y, si andando no llega, con dash. |
| Tornado | Cerca del borde de su alcance intenta salir con dash; más dentro se coloca del lado del centro de la arena, para que el empujón la mande hacia dentro. |
| Onda de choque | Mientras le queden ondas, si está junto a la pared (a menos de 2,5 m) dentro de su alcance, se pone del lado del centro y no ataca. Lejos de la pared juega como siempre: huir de un rival que la persigue no sirve en una arena de 7 m. |
| Defensa, Pinchos, Rebote | Ataca menos (decidido el 2026-10-14): ×0,5, y ×0,75 con el perfil de Ataque (`AIProfile.defensiveRivalAttackScale`). |
| Rastro de agua *(D11.3, desde Difícil)* | Se aparta de los charcos de cualquier rival (mira dónde estará en 0,35 s). **Mojada no ataca ni hace dash** (resbala), salvo para esquivar un poder; el parry sí. |
| Escudo *(D11.3, desde Difícil)* | **Ataca por los huecos:** calcula cuándo llegará el golpe (hueco ÷ impulso del nivel + lo que el rival viene hacia ella + 0,08 s) y no lo suelta si entonces habrá un escudo delante, con 20º de margen. Si no hay hueco, deja la embestida (seguir de frente lo chocaba contra el escudo sin atacar) y espera hasta 0,6 s mirando cada frame; el cargado lo sigue cargando mientras quede más de 1 m. |
| Fantasma *(D11.3, desde Difícil)* | Va a por la peonza de verdad, no a por el clon (Normal y Fácil se lo creen). |
| Fuego, Hielo, Spin Boost *(D11.3, desde Difícil)* | Mientras dura el del rival, evita el contacto: lo rodea a 5 m y ataca menos (el mismo ×0,5 que contra Defensa). Si la IA tiene activo uno de estos tres, no. |
| De otro rival *(D11.3)* | Embestida, Salto, Tornado y Onda de choque también los lee de un rival que no sea el más cercano (el más cercano de los que lo usan), si el del más cercano no es uno de estos. |

- **Poder propio:**

| Poder | Qué hace la IA |
|---|---|
| Salto y Embestida | Durante la carga apunta adonde estará el rival (con su error de puntería y, si predice, adelantando como mucho 0,6 s: en lo que tarda el salto el rival gira). El Salto solo lo activa con el rival a la distancia del salto (3,2-7,4 m; si predice, la que habrá al caer) y, desde Difícil, no mientras el rival se mueve de lado a más de 3 m/s (como mucho espera 4 s). Con capa táctica, la segunda embestida (y las ondas) salen seguro si el rival está junto a la pared. |
| Tornado | Deja que la atracción acabe (empujón entero con el rival pegado); con capa táctica lo suelta antes, desde la mitad, si el empujón estampa al rival contra la pared. |
| Defensa, Pinchos, Rebote | «En buen momento» (Difícil o más) se guarda el poder hasta que la van a atacar (ataque que llega o rival cargando cerca), tiene menos del 30% de vida o lleva 6 s lleno. Con Defensa activa ataca ×1,5, arriesga más (+0,3 de agresividad) y no hace parry ni esquiva; con Pinchos no esquiva (el parry sí); con Rebote se queda en su sitio, ataca ×0,2 y no hace parry ni esquiva. La IA de Defensa se queda en el centro con Pinchos y Rebote. |
| Escudo | No lo reactiva mientras le quede más de un escudo (todos los perfiles). |
| Fantasma *(D11.3)* | No lo reactiva con el clon fuera: lo vuelve a sacar cuando se rompe. Mientras el rival pelea con el clon (lo tiene a menos de 3 m y más cerca que a ella), ataca ×1,4 y arriesga más (+0,2). |
| Rastro de agua *(D11.3)* | Mientras lo usa, rodea al rival a 2,8 m para dejarle el agua alrededor y ataca ×0,5; con el rival mojado, ataca ×1,5. |
| Rayos *(D11.3)* | Con capa táctica lo guarda hasta tener al rival a menos de 2,5 m de la pared (como mucho 4 s); activo, coge carrerilla antes de cada golpe si hay sitio (el golpe fuerte pide 6 m/s). |
| Fuego, Hielo, Spin Boost *(D11.3)* | Mientras duran, ataca ×1,4 y arriesga más (+0,2). |
| Todos | Mientras su poder bloquea las acciones (carga y vuelo del Salto, carga y embestida) no intenta atacar, hacer parry ni esquivar. |

  Medido en el Sandbox (J1 contra una IA Difícil Balanceada, mismas piezas; golpes que le quitan un 1,5% o más, con la pared): contra la Embestida de J1, **20 golpes en 16 embestidas leyendo frente a 34 sin leer**; contra el Salto, **8 en 8 saltos frente a 14**; contra la Onda de choque, **24 en 18 ondas frente a 32** (huyendo hacia atrás salían 21 de los fuertes, más que sin leerla: el rival la empujaba contra la pared). La IA con Embestida, con J1 dando vueltas: 23 golpes en 8 activaciones. Con Salto: J1 quieto, 6 de 6 caen a 1 m; en línea recta, 3 de 6 caen sobre él (1-3 m); dando vueltas sin parar, 1 de 6 (salta cuando se cansa de esperar). Escudo con la esfera siempre llena: 5 activaciones en 25 s, siempre tras perder los escudos. Defensa: la activa 0,12-0,38 s después del ataque de J1. Contra el Tornado de J1 la lectura casi no cambia nada (no se escapa: la atracción es más fuerte que su aceleración, como se diseñó).

  **Segunda parte (D11.3, 2026-10-15),** medido en el Sandbox contra una IA Difícil Balanceada, leyendo frente a sin leer (con el poder de J1 lleno cada 12 s, 40-60 s por prueba): contra el **Rastro de agua**, mojada el 22% del tiempo frente al 52%; contra el **Fantasma**, persigue al clon el 0% del tiempo con el clon fuera frente al 41%; contra el **Escudo**, rompe un escudo en el 34% de sus ataques frente al 43% (con mucho ruido entre pruebas: los fallos que quedan son errores de unos 0,05 s al prever el choque, y los escudos giran a una vuelta por segundo); contra el **Fuego** casi no cambia (quemada el 42% frente al 44%, mismo daño): una IA de Ataque que la persigue la alcanza igual, como pasaba al huir de la Onda de choque. Con sus poderes (esfera llena cada 3 s, 80 s): con **Rayos**, 29 de 29 activaciones acaban en golpe fuerte (1,4 s de media); con **Fantasma**, 6 activaciones, siempre con el clon roto; con **Rastro de agua**, J1 pasa el 83% del tiempo mojada.

**Datos de la IA (D2, 2026-10-12):** todos los valores de las tablas de arriba están en assets, no en el código, para ajustarlos en el Inspector y para crear rivales con su forma de jugar sin programar.

- **Dos ejes:** el **nivel** (`AILevel`, lo *bien* que juega: reacción, parry, puntería, qué lee y fallos humanos) y el **perfil** (`AIProfile`, *cómo* juega). El perfil multiplica la habilidad del nivel (parry, esquiva, reacción, ataque y carga), suma agresividad, fija la distancia que guarda y elige su **comportamiento**: la clase de IA que lo juega (Balanceada, Ataque, Defensa o Agilidad). Cada comportamiento tiene sus propios valores en el perfil: presión (Ataque), centro (Defensa) y rodear (Agilidad).
- **Dónde están:** `Resources/AI/Levels` (un asset por nivel, `AILevel_Easy` a `AILevel_Impossible`) y `Resources/AI/Profiles` (uno por arquetipo, `AIProfile_Balanced`, `_Attack`, `_Defense` y `_Agility`). El menú *FakeBlade → Setup AI* crea los que faltan con los valores por defecto del código, sin tocar los que existen. Si falta un asset, la IA usa esos valores por defecto (los tests de `AIDataTests` avisan).
- **En vivo:** la IA lee los assets en cada decisión, así que lo que se cambie en el Inspector en Play se nota al momento. **Ojo:** se queda guardado al salir de Play (es un asset). El comportamiento de un perfil se aplica al volver a poner la IA (en el Sandbox, al tocar cualquier fila de la IA).
- **Un rival nuevo** (por ejemplo, para el modo desafíos): duplicar un perfil, darle otro `nameKey` (en `Loc`) y un `order` mayor que 3, y cambiar sus valores. Sale solo en la fila «Perfil IA» del Sandbox. «Según peonza» usa el primer perfil (por orden) de cada comportamiento, así que los rivales no lo cambian. Si un rival necesita algo que ningún comportamiento hace, se programa una clase pequeña que hereda de uno (como `AttackAIBrain` de `AIBrain`) y se añade al enum `AIStyle`.
- **Más adelante (campaña):** combinar comportamientos (por ejemplo, la presión del Ataque con los parrys de la Agilidad) pide partir cada comportamiento en piezas que se eligen por separado en el perfil (D10).

## 6.5 Jugar a distancia y en red *(2026-10-12)*

**No habrá servidores ni partidas online propias.** El orden es: primero la versión de PC completa con multijugador local; la red, después.

**PC a distancia: juego remoto sobre el multijugador local** (sin programar red). Un jugador abre el juego y los demás se conectan a su pantalla con un programa de juego remoto. A cada invitado le llegan el vídeo y el sonido, y sus mandos aparecen en el PC del anfitrión como si estuvieran enchufados ahí. Para el juego, todos son jugadores locales.

- **Parsec:** funciona con cualquier juego, sin hacer nada. Los mandos de los invitados llegan como mandos de Xbox virtuales.
- **Steam Remote Play Together:** solo con el juego en Steam. Se activa en la configuración de Steamworks, sin código. Cada invitado aparece como un mando más.
- **Qué pide al juego** (casi todo ya se cumple): 4 mandos a la vez con asignación libre en el lobby, ventana sin bloqueos de foco (*Run In Background* ya activado) y nada que dependa del ratón. **El teclado no sirve para invitados**: el teclado de todos llega como uno solo al anfitrión, así que a distancia se juega con mando.
- **Latencia:** el invitado ve la imagen y su mando llega con retraso (unos 30-80 ms entre vídeo e input, según la conexión). Lo que más lo nota es el **parry, con una ventana de 0,12 s**. Habrá que probarlo y, si hace falta, añadir una opción de partida "ventana de parry amplia" para jugar a distancia.

**Android: multijugador en red local (LAN), más adelante.** En un móvil no caben varios jugadores, así que cada jugador usaría su propio dispositivo y todos en la misma red Wi-Fi:

- Un dispositivo **crea la partida** (anfitrión) y los demás la **encuentran en el lobby** sin escribir direcciones (descubrimiento por la red local). Sin servidor, sin cuentas y sin Internet.
- El anfitrión simula la partida (física, daño, IA) y los demás envían sus órdenes y reciben el estado. La forma de controlar la peonza ya pasa por una fuente de órdenes (`IBladeInputSource`), que es justo el punto donde entraría un jugador remoto.
- Herramienta candidata: **Netcode for GameObjects + Unity Transport**, que son de Unity, gratis y admiten descubrimiento en LAN. Se estudiará al empezar.
- Cambios grandes que trae: **interfaz táctil** (controles en pantalla, menús y lobby para dedos y pantallas pequeñas), un **lobby de red** (crear, buscar y unirse, desconexiones) y el **combate sincronizado** (lo que ve cada uno tiene que coincidir con el anfitrión).
- Si existe, la misma LAN también podría servir para PC.

**Para no cerrarse puertas desde ahora** (sin implementar nada de red):

1. Que todas las órdenes de una peonza pasen por su `IBladeInputSource`, nunca leídas directamente del mando en otro sitio.
2. Que las reglas (daño, K.O., puntos, rondas) se decidan en un único sitio (`GameManager` y los sistemas de combate), no repartidas entre la UI y los efectos.
3. Que la configuración de partida (`MatchSetup`) sean datos simples: piezas por ID del catálogo y reglas, sin referencias a objetos de la escena. Así se podrá enviar por red.
4. Que los efectos, el sonido y el HUD reaccionen a eventos y no decidan nada del combate (ya es así, 7.2).

## 6.6 Telemetría de las partidas *(C19, 2026-10-15)*

Cada partida local (no el Sandbox, que tiene su propia grabación, 6.3) deja un **CSV** con todo lo que sirve para el equilibrio, para que Claude lo analice. Es **anónima** (el ordenador solo aparece como un código) y está **activada por defecto**; se apaga en *Opciones → Guardar datos de partida*. En WebGL no se graba.

- **Dónde**: en una build, en la carpeta `Telemetria` junto al ejecutable (si no se puede escribir ahí, en `persistentDataPath/Telemetria`); en el editor, en `Logs/Telemetria/`. Un archivo por partida: `partida_<fecha>_<sesión>.csv`.
- **Cuándo**: al terminar la partida. Si se reinicia o se sale a medias, se guarda igual, como *abandonada*. Durante la partida solo se apuntan números en un búfer reservado de antemano (cero GC por frame, 7.3); el archivo se escribe al final.
- **Formato**: las columnas del CSV del Sandbox sin `real`: `t;event;player;other;v1;v2;v3;v4;info` (`;` como separador, punto decimal, `t` = segundos de partida sin pausas, `info` = pares `clave=valor` separados por `|`).

| Evento | Qué apunta |
|---|---|
| MATCH | v1-v4: jugadores, humanos, IA y número de partida de la sesión. Info: fecha, versión, plataforma, sesión, ordenador, modo, condición de victoria, vidas, tiempo, puntos, equipos, fuego amigo y arena |
| CONFIG | Valores clave de `CombatConfig` |
| PLAYER | v1-v4: RPM máximas, peso, ataque y defensa. Info: humano o IA (nivel y perfil), dispositivo, equipo, preset (si no se ha cambiado ninguna pieza; si no, `custom`), arquetipo, las 4 piezas, poder y su energía necesaria, velocidad, cargas, dash, desgaste y rasgos |
| CLASH | Choque entre dos peonzas: velocidad de cada una hacia la otra (v1, v2) y daño que recibe cada una (v3, v4). Info: tipo (normal, parejo, parry, doble parry, lanzada, pegadas, bloqueo), dash y nivel de carga de cada una |
| DAMAGE | Daño de 0,5 RPM o más: cantidad y % de RPM que le queda; `from=rival` (golpes, estados y poderes, con quien lo causa en `other`) o `from=wall` |
| ATTACK / DASH | Nivel de carga, % de RPM y velocidad al lanzarlo |
| SPECIAL_ON / SPECIAL_OFF | Poder, % de RPM, cargas al activarlo y duración al terminar |
| STATUS | Estado que recibe y quién se lo pone |
| PARRY / BLOCK / HEAL_CUT / WALL | Parry (o doble), bloqueo (nivel de la carga), curación cortada y golpe contra la pared (velocidad) |
| KO / ELIMINATED / RESPAWN | K.O. (con quien lo provoca, o `self=1`), eliminación y reaparición |
| SNAPSHOT | Uno por peonza y segundo: % de RPM, velocidad, energía del especial y distancia al centro |
| END | Duración, empate, equipo ganador, sucesos que no cupieron en el búfer. Info: `end=condition`, `time` o `abandoned`, y los ganadores |
| SUMMARY | Uno por jugador: ganador, daño hecho y recibido por minuto, tiempo vivo y, en info, todos los contadores (K.O., autoeliminaciones, daño por origen, choques ganados y perdidos, golpes de ataque y de dash, ataques por nivel, dashes, parrys, bloqueos, especiales, tiempo con el poder activo, curación, estados, golpes de pared, velocidad media y distancia media al centro) |

**Informe** (*FakeBlade → Informe de telemetría*): junta todos los `partida_*.csv` de `Logs/Telemetria/` y sus subcarpetas (para analizar las partidas de otro ordenador, copiar allí su carpeta `Telemetria`; un archivo repetido cuenta una vez) y deja en `Logs/Telemetria/Informes/` un informe en Markdown y una tabla `jugadores_*.csv` con una fila por jugador y partida. El informe compara las **victorias con las esperadas** (1 / jugadores o 1 / equipos; 1 es lo justo) por arquetipo, preset, poder, pieza y control (humano o nivel de IA), y da el uso de cada poder, los duelos 1 contra 1 entre arquetipos y el origen del daño. Las partidas abandonadas y las que no tienen ni un choque solo cuentan en el resumen.

7. # Sistema Técnico

## 7.1 Motor de Física

- **Rigidbody:** física principal de la peonza (el root nunca rota; el giro y la inclinación son visuales).
- **Collider:** detección precisa de impactos.
- **Particle System:** efectos visuales de velocidad e impacto.
- **Audio Source:** sonido dinámico basado en las RPM.

## 7.2 Arquitectura

- **Datos en ScriptableObjects:**
  - `FakeBladeComponentData`: las piezas.
  - `CombatConfig`: el ajuste global de combate y movimiento.
  - `MatchRules`: los modos y reglas de partida.
  - `HUDTheme`: colores, fuente y tamaños del HUD.
- **Input System** para teclado y mandos, con esquemas de teclado J1/J2 y asignación de mando por jugador. Los controles se pueden reasignar y se guardan.
- **Fuentes de control intercambiables:** cada peonza recibe sus órdenes de una fuente (jugador humano, dummy o, en el futuro, IA).
- **Poderes como datos + comportamiento** *(implementado)*: cada poder es un asset en `Resources/SpecialAbilities` (`SpecialAbilityData`) con sus valores comunes (nombre ES/EN, color, icono, energía necesaria, duración, intensidad del estallido) y los propios del poder (radio de la onda, regeneración...). Cada asset crea su clase de comportamiento (`SpecialAbility`), con ganchos al activarse, cada frame y al terminar, y con modificadores de daño, empuje, movimiento, dash y carga. `SpecialAbilitySystem` aplica el efecto común y la energía. Añadir un poder: un valor en `SpecialAbilityType`, su clase de datos con su comportamiento y su asset (menú *FakeBlade → Setup Specials*). Los colores y nombres de los poderes ya no están repetidos en `VfxLibrary`, `HUDTheme` ni `Loc`, ni sus valores en `CombatConfig`. Los estados alterados (quemadura, congelación, lanzada) serán un sistema común de la peonza.
- **Escenas:** `MainMenu` (menú principal, opciones, controles e información, con la arena de fondo), `Assembly` (unirse, montar peonzas y parámetros de partida), `BattleArena` (combate) y `Sandbox` (campo de pruebas: `BattleBootstrap` + `SandboxController` con su panel y sus dummies). La configuración pasa de una escena a otra en `MatchSetup`.
- **Catálogo** (`FakeBladeCatalog`): piezas, presets, paleta de colores, arenas y prefab de jugador. Añadir contenido es añadir entradas al catálogo.
- **HUD dirigido por eventos:** no busca objetos en la escena ni formatea strings cada frame.
- **Assembly definitions** (`FakeBlade.Runtime` y `FakeBlade.Editor`) para compilar más rápido y separar el código de editor.
- **Tests automáticos (EditMode)** *(2026-10-10)*: ensamblado `FakeBlade.Tests.EditMode` en `Assets/Tests/EditMode`, solo de editor (no entra en la build). Usa los assets reales y tiene tres grupos: `BladeFormulasTests` (límites y sentido de las fórmulas: más peso acelera menos, la defensa nunca anula todo el daño...), `BalanceTargetsTests` (objetivos de 2.7) y `CatalogTests` (contenido completo: piezas en el catálogo y con modelo, nombres `Tipo_Arquetipo_Nombre`, núcleos con poder y asset, presets completos con su arquetipo, textos ES/EN con los mismos `{0}`). Se ejecutan en *Window → General → Test Runner → EditMode → Run All*. Si la escena abierta tiene cambios sin guardar, el Test Runner pregunta antes si guardarla.

## 7.3 Optimización

Presupuesto de rendimiento por frame:

* 4 BeyBlades simultáneos: 16.6ms total
* Physics: 8ms
* Rendering: 6ms
* Game Logic: 2ms
* Audio: 0.6ms

Técnicas de optimización:

* **Pooling de partículas (implementado):** hay un único emisor por tipo de efecto, compartido por todas las peonzas: golpes (choque, pared, dash, ataque, parry, K.O., reaparición), poderes (activación y un aura por poder), carga del ataque (partículas, anillo y llamas), estela, chispas y RPM bajas (humo y chispas). Cada ráfaga se emite con `Emit()` en la posición del evento. Los efectos continuos de cada peonza (`BladeParticles`) emiten partícula a partícula con su posición y velocidad. No se instancia ni destruye nada durante el combate, y `maxParticles` limita la memoria de cada efecto. Los efectos están en `Assets/VFX/Prefabs` y la biblioteca, con colores y tasas ajustables, en `Resources/VfxLibrary`.
* **Estilo de los efectos (implementado):** sprites pixel art de 2 a 16 píxeles con filtro Point: píxel, chispa, estrella, cruz, llama, anillo, humo y rayo. Los genera el menú *FakeBlade → Setup VFX*. Usan transparencia normal con un color HDR por encima de 1, para que el bloom los haga brillar. No se usa mezcla aditiva porque sobre el suelo claro de la arena satura a blanco y se pierde el color de cada efecto. El humo no brilla.
* **Culling dinámico de efectos (implementado):** no se emite lo que queda fuera de la cámara; los efectos continuos lo comprueban una vez por peonza y frame. La cantidad de partículas se escala con la opción Partículas (Bajas, Medias o Altas).
* LOD system para meshes de peonzas (pendiente).
* Batching de audio events (pendiente).
* Cero asignaciones de memoria (GC) por frame en gameplay, HUD y efectos. Medido con 4 peonzas combatiendo: 0 KB por frame.
* Canvas del HUD separados por panel y actualizados solo cuando cambia un valor.
* Logs de depuración solo si se activan desde el Inspector.
* Física estable con cualquier masa: los frenados se aplican como cambios de velocidad acotados.

Medición en editor (4 peonzas con IA y efectos): física ~0,3 ms por paso y render ~1 ms, muy por debajo del presupuesto.

## 7.4 Assets de terceros (Asset Store)

**Licencia.** Los assets de la Asset Store, también los que llegaron por Humble Bundle (ya canjeados en la cuenta de Unity), usan la licencia estándar de la Asset Store (EULA):

- Se pueden usar en un juego comercial **sin pagar royalties** a sus autores, siempre que vayan integrados en el juego.
- **No se pueden redistribuir sueltos.** Como el repositorio de GitHub es público, **no se suben al repo**: sus carpetas están en `.gitignore`. Quien clone el proyecto tiene que descargarlos con su propia cuenta.
- Las **herramientas de editor** (licencia *Extension*: Odin, Console Pro, NodeCanvas...) son **por puesto**: cada persona que las use necesita su propia licencia.
- Cada asset indica en su página su tipo de licencia (Standard, Extension o, rara vez, Restricted). Se revisa antes de usarlo.
- **Odin Inspector** (versión de la Asset Store) solo vale mientras se facture menos de 200.000 $ al año; por encima hace falta Odin Enterprise. Unity Personal tiene el mismo límite (por encima, Unity Pro).

**Reglas de uso:**

- Se importa **solo lo que se va a usar**, desmarcando el resto en la ventana de importación, y sin escenas de demo.
- **Todo lo que no es nuestro va en `Assets/ThirdParty/`**, que está en `.gitignore`. Los paquetes de código o shaders son la excepción: se quedan en su carpeta original (para poder actualizarlos desde la Asset Store sin duplicarlos), y esa carpeta se añade al `.gitignore`.
- **Paquetes con licencia libre** (CC0, MIT...): van en `Assets/ThirdParty/Free/`, que **sí se sube** al repo. La licencia se comprueba en el archivo del propio paquete.
- **Organización actual:**
  - `Assets/ThirdParty/Free/Audio/Casual Game Sounds U6/`: FREE Casual Game SFX de Dustyroom (CC0, comprobado en su `license.pdf`). Sí va en el repo.
  - `Assets/ThirdParty/Audio/` (2026-10-13): paquetes de sonido con licencia del usuario, importados enteros para escoger más adelante (G4): Pro Sound Collection v1.3 de Gamemaster Audio (2,4 GB, 8.076 sonidos; su carpeta se renombró a `ProSound` porque 11 rutas llegaban a los 260 caracteres, el límite de Windows, y Unity no podía importarlas), Battle RPG Music Pack, Game Music Stingers and UI SFX Pack 2, Inventory SFX Bundle, Magic Spells SFX Bundle y Medieval Fantasy SFX Pack (unos 2,7 GB en total). No se suben. Los elegidos se copiarán, renombrados, a `Assets/Audio/SFX/` (G4.2); el resto conviene sacarlo del proyecto después, porque tanto audio alarga las importaciones y la carpeta `Library`.
  - `Assets/ThirdParty/Free/PixelArt by BDragon1727/` y `PixelArt by CodeManu/` (2026-10-13): sprites pixel gratis de itch.io para los efectos de las peonzas (estelas, impactos, hechizos), para cuando se trabaje el aspecto (K, G11). **CodeManu** (Free Pixel Effects Pack) es de dominio público (su `README.txt`). **BDragon1727** no trae licencia en el paquete: pide compensación si el proyecto da dinero; por ahora, agradecerlo en los créditos y decir para qué se usa. Las dos carpetas están en `.gitignore` (BDragon1727 son 85 MB de `.zip`/`.rar`, que Unity no importa: hay que descomprimir lo que se use).
  - `Assets/ThirdParty/UnityTemplate/`: el readme de la plantilla URP de Unity (`TutorialInfo` y `Readme.asset`). No lo usa nada; no se sube.
  - `Assets/Plugins/AllIn1SpriteShader/`: All In 1 Sprite Shader (sin demos). En su carpeta original y en `.gitignore`.
  - Se quedan en su sitio aunque no sean nuestros: `TextMesh Pro` (recursos de Unity que usa el HUD; hacen falta para que el repo funcione al clonarlo) e `InputSystem_Actions` (registrado como acciones globales del proyecto en la configuración de Input System).
- El código del juego **no depende de clases de paquetes de pago**: los shaders se usan a través de materiales y las herramientas solo en el editor. Así el repo compila aunque falten.
- **Qué entra en la build:** lo que usan las escenas de la build (y sus dependencias), todo lo que esté en carpetas `Resources` y todo el código de runtime. Lo que no se usa no entra, pero hay que vigilar las carpetas `Resources` y los scripts de cada paquete (se compilan siempre, también en WebGL).

## 7.5 Plataformas, API gráficas y builds *(revisado el 2026-10-11, sin cambios en el proyecto)*

Objetivo: builds de **Windows, Linux y WebGL**, y más adelante **Android**. Esta sección recoge cómo está configurado hoy el proyecto y qué hay que tener en cuenta para cada plataforma. Las tareas están en **Fase J** (Quehaceres).

**Cómo elige Unity la API gráfica.** Cada plataforma tiene una lista de API en *Player Settings → Other Settings → Graphics APIs*. Con *Auto Graphics API* activado, Unity usa su lista por defecto; desactivado, se usa la primera de la lista que funcione en la máquina, y las siguientes son de reserva. Cada API añadida multiplica las variantes de shader (más tiempo de build y más tamaño). Por defecto: Windows DirectX 12 en los proyectos nuevos desde Unity 6.1 (antes DirectX 11); Linux Vulkan (reserva OpenGL Core); WebGL WebGL 2 (WebGPU solo si se añade a mano); Android Vulkan con reserva OpenGL ES 3.

**Configuración actual** (Unity 6000.3.11f1, URP 17.3):

| Plataforma | API gráficas | Calidad por defecto | Scripting |
|---|---|---|---|
| Windows | Manual: **DirectX 11** (la que se usa) y DirectX 12 de reserva | PC (`PC_RPAsset`) | Mono (por defecto) |
| Linux | Automática (Vulkan, reserva OpenGL Core) | PC | Mono (por defecto) |
| WebGL | Automática (WebGL 2) | Mobile (`Mobile_RPAsset`) | IL2CPP (obligatorio) |
| Android | Manual: Vulkan y OpenGL ES 3 | Mobile | IL2CPP, solo ARM64, API mínima 25 |

- Espacio de color **lineal**: válido en todas (WebGL 2 y OpenGL ES 3 lo admiten).
- Los dos renderers (PC y Mobile) usan **Forward+**.
- `Mobile_RPAsset`: escala de render 0,8, una cascada de sombras, sin sombras suaves, **sin Depth Texture ni Opaque Texture**. `PC_RPAsset`: 4 cascadas, sombras suaves, con Depth y Opaque Texture.
- GPU Resident Drawer apagado y sin compute shaders propios: bien, porque WebGL 2 no tiene compute shaders.
- Shaders propios: ninguno. Los de terceros (All In 1 Sprite Shader, TextMesh Pro) son compatibles con OpenGL ES 3 / WebGL 2.
- El código no usa hilos ni `Task.Run` (WebGL no tiene hilos sin activarlos aparte).

**Qué implica DirectX 11 en Windows.** Es la API más compatible (GPUs antiguas y portátiles) y la más probada. DirectX 12 solo compensa con mucha carga de CPU en el render (*graphics jobs*), que este juego no tiene (render ~1 ms con 4 peonzas, ver 7.3). Lo que perdemos con DirectX 11: ray tracing y algunas funciones nuevas de URP que FakeBlade no necesita. **Se puede dejar así.** El editor también usa DirectX 11, así que lo que se ve en el editor es lo que sale en la build de Windows.

**Windows**

- Sin limitaciones conocidas. Es la plataforma de referencia: lo que se mide en el editor vale aquí. Primera build hecha y probada en otro ordenador el 2026-10-15 (J1): mandos detectados, 3 jugadores y una IA sin problemas.

**Linux**

- Se compila desde Windows con el módulo *Linux Build Support (Mono)* instalado en Unity Hub. Para compilar con IL2CPP desde Windows hace falta además el toolchain de Linux (paquete de Unity); con Mono no.
- Usa **Vulkan**: hay que probar el aspecto (bloom, partículas HDR, pixelado) y los drivers; si algo falla, poner OpenGL Core de reserva a mano.
- **Rutas y nombres de archivo distinguen mayúsculas.** `System.IO` (CSV del Sandbox y del banco) debe usar `Path.Combine` y el nombre exacto; `Resources.Load` con la ruta tal como está escrita.
- Mandos: Input System los lee por SDL. Xbox y genéricos van bien; los de PlayStation pueden verse como mando genérico (otros nombres de botón, sin vibración o con otro mapeo). Probar en Steam Deck si se publica en Steam.
- Pantalla completa y resoluciones: dependen del gestor de ventanas (X11 o Wayland); probar `Screen.SetResolution` de Opciones.

**WebGL** (la más limitada)

- **Forward+ da problemas en WebGL 2 con Unity 6**: hay informes de builds que se congelan al cargar y de decals rotos; se arregla pasando a Forward. Como WebGL usa `Mobile_Renderer` y está en Forward+, **antes de la primera build de WebGL hay que poner `Mobile_Renderer` en Forward** (o crear un renderer Web aparte) y comprobarlo. Con 4 peonzas y pocas luces, Forward basta.
- **Opciones → Calidad** deja elegir el nivel PC también en el navegador: comprobar que funciona o limitarlo a Mobile en WebGL.
- Sin Depth Texture en `Mobile_RPAsset`: las partículas suaves (Cartoon FX, G11) no se verían. Activarla en el asset de WebGL/Android si se usan esos efectos.
- **Coste:** bloom con HDR, sombras y muchas partículas transparentes pesan más en el navegador y en gráficas integradas. El pixelado de la escena (Render Scale bajo, en Otros pendientes) ayuda mucho aquí.
- **WebGPU:** en Unity 6.3 no está lista para producción. Según Unity, desde 6.6 está soportada pero sigue sin activarse por defecto. No se usa hasta actualizar Unity y probarla.
- **Mandos en el navegador:** un mando no aparece hasta que se pulsa un botón con la página enfocada; el mapeo depende del navegador; **la vibración no funciona** en WebGL (`SetMotorSpeeds` no hace nada, no da error). Con 4 mandos puede haber diferencias entre Chrome y Firefox: probarlo.
- **Teclado:** el navegador se queda algunas teclas (F5, Ctrl+W, Tab, F11). Los controles de teclado por defecto no deben usarlas.
- **Audio:** el navegador no deja sonar nada hasta el primer clic o tecla. La música del menú tiene que empezar después de esa primera pulsación.
- **Guardado:** `PlayerPrefs` y `persistentDataPath` van al almacenamiento del navegador (IndexedDB, poco espacio). Los ajustes y controles se guardan bien; los CSV del Sandbox y del banco no se pueden abrir desde fuera, así que en WebGL no tienen sentido.
- Ya resuelto: Salir se oculta y Resolución y Pantalla completa no aparecen en Opciones (las controla la página).
- *Run In Background* tiene su propia casilla para web; los navegadores frenan las pestañas en segundo plano de todos modos.
- **Publicación:** compresión Brotli activada (`webGLCompressionFormat`), que exige que el servidor envíe la cabecera `Content-Encoding` (itch.io lo hace). Si se publica en otro sitio sin esa cabecera, activar *Decompression Fallback* o usar Gzip.
- **IL2CPP** (también en Android): compila más lento y quita el código que no ve usado (*stripping*). Lo que se use solo por reflexión o por nombre puede desaparecer y fallar solo en la build; si pasa, se protege con `[Preserve]` o un `link.xml`.
- Memoria: crece hasta 2 GB; con este juego no debería ser un problema. Vigilar el tamaño de la descarga (texturas, audio y paquetes que no se usan, como Visual Scripting).

**Android** (más adelante)

- Usa el nivel de calidad Mobile, en **Forward+**: funciona en Vulkan, pero los móviles con OpenGL ES 3 pueden ir peor. Lo mismo que en WebGL: probar con Forward.
- **Controles y multijugador** *(decidido el 2026-10-12)*: un jugador por dispositivo, con controles táctiles, y partidas en **red local** (ver 6.5). Es un cambio grande de interfaz y de multijugador, así que va **después de tener la versión de PC completa**.
- **Orientación:** hoy está en rotación automática con vertical permitida. Para Android, solo horizontal.
- **HUD:** zona segura (notch y esquinas redondeadas, `Screen.safeArea`), relaciones de aspecto de 18:9 a 21:9 y tamaño de texto legible en pantalla pequeña.
- **Frecuencia:** Android va a 30 FPS por defecto; hay que fijar `Application.targetFrameRate = 60`. Vigilar batería y temperatura (Adaptive Performance ya está en los módulos).
- Texturas en ASTC (lo normal en Unity para Android). Publicar en Google Play exige AAB, ARM64 (ya configurado) y la API objetivo reciente (hoy "la más alta instalada").
- Botón Atrás del sistema: debe hacer lo mismo que Volver en los menús, y abrir la pausa en combate.
- Vibración: los mandos Bluetooth pueden no vibrar; la del móvil es otra API.

**Reglas desde ahora** (para no cerrarse puertas):

1. Nada de compute shaders, hilos, `Task.Run` ni librerías nativas solo de Windows en el código del juego.
2. Escribir archivos solo en el Sandbox y en herramientas de debug, siempre con `Path.Combine` y `persistentDataPath` en las builds.
3. Shaders y efectos nuevos: comprobar que funcionan sin Depth Texture o activarla en el asset Mobile.
4. Teclas por defecto que el navegador no se quede.
5. Textos y HUD que se lean en pantallas pequeñas y con cualquier relación de aspecto (anclajes, no posiciones fijas).
6. Mantener cero GC por frame (7.3): en WebGL y Android las pausas del recolector se notan mucho más.
7. Probar cada plataforma con una build, no solo en el editor: el editor de Windows (DirectX 11, Mono) no muestra los fallos de IL2CPP, Vulkan ni WebGL.

8. # Progresión y contenido

Por ahora será un minijuego gratuito con todo el contenido disponible.

Si el juego tiene éxito y puede generar ingresos, se hará un sistema de progresión para personalizar piezas de las peonzas, mejorar estadísticas y poderes, y un modo de duelos contra bosses que desbloqueen mejoras o piezas.
Como fuente de ingresos se podría optar por skins de pago (descartado por ahora).

9. # Interfaz de usuario

**Estilo:** retro pixel-art, a juego con los shaders pixelados:

- Fuente pixelada.
- Bordes duros sin antialiasing.
- Colores planos y saturados.
- Animaciones por pasos.

**Idiomas:** español e inglés, con selector en Opciones.

## 9.1 HUD de batalla

Interfaz minimalista y clara. Cada jugador tiene su panel en una esquina de la pantalla (J1 arriba a la izquierda, J2 arriba a la derecha, J3 abajo a la izquierda, J4 abajo a la derecha). En los paneles de la derecha la disposición se refleja.

- **Barra de vida (RPM) con dos capas.** La capa superior baja de golpe cuando se pierden RPM. La inferior la sigue poco a poco hasta el mismo valor. Junto a la barra se muestra el **porcentaje** de RPM.
- **Puntos de carga de ataque**, debajo de la barra. Poco antes de recargarse, su contorno parpadea. Al recargarse, cambian un poco de tamaño.
- **Esfera de habilidad especial**, en la esquina exterior, a la altura de la barra. Se rellena al cargarse. Cuando está llena, brilla y muestra partículas del poder. Mientras el poder está activo, el color y las partículas se exageran y el relleno baja con el tiempo.
- **Vidas o puntos** del jugador, según el modo.
- **Indicador de dash:** una línea fina que muestra el cooldown.
- **Centro de la pantalla:** cuenta atrás, avisos de K.O. y tiempo restante (si hay límite).

## 9.2 Menús Principales

Todos los menús se pueden recorrer con las flechas o WASD del teclado y con el stick o la cruceta de los mandos. **Confirmar** es Enter, Espacio o A, y **Atrás** es Esc, Retroceso o B. También funcionan con el ratón.

**Menú principal:** Jugar, Opciones, Controles, Información y Salir. De fondo se ve la arena en 3D con varias peonzas controladas por la máquina combatiendo solas, pixelada, con el título encima.

**Salir:** muestra una ventana de confirmación y, si se acepta, cierra la aplicación. En WebGL el navegador no deja cerrar la pestaña, así que el botón Salir se oculta.

### 9.2.1 Selección de peonzas (antes de jugar)

La pantalla se divide en **columnas, una por jugador**:

- **Al entrar hay 2 columnas.** Cada vez que alguien se une aparece una columna nueva para el siguiente jugador, hasta un máximo de 4. Las columnas ocupadas se reparten el ancho de la pantalla.
- **Unirse:** en una columna libre se muestra "Mantén A / Espacio / Ctrl der. para unirte". El jugador **mantiene pulsado** el botón de ataque de su dispositivo (teclado J1, teclado J2 o un mando) hasta llenar el indicador. El orden de unión fija J1, J2, J3 y J4 y el dispositivo de cada uno.
- **Montaje en su columna:**
  - Vista previa 3D de la peonza girando, pixelada, centrada y vista casi de frente.
  - Arquetipo resultante (con el color de su arquetipo) y barras de estadísticas **por tramos**: lo que da la peonza sin piezas (gris) y, a continuación, lo que suma cada pieza (un tono de verde por pieza). Lo que resta una pieza se ve en rojo al final de la barra. El tramo de la pieza de la fila seleccionada parpadea, para ver qué aporta.
  - Bajo el nombre de cada pieza, en pequeño y con su color, su **arquetipo**.
  - **Preset** (Ataque, Defensa, Agilidad, Balanceada o Aleatorio).
  - **Personalizar:** cambiar Punta, Cuerpo, Disco y Núcleo una a una. Al tocar una pieza, el preset pasa a "Personalizada". El nombre del núcleo se muestra **con el color de su poder** (el de su aura y su esfera) para reconocerlo de un vistazo.
  - Núcleo de cada preset: Ataque → Fuego, Defensa → Defensa, Agilidad → Rayos, Balanceada → Spin Boost.
  - **Color:** paleta fija de colores retro. Un color elegido por un jugador no lo puede coger otro.
  - **Equipo** (A o B): solo se usa si la partida es por equipos.
  - **Confirmar.**
- **Atrás:** si el jugador ha confirmado, deja de estar listo. Si no, al mantener Atrás abandona su columna. Con todas las columnas libres, Atrás vuelve al menú principal.
- **Rivales con IA (D7, 2026-10-12):** un jugador que no ha confirmado pulsa **Select** (teclado J1: Tab; teclado J2: Num +) y se añade una IA en la siguiente columna libre, con un preset al azar, el primer color libre y nivel Normal. Con **LB/RB** (Q/E; Num 7/9) el jugador pasa de su columna a la de cada IA, en bucle, y la edita con los mismos controles: preset, piezas, color, equipo y **Nivel IA** (Fácil, Normal, Difícil, Experto e Imposible), que ocupa el sitio de Confirmar. En la columna de una IA, Ataque vuelve a la propia y mantener Atrás **quita la IA**.
  - La cabecera de la IA dice «IA» y su nombre (IA 1, IA 2...) y, mientras alguien la edita, quién (por ejemplo «IA 1 · J1»). La columna del jugador dice «Editando IA 1».
  - Las IA **siempre están listas**: no se confirman. Van detrás de los jugadores (un jugador que se une después ocupa su sitio delante de ellas) y, si sale el último jugador, se quitan.
  - **Comportamiento:** el perfil de IA del arquetipo de su peonza (6.4). Con piezas muy mezcladas, el arquetipo es Balanceada y juega como Balanceada.
  - Cuentan como jugadores para los modos (con un jugador y una IA ya hay modos normales) y para los equipos. Al volver con «Cambiar peonzas» se recuperan.
- Cuando **todos los jugadores unidos han confirmado**, se pasa a los parámetros de partida.

### 9.2.2 Parámetros de partida

Solo **J1** (el primero que se unió) los configura. Los demás esperan.

- Modo: Último en pie, Vidas, Todos contra todos o Por equipos. Con 1 jugador el modo es siempre Práctica.
- Vidas, límite de tiempo, puntos objetivo y fuego amigo, según el modo.
- Escenario.
- **¡Empezar!** carga la batalla. Atrás vuelve a las columnas, y todos tienen que volver a confirmar.
- En modo por equipos, cada equipo tiene que tener al menos un jugador.

### 9.2.3 Opciones

Los ajustes se guardan entre sesiones.

- **Gráficos:** resolución, pantalla completa, calidad (preset), sombras, antialiasing, bloom, cantidad de partículas, VSync y mostrar FPS. Más adelante se añadirán otros efectos de postprocesado.
- **Audio:** volumen general, de la música y de los efectos.
- **Juego:** idioma, vibración del mando, sacudida de cámara e **icono del núcleo** (el icono del poder sobre el núcleo de las peonzas; desactivado por defecto, se aplica al momento).
- **Colores de peonza** (submenú, solo desde el menú principal): se elige un color de la paleta (1 a 8) y, para él, el color del disco, el cuerpo, la punta y el núcleo de una **lista fija de 24 colores retro** (`paintColors` del catálogo), con flechas y vista previa 3D. "Restaurar colores" vuelve a los de por defecto de ese color. Se guarda al momento y se usa en la selección, en partida y en el fondo del menú.

### 9.2.4 Controles

Reasignación completa de los controles de **Teclado J1**, **Teclado J2** y **Mando** (el mapeo de mando es común a todos los mandos):

- Se selecciona una acción y se pulsa la tecla o el botón nuevo.
- Si la tecla ya estaba en uso, se **intercambia** con la acción que la tenía, para que nunca quede un control duplicado ni vacío.
- "Restablecer" vuelve a los controles por defecto.

**Dispositivo de cada jugador** (arriba del todo; probado con mando el 2026-10-07): una fila por jugador humano con *Teclado J1 / Teclado J2 / Mando 1..N* (los mandos que detecte el juego; la lista se actualiza si se conecta o desconecta uno).

- Desde la **pausa** (partida o sandbox) se aplica al momento a la peonza de ese jugador y se mantiene al reiniciar. Los dummies y la IA no salen.
- Si otro jugador ya usaba ese dispositivo, se **intercambian**.
- Se guarda como preferencia: las partidas que se abren sin pasar por la selección (escena abierta desde el editor) empiezan con ese dispositivo, sin repetirlo entre jugadores. En el menú principal salen J1 y J2 para elegir esa preferencia.
- En la selección de peonzas manda el dispositivo con el que se une cada jugador, como hasta ahora.

### 9.2.5 Información

Créditos del juego con enlaces a las redes del autor. Los datos están en un asset editable (`CreditsData`), sin tocar código.

### 9.2.6 Escena de batalla

- **Pausa:** si cualquier jugador pulsa Start (o Esc), el juego se pausa. El menú de pausa permite reanudar el combate, ir a las opciones gráficas y de controles, o salir al menú principal (con confirmación). Al reanudar hay una cuenta atrás corta para que todos empiecen en las mismas condiciones.
- **Fin de la batalla:** cuando se cumplen las condiciones de victoria del modo, aparece un panel con los ganadores y tres botones:
  - **Revancha:** mismas condiciones.
  - **Cambiar peonzas:** vuelve a la selección manteniendo a los jugadores unidos y su montaje.
  - **Menú principal.**

## 9.3 Efectos de la UI *(en pruebas, G2)*

Se hacen con el shader **All In 1 Sprite Shader** (ver 7.4). Reglas:

- **Solo efectos que respetan la rejilla pixel:** destello (Hit Effect), cambio de tono (Hue Shift), Color Swap, contorno (Outline), gris (Greyscale), pixelado (Pixelate) y Shine siempre con Pixelate activado. Nada de blur, ondas ni distorsiones suaves.
- **Animación por pasos:** el código cambia los valores a saltos, igual que la barra y la esfera (`Quantize`), nunca de forma continua.
- **Materiales como assets** en `Assets/Materials/UI/`, asignados en `HUDTheme`. Cada panel crea su instancia al construirse (no cada frame: 0 GC) y solo cambia propiedades por ID (`Shader.PropertyToID`), sin usar clases del paquete.
- **Sin el paquete el juego sigue igual:** si falta el material o su shader (por ejemplo, en un clon del repo sin el paquete), se usa el aspecto actual.
- No vale para textos TMP.

| Elemento | Código | Efecto | Cuándo |
|---|---|---|---|
| Esfera del especial | `PlayerHUDPanel` (`BuildSphere`, `UpdateSphere`) | Glow + Shine que la cruza cada ~1 s | Al llenarse |
| | | Hue Shift por pasos y más glow | Mientras el poder está activo |
| Panel del jugador | `PlayerHUDPanel.UpdateHealth` (ya detecta el golpe) | Destello blanco de 2-3 frames + sacudida de 1-2 píxeles de UI | Al perder RPM por un golpe |
| Icono de estado | `BladeParticles` (`_statusIcon`, SpriteRenderer) | Outline + parpadeo por pasos | Mientras dura el estado; parpadeo rápido al final |
| Columnas del lobby | `LobbyColumn` | Gris | Columna libre |
| | | Outline o Shine | Opción seleccionada y jugador listo |
| Transiciones | `MenuScreen` / `SceneFlow` | Pixelate creciente + fundido | Al cambiar de pantalla o de escena |

## 9.4 Sonido *(en preparación, G4)*

Los sonidos elegidos se copian renombrados a una carpeta propia: `Assets/Audio/SFX/` si su licencia lo permite (CC0) o `Assets/ThirdParty/Audio/` si no (ver 7.4). Nombres por tipo: `ui_confirm`, `combat_clash`, `flow_countdown`...

| Evento | Código | Candidato |
|---|---|---|
| **Combate** | | |
| Choque entre peonzas | `FakeBladeController.collisionSound` | Pendiente (metálico, G5) |
| Choque contra la pared | — | Pendiente |
| Ataque rápido | `attackSound` | Pendiente |
| Ataque cargado: sube de nivel / máximo | — | Pendiente |
| Dash | `dashSound` | Pendiente |
| Activar especial | `specialSound` | Pendiente (uno por poder, packs de poderes) |
| Parry | `parrySound` | Pendiente |
| K.O. (peonza parada) | `spinOutSound` | Pendiente |
| Zumbido de giro en bucle (tono según las RPM, 7.1) | — | Pendiente (G5) |
| Estados: quemar, congelar, lanzada | — | Pendiente |
| **Menús y flujo** | | |
| Moverse entre botones de un menú | — | **DM-CGS-01** (decidido). Es el sonido básico de la UI; se copiará como `ui_move` |
| Pulsar un botón (confirmar) | — | Pendiente de elegir (Casual SFX cortos) |
| Atrás | — | Pendiente (Casual SFX cortos) |
| Cambiar un valor (izquierda/derecha) | — | Casual SFX (cortos) |
| Unirse en el lobby (mantener → completado) | — | Casual SFX |
| Jugador listo | — | Casual SFX |
| Cuenta atrás (3, 2, 1) y ¡Ya! | — | Casual SFX |
| Pausa / reanudar | — | Casual SFX |
| Victoria (panel de resultados) | — | Casual SFX (largos) |

**FREE Casual Game SFX, ordenados por duración** para escucharlos (los nombres solo llevan número):

- **Cortos (≤ 0,25 s), para la UI:** 01, 03, 14, 15, 16, 20, 21, 22, 32, 34, 35, 40, 41, 44, 47.
- **Medios (0,26-1 s), para avisos y la cuenta atrás:** 02, 04, 07, 08, 13, 17, 18, 19, 26, 27, 28, 29, 30, 31, 36, 37, 38, 39, 42, 46.
- **Largos (> 1 s), para jingles y la victoria:** 05, 06, 09, 10, 11, 12, 23, 24, 25, 33, 43, 45, 48, 49, 50.

10. # Ideas futuras

- **Rivales controlados por IA en partidas normales** (añadir CPU en la selección de peonzas). La IA se diseña en 6.4 y se usará primero en el sandbox. También permitiría el modo Survival.
- **Survival:** 1 contra oleadas de enemigos con dificultad progresiva (necesita IA; no está en el v2).
- Hazards y powerups de arena (ver sección 4); escenarios con obstáculos, rampas, elementos interactivos y coleccionables (sección 4, 2026-10-12).
- Poderes nuevos: Tornado, Rebote, Pinchos, Rastro de agua, Salto, Gancho y Escudo (sección 5, Fase L).
- Modos nuevos: Carreras, Pilla pilla de fuego, Virus y Desafíos (6.1, Fase M).
- Más efectos de postprocesado en Opciones.
- Portar a móvil con partidas en red local (6.5 y 7.5).
- **Arquetipos mixtos:** piezas que mezclen dos arquetipos (por ejemplo Ataque-Agilidad o Defensa-Balanceada) y compartan características de ambos. Por eso las piezas se nombran por arquetipo y no por peso (registro del 2026-10-08).

11. # Registro de decisiones

| Fecha | Tema | Decisión |
|---|---|---|
| 2026-09-30 | Prioridad de documentos | Si hay contradicción, manda el doc v2 y se elimina del GDD. |
| 2026-09-30 | Arquetipos y piezas | Los arquetipos (Ataque, Defensa, Agilidad, Balanceada) son builds que salen de combinar piezas intercambiables. El Núcleo define el poder. |
| 2026-09-30 | Eliminación | Solo al llegar a 0 RPM. Sin ring-out. |
| 2026-09-30 | Choque | La peonza más rápida recibe una fracción del daño (por defecto 25%). |
| 2026-09-30 | Dash | Sirve para esquivar y para atacar. Si choca, cuenta como un ataque normal (prioridad por velocidad). Sin armadura ni bonus extra. |
| 2026-09-30 | Botón de ataque | Se mantienen Ataque (rápido/cargado con cargas) + Dash + Especial. |
| 2026-09-30 | Modos | "Todos contra todos" = por puntos con tiempo y reaparición. "Último en pie" = sin reaparición. |
| 2026-09-30 | Teclado | Hasta 2 jugadores en el mismo teclado (WASD y flechas). |
| 2026-09-30 | Tiempo y reglas | Por defecto sin tiempo. Reglas personalizables: vidas, tiempo, puntos y fuego amigo. |
| 2026-09-30 | HUD | Se muestra el porcentaje de RPM. |
| 2026-09-30 | Estilo UI | Retro pixel-art. |
| 2026-09-30 | Idioma | Español + inglés. |
| 2026-09-30 | Fuego amigo | Configurable en las reglas de la partida. |
| 2026-09-30 | Flujo previo a jugar | Primero se unen los jugadores y montan su peonza; después J1 elige los parámetros. Sustituye al orden anterior del GDD. |
| 2026-09-30 | Unirse | Mantener el botón de ataque en una columna libre. Empiezan 2 columnas y se añade una por jugador unido (máximo 4). |
| 2026-09-30 | Montaje | Preset de arquetipo + personalización pieza a pieza. Vista previa 3D pixelada girando. |
| 2026-09-30 | Parámetros | Solo J1 configura la partida. |
| 2026-09-30 | Equipos | Se eligen en la columna de cada jugador. |
| 2026-09-30 | Colores | Paleta fija; un color no se puede repetir. |
| 2026-09-30 | 1 jugador | Modo práctica contra un dummy. Rivales con IA en el futuro. |
| 2026-09-30 | Opciones | Resolución, calidad, sombras, antialiasing, bloom, partículas, VSync, FPS, volúmenes, idioma, vibración y sacudida de cámara. |
| 2026-09-30 | Controles | Reasignación completa (teclado J1, J2 y mando) con intercambio de teclas repetidas. |
| 2026-09-30 | Fondo del menú | Arena 3D con peonzas combatiendo solas. |
| 2026-09-30 | Créditos | Asset editable con nombre y enlaces. |
| 2026-09-30 | Arena | Se usa `Arena 00.fbx` como primera arena. |
| 2026-09-30 | Resultados | Se añade "Cambiar peonzas" (volver a selección). |
| 2026-09-30 | Giro visual | Curva de forma logarítmica: casi al máximo hasta estar muy cerca de 0 RPM. Ajustable con `visualSpinKnee`. |
| 2026-09-30 | Dash acertado | Si el dash gana un choque contra un enemigo, recupera el 50% de su coste (una vez por dash). |
| 2026-09-30 | Autoapuntado | Sin asistencia cuando hay dirección. Sin dirección, apunta al enemigo más cercano. |
| 2026-09-30 | Parry | Ventana de 0,12 s al inicio de un ataque rápido. Bloquea todo el daño; el atacante se lleva su fracción, rebota aturdido y se le corta el ataque. Recompensas: carga devuelta, energía y aviso con hit-stop. Doble parry = se anulan. |
| 2026-09-30 | Parry y piezas | Las piezas modificarán la ventana de parry (agilidad +, defensa −). Campo ya disponible; pendiente de dar valores. |
| 2026-09-30 | Suelo y paredes | El suelo nunca quita RPM; en paredes solo cuenta el impacto perpendicular. |
| 2026-09-30 | Estilo de partículas | Sprites pixel art con brillo (bloom). Formas: chispa, estrella, cruz, llama, anillo, humo y rayo. |
| 2026-09-30 | Auras de poderes | Un aura distinta por poder: espiral verde (Spin Boost), ondas naranjas (Onda de choque), anillo azul (Storm Breaker) y rayos amarillos (Dash eléctrico). |
| 2026-09-30 | Carga del ataque | Partículas que convergen, crecen y se calientan por nivel; anillo al subir de nivel; destello y llamas al máximo. |
| 2026-09-30 | Estela y RPM bajas | Estela según la velocidad y chispas contra el suelo con muchas RPM. Por debajo del 30% de RPM: bamboleo visual, humo y chispas sueltas. |
| 2026-09-30 | Especiales: efecto común | Al activar cualquier poder: +25% de RPM máximas (igual para todos) y se rellenan todas las cargas de ataque. Después, cada poder tiene su efecto propio. |
| 2026-09-30 | Especiales: energía | Cada poder necesita una cantidad distinta de energía para llenarse. |
| 2026-09-30 | Lista de poderes | Se fusionan los actuales con los nuevos: Spin Boost, Onda de choque, Defensa (sustituye a Storm Breaker), Fuego (sustituye a Rastro de fuego), Hielo, Rayos (sustituye a Dash eléctrico) y Fantasma. 7 núcleos, uno por poder. |
| 2026-09-30 | Poder Defensa | Defensa casi al 100%: no pierde RPM por golpes, paredes ni desgaste, y el empuje recibido es casi nulo. Atacar y hacer dash sí cuestan RPM. |
| 2026-09-30 | Poder Fuego | Los golpes queman: el enemigo pierde RPM por tic durante unos segundos. |
| 2026-09-30 | Poder Hielo | Aura de hielo y humo blanco. Los enemigos golpeados se mueven más lento y recargan los ataques más despacio durante unos segundos. |
| 2026-09-30 | Poder Rayos | Modo cargado durante unos segundos. El primer golpe o choque es fuerte (algo más de daño y mucho más empuje) y gasta el poder. La peonza golpeada queda "lanzada" ~0,5 s: sin prioridad por velocidad, recibe el daño al chocar contra paredes u otras peonzas, y ese daño cuenta para el usuario de Rayos. |
| 2026-09-30 | Poder Fantasma | Clon fantasma invulnerable que dura lo que el poder. Persigue al enemigo más cercano; sus golpes quitan el 10% del daño normal, pero empujan y cortan la carga de ataque. |
| 2026-09-30 | Sandbox | Sustituye al modo Práctica, en una escena propia. Sirve para practicar y de entorno de debug; todo se cambia en ejecución. |
| 2026-09-30 | Panel del sandbox | Botón propio (Select/Back o Tab) que pausa y abre el panel, navegable con mando y ratón. |
| 2026-09-30 | IA | 4 niveles (Fácil, Normal, Difícil, Experto). El comportamiento depende del arquetipo; Agilidad es agresiva y busca parrys. |
| 2026-09-30 | Energía por poder | Spin Boost 1, Onda de choque 1, Fuego 1, Hielo 1, Defensa 1,2, Fantasma 1,3, Rayos 0,8 (1 = la barra actual). |
| 2026-09-30 | Duración de los poderes | 5 s todos. Rayos: hasta 6 s o hasta dar su golpe fuerte. No se gana energía mientras el poder está activo. |
| 2026-09-30 | Spin Boost y Onda de choque | Se quedan como están por ahora y se rehacen más adelante. |
| 2026-09-30 | Valores de Fuego | 1,5% de RPM máx. cada 0,5 s durante 3 s; se reinicia, no se acumula. |
| 2026-09-30 | Valores de Hielo | −35% de movimiento y recarga de ataques a la mitad durante 3 s; se reinicia, no se acumula. |
| 2026-09-30 | Valores de Rayos | +20% de daño, empuje x2,5 y lanzada 0,5 s. Los choques pequeños empujan fuerte sin gastar el poder; un choque por encima de una velocidad mínima da el golpe fuerte y lo gasta. |
| 2026-09-30 | Fantasma | Un solo clon, a la velocidad de su dueño; no hace parry ni se le puede hacer. |
| 2026-09-30 | Defensa (extras) | Ataques se recargan 1,5 veces más rápido, movimiento −15%, el dash cuesta el 10% de lo normal y su alcance baja un 40%. |
| 2026-09-30 | Estados alterados | Solo uno a la vez; el nuevo sustituye al anterior si no está bloqueado. Triángulo: Fuego > Hielo > Rayos > Fuego. Rayos contra una peonza congelada = choque normal y no se gasta. |
| 2026-09-30 | Estados: aspecto | Partículas que salen del panel del jugador (barra de RPM), partículas sobre la peonza e icono del estado encima de la peonza. |
| 2026-09-30 | Nombres de poderes | Defensa / Defense, Fuego / Fire, Hielo / Ice, Rayos / Lightning, Fantasma / Ghost. |
| 2026-09-30 | Sandbox: jugadores | Hasta 4 humanos, que se unen manteniendo ataque. |
| 2026-09-30 | Sandbox: panel v1 | Solo rivales y comportamiento del dummy. Piezas/núcleo, trucos, reinicio, debug y cámara lenta van después. |
| 2026-09-30 | IA por arquetipo | Se aceptan las propuestas de Ataque, Defensa y Balanceada. |
| 2026-09-30 | Rivales CPU | Se podrán añadir en partidas normales. Máximo 4 jugadores (humanos + CPU) ahora; escalable a 8 en el futuro. |
| 2026-09-30 | Autoeliminación | En el modo por puntos, quedarse sin RPM sin que nadie te haya golpeado resta 1 punto. |
| 2026-09-30 | Moverse cargando | Sí, al 60% de velocidad (ajustable). |
| 2026-09-30 | Bamboleo | Solo visual; no afecta al control. |
| 2026-09-30 | Parry por pieza | Agilidad +0,03 s, balanceada 0, ataque −0,01 s, defensa −0,03 s; mínimo 0,02 s. |
| 2026-10-05 | Assets de terceros | Los assets de la Asset Store no se suben al repo público (van en `.gitignore`). Se importa solo lo que se usa y el código del juego no depende de paquetes de pago. Lista de candidatos en la fase G. |
| 2026-10-05 | Quemadura y defensa | La quemadura es daño fijo: no la reduce la defensa de las piezas (sí un poder defensivo activo). |
| 2026-10-05 | Lanzada contra la pared | La pared la golpea como una peonza parada con la fuerza del usuario de Rayos (daño base + diferencia de velocidad). |
| 2026-10-05 | Lanzada: efecto bolos | Todo el daño que provoca una peonza lanzada (el suyo y el de la peonza contra la que choca) cuenta para el usuario de Rayos. |
| 2026-10-05 | Estados e invulnerabilidad | Las peonzas invulnerables (recién reaparecidas) no reciben estados alterados. |
| 2026-10-05 | Sonido al moverse por menús | DM-CGS-01 (pack Casual de Dustyroom) es el sonido básico al pasar de un botón a otro. El de pulsar un botón se elegirá más adelante. |
| 2026-10-06 | Entrada al sandbox | Por JUGAR: Sandbox es un modo del lobby (sustituye a Práctica); con 1 jugador es el único modo. |
| 2026-10-06 | Sandbox: máximo | 4 peonzas en total (humanos + dummies). |
| 2026-10-06 | Sandbox: dummies | Un comportamiento para todos (Quieto, Moverse, Atacar, Dash hacia ti, Especial) y un intervalo (0,5 / 1 / 2 / 3 s). |
| 2026-10-06 | Sandbox: panel | Lo navega cualquiera (mando, teclado o ratón), como la pausa. Al cerrarlo no hay cuenta atrás. |
| 2026-10-05 | Carpeta ThirdParty | Todo lo que no es nuestro va en `Assets/ThirdParty/` (ignorado); los paquetes libres (CC0, MIT...) en `ThirdParty/Free/`, que sí se sube. Los paquetes de código o shaders se quedan en su carpeta original (ignorada). |
| 2026-10-06 | Arquetipo de cada pieza | Toda pieza sigue un arquetipo, que se muestra en pequeño bajo su nombre en la selección. El número de piezas no importa; lo importante es identificarlas. |
| 2026-10-06 | Barras por tramos | Las barras de la selección muestran lo que aporta cada pieza (base + un tramo por pieza, lo que resta en rojo) y parpadea el tramo de la fila seleccionada. |
| 2026-10-06 | Tipos de modelo | Cada pieza tiene 4 tipos de modelo, uno por arquetipo: A = Ataque, B = Balanceada, C = Defensa, D = Agilidad. |
| 2026-10-06 | Combates más largos | Daño global x0,8 (`damageMultiplier`; la quemadura va aparte) y RPM base 350 → 400: combates ≈ 1,45 veces más largos. |
| 2026-10-06 | Ataque cargado | Daño = el de un ataque rápido × (1 + 0,10 × nivel) (`chargedDamagePerLevel`); la carga sigue dando alcance (+60%/nivel) y empuje (+40%/nivel). Sin bonus de masa por nivel. |
| 2026-10-06 | Movimiento más parejo | Ágiles algo más lentas y con giro más pesado; lentas algo más rápidas (`speedSpread`, `referenceMaxSpeed`, `turnByWeight`, `accelerationByWeight`). |
| 2026-10-07 | Más peso a las ágiles | Tras probarlo: `speedSpread` 0,5, giro de las ligeras −25%, aceleración −20% y masa física mínima 0,6. |
| 2026-10-07 | Daño de las ágiles | Relación de masas limitada a ×0,75-×1,5 y diferencias de ataque al 60%: las ligeras pegan más y las pesadas algo menos. |
| 2026-10-07 | Rasgos de piezas | Las piezas podrán tener rasgos (porcentajes sobre valores del combate, con contrapartidas). Base hecha en el código; el diseño de piezas con rasgos queda para más adelante. |
| 2026-10-07 | Carga del especial | Por tipo de golpe acertado, no por daño: rápido 8%, dash 10%, cargado 12% + 5% por nivel, sin atacar 3%, parry 20%, parejo la mitad; × 1,5 para tenerlo a mitad de combate. Golpear a una Defensa activa da energía; a un aliado, no. |
| 2026-10-06 | Datos del sandbox | Se guardan en un CSV por sesión (Debug → Guardar datos, activado por defecto) para analizar el equilibrio. |
| 2026-10-06 | Sandbox: debug, tiempo y ajustes | Debug con un Sí/No por cosa (todo apagado al empezar); velocidad con PAUSA y avance de frame con «.» / clic del stick derecho; valores de combate temporales sobre una copia de `CombatConfig`, con restaurar. |
| 2026-10-06 | Panel del sandbox | Por secciones en subpaneles (Rivales, Mi peonza, Trucos, Reiniciar todo). Trucos con destino por truco (nadie, jugadores, dummies, todos). Mi peonza con selector de jugador. |
| 2026-10-06 | Icono del núcleo | Se queda, en modo calcomanía, como opción en Opciones → Juego (desactivada por defecto). |
| 2026-10-06 | Colores por pieza | El color del jugador es el protagonista: anillas de ese color, cuerpo blanco, punta negra y núcleo en otro tono del mismo color. Personalizable por color de la paleta en Opciones y guardado. |
| 2026-10-06 | Núcleo genérico | Un único modelo de núcleo que cambia de color: tono de la paleta + brillo del color de su poder que crece con la carga de la esfera. |
| 2026-10-06 | Personalizar colores | Lista fija de colores retro (24), con flechas y vista previa; no color libre. |
| 2026-10-06 | Estadísticas de los núcleos | Poco diferenciadores pero equilibrados: todos +50 RPM máx. y una ventaja y un coste de 2 puntos cada uno, a juego con su poder (tabla en 3). |
| 2026-10-06 | Núcleos de los presets | Ataque → Fuego, Defensa → Defensa, Agilidad → Rayos, Balanceada → Spin Boost. Onda de choque e Hielo, solo en Personalizar. |
| 2026-10-06 | Color del núcleo en la selección | El nombre del núcleo se muestra con el color de su poder. |
| 2026-10-06 | Orden: Fantasma | La C5 (Fantasma) se aplaza hasta empezar la D1, para que el clon use la persecución de la IA. |
| 2026-10-06 | Rayos: qué choques cuentan | Solo cuenta su propia velocidad hacia el rival: 6 m/s o más es choque fuerte; de 1 a 6 m/s, choque pequeño (empuje x1,8, sin daño extra ni lanzada). Si la embisten estando quieta, choque normal. En parrys y choques con peonzas ya lanzadas no hay efecto de Rayos. Ya no tiene los extras de Dash eléctrico. |
| 2026-10-06 | Fuego y Hielo: qué es un golpe | Cualquier choque en el que le quite RPM al rival (gane o pierda el choque, la embistan o haga parry). El roce continuo no cuenta. Contra una Defensa activa no quema ni congela, porque no le quita RPM. |
| 2026-10-06 | Defensa: alcance del dash | "−40% de alcance" es −40% de impulso: el acelerón llega al 60% de la velocidad normal. La distancia total baja algo menos (~20% sin tocar el stick), porque después sigue deslizando. Se revisa en C7. |
| 2026-10-06 | Defensa: empuje | Recibe el 10% del empuje, también el que da la física al separar las dos peonzas. Ya no tiene los extras de Storm Breaker (ataques rápidos como cargados, +15% de velocidad). |
| 2026-10-07 | Dash frente a ataques | El dash hacía 2-3 veces el daño de un ataque rápido en todos los arquetipos (registro del sandbox), así que todo se reducía a dashear. Golpe con dash ×0,75 y golpe con ataque ×1,2; dash 8% de RPM y 1,8 s de espera; energía por dash 6%. Solo la Agilidad (la más rápida) saca más con el dash que con el ataque. |
| 2026-10-07 | Defensa demasiado fuerte | Pegaba más que el Ataque (Crush Wheel +15 de ataque y peso ×1,5). Crush Wheel 6 de ataque, Iron Fortress 0, relación de masas ×0,8-×1,3 y espera del dash +20% por pieza pesada. |
| 2026-10-07 | Agilidad débil | Ataques rápidos más baratos (Needle Point −25%) y que recargan antes (Aero Shell −20%), Aero Shell sin penalización de ataque y Razor Edge 12 de ataque. Golpe rápido contra el dummy: 16 → 27. |
| 2026-10-07 | Ritmo del especial | Salía cada 20-30 s: multiplicador 1,5 → 1,2 y dash 10% → 6%. |
| 2026-10-07 | Curaciones de los poderes | Con el tiempo, no al momento: el 25% común en 2 s y Spin Boost 4%/s (antes 6%/s). Un golpe de ataque enemigo corta lo que quede; paredes, roce, estados y poderes no. |
| 2026-10-07 | Paredes | Mismo daño que un choque parejo contra una peonza a esa velocidad (el doble que antes). Interpretación de "el mismo daño que chocar con una": se confirma o se cambia al probarlo. |
| 2026-10-07 | Peonzas pegadas | Si siguen en contacto 0,3 s tras un choque, se separan con un empuje de 7 m/s y un choque parejo pequeño (5 m/s), con chispazo. |
| 2026-10-07 | Sensación de los ataques | Estela continua durante el acelerón y vibración en ataques, carga, dash, golpes dados y recibidos, paredes y corte de curación. |
| 2026-10-07 | Masa y daño | La masa (peso de las piezas y bonus de masa del ataque) solo cuenta para el empuje, no para el daño, para equilibrar las estadísticas más fácilmente. Sustituye a la prueba de contar el bonus del ataque a la mitad, que seguía dando golpes de 40-60 con mando. |
| 2026-10-07 | Daño más bajo | Un golpe normal debe quitar 20-30 RPM. Con el registro del sandbox (media 38, la mitad por encima de 40): daño global ×0,8 → ×0,5 y golpe con dash ×0,75 → ×0,5 (sin la masa en el daño, el dash volvía a pegar más que los ataques en el arquetipo Ataque). Media esperada ≈ 23. |
| 2026-10-07 | Método de equilibrado | 1) Objetivos escritos antes de tocar valores. 2) Referencia: Balanceada contra Balanceada; primero la fórmula común, después los arquetipos con las piezas, luego los rasgos y al final los poderes. 3) Banco de pruebas que se repite igual (C10) para medir, y partidas con mando y el CSV para confirmar la sensación. 4) Un cambio cada vez, anotado con el antes y el después. 5) La Defensa se mide por lo que aguanta (daño recibido, golpes hasta el K.O.); si sigue débil, rasgos en sus piezas. |
| 2026-10-07 | Duración de los combates | Objetivo: 60 s en Balanceada contra Balanceada. Si se hace corto, se sube a 120 s y se va adaptando. |
| 2026-10-07 | Moverse cargando | Se quita el freno al 60% mientras se carga (comentado en el código, `moveMultiplierWhileCharging` sin uso): jugando no se notaba y hacía que el rápido pegara igual o más que el cargado. Probado con mando: no se nota nada especial (para apuntar ya hay que moverse o soltar un instante para el autoapuntado), así que se queda sin freno. |
| 2026-10-07 | Daño del cargado | Objetivo: nivel 3 = ×1,75 del rápido (Balanceada: rápido 20 → 25 / 30 / 35). `chargedDamagePerLevel` 0,10 → 0,25. Con +10% por nivel, jugando el cargado 3 solo pegaba ~50% más que el rápido y no se apreciaba. |
| 2026-10-07 | Cargado que no hacía daño | El daño del cargado restaba una velocidad fija (toda la que suma la carga, ~16 m/s en nivel 3): si el golpe llegaba tarde o frenado se quedaba en 0 y solo empujaba, y a toda velocidad se disparaba (0-90 RPM en el mismo registro). Ahora se quita en proporción (2.3). |
| 2026-10-07 | Objetivos de equilibrio | Tabla en 2.7: rápido ≈ 20, cargado ×1,75, tope de 60 por golpe, duelos de 60 s y arquetipos con diferencias moderadas (Ataque hace +25% y recibe +10%; Defensa −25% / −35%; Agilidad +10% / +20%). |
| 2026-10-07 | Daño por golpe | El daño de un golpe entre peonzas es un daño base (`hitBaseDamage` 50, calibrado para un rápido ≈ 20 en Balanceada contra Balanceada) que la velocidad solo mueve ±25%. Sustituye al daño que crecía en línea recta con la velocidad: los golpes a tope pasaban de 100. El empuje sigue dependiendo de la velocidad igual. |
| 2026-10-08 | Rasgos generales | 16 tipos de rasgo nuevos para cualquier pieza (daño por tipo de golpe, empuje, estados, daño recibido, paredes, espinas, golpe lento, parry, giro, aceleración, duración del poder y curación; tabla en 3). Primero se hacen y prueban los tipos; después se decide qué pieza lleva cuál. Los rasgos con condición quedan apuntados para más adelante. |
| 2026-10-08 | Piezas a mano | Las estadísticas y los rasgos de las piezas se ajustan a mano en sus assets; el menú de piezas solo crea las que faltan. |
| 2026-10-08 | Fórmulas y tablas de estadísticas | Las fórmulas que convierten estadísticas en valores del juego se recogen en 2.8 y viven en el código en `BladeFormulas`. Las tablas de estadísticas van aparte, en "Estadísticas de las piezas.html" (para leer) y ".md", que se generan enteras desde las piezas (valen aunque se añadan o renombren) con *FakeBlade → Export ComponentsData Stats* (no se edita a mano). |
| 2026-10-10 | Tests automáticos | Tests de EditMode para las fórmulas, los objetivos de equilibrio (2.7, ±10% como el banco) y el contenido (7.2). Los tres que no cumplen hoy (daño que recibe el Ataque 1,25 y la Defensa 0,74; Ataque cargado 3 a tope contra Agilidad, 68) se marcan como *Pendiente C12* (salen en amarillo y no cuentan como fallo) hasta que se haga C12. |
| 2026-10-10 | Trabajo con Claude | `CLAUDE.md` en la raíz con las normas del proyecto y skills del proyecto en `.claude/skills`: `/inicio-sesion` y `/fin-sesion` (preparan y cierran cada sesión), `probar-en-play`, `equilibrio` y `ejecutar-tests`. Las skills de Minecraft pasan a la carpeta de esos proyectos. Obsidian no se usa por ahora: los documentos ya son Markdown en el repo. |
| 2026-10-10 | Ramas de git | `main` queda estable (con lo probado) y el trabajo diario va a `desarrollo`, que se pasa a `main` cuando el usuario lo aprueba (al cerrar una fase o tras probar con mando). El `main` antiguo se guarda en `Legacy`. |
| 2026-10-10 | Cargas de la Balanceada | Se queda con 2 cargas con el Endurance Core: cura tanto que perder un ataque está bien. Se revisará con partidas de más jugadores. |
| 2026-10-10 | Equilibrio por arquetipo | El equilibrio busca que los 4 presets básicos funcionen; con todas las combinaciones de piezas y núcleos no se puede cuadrar todo a mano, así que más adelante, con más jugadores, se recogerá telemetría (qué se usa y qué funciona). Regla para las piezas: en cada hueco, la de un arquetipo es la mejor en la estadística de ese arquetipo y la Balanceada queda en medio en todo. |
| 2026-10-10 | Primero el movimiento | Antes de equilibrar (C12) se cierra el movimiento (C11 y C13), porque la velocidad cambia el daño de cada golpe (±25%) y lo equilibrado se desajustaría. Orden: C11 + C13 → banco con costes, desgaste y curación (C14) → C12 → rasgos (H8) → poderes (C7). |
| 2026-10-10 | Una estadística, un efecto | Velocidad = velocidad máxima; peso = aceleración, acelerones y empuje (una sola vez: la aceleración se aplica igual para cualquier masa); agarre = giro y frenado al soltar. La masa física solo cuenta en los choques (2.8). |
| 2026-10-10 | Peso sin topes | Los efectos del peso son curvas `(2,5 ÷ peso)^exponente` en vez de rectas cortadas entre 0,5 y 3: cada décima cuenta en todo el rango, y sirve para jefes muy pesados y para muchas variantes de cada arquetipo (campaña). Ajustadas para que los presets se muevan como antes. Lo especial de un jefe (inmune al empuje, más daño) irá en rasgos o en su asset, no en pesos extremos. |
| 2026-10-10 | Agarre | Estadística nueva de las piezas: giro y frenado al soltar, ±10% por punto. Está en todas las piezas pero solo la modifican las puntas. Los rasgos de Aceleración y Giro siguen sirviendo para ajustar encima. |
| 2026-10-10 | Derrape | El giro baja a la mitad (8 correcciones por segundo con agarre 0, antes 14-18) para que se note el derrape al cambiar de dirección; se ajusta probando con mando. La sensación general del movimiento se mantiene: solo ajustes pequeños. |
| 2026-10-10 | Duración de referencia | Se mantiene el objetivo de 90-120 s para el espejo de Balanceada, medido ya con el duelo completo del banco (C14, hoy 67 s). Los combates son a muerte hasta que solo quede una; el tiempo sirve de referencia del tiempo de juego de las partidas (enfoque de partidas rápidas) y se calibrará con jugadores reales o con una IA más completa. |
| 2026-10-10 | Agarre de las puntas | Puesto a mano: Needle Point +3, Flat Base +1, Wide Ball −3, Striker Point 0. Probado con mando: el derrape se nota un poco y basta; si hace falta más, se baja el giro base y se sube el agarre de las puntas que deban girar cerrado. |
| 2026-10-09 | Peso y velocidad de las piezas | El usuario ajustó las piezas a mano: casi todos los pesos son positivos (con pesos negativos las peonzas se volvían enseguida demasiado rápidas) y cada arquetipo se caracteriza con la velocidad. Sensación con mando: Agilidad la más rápida sin ser una locura; Ataque rápida; Balanceada algo más pesada pero pega bien y no es lenta; Defensa más lenta pero no tanto como antes, y pega bien. Pesos: Ataque 2,2, Agilidad 2, Balanceada y Defensa 3,2 (la Balanceada se mueve casi como la Defensa; se le subió algo la velocidad para diferenciarlas, ver C11). |
| 2026-10-09 | Defensa pega como la Balanceada | Objetivo de daño de la Defensa ×0,75 → ≈×1 (2.7): ya le cuesta más golpear por ser lenta. |
| 2026-10-09 | Primeros rasgos en las piezas | Puestos a mano: Striker Point cargado +15%, Razor Edge y Assault Frame rápido +10%, Flat Base daño recibido −10%, Standard Frame recarga −20%, Balanced Ring duración de tus estados +20%, Gale Ring empuje recibido +40%, Frost Core empuje recibido −20%. |
| 2026-10-09 | Editar estadísticas fuera de Unity | El HTML de estadísticas tiene gráficas (presets respecto a la Balanceada y aporte de cada pieza) y un editor: los cambios se descargan en JSON y se aplican en Unity con *FakeBlade → Import ComponentsData Stats*. Colores de arquetipo validados para daltonismo (Ataque naranja, Defensa azul, Agilidad verde agua, Balanceada gris). Añadir o quitar rasgos sigue haciéndose en Unity. |
| 2026-10-08 | Nombres de las piezas | Las piezas se clasifican por arquetipo, no por peso: `Tipo_Arquetipo_Nombre` (p. ej. `Tip_Attack_StrikerPoint`, `Core_Defense_ShockWave`) en vez de `Light/Medium/Heavy`. Los menús las ordenan por arquetipo (Balanceada, Ataque, Defensa, Agilidad). Deja sitio a arquetipos mixtos en el futuro (Ideas futuras). |
| 2026-10-08 | Velocidad base | La velocidad base de las peonzas sube de 4 a 6 (prefab de la peonza y `BladeBaseStats.Default`). La máxima real sube 0,4-0,8 m/s, porque `speedSpread` iguala en parte las diferencias. |
| 2026-10-08 | Arquetipo de la peonza | Es el que más se repite entre sus 4 piezas (punta, cuerpo, anilla y núcleo); con empate en cabeza, Balanceada. Antes se deducía de las estadísticas y, con los valores nuevos, el preset Ataque salía como Agilidad. |
| 2026-10-07 | Dispositivo por jugador | En Controles se elige el dispositivo de cada jugador (Teclado J1, Teclado J2, Mando 1..N), al momento desde la pausa y guardado como preferencia, para probar con mando cómodamente (9.2.4). |
| 2026-10-07 | Vista previa de piezas | Al enseñar una pieza en la selección, la peonza se queda quieta para ver bien el cambio (H7). |
| 2026-10-07 | Golpes de pared | Uno por peonza cada 0,1 s; si llega otro más fuerte en ese tiempo, solo se suma la diferencia. El golpe directo de Rayos no se toca hasta volver a medirlo. |
| 2026-10-07 | Arena plana | La arena por defecto pasa a tener suelo plano (`Arena V2 Plana`) para que la física sea predecible. Los cuencos quedan para arenas futuras (I2). |
| 2026-10-11 | IA: clase base y herencia | `AIBrain` nueva (no se amplía `SimpleAIBrain`); los comportamientos por arquetipo heredarán de ella (6.4). |
| 2026-10-11 | IA: qué percibe | Puede leer todos los datos internos del juego; cada nivel de dificultad decide qué ignora (6.4). |
| 2026-10-11 | IA: prioridades | Pared → defensa (parry o esquiva) → ataque → acercarse. Más adelante, capas según la vida del rival y el riesgo o las posibilidades de ganar (6.4). |
| 2026-10-11 | IA en el Sandbox | Comportamiento «IA» de los dummies con selector de nivel, adelantado de D6. |
| 2026-10-11 | IA: orden de trabajo | Primero que las IA funcionen; calibrar la duración de las partidas con ellas va después. Fantasma (C5) sigue aparcado, con poca prioridad, hasta tener las IA funcionando. |
| 2026-10-11 | IA: parry no infalible | Parry por nivel 25 / 45 / 70% (Normal / Difícil / Experto) y error de timing ±0,08 / ±0,05 / ±0,03 s (6.4). |
| 2026-10-11 | IA: capa táctica | Difícil y Experto calculan cuánto arriesgar: vida de cada una, parrys que les hace el rival, rival quieto y rival cerca de la pared. Guardan carga para el parry o no, prefieren cargados, esperan a juntar cargas y cogen carrerilla contra un rival quieto (6.4). |
| 2026-10-11 | Empujes con pérdida de control | Un empuje de 4 m/s o más quita el control `0,1 + 0,015 × empuje` s, que vuelve en 0,15 s; la lanzada de Rayos lo quita sus 0,5 s. Se puede atacar y hacer dash. Antes el stick a contrapelo cancelaba los empujes (2.8). |
| 2026-10-11 | Recarga solo sin atacar | Cada ataque (también el parry) pone a 0 la carga en curso; la recarga espera 0,5 s tras el ataque y no corre mientras se carga (2.3). |
| 2026-10-11 | Parry sin devolver la carga | El parry cuesta su carga; queda la recompensa de energía (20%), además de no recibir daño y aturdir al atacante (2.5). |
| 2026-10-11 | Run In Background | Activado en Player Settings: el juego sigue corriendo aunque la ventana pierda el foco (también en el juego compilado). |
| 2026-10-11 | Arena básica por defecto | La arena del usuario `Basic 2` (suelo plano, paredes verticales, dos FBX) es la primera del catálogo y la de por defecto, con los materiales del proyecto. Orden: Básica, Clásica (cuenco), Pruebas; la Plana V2 entra cuando exista su FBX (I3). |
| 2026-10-11 | Plataformas objetivo | Windows y WebGL, y más adelante Linux y Android. Revisión de API gráficas y limitaciones en 7.5, sin cambiar nada del proyecto todavía; las tareas van en la Fase J. |
| 2026-10-12 | Red y juego a distancia | Sin servidores ni online propio. En PC, el juego a distancia se hace con Parsec o Steam Remote Play Together sobre el multijugador local. Android tendrá un jugador por dispositivo y partidas en red local (Wi-Fi), después de completar la versión de PC (6.5). |
| 2026-10-12 | IA: perfil por arquetipo | Por defecto, el arquetipo de su peonza; en el Sandbox también se elige a mano («Perfil IA»), junto con la peonza de los dummies («Su peonza»). Balanceada = la IA de D1 (6.4). |
| 2026-10-12 | IA: perfil, estilo y habilidad | El perfil cambia el estilo (colocación, ataques, carrerilla, especial) y multiplica la habilidad del nivel (parry, esquiva, reacción): Ataque ×0,7 / ×0,8 / ×1, Defensa ×1,1 / ×0,5 / ×1, Agilidad ×1,3 / ×1,3 / ×0,9. Valores en código, al asset con D2 (6.4). |
| 2026-10-12 | IA: perfiles en Fácil | Los perfiles valen en los cuatro niveles; en Fácil solo se nota la tendencia (dónde se coloca y cuánto ataca o carga), sin carrerillas, dashes de entrada o salida ni contraataques (6.4). |
| 2026-10-12 | Bloqueo | El parry completo vale contra rápidos, dash y cargados de nivel 1. Contra un cargado de nivel 2 o más cuenta como parry pero bloquea: quien para recibe la mitad del daño y del empuje que recibiría quieta y la mitad de la energía; el atacante ni recibe daño ni queda aturdido. Compensa el riesgo de juntar cargas y da a la Defensa un punto débil (2.5). |
| 2026-10-12 | Bloqueo: energía y aturdimiento | El bloqueado gana la energía de su golpe como si hubiera entrado y queda aturdido `0,15 × (2,5 ÷ peso)` s (0,08-0,25), algo simbólico para dar impacto al golpe (2.5). |
| 2026-10-12 | Energía del parry | Quien para gana 12% (antes 20%) y a quien le paran, 6%: contra un rival que lo para todo no se cargaba nunca el especial (2.5). La energía por perder vida al recibir golpes queda para el pase de equilibrio (C12). |
| 2026-10-12 | Cámara de ángulo fijo | Nunca gira; seguimiento y zoom suaves por separado y un 25% de tirón hacia el centro de la arena (6). |
| 2026-10-12 | Agilidad a 3 cargas | El usuario baja a mano Gale Ring y Aero Shell de +1 a 0 cargas: la Agilidad pasa de 5 a 3 cargas. |
| 2026-10-12 | IA Imposible | Quinto nivel para retos muy difíciles: el Experto mejorado (reacción 0,08 s, puntería exacta, parry 85% con ±0,02 s, esquiva 60%) y sin ningún fallo humano (6.4). |
| 2026-10-12 | Anticipación de los ataques | Jugando contra la IA cuesta hacer parry porque los ataques rápidos no se ven venir. Se resolverá en la fase visual (después de la IA) con efectos más claros o llamativos al lanzar un ataque rápido (Fase K). |
| 2026-10-12 | IA: fallos humanos | Tras probarla con mando («lee demasiado bien mis movimientos»): ve al rival con retraso, predicción con error, despistes, pulso imperfecto y pasarse al rodear o al llegar (puede rozar la pared). Por nivel, frecuentes en Fácil y casi nunca en Difícil y Experto, que también los tiene (6.4). |
| 2026-10-12 | Agilidad con menos cargas | Tras probarla con mando la Agilidad es demasiado fuerte: el usuario la baja a mano a 3 cargas (hoy 5: base 3 + Gale Ring +1 + Aero Shell +1), confiando en su bonus de recarga para compensar (C12). |
| 2026-10-12 | IA: Defensa en el centro | Que el rival no tenga cargas solo la hace atacar más a tiro, no la saca del centro: con la recarga lenta (C16) el rival está sin cargas casi siempre y se pasaba la partida persiguiéndolo (6.4). |
| 2026-10-12 | Datos de la IA en assets (D2) | Un asset por nivel y uno por perfil (no un asset único ni piezas combinables todavía). El perfil elige el comportamiento (la clase de IA) y lleva sus valores, así que un rival nuevo es un asset y solo pide código si hace algo que ningún comportamiento hace. Los valores se leen en vivo (ajustarlos en Play con mando). D2 solo pasa los datos; separar Fácil de Normal se ajusta después de probar con mando (6.4). |
| 2026-10-12 | Rivales con nombre | El usuario quiere rivales con nombre para un **modo desafíos** y, más adelante, una **campaña** con personajes cuyo comportamiento y arquetipo se puedan cambiar e incluso combinar. Los perfiles de IA se preparan para eso (D2); combinar comportamientos queda para D10 (12, 13). |
| 2026-10-12 | Ideas para más adelante | Apuntadas como ideas, sin diseñar del todo: 7 poderes nuevos (Tornado, Rebote, Pinchos, Rastro de agua, Salto, Gancho y Escudo; 5, Fase L), 3 modos (Carreras, Pilla pilla de fuego y Virus; 6.1, Fase M) y escenarios con obstáculos, rampas, elementos interactivos y coleccionables (4, I4 e I5). El Gancho queda por decidir. Las dudas, en 12. |
| 2026-10-12 | Primeras decisiones de las ideas | Tornado **convive** con Onda de choque (cada uno con su recarga y sus usos). Rebote **convive** con Defensa: es un parry pasivo con rayo (empuja a quien la embiste). Pinchos refleja el **60-80%** del daño recibido (por equilibrar). En Pilla pilla y Virus el fuego **no quita vida**; en Virus, al acabar el tiempo ganan los no infectados (5, 6.1). |
| 2026-10-12 | IA en las partidas normales (D7) | Se añade desde la selección de peonzas: Select añade una IA y LB/RB pasa a su columna para elegir piezas, color, equipo y nivel (no hay fila de perfil). Juega con el perfil del arquetipo de su peonza y, con piezas muy mezcladas, como Balanceada. Siempre está lista y cuenta como jugador para los modos y los equipos. Mantener Atrás en su columna la quita (9.2.1). |
| 2026-10-12 | D4 y D5 hechas; D11 para más adelante | El usuario da por hechas D4 (parry) y D5 (especiales) porque la IA ya los usa. El comportamiento según el poder del núcleo (agresiva, segura o defensiva según el especial) se apunta como D11 y se hace cuando estén todos los especiales. |
| 2026-10-13 | Spin Boost y Onda de choque rehechos (F1) | Medidos con un banco de poderes, no se distinguían de no tener poder. **Spin Boost** pasa a ser un «Acelerón» (+25% de velocidad y aceleración, recarga ×1,5, sin desgaste y cura 2%/s hasta un golpe). **Onda de choque** son 3 ondas que lanza el jugador pulsando Especial (6% de daño, empuje fuerte y lanzada 0,4 s, para que la pared haga daño); la última termina el poder. Con 10 duelos cada uno: daño hecho por activación +16 (Spin Boost) y +52 (Onda), frente a +41 de Fuego. |
| 2026-10-13 | Reparto de los poderes por arquetipo | Ataque: Fuego, Onda de choque, Salto y Gancho. Balanceada: Spin Boost, Hielo, Tornado y Escudo. Defensa: Defensa, Pinchos y Rebote. Agilidad: Rayos, Fantasma y Rastro de agua (5). Onda de choque pasa de Defensa a Ataque: falta cambiar su núcleo (12). |
| 2026-10-13 | Repaso de lo pendiente (12) | **Cerrados:** ataque rápido frente a cargado (ya resuelto; los picos se miran en C12), cuándo se ejecutan los tests (en `/fin-sesion` y tras tocar fórmulas, piezas, el catálogo o `Loc`) y el render de WebGL y Android (`Mobile_Renderer` en Forward; PC sigue en Forward+). **Descartado:** que cualquiera que toque a Rayos salga impulsado (lo hace Rebote). **Pasan a tareas:** el collider según el disco (H10) y una opción propia para la sacudida del panel (G2.3). **Gancho** se hace el último de la Fase L. Las propuestas de cada poder nuevo se deciden al empezarlo. Siguen abiertos: autoeliminación, pérdida de control por empuje, recarga de cargas, mezcla de audio, DirectX, WebGPU y dónde se publica WebGL. |
| 2026-10-13 | El especial se carga también con el tiempo | Idea del usuario: a la vez que se pierde RPM por desgaste, se gana energía del especial (0,04 por segundo, aparte de los golpes). Con el banco: sin ella, en duelos de ~27 s entre IA Difíciles casi nunca se llegaba a usar (subir el multiplicador de los golpes a 2,4 daba lo mismo, pero solo premiaba acertar); con 0,04, el primero llega hacia la mitad del duelo (14 s) y una vez por duelo. |
| 2026-10-13 | Pinchos (L3) y Fantasma (C5) | **Pinchos:** refleja el 70% de los ataques recibidos (como Espinas, solo ataques), recibe −20% y hace +10%; los pinchos solo se ven. Banco (10 duelos): daño hecho +27 y recibido −24 frente al control (Fuego: +66 y −25). **Fantasma:** el clon juega con la IA y las estadísticas básicas de la Agilidad, hace el 10% del daño y aguanta 3 ataques (sin vida) antes de deshacerse en humo. **Rayos** y la energía del especial (por tiempo y quizá al recibir daño) no se tocan hasta tener todos los poderes; los arquetipos de los poderes no son prioritarios. |
| 2026-10-13 | Rebote (L2) | Lo activa cualquier embestida (no solo los ataques): quien va hacia ella sale despedido con el empuje entero ×1,8 y recibe el daño normal del choque, y ella no pierde RPM en ese choque. Banco (10 duelos): 10-0, daño recibido −58 frente al control (Pinchos −24): el más fuerte en defensa hasta ahora; se revisa en el equilibrio final, con todos los poderes. |
| 2026-10-13 | Escudo (L6) | Tres escudos de 60º (cubren la mitad) a una vuelta por segundo; cualquier embestida que entre por un escudo lo rompe, se para sin daño para ninguna de las dos y la que embiste sale despedida ×1,8 (el escudo protege, no castiga). Por un hueco, choque normal. Banco (10 duelos): 10-0, daño hecho +17 y recibido −17 frente al control (Rebote: −58). |
| 2026-10-13 | Escudo indefinido y Tornado (L1) | **Escudo**, tras probarlo con mando: los escudos se quedan hasta romperse (la fase activa dura 0,5 s), reactivarlo los rellena y necesita 0,6 de energía, porque es un poder flojo y difícil de controlar. **Tornado:** atrae 2 s a 6 m (30 m/s² en el centro: se resiste a duras penas) y expulsa con un empujón que deja lanzadas; el empujón se carga mientras atrae, se puede soltar antes (más flojo) y si no, sale al acabar con toda la fuerza. Banco (10 duelos): Tornado +22 de daño hecho y −5 recibido; Escudo, con casi 4 activaciones por duelo (cada una con el efecto común), −57 recibido y acaba con el 83% de RPM: hay que revisarlo en el equilibrio final (12). |
| 2026-10-13 | Tornado, tras probarlo con mando | Atrae 1,5 s (antes 2) para que sea un ataque más rápido, con más fuerza (40 m/s²: ya no se escapa, solo se frena) y un empujón más fuerte (26 m/s, anulando antes la velocidad con la que venían atraídas). Mientras atrae es inamovible: sin empuje de los choques y con 50 veces su masa física. |
| 2026-10-13 | Salto (L5) | Salto bajo (su altura o algo más) de 5 m hacia el stick. En el aire, los choques empujan sin daño para nadie y no se le puede hacer parry ni bloquearla; el daño y el empuje llegan al caer (10% en el centro, 22 m/s y lanzadas en 3 m). Un salto por activación. Banco (10 duelos): +12 de daño hecho y −29 recibido frente al control. |
| 2026-10-14 | Salto con carga | A petición del usuario: antes de saltar se carga 0,3 s (se frena y apunta) y salta el doble de alto (unos 0,9 m en vez de 0,45); el impacto al caer no cambia. |
| 2026-10-14 | Salto sin ataques ni dash | Probándolo, el usuario vio que en el aire podía atacar y hacer dash y cambiar así la dirección. Durante toda la activación (carga y vuelo) se bloquean los ataques y el dash (`SpecialAbility.BlocksActions`, reutilizable por otros poderes). |
| 2026-10-14 | Rastro de agua (L4) y estado Mojada | 4 s dejando agua que se seca a los 3 s; quien la pisa queda Mojada 2 s (casi sin agarre). Mojada es un **estado** con su icono, pero **aparte** del principal: convive con Congelada y Lanzada; mojarse apaga la quemadura y mojada no se quema. La dueña patina a favor (+20% de velocidad sobre su agua). Banco (10 duelos): +19 de daño hecho frente al control; la IA aún no lo aprovecha (D11). |
| 2026-10-14 | Fantasma (C5) | El clon dura hasta que lo rompan (3 ataques; los empujones no cuentan) o hasta que cae su dueña; reactivarlo lo trae de vuelta con los 3 golpes. IA de Agilidad en Normal. Por ahora, pálido y con humo (la transparencia, en K). No cuenta como jugador y se crea al equipar el núcleo, escondido (sin instanciar en combate). Banco (10 duelos): +9 de daño hecho y −19 recibido frente al control: sirve de señuelo. |
| 2026-10-14 | Embestida (poder nuevo) y Gancho para el futuro | Idea del usuario: tres embestidas a velocidad media con mucho empuje, área de golpe más amplia que un choque y sin parry ni bloqueo; cada golpe empuja fuerte, quita un 6% y deja lanzado; 6 s para usar las tres. Núcleo de Ataque. Banco (10 duelos): +62 de daño hecho y −36 recibido frente al control (como Fuego, de los más fuertes; se revisa en el equilibrio final). Gancho se deja para el futuro. Nota abierta: qué pasa si chocan dos Embestidas (12). |
| 2026-10-14 | Embestida: dos y con carga | Probándola, el usuario vio que se activaba muy rápido y que si la primera entraba encadenaba las tres y destrozaba. Pasa a **dos** embestidas (ajustable en el asset para el equilibrio, incluso a una) y cada una empieza con una **carga de 0,3 s** (se frena y apunta), como Salto. |
| 2026-10-14 | IA y especiales (D11), primera parte | El usuario pide que la IA use bien los especiales y sepa leer cuando los usa el rival (por ejemplo, esquivar la Embestida y el Salto). Decidido: **Normal lee solo lo que se ve venir** (Embestida, Salto, Onda de choque) y **desde Difícil, todo**; contra Defensa, Pinchos y Rebote del rival, **atacar menos** (no esperar a que acabe); **todos los perfiles** con matices (el de Ataque espera menos, la Defensa se queda en el centro con Pinchos y Rebote); se hace **por partes**. Valores nuevos en los assets (`readsSpecials` y `specialDodgeChance` en el nivel, `defensiveRivalAttackScale` en el perfil). Detalle y medidas en 6.4. |
| 2026-10-14 | IA: Onda de choque y Tornado, tras medirlos | Huir de la Onda de choque del rival la llevaba contra la pared (más golpes que sin leerla): solo se coloca del lado del centro cuando está junto a la pared. Su propio Tornado lo soltaba al tener al rival pegado, casi nada más activarlo y con la mitad de fuerza: ahora deja acabar la atracción salvo para estampar al rival contra la pared. |
| 2026-10-15 | Primera prueba con mando de D11 | La IA esquiva bien la Embestida y el Salto. El **Salto** era lento y costaba acertar: cae con el **doble de gravedad** (0,64 s en el aire en vez de 0,95; misma altura y distancia). El **Escudo** se cargaba con dos golpes cargados: su energía sube de **0,6 a 0,8** (más adelante, quizá que curar al reactivarlo pronto dé menos). En el Sandbox, fila **«Su núcleo»** para elegir el núcleo de los dummies (antes solo venía con su preset). |
| 2026-10-15 | IA y especiales (D11.3), segunda parte | Hecha la lista de 13 (D11.3) tal cual. Decidido al medir: la lectura del Escudo, del Rastro de agua, del Fantasma y de Fuego, Hielo y Spin Boost del rival va **desde Difícil** (como el resto de lo que no se ve venir); mojada sigue haciendo **parry** y esquivando poderes. El golpe contra el Escudo se calcula con el impulso del nivel y sin la velocidad que ya lleva (sumándola fallaba medio hueco) y espera el hueco mirando cada frame. Medidas en 6.4. |
| 2026-10-15 | Primera build de Windows (J1) | DirectX 11 y Mono, tal como está en 7.5. El usuario la probó en otro ordenador: detecta los mandos y va bien con 3 jugadores y una IA. Al compilar, Unity reescribe solo algunos ajustes (variantes de shader de URP, ajustes de ejecución, el `InputSystem_Actions` precargado): se guardan en el repo. |
| 2026-10-15 | Segunda prueba con mando: Salto, Escudo y equilibrio | La IA salta y embiste con eficacia: se queda así. El **Salto** se exagera: el doble de alto otra vez (1,9 m) y más rápido (0,54 s en el aire, con 6 veces la gravedad). El **Escudo** sigue siendo muy fuerte: energía **1,2** (como Defensa). El **equilibrio** se deja para más adelante, con datos de partidas reales: **telemetría** en la build que guarde archivos de cada partida jugada en local, también con jugadores nuevos (C19). |
| 2026-10-15 | Telemetría de las partidas (C19) | Tal como se propuso: un CSV por partida local (no el Sandbox), anónimo, activado por defecto y con opción para apagarlo en Opciones; en una build va a `Telemetria/` junto al ejecutable y en el editor a `Logs/Telemetria/`. El usuario pidió guardar **todo lo que ayude a analizar el equilibrio**, así que además de lo propuesto apunta cada choque, daño, ataque, dash, poder, estado, parry, bloqueo, golpe de pared y K.O., una foto por segundo de cada peonza y un resumen por jugador (6.6). Las partidas reiniciadas o dejadas a medias se guardan como abandonadas. Herramienta *Informe de telemetría* que lo junta todo, con victorias frente a las esperadas. No se graba en WebGL. |

12. # Pendiente de definir

Entre paréntesis, la propuesta por defecto si no se decide otra cosa.

- Valores de equilibrio: coste en RPM de ataque y dash, tiempos de recarga, fracción de daño de la peonza rápida, etc. Se ajustan en el asset `CombatConfig`.
- Qué otros efectos de postprocesado se añaden a Opciones.
- **Estadísticas de las piezas nuevas** (H9): Striker Point, Assault Frame y Gale Ring tienen valores provisionales; se fijan en el pase de equilibrio. También queda por decidir si los presets usan las piezas de su arquetipo (hoy el preset Agilidad lleva Razor Edge, la anilla de Ataque, y el preset Ataque lleva Flat Base, Aero Shell y Crush Wheel).
- Sonido al pulsar un botón (confirmar) y al volver atrás en los menús (alguno de los cortos del pack Casual, distinto de DM-CGS-01, que es el de moverse entre botones).
- Autoeliminación: cuántos segundos sin recibir golpes hacen que cuente como autoeliminación (los mismos 5 s que dan el punto de K.O.).
- Tests en Play de las reglas de combate (hacerlos cuando se toque el choque, el parry o el dash; con la escena Sandbox y dos dummies).
- **Ritmo de golpes por arquetipo en el duelo del banco** (C14): hoy todas golpean cada 4,2 s. Propuesta: medirlo jugando con cada preset contra un dummy en el sandbox (los registros ya lo dan) y, mientras tanto, dejarlo igual para todas.
- **Barra de agarre en la selección de peonzas** (añadirla junto a velocidad y peso cuando las puntas tengan valores).
- **Objetivo de daño recibido de la Defensa** (2.7 dice 0,65; con los valores del 2026-10-10 recibe 0,98 y aguanta por tener más RPM, 550 frente a 405). Propuesta: cambiar el objetivo a "aguanta ×1,4 por vida" si se confirma jugando.

**Poderes:**

- **Mover Impact Core a Ataque** (reparto del 2026-10-13): cambiar su arquetipo y su nombre de asset (`Core_Defense_ShockWave` → `Core_Attack_ShockWave`) y quizá sus estadísticas (hoy es pesada: +30 RPM, −0,2 velocidad, +2 defensa). Propuesta: lo hace el usuario en el Inspector, como el resto de piezas. **No es prioritario** (2026-10-13): los arquetipos de los poderes se repasan más adelante.
- Lanzada en arenas con forma de cuenco: la peonza lanzada sube por la pendiente (cuenta como suelo) y casi nunca llega a golpear una pared, así que el daño de pared de la lanzada apenas sale en la arena actual (se revisa con las arenas y en C7).
- Rayos: daño real del golpe fuerte (registro del 2026-10-07, 10 golpes contra un dummy de 580 RPM): el golpe en sí quita ~38 (+20%), pero con los choques contra la pared de la lanzada el total sale en **~105 RPM de media (62-161)**, ~18% de la vida. Se nota poco porque casi todo llega después, por la pared. El doble golpe de pared ya está corregido (C9). Con el daño global a ×0,5 (probe, preset Agilidad con dash): golpe directo ~25-28 y un golpe de pared de 12-58, total **39-90** cuando llega a la pared (antes 115-152). Si sigue siendo mucho, la idea es que Rayos tarde más en cargarse: hoy necesita 0,8 de energía, menos que la barra normal (propuesta: subirla a 1-1,2 en C7). **No se toca hasta tener todos los poderes** (2026-10-13): entonces se ajustan y se prueban todos juntos.
- **Energía del especial, para el equilibrio final** (2026-10-13, con todos los poderes hechos): cuánta se gana con el tiempo (hoy 0,04/s) y si **recibir daño de otra peonza** también da un porcentaje de energía (propuesta: medirlo con el banco de poderes y el de ritmo).
- **Escudo se activa demasiado** (banco del 2026-10-13): con 0,6 de energía y la carga por tiempo se activa casi 4 veces por duelo y cada vez cura un 25% y rellena las cargas, así que acaba los duelos con el 83% de RPM. Propuesta: que reactivarlo con escudos todavía puestos no dé el efecto común (solo rellene los escudos), o subir su energía a 0,8; se decide en el equilibrio final, con todos los poderes. *(2026-10-14, D11: la IA ya no lo reactiva mientras le quede más de un escudo; hay que volver a medirlo con el banco, porque el banco juega con la IA.)* *(2026-10-15: probado con mando, se cargaba con dos golpes; su energía sube a 0,8. Queda por decidir si reactivarlo pronto debe curar menos, y lo mismo con las cargas; se vuelve a medir en C7.)* *(2026-10-15: con mando, todavía muy fuerte a 0,8; sube a 1,2.)*
- **Poderes futuros (Fase L, 2026-10-12)**. El arquetipo de cada uno ya está decidido (reparto en 5, 2026-10-13). Para todos falta: las estadísticas de su núcleo (propuesta: como los demás, una ventaja y un coste del mismo tamaño, a juego con el poder) y su energía necesaria (propuesta: 1 y ajustarla en C7 con el banco de poderes). Las propuestas de cada uno **se deciden al empezar ese poder** (2026-10-13).
  - Gancho: **para el futuro** (2026-10-14: se deja para más adelante; antes, el último de la Fase L). Falta cómo se suelta (propuesta: con un dash o al chocar contra una pared).
  - **Dos Embestidas que chocan** (nota para el futuro, 2026-10-14): qué pasa si las dos embisten a la vez y se golpean. Hoy cada una golpea a la otra (las dos se empujan y se dañan); propuesta: probarlo con mando y decidir si es lo que se quiere o si gana la más rápida.

**Modos y escenarios futuros (2026-10-12):**

- Carreras: circuito, vueltas y si hay choques y poderes (propuesta: circuito cerrado con puntos de control, choques y poderes activos; necesita arenas propias).
- Pilla pilla de fuego (el fuego no quita vida, decidido): si hay varias rondas (propuesta: quien pierde cada ronda queda eliminado y se juega hasta que quede uno). Un tiempo sin poder devolverlo al que te lo pegó (propuesta: 1 s).
- Virus (no quita vida y al acabar el tiempo ganan los no infectados, decidido): qué pasa si todos quedan infectados antes de tiempo (propuesta: la partida acaba antes y gana quien empezó con el virus). El fuego verde reutiliza el estado de quemadura con otro color.
- Coleccionables: cuántos, cada cuánto aparecen y si se activan por modo (propuesta: uno cada 10-15 s, como mucho 2 a la vez, activables en las reglas de partida).

**Efectos de UI y sonido:**

- Biblioteca de sonidos (un asset `SfxLibrary` en `Resources`, como `VfxLibrary`, con clip, volumen y variación de tono por evento).
- Cómo suenan los menús (un reproductor de UI único que no se destruye entre escenas, con el volumen de efectos de Opciones).
- Mezcla de audio (sin AudioMixer por ahora; volúmenes desde `SettingsService`).

**Sandbox e IA:**

- Parámetros exactos de cada nivel de IA (assets `AILevel`, valores en 6.4). Fácil y Normal casi no se distinguen en duelos cortos (propuesta: ajustarlos jugando con mando en los assets y medir con duelos más largos o repetidos).
- Multiplicadores y valores de los perfiles de IA (assets `AIProfile`, tabla en 6.4): provisionales (propuesta: ajustarlos jugando con mando, en Play desde el Inspector).
- Valores de los fallos humanos de la IA (en los assets de nivel, tabla en 6.4; propuesta: probarlos con mando y ajustarlos, sobre todo el retraso al ver y la deriva).
- **Modo desafíos y campaña** (idea del usuario, 2026-10-12): rivales con nombre, cada uno con su peonza, su nivel y su perfil de IA. Falta todo el diseño del modo: cómo se eligen los desafíos, si se desbloquean, qué se gana (propuesta: un asset por rival con nombre, peonza, nivel y perfil; diseñarlo cuando la IA esté terminada, después de D7).
- Combinar comportamientos de IA (D10): elegir por separado la colocación (centro, rodear, presión), la forma de atacar (carrerilla, contraataque, golpe y fuera) y la defensa (parrys, esquivas) (propuesta: hacerlo cuando la campaña lo pida, partiendo los comportamientos actuales en esas piezas).
- Valores del bloqueo (2.5; propuesta: dejarlos en la mitad y revisarlos jugando con mando y en C12; el banco de equilibrio no modela el parry).
- IA Fácil con el perfil de Ataque: gana a una IA Normal Balanceada en el probe, sobre todo por el preset (propuesta: probarla con mando; si es demasiado para un principiante, quitarle en Fácil el ×1,25 de ataque).
- Pérdida de control por empuje: `0,1 + 0,015 × empuje` s, desde 4 m/s, y recuperación en 0,15 s (propuesta: dejarla así, porque se siente bien con mando; ajustarla en C7 o C12 si algún poder empuja demasiado).
- Recarga de cargas: espera de 0,5 s tras atacar; se siente lenta, pero el usuario prefiere esta mecánica (propuesta: revisarla con el banco en C12).
- **IA contra Fuego, Hielo o Spin Boost del rival** (D11.3, 2026-10-15): rodearlo a 5 m para evitar el contacto casi no cambia nada contra una IA de Ataque que la persigue (quemada el 42% frente al 44%). Propuesta: probarlo con mando; si tampoco se nota, quitar el rodeo y dejar solo que ataque menos.
- **IA contra el Escudo del rival** (D11.3): atacar por los huecos solo mejora un poco (34% de ataques contra un escudo frente al 43%), porque los escudos giran a una vuelta por segundo y prever el choque falla por unos 0,05 s. Propuesta: dejarlo así; si con mando se ve torpe, que contra el Escudo ataque menos y lo rompa con empujones (no cuestan nada).

**Plataformas (7.5):**

- API gráfica de Windows (propuesta: dejar DirectX 11 primero y DirectX 12 de reserva).
- Juego a distancia con la ventana de parry de 0,12 s (propuesta: probarlo con Parsec y, si no se puede hacer parry, añadir a las reglas de partida una "ventana de parry amplia").
- Herramienta de red para la LAN de Android (propuesta: Netcode for GameObjects + Unity Transport; se decide al empezar, cuando la versión de PC esté completa).
- Opción Calidad en WebGL: dejar elegir PC o limitarla a Mobile (propuesta: dejarla y avisar de que puede ir lento, después de probarla).
- WebGPU (propuesta: no usarla hasta actualizar a Unity 6.6 o posterior y probarla).
- Dónde se publica la versión WebGL (propuesta: itch.io, que admite la compresión Brotli).

13. # Quehaceres

Lista de trabajo por fases, para ir añadiendo poco a poco. Se marca `[x]` al terminar. 🎮 = conviene probarlo con mandos reales. 🎧 = lo tiene que escuchar el usuario.

Orden propuesto: primero la base común de los poderes, después un sandbox básico (para poder probar cada poder según se hace), luego los poderes uno a uno y por último la IA completa.

**Fase A. Base común de los especiales**

- [x] A1. Efecto común al activar: +25% de RPM máximas y rellenar todas las cargas de ataque (`CombatConfig.specialActivationSpinPct` + `AttackSystem.RefillCharges`).
- [x] A2. Poderes como datos + comportamiento: un asset por poder (energía necesaria, duración, color, icono, nombre ES/EN) y una clase con su efecto. Migrados los 4 actuales (Spin Boost, Onda de choque, Storm Breaker y Dash eléctrico).
- [x] A3. Energía necesaria por poder (Spin Boost 1, Onda 1, Fuego 1, Hielo 1, Defensa 1,2, Fantasma 1,3, Rayos 0,8) y esfera del HUD según ese valor. Storm Breaker y Dash eléctrico ya usan la de Defensa (1,2) y Rayos (0,8). Fuego, Hielo y Fantasma la tendrán al crear su asset.
- [x] A4. Estados alterados en la peonza (quemadura, congelación, lanzada): uno a la vez, sustitución, triángulo de bloqueos (Fuego > Hielo > Rayos > Fuego), partículas que salen del panel del jugador, partículas sobre la peonza e icono encima de ella (`StatusEffectSystem`; los aplicarán los poderes con `TryBurn`, `TryFreeze` y `TryLaunch`).
- [x] A5. Fusión: Storm Breaker → Defensa, Dash eléctrico → Rayos, Rastro de fuego → Fuego. Actualizar enum, núcleos, textos (Defensa/Defense, Fuego/Fire, Hielo/Ice, Rayos/Lightning, Fantasma/Ghost) y auras. Hecho: enum con los 7 poderes (Fuego, Hielo y Fantasma sin asset hasta C2, C3 y C5), assets `Defense` y `Lightning`, núcleos `Core_Heavy_Defense` y `Core_Light_Lightning`, auras `AuraDefense` y `AuraLightning`. Defensa y Rayos conservan el efecto antiguo hasta C1 y C4.

**Fase B. Sandbox básico** (sustituye a Práctica)

- [x] B1. Escena `Sandbox` (copia de BattleArena con `SandboxController`); se entra con el modo Sandbox del lobby, que sustituye a Práctica.
- [x] B2. Botón propio del sandbox (Select/Back y Tab, reasignable en Controles) que pausa y abre el panel. Probado con teclado y con mando (2026-10-06).
- [x] B3. Panel navegable con mando y ratón (reutilizando los widgets pixel del menú). Probado con teclado y con mando (2026-10-06); falta con ratón.
- [x] B4. Rivales: ninguno o dummy, de 1 a 3, hasta 4 peonzas en total (la IA se añade al panel en D6).
- [x] B5. Comportamientos del dummy: quieto, moverse, atacar cada X s (practicar parry), dash hacia el jugador, usar especial. Si la acción no está disponible (cooldown del dash, sin cargas, poder activo) esperan a poder hacerla.
- [x] B6. Hasta 4 jugadores humanos, que se unen manteniendo ataque en la selección de peonzas. Probado con teclado; falta con varios mandos. 🎮

**Fase C. Poderes, uno a uno** (cada uno con su aura, sus textos y su prueba en el sandbox)

- [x] C1. Defensa: sin pérdida de RPM por golpes, paredes ni desgaste; empuje casi nulo; recarga de ataques x1,5; movimiento −15%; dash al 10% de coste y −40% de alcance. Hecho con modificadores nuevos en `SpecialAbility` (desgaste, recarga de ataques, coste del dash) y amortiguación del choque físico. Probado en el sandbox: desgaste 0, daño 0, empuje 14,7 → 1,5 m/s, recarga 1,5 → 1,0 s, dash 22 → 2,2 RPM y 22 → 13 m/s.
- [x] C2. Fuego: quemadura (1,5% cada 0,5 s durante 3 s) + llamas. Hecho: asset `Fire`, gancho `OnClashDamageDealt` en los poderes (lo usará también Hielo) y aura `AuraFire`. Probado en el sandbox: cada tic quita el 1,5% de las RPM máximas cada 0,5 s y un golpe nuevo reinicia los 3 s. Falta núcleo propio (C6): de momento solo se prueba forzando el poder.
- [x] C3. Hielo: congelación (−35% de movimiento, recarga a la mitad, 3 s) + cristales y humo blanco. Hecho: asset `Ice` (mismo gancho que Fuego) y aura `AuraIce`. Probado en el sandbox: el congelado recarga a la mitad (0,34 frente a 0,67 tras 1 s), un golpe nuevo reinicia los 3 s y una peonza en llamas no se congela. Falta núcleo propio (C6).
- [x] C4. Rayos: modo cargado hasta 6 s; choques pequeños empujan fuerte sin gastar; choque fuerte con +20% de daño, empuje x2,5 y lanzada 0,5 s, que gasta el poder; contra una peonza congelada, choque normal sin gastar; regla de daño de la peonza lanzada + destello del golpe. Hecho: bonus de choque en los poderes (`GetClashBonus`, aplicado en `CollisionResolver`), `EndSpecial` en el controller y asset `Lightning` con 6 s. La regla de daño de la lanzada ya estaba (A4). Probado en el sandbox: choque lento empuja 6,3 → 10,7 m/s sin gastar; contra congelado, choque igual que sin poder; golpe fuerte +20% de daño, rival a 40 m/s, lanzado 0,5 s y poder gastado.
- [x] C5. Fantasma: un clon con la IA de Agilidad (Normal) y las piezas básicas de la Agilidad, 10% de daño, empuja y corta cargas, sin parry; aguanta 3 ataques y desaparece en humo. **Hecho (2026-10-14):** dura hasta que lo rompan (o cae su dueña), reactivarlo lo trae de vuelta con los 3 golpes; no cuenta como jugador; se crea al equipar el núcleo (sin instanciar en combate); núcleo Phantom Core en el catálogo; aspecto pálido con humo (la transparencia, en K). Probado con un probe (ver 5). Falta probarlo con mando. 🎮
- [x] C6. Núcleos en el catálogo: uno por poder, con estadísticas. Hecho: 6 núcleos (Fantasma, con C5) con los valores de la tabla de 3, nuevos `Core_Medium_Fire` y `Core_Medium_Ice`, preset Ataque con Fuego y nombre del núcleo con el color de su poder en la selección. Los 4 presets siguen dando su arquetipo. Falta verlo en la selección con mandos. 🎮
- [ ] C7. Equilibrio de valores de cada poder en partida. 🎮 **Aplazado (2026-10-15):** se hará con los datos de la telemetría de C19, de partidas reales en local. Primera medida con el banco de poderes (2026-10-13, en 5): Fuego el más fuerte y Spin Boost y Onda de choque como sin poder (rehechos en F1). El especial llegaba tarde: ahora se carga también con el tiempo (5). Pendiente: convertir el probe en una herramienta (como el banco de equilibrio) para repetirlo tras cada cambio.
- [ ] C12. **Pendiente del equilibrio** (tras el ajuste a mano del 2026-10-09, que deja bien el daño de los golpes y la sensación de movimiento de cada arquetipo). **Se hace después de C11, C13 y C14** (registro del 2026-10-10). Sexto informe (`bench_2026-10-10_02-18-38.md`, con la Defensa bajada a mano: RPM 630 → 550 y defensa 46 → 25): tumbar a la Defensa cuesta 79-121 s (antes 125-193); la Defensa recibe 0,98 (objetivo 0,65, ver 12), el Ataque hace 1,39 / recibe 1,19 y su cargado 3 a tope sigue pasando de 60 (63 y 67). **Preparado para empezar (2026-10-10, sin tocar valores):** medir con la tabla *Duelos completos* del banco (C14, séptimo informe `bench_2026-10-10_23-50-29.md`). Puntos de partida: (1) el desgaste de las ligeras (Agilidad 3,2 y Ataque 2,5 RPM/s frente a 0,5; propuesta ~1,5 y ~1,2) es lo que más las hunde; (2) la Defensa gana todos sus duelos (decidir su objetivo de daño recibido, 12); (3) el cargado 3 del Ataque a tope; (4) la Agilidad hace 0,99 (objetivo 1,1); (5) la regla de piezas de §11 (cada pieza, la mejor en la estadística de su arquetipo). Ojo: la Balanceada sigue pesando 3,2 como la Defensa.
  - **Agilidad (2026-10-12, probada con mando contra la IA):** demasiado fuerte. El usuario le baja a mano las cargas de 5 a 3 (Gale Ring y Aero Shell de +1 a 0) y confía en su bonus de recarga para compensar; después, exportar las estadísticas y pasar el banco. **Hecho a mano (2026-10-12)** y estadísticas exportadas; falta pasar el banco.
  - **Energía al recibir daño (idea del usuario, 2026-10-12):** dar energía del especial al perder vida por los golpes recibidos, para que quien va perdiendo pueda remontar. Se decide aquí, con el resto del equilibrio de la energía (parry 12% / parado 6%, bloqueo).
  - **Ataque:** bajar un poco su ataque para que no sobresalga tanto (hace ×1,39 y recibe ×1,25 respecto a la Balanceada; objetivos ×1,25 / ×1,1). Un golpe suyo cargado 3 a tope aún pasa de 60.
  - **Vida (RPM) y defensa:** los duelos contra la Defensa duran 125-193 s (el resto 37-92 s): ajustar RPM y defensa de cada arquetipo para acercarlos. No bajar el daño de la Defensa (ver 2.7).
  - **Cargas de la Balanceada:** el Endurance Core quita una carga y la Balanceada se queda en 2 (sin cargado 3). **Decidido (2026-10-10):** se queda así, porque cura mucho; con el Frost Core (también de Balanceada) tiene sus 3.
  - **Poderes y costes:** energía, duración y efecto de cada poder (C7) y costes en RPM de ataque, dash y especial.
  - **Tests:** tres tests de `BalanceTargetsTests` están marcados *Pendiente C12* (daño que recibe el Ataque y la Defensa, y el tope de 60). Al ajustar, ejecutarlos para ver al momento si se llega al objetivo. Al terminar C12, quitar las marcas `Ignore` y comprobar que pasan.
- [x] C15. **Empujes efectivos** (pedido el 2026-10-11 tras jugar contra la IA: los empujes, sobre todo el de Rayos, se cancelaban al ir en dirección contraria). **Hecho (2026-10-11):** pérdida de control proporcional al empuje y lanzada sin control (2.8). Probado con un probe (empuje de 12 m/s con el stick en contra: 0,6 → 2,5 m). **Probado con mando por el usuario (2026-10-11)**, con Rayos, Defensa y Agilidad: «se siente mejor; al empujar, la peonza sale despedida de verdad».
- [x] C19. **Telemetría de las partidas** (pedido el 2026-10-15): que la build guarde un archivo por partida jugada en local (también con jugadores nuevos), con los datos que sirvan para el equilibrio, para que Claude los analice. **Hecho (2026-10-15, 6.6):** `MatchTelemetry` (un CSV por partida, también las abandonadas), opción *Guardar datos de partida* en Opciones y menú *FakeBlade → Informe de telemetría*. Probado en el editor con dos IA Experto (duelo completo y partida reiniciada a medias) y con una partida sin choques. Falta: comprobarlo en una build (que la carpeta `Telemetria` aparezca junto al ejecutable) 🎮.
- [x] C18. **Energía del parry y cámara** (pedido el 2026-10-12, tras jugar contra la IA, que para casi todo): parry 12% para quien para y 6% para a quien le paran (2.5); cámara de ángulo fijo con seguimiento suave (6). Probado con probes: energías como se esperaba y la cámara no gira nada (0°) en 15 s con 3 peonzas, 2,7 m/s de media. Falta probarlo con mando. 🎮
- [x] C17. **Bloqueo** (pedido el 2026-10-12): el parry contra un cargado de nivel 2 o más solo bloquea (mitad de daño y de empuje, mitad de energía, sin reflejar), 2.5. Segunda vuelta (2026-10-12, tras probarlo con mando): el bloqueado gana la energía de su golpe y queda aturdido un instante según su peso; probado con un probe (cargado 2: bloqueado +26,4% de energía y 0,17 s aturdido con peso 2,2; quien bloquea +6%). La IA que lo ve venir prefiere esquivarlo. Probado con un probe en el Sandbox (Ataque contra Defensa): cargado 2, 24,6 → 12,5; cargado 3, 30 → 15,1; rápido y cargado 1, parry completo. Tests: pasan los 31. Falta probarlo con mando. 🎮
- [x] C16. **Recarga de cargas solo sin atacar y parry sin devolver la carga** (pedido el 2026-10-11: la IA Experta parecía tener parry infinito). **Hecho (2026-10-11):** probado con un probe (tras un ataque rápido, la recarga empieza a los 0,85 s y la carga vuelve a los 2,36 s). **Probado con mando (2026-10-11):** la recarga se siente lenta y enseguida se queda uno sin ataques, pero el usuario prefiere esta mecánica; se deja así por ahora. Ojo: cambia el ritmo de los combates; el banco de equilibrio no lo modela, así que hay que tenerlo en cuenta en C12. Falta probarlo con mando. 🎮
- [x] C13. **Giros con más arrastre (inercia).** El movimiento se siente bien, pero los giros eran demasiado perfectos y cerrados: no se notaba física. **Hecho (2026-10-10):** estadística nueva **agarre** en las piezas (`gripModifier`; solo la modifican las puntas) que mueve el giro y el frenado al soltar (±10% por punto); el giro ya no depende del peso y baja a 8 correcciones por segundo con agarre 0 (antes 14-18), para que se note el derrape. En el HTML de estadísticas, en el exportador y en el importador, y con tests. Agarre puesto a mano por el usuario: Needle Point +3, Flat Base +1, Wide Ball −3 (Striker Point 0). **Probado con mando (2026-10-10):** el derrape se nota un poco y basta para seguir; el usuario lo irá ajustando. Si se queda corto, bajar `turnRate` (el giro con agarre 0) y subir el agarre de las puntas que deban girar cerrado. Pendiente: barra de agarre en la selección (12).
- [x] C11. **El peso contaba dos veces en el movimiento** (la aceleración y el frenado multiplicaban por una curva de peso y además se dividían entre la masa física). **Hecho (2026-10-10):** una estadística, un efecto (2.8): la aceleración se aplica igual para cualquier masa y el peso entra una sola vez, con curvas `(2,5 ÷ peso)^exponente` sin topes (antes una recta cortada entre peso 0,5 y 3); el peso ya no toca la velocidad máxima ni el giro. Valores ajustados para que los presets se muevan como antes (aceleración 42,8 / 29 / 17,5 / 17,5 m/s², antes 42,6 / 28,9 / 17,5 / 17,5). Tests de las fórmulas nuevas (peso de referencia, sin topes, agarre): pasan los 26. Probado con mando junto con C13 (2026-10-10).
- [x] C14. **Banco de equilibrio con costes, desgaste y curación.** Hoy la duración de los duelos es golpes hasta el K.O. × 4,2 s: no cuenta lo que cuestan los ataques (2% por carga) y los dashes (8%), el desgaste (RPM por segundo, que castiga mucho más a las ligeras: Agilidad 3,2/s, Defensa 0,5/s) ni la curación con el tiempo. Añadirlos para que la duración se acerque a la de una partida. Va antes de C12. **Hecho (2026-10-10):** `DuelEstimator` (modelo en 2.7) y tabla *Duelos completos* en el informe, con 5 tests (pasan los 31); el banco también tenía desfasados los objetivos de la Defensa (hace 0,75 → 1) y del espejo (60 s → 90-120 s). Séptimo informe (`bench_2026-10-10_23-50-29.md`): los duelos se acortan mucho (espejo de Balanceada 87 → 67 s, de Ataque 40 → 25 s, de Agilidad 52 → 34 s) y salen en orden Defensa > Balanceada > Ataque > Agilidad: la Defensa gana todos sus duelos y la Agilidad los pierde todos. Lo que más pesa en las ligeras es el desgaste (la Agilidad pierde un tercio de su vida en 34 s, 3,2 RPM/s) y que el modelo les da el mismo ritmo de golpes que a las lentas (la Agilidad debería golpear más a menudo, 2.7).
- [ ] C10. Banco de pruebas de equilibrio (método en el registro de decisiones): herramienta del sandbox o del editor que enfrenta cada preset contra cada preset con golpes de prueba fijos (parado y a velocidad máxima) y saca una tabla de daño dado, daño recibido y golpes hasta el K.O., para compararla con la tabla de objetivos (combate de 60 s en Balanceada contra Balanceada). Hecho: tabla de objetivos (2.7) y banco (`BalanceBench`, menú *FakeBlade → Banco de equilibrio*). Primer informe (2026-10-07, `Logs/Balance/bench_2026-10-07_13-09-31.md`): lo que reciben los arquetipos cumple; lo que hacen no (Ataque 1,5, Defensa 1,02 y Agilidad 1,88, frente a 1,25 / 0,75 / 1,1); 28 golpes pasan de 60 (todos a tope, sobre todo cargados de Agilidad y Ataque, hasta 135); espejo de Balanceada ≈ 86 s. Fórmula común decidida: golpe base + velocidad ±25% (2.5). Segundo informe (`bench_2026-10-07_13-24-37.md`): 11 golpes pasan de 60 (máx. 84, antes 135), Balanceada rápido 17 / 25 y cargado 3 27 / 44 (cumple); lo que hacen los arquetipos sigue alto (Ataque 1,45, Defensa 1,11, Agilidad 1,59) porque sale del ataque de sus piezas (Balanceada tiene el más bajo, 11); el dash ha quedado flojo (Balanceada 9-11) y el espejo de Balanceada sale en ~100 s. Ajustes (decididos con el usuario): dash ×0,5 → ×0,75; duelos más largos aceptados; cada preset con las piezas de su arquetipo (Ataque: Striker Point + Assault Frame + Razor Edge + Blaze Core; Agilidad: Gale Ring en vez de Razor Edge); golpe base 50 → 42; ataque de piezas: Balanced Ring 9, Striker Point 2, Assault Frame 2, Razor Edge 3, Crush Wheel 0, Gale Ring 6; defensa: Striker Point 0, Assault Frame +4, Razor Edge +4, Gale Ring 0, Needle Point −3. El banco además espera a que el ataque salga de verdad antes de medir (antes un rápido a tope a veces salía tarde). Cuarto informe (`bench_2026-10-07_14-00-58.md`): **todos los arquetipos cumplen** (hace / recibe: Ataque 1,28 / 1,1; Defensa 0,71 / 0,67; Agilidad 1,1 / 1,22), Balanceada rápido 17 / 23 y cargado 3 27 / 43, dash ≈ 75% del rápido, espejo de Balanceada ≈ 97 s; solo un golpe pasa de 60 (Ataque cargado 3 a tope contra Agilidad, 67). Falta: probarlo con mando y decidir si se recorta ese último pico. Quinto informe (`bench_2026-10-09_01-00-51.md`, con los valores y los primeros rasgos que puso el usuario a mano el 2026-10-09): golpes rápido 13-20 (parado / a tope), cargado 1-2 25-44, dash 10-16, como en sus partidas (media 18,8 en 498 golpes); hace / recibe: Ataque 1,39 / 1,25, Defensa 1,01 / 0,74, Agilidad 1 / 1,32 (objetivos 1,25 / 1,1, 0,75 / 0,65, 1,1 / 1,2); espejo de Balanceada ~92 s, pero contra la Defensa 125-193 s; dos golpes por encima de 60 (Ataque cargado 3 a tope). Ojo: la Balanceada pesa ya 3,2 como la Defensa (se mueve igual que ella) y tiene 2 cargas (sin cargado 3).
- [x] C8. Ajuste tras las pruebas del sandbox (2026-10-07): dash con menos daño, más caro y más espera; ataques con más daño; Defensa con menos ataque y dash más lento (rasgos); Agilidad con ataques baratos y rápidos (rasgos); especial más lento; curaciones con el tiempo y cortadas por golpes de ataque; paredes como un choque parejo; peonzas pegadas que se separan; estela del acelerón y vibración. Probado con un probe en el sandbox: daños por preset (tabla en 2.5), 2 separaciones en 2 s con dos peonzas empujándose, curación de 62% a 70% en 0,5 s, cortada por un ataque del dummy (se queda en 69%) y sin golpes llega al 100%. Probado con mando por el usuario (2026-10-07): el dash está bien equilibrado y ya no se abusa, la vibración al atacar se siente muy bien y la Agilidad ya no es débil.
- [x] C9. Daño más bajo y golpe de pared de Rayos. Primera prueba (bonus de masa del ataque a la mitad en el daño): con mando seguían saliendo golpes de 40-60. Ahora: la masa solo cuenta para el empuje, daño global ×0,5 y golpe con dash ×0,5; un solo golpe de pared por peonza cada 0,1 s (`wallHitCooldown`). Probado con un probe en el sandbox: daños de referencia en 2.5 (desde parado 6-27, en marcha 19-36; el Ataque pega más con el cargado que con el dash y solo la Agilidad saca más con el dash que con el rápido) y Rayos 39-90 con la pared (antes 115-152). El doble golpe en la unión de dos tramos no se ha podido reproducir en el probe (72 choques contra la pared sin ninguno), así que esa corrección solo se ha comprobado leyendo el código. Probado con mando el 2026-10-07: el daño está bien en general; el cargado se ajustó después (registro de decisiones, «Daño del cargado»).

**Fase D. IA**

- [x] D1. Base de la IA: percepción (rivales, paredes, ataques enemigos) y decisión por prioridades, sobre `IBladeInputSource` (partiendo de `SimpleAIBrain`). **Hecho (2026-10-11):** `AIBrain` (clase base para los perfiles de D3), `AIPerception` y `AILevel` (4 niveles en código, provisional hasta D2); diseño en 6.4. En el Sandbox: dummies en «IA» con «Nivel IA» (adelantado de D6). El banco de equilibrio deja quieto al defensor aunque los dummies estén en «IA». Probado con probes en el Sandbox (Arena básica). Defensa contra ataques rápidos de J1 cada 1,5 s: el Experto llegó a parar 9 de 13 tras guardar una carga y distancia (antes 0-2, porque gastaba todas las cargas y se pegaba al rival); de una ejecución a otra varía mucho (3-9). Duelos de 45 s con el preset Balanceado en las dos contra una IA Normal (daño hecho / recibido): Fácil 0,62, Normal 0,48 (espejo, debería salir ~1: ruido), Difícil 1,14, Experto 2,85 (16 parrys frente a 2). **Probado con mando por el usuario (2026-10-11):** se siente bien; la Experta es difícil, hace parry a todo y espera el momento justo para golpear.
  - Segunda vuelta (2026-10-11, tras probarla el usuario: «hay que atacar mucho y agotarla, sigue siendo muy buena»; «contra un rival quieto ataca poco, solo rápidos, y se queda corta»): parry no infalible, sigue empujando hacia el rival durante su ataque y capa táctica en Difícil y Experto (6.4). **Probado con mando (2026-10-11):** el Experto sigue siendo complicado, pero se le puede ganar (el usuario le ganó un set): es el desafío que se busca. La carrerilla no se notó (solo la hace contra un rival quieto); se ve una IA que guarda una distancia prudente, lista para atacar o hacer parry, y que persigue muy de cerca si el rival huye.
  - Para D2: Fácil y Normal no se distinguen en duelos cortos; el parry del Difícil (reacción 0,18 s) apenas llega, porque un ataque rápido tarda ~0,2 s en llegar; las medidas necesitan duelos más largos o varias repeticiones.
- [x] D2. Niveles de dificultad como asset de datos (reacción, puntería, parry, esquiva con dash, uso del especial). **Hecho (2026-10-12):** un asset por nivel (`AILevel`, los 5) y uno por perfil (`AIProfile`, los 4 arquetipos) en `Resources/AI`, creados con *FakeBlade → Setup AI*; el perfil elige su comportamiento (la clase de IA) y lleva sus valores (6.4, *Datos de la IA*). Se leen en vivo, para ajustarlos en Play jugando con mando. La fila «Perfil IA» del Sandbox lista los perfiles que haya. Comprobado: los assets salen con los valores de la tabla anterior y cada perfil con su clase. Solo se han pasado los datos: el ajuste (Fácil frente a Normal, fallos humanos) viene después de probar con mando. 🎮
- [x] D3. Perfiles por arquetipo: Agilidad agresiva y con parrys; Ataque con presión y cargados; Defensa que aguanta, contraataca y estampa contra paredes; Balanceada mixta. Ideas del usuario (2026-10-11, tras probar D1: «se siente bien; la Experta hace parry a todo y espera el momento justo para golpear»): comportamientos más variados, como rodear al jugador o dominar el centro de la arena. La carrerilla de la capa táctica (hoy solo contra un rival quieto, y no se nota jugando) se revisa aquí: que la usen más los perfiles a los que les pegue, como el de Ataque. **Hecho (2026-10-12):** `AttackAIBrain`, `DefenseAIBrain` y `AgilityAIBrain` heredan de `AIBrain` (la Balanceada) y `AIProfiles` elige la clase; el perfil sale de la peonza o se elige en el Sandbox. Ataque: presión, cargados y carrerilla siempre que tenga sitio. Defensa: domina el centro y contraataca o empotra. Agilidad: rodea, entra y sale con dash y busca parrys. Cada perfil multiplica el parry, la esquiva y la reacción del nivel (tabla y medidas en 6.4). Probado con probes en el Sandbox en Difícil, Normal y Fácil contra una IA Normal: cada perfil sale con su clase y se distingue en las medidas (la Agilidad hace 8 dashes, frente a 0-1; el Ataque, 7 carrerillas, frente a 1-2; la Defensa sujeta el centro la mitad del tiempo). **Probado con mando (2026-10-12):** los perfiles se notan.
  - Segunda vuelta (2026-10-12, tras probarla el usuario: «lee demasiado bien mis movimientos; se nota que es todo demasiado calculado»): fallos humanos por nivel (ver con retraso, predicción con error, despistes, pulso imperfecto y pasarse al rodear o al llegar), tabla en 6.4. Probado con probes: los fallos salen más en Fácil que en Experto y el parry no se rompe. Falta probarlo con mando: que se le pueda engañar con fintas sin que parezca tonta. 🎮
- [x] D9. **Nivel Imposible** (pedido el 2026-10-12): Experto mejorado y sin fallos humanos (6.4); en el Sandbox, quinta opción de «Nivel IA». Probado con un probe (0 despistes, 8 parrys y daño 135 / 67 contra una IA Normal en 40 s). Falta probarlo con mando. 🎮
- [x] D4. Parry de la IA: lanzar un ataque rápido justo cuando el rival ataca.
- [x] D5. Uso de los especiales por la IA (cada poder en su buen momento).
- [x] D6. IA en el panel del sandbox: 1-3 rivales, nivel y arquetipo. *Hecho en D1: comportamiento «IA» y nivel (para todos los dummies a la vez).* **Completado con D3 (2026-10-12):** filas «Perfil IA» (según peonza o uno de los cuatro) y «Su peonza» (preset de los dummies), también para todos a la vez. Comprobado con una captura del panel; falta con mando. 🎮
- [x] D7. Rivales CPU en partidas normales (hasta completar 4 jugadores). **Hecho (2026-10-12):** en la selección de peonzas, Select añade una IA y LB/RB pasa a su columna para elegir sus piezas, color, equipo y nivel; siempre está lista y juega con el perfil del arquetipo de su peonza (9.2.1). Probado con un probe: añadir dos IA, cambiar de columna, nivel, quitar una, ajustes y partida con IA 1 (Experto) e IA 2 (Fácil) con su perfil, el HUD con «IA». **Probado con mando por el usuario (2026-10-13): funciona bien.** Falta señalarlo mejor en la interfaz (ver «Otros pendientes»).
- [ ] D11. **Comportamiento según el especial** (idea del usuario, 2026-10-12): que la IA juegue de una forma u otra según el poder de su núcleo, antes y durante su uso. Por ejemplo, ser agresiva y usar el poder que recarga todas las cargas para seguir atacando; con poca vida, jugar seguro y retirarse para recuperar vida con el especial; con un especial pasivo, una actitud lenta y defensiva para sacarle partido. **Se hace cuando estén todos los especiales planteados** (Fase L y F1). Ampliada el 2026-10-14: también **leer el especial del rival**. Por partes (6.4, *IA y especiales*):
  - [x] D11.1. Base (qué lee cada nivel, esquiva de poderes), apuntar la carga del Salto y la Embestida, esquivar la Embestida y el Salto, Tornado y Onda de choque (propios y del rival), Defensa, Pinchos y Rebote (propios y del rival) y no reactivar el Escudo con escudos puestos (2026-10-14).
  - [ ] D11.2. Probar la primera parte con mando 🎮: esquivas de la Embestida y del Salto, la IA saltando y embistiendo (¿demasiado certera o demasiado floja?) y la IA con Defensa, Pinchos y Rebote. Ajustar en los assets de nivel y perfil. **Primera prueba (2026-10-15):** esquiva bien la Embestida y el Salto; Salto más rápido (gravedad ×2), Escudo a 0,8 de energía y fila «Su núcleo» en el Sandbox para probar la IA con cada poder (11). Falta: la IA saltando y embistiendo, y la IA con Defensa, Pinchos y Rebote. **Segunda prueba (2026-10-15):** la IA salta y embiste con eficacia (se queda así); Salto exagerado (1,9 m, 0,54 s) y Escudo a 1,2. Falta: la IA con Defensa, Pinchos y Rebote.
  - [x] D11.3. Segunda parte: Rastro de agua (rodear al rival dejando agua y pelear sobre ella; esquivar los charcos del rival; mojada, no atacar ni hacer dash), Rayos (carrerilla para pasar de 6 m/s, mejor con el rival junto a la pared), Fantasma (volver a sacarlo al romperse, atacar mientras el rival se entretiene; preferir la peonza de verdad al clon), Escudo del rival (atacar por los huecos, desde Difícil), Fuego, Hielo y Spin Boost (más agresiva con el suyo; evitar el contacto con el del rival) y leer el poder de un rival que no sea el más cercano (hoy solo lee el del más cercano). **Hecho (2026-10-15):** todo lo de la lista (6.4, tablas y medidas); el Escudo del rival mejora poco (rompe un escudo en el 34% de sus ataques frente al 43%) y evitar el Fuego del rival casi no cambia nada contra una IA que la persigue. Falta probarlo con mando 🎮, sobre todo la lectura del poder de otro rival (en el Sandbox los dummies son del mismo equipo: hace falta una partida de 3-4).
- [ ] D8. Menú de fondo con la IA nueva en vez de la actual.
- [ ] D10. **Combinar comportamientos** (para la campaña, 2026-10-12): partir los comportamientos de la IA en piezas que se eligen por separado en el perfil (colocación, forma de atacar, defensa), para crear personajes que mezclen estilos (por ejemplo, la presión del Ataque con los parrys de la Agilidad). Se hace cuando la campaña lo pida (12).

**Fase E. Sandbox completo**

- [x] E1. Cambiar núcleo (especial) y piezas en caliente. Hecho en Mi peonza (selector de jugador). Probado: de Blaze a Endurance Core el poder pasa de Fuego a Spin Boost, el disco Razor Edge cambia el modelo y las RPM máximas, y el HUD actualiza cargas y color de la esfera. Probado también con mando por el usuario (2026-10-06).
- [x] E2. Trucos: RPM infinitas, especial siempre lleno, cargas infinitas, dash sin cooldown, invulnerable. Cada uno para nadie, jugadores, dummies o todos. Probado: RPM llenas tras un golpe, especial lleno, dos dashes seguidos, cargas al máximo y dummy invulnerable (0 de daño). Probado con mando.
- [x] E3. Reiniciar posiciones, RPM, cargas y energía. Reiniciar todo: además quita los estados alterados y cierra el panel. Probado. Probado con mando.
- [x] E4. Debug visual: ventana de parry, vectores de velocidad, números de daño, estados y FPS. Sección Debug (`SandboxDebugView`). Probado (capturas).
- [x] E5. Registro de eventos en pantalla (choques, parrys, especiales). También estados y K.O.; cada choque en una línea. Probado.
- [x] E6. Cámara lenta (x0,25 / x0,5) y avance frame a frame. Velocidad en el panel principal; PAUSA + «.» / clic del stick derecho. Probado (el avance con teclas reales, pendiente). 🎮
- [x] E7. Cambiar de arena y retocar valores clave de `CombatConfig` sin salir. Sección Ajustes; valores temporales sobre una copia (el asset no se toca). Probado: arena con recarga, ventana de parry x2 y vuelta a los valores originales al salir.

**Fase H. Aspecto de las peonzas** (modelos por pieza y colores)

- [x] H1. Colores por pieza: paleta por color del jugador (disco del color elegido, cuerpo blanco, punta negra, núcleo en tono profundo), pintado por nombre de pieza (`BladePaint`) en partida, selección y fondo del menú, y brillo del núcleo con el color de su poder según la carga. Probado con un núcleo provisional: el brillo sube con la carga y late con el poder activo.
- [x] H2. Opciones → Colores de peonza: lista de 24 colores retro por pieza, vista previa 3D, restaurar y guardado. Probado con teclado; falta con mando. 🎮
- [x] H5. Selección de peonzas: arquetipo de cada pieza bajo su nombre, barras de stats por tramos con parpadeo de la pieza seleccionada y vista previa centrada y más de frente. Probado con 4 jugadores (captura); falta con mandos. 🎮
- [x] H3. Modelo genérico del núcleo en la peonza: todos los núcleos usan `Nucleo_Generico` con `BladeCoreMaterial` (lo pone `BladeModel` desde los ajustes del prefab).
- [ ] H8. Rasgos de las piezas (ver 3): la base está hecha (`PartTrait`, lista *Rasgos* en cada pieza, aplicados en energía del especial, dash, recarga, costes y carga). Primeros rasgos puestos el 2026-10-07 (dash de las pesadas, ataques de las ágiles; tabla en 3). Hecho el 2026-10-08: 16 tipos nuevos (26 en total, tabla en 3), aplicados en el combate y probados uno a uno con un probe en el sandbox (17 comprobaciones bien: cada rasgo cambia su valor en lo esperado); nombres ES/EN y texto para la UI; el menú de piezas ya no sobrescribe las que existen. Falta: decidir qué rasgos lleva cada pieza (el usuario los reparte a mano), mostrarlos en la selección (bajo el arquetipo, o al tener la pieza seleccionada), pasar el banco de equilibrio con los rasgos puestos y, más adelante, los rasgos con condición. 🎮
- [ ] H9. Una pieza por arquetipo en punta, cuerpo y anilla (4 de cada, con su modelo A-D). Faltaban los ScriptableObject (el script de piezas solo creaba una por clase de peso): creadas **Striker Point** (punta de Ataque, `Punta_Type_A`), **Assault Frame** (cuerpo de Ataque, `Body_Type_A`) y **Gale Ring** (anilla de Agilidad, `Ring_Type_D`) en `FakeBladeComponentPresets` y en el catálogo, y salen en todos los menús de selección (comprobado con mando el 2026-10-07). Falta: fijar sus estadísticas y decidir los presets en el pase de equilibrio.
- [ ] H7. Selección de piezas: al tener el foco en una fila de pieza (punta, cuerpo, disco o núcleo), la vista previa mueve la cámara o el modelo para enseñar esa pieza (por ejemplo, de lado para la punta, desde arriba para el núcleo). Mientras se enseña una pieza, **la peonza se queda quieta** (sin girar), para que el jugador vea bien cada cambio; girando no se aprecia. Vuelve a girar al salir de las filas de piezas.
- [ ] H10. **Collider según el disco** (decidido el 2026-10-13): hoy todas las peonzas tienen una esfera fija de 0,6 m y las anillas más anchas sobresalen; que el radio dependa del disco montado (un valor por pieza de disco o medido de su modelo). Revisar después el banco de equilibrio, porque cambia el alcance de los choques.
- [x] H6. Imagen del núcleo (plano `Imagen de Nucleo` con el icono pixel del poder, teñido con su color). **Aplicada para probarla (2026-10-06)**: `CoreImage`, creada por `BladeModel` con el núcleo; va en el pivote de inclinación (se inclina con la peonza pero no gira) y se orienta a la cámara cada frame. Dos modos en el prefab (`FakeBladeController` → Model Settings → Core Image Mode): **calcomanía** (tumbada sobre el núcleo, por defecto) y **cartel** (de pie, de frente a la cámara). La orientación de la textura sale de las UV de la malla (el FBX del plano necesita Read/Write activado, ya puesto). Iconos en `PowerIcons` (Fuego, Hielo y Rayos usan los de su estado; flecha, ondas, escudo y fantasma para el resto; el icono del asset del poder tiene prioridad). **Se queda como opción**: Opciones → Juego → Icono del núcleo, desactivada por defecto. Falta verla con mandos y a pantalla completa. 🎮
- [x] H4a. Script de Blender para exportar cada pieza a su FBX (`Tools/Blender/export_blade_parts.py`). Probado con Blender 5.0: en Unity llegan con rotación 0, escala 1, Y arriba y montadas en su sitio (la punta, con escala negativa en Blender, sin caras invertidas). No exporta los tipos 0 (modelos base). Exportadas al proyecto el 2026-10-06: 15 piezas en `Assets/3D Models/Bayblade 01/Parts/`.
- Nota sobre las UV de las piezas: no hace falta arreglarlas mientras se pinten con color plano (`_BaseColor`) y sin texturas ni lightmaps. Solo importan en las piezas que lleven textura (como el plano de la imagen del núcleo, que ya las tiene bien).
- [x] H4b. Modelos de las piezas en las peonzas: cada pieza del catálogo apunta a su modelo (el del tipo de su arquetipo) y la peonza se monta con las piezas equipadas, en partida, en la selección, en Opciones y en el fondo del menú; también al cambiar una pieza en caliente. Probado en el sandbox y en la selección con 4 jugadores: escala correcta (los modelos ya están a la escala del juego), apoyadas en el suelo, modelo antiguo oculto y colores por pieza. El collider (esfera de 0,6 m de radio) coincide con el borde de las anillas; solo sobresalen un poco las puntas de algunos anillos (hasta 0,13 m el A). 🎮

**Fase I. Arenas**

- [ ] I1. Arena plana (`Arena V2 Plana`): exportada con `Tools/Blender/export_arena.py`, montada por el setup (losa de suelo, pared con MeshCollider, materiales de la arena) y puesta como arena por defecto. Falta probarla en partida: rebotes en la pared, aparición, fondo del menú y que la lanzada de Rayos llegue ahora a la pared (en el cuenco casi nunca llegaba: revisar su daño de pared, ver C9). 🎮
- [ ] I3. **Arena básica** (`Arena_Basic_2`, modelo del usuario en dos FBX). **Integrada (2026-10-11):** el montador admite varios FBX por arena y detecta el suelo plano por raycasts; menú nuevo *FakeBlade → Setup Arenas*; prefab, `ArenaData`, nombre en `Loc` (Arena básica / Basic arena) y primera del catálogo. Comprobado con raycasts en el prefab: suelo a 0 en todo el radio, pared a 7,00 m en 8 direcciones y 3 alturas, 2,5 m de alto; carga en Play en el Sandbox sin errores. Falta probarla jugando: rebotes en la pared, aparición, fondo del menú y la lanzada de Rayos contra la pared (C9). 🎮
- [ ] I2. **Cuencos en arenas futuras.** Dos caminos, a decidir al hacer la primera:
  - **Cuenco real (geometría):** la pendiente empuja hacia el centro y provoca más choques. Necesita un perfil curvo continuo hasta la pared (sin pliegues donde se juntan suelo, rampa y pared) y es más difícil de equilibrar.
  - **Cuenco simulado (propuesta):** suelo físico plano y una fuerza hacia el centro según la distancia, con su intensidad en el asset de la arena. El cuenco se modela solo como decoración. Así la física sigue siendo estable y cada arena puede tener un cuenco más o menos agresivo sin remodelar nada.

- [ ] I4. **Arenas con obstáculos** (idea del usuario, 2026-10-12): rampas, obstáculos diversos y elementos con los que interactúan las peonzas (rebotadores, barras que giran, paredes que se mueven, trampolines...). Ver 4.
- [ ] I5. **Coleccionables** en el escenario (idea del usuario, 2026-10-12): pequeñas curas, escudos temporales, vida temporal, recarga del especial... Aparecen por la arena, con pool y activables en las reglas de partida. Ver 4 y 12.

**Fase L. Poderes nuevos** (ideas del usuario, 2026-10-12; tabla en 5, dudas en 12). Cada uno con su asset, su núcleo, su aura y su prueba en el Sandbox.

- [x] L1. Tornado: atrae a las peonzas de un área y después las expulsa con un empujón de área. **Hecho (2026-10-13):** atrae 1,5 s a 6 m (no se escapa, solo se frena), inamovible mientras atrae, el empujón se carga mientras atrae y se puede soltar antes (más flojo), deja lanzadas; núcleo Cyclone Core (Balanceada, valores provisionales) en el catálogo; aura de espiral e icono propio. Probado con un probe (ver 5) y una primera vez con mando: ajustado (más rápido, más fuerte e inamovible). Falta volver a probarlo con mando. 🎮
- [x] L2. Rebote: quien choca con ella sale empujado en dirección contraria y ella no pierde vida en esos choques. **Hecho (2026-10-13):** cualquier embestida (desde 1 m/s), empuje entero ×1,8 y daño normal para el que embiste; núcleo Rebound Core (Defensa, valores provisionales) en el catálogo; aura de anillo que late e icono propio. Probado con un probe (ver 5). Falta probarlo con mando. 🎮
- [x] L3. Pinchos: pinchos que giran a su alrededor, algo más de daño y reflejo del daño recibido (para la Defensa). **Hecho (2026-10-13):** refleja el 70% de los ataques (como Espinas), recibe −20% y hace +10%; núcleo Spike Core (Defensa, valores provisionales) en el catálogo; aura de puntas que giran e icono propio. Probado con un probe (ver 5). Falta probarlo con mando. 🎮
- [x] L4. Rastro de agua: rastro de unos segundos que hace resbalar a quien lo pisa. **Hecho (2026-10-14):** 4 s dejando agua que se seca a los 3 s; quien la pisa queda Mojada 2 s (estado aparte, casi sin agarre; apaga la quemadura); la dueña patina a favor (+20% sobre su agua); núcleo Tide Core (Agilidad, valores provisionales) en el catálogo; charcos, gotas e icono de gota. Probado con un probe (ver 5). Falta probarlo con mando. 🎮
- [x] L5. Salto: salto hacia delante con onda de choque al caer (daño de área y empuje fuerte en un área pequeña). **Hecho (2026-10-13):** salto bajo de 5 m hacia el stick; en el aire empuja sin daño y no admite parry ni bloqueo; al caer, 10% de daño, 22 m/s de empuje y lanzadas en 3 m; núcleo Meteor Core (Ataque, valores provisionales) en el catálogo; rastro e icono propios. Probado con un probe (ver 5). Falta probarlo con mando. 🎮
- [x] L6. Escudo: 3 escudos que giran alrededor, paran y expulsan los ataques y se rompen al recibir un choque. **Hecho (2026-10-13):** escudos de 60º a 1 vuelta/s (cubren la mitad), los rompe cualquier embestida, el que choca sale despedido ×1,8 sin daño y ella no pierde RPM; núcleo Aegis Core (Balanceada, valores provisionales) en el catálogo; aura de arcos en su ángulo real e icono propio. Probado con un probe (ver 5). **Probado con mando (2026-10-13): bien.** Después: duración indefinida (los escudos se quedan hasta romperse, reactivarlo los rellena) y energía 0,6 (5).
- [ ] L7. Gancho: engancha a la peonza con la que choca y la arrastra. **Para el futuro** (2026-10-14); si resulta contraproducente, se descarta.
- [x] L8. **Embestida** (idea del usuario, 2026-10-14): tres embestidas con mucho empuje, área de golpe amplia y sin parry. **Hecho (2026-10-14):** dos embestidas (eran tres) con una carga de 0,3 s cada una; 13 m/s, golpe a 1,8 m, 24 m/s de empuje, 6% y lanzada; 6 s para usarlas; núcleo Rhino Core (Ataque, valores provisionales) en el catálogo; aura e icono propios. Probado con un probe (ver 5). Falta probarlo con mando. 🎮

**Fase M. Modos de juego nuevos** (ideas del usuario, 2026-10-12; en 6.1, dudas en 12)

- [ ] M1. Pilla pilla de fuego: quien tiene el fuego se lo pega a otro al tocarlo; al acabar el tiempo pierde quien lo tenga.
- [ ] M2. Virus: como el pilla pilla, pero el fuego (verde) se contagia y quien lo pega lo conserva.
- [ ] M3. Carreras: las peonzas compiten en un circuito (necesita arenas propias).
- [ ] M4. Desafíos: rivales CPU con nombre, peonza, nivel y perfil de IA (6.4; después de D7). Más adelante, campaña (D10).

**Fase F. Más adelante**

- [x] F1. Rehacer Spin Boost y Onda de choque. **Hecho (2026-10-13):** Spin Boost es un «Acelerón» (velocidad, aceleración, recarga y sin desgaste, más una cura pequeña) y Onda de choque son 3 ondas que lanza el jugador, con lanzada contra la pared (5). La IA vuelve a pulsar para las ondas con el rival a tiro. Probado con un probe: las 3 ondas, la cuarta pulsación no hace nada y el poder termina con la tercera; Spin Boost sube la velocidad máxima de 7,2 a 9,4 m/s. Banco de poderes (10 duelos): daño hecho por activación +16 con Spin Boost y +52 con Onda (Fuego, +41). Falta probarlo con mando. 🎮
- [ ] F2. Escalar a 8 jugadores: que el máximo sea un único valor configurable (hoy está repetido en `LobbyController.MaxPlayers`, `GameManager.maxPlayers`, `FakeBladeController.ClashMemory` y `MenuArenaBackground`), HUD para más de 4 paneles, paleta de 8 colores o más, puntos de aparición, columnas del lobby y equipos.

**Fase G. Assets de la Asset Store** (importar y analizar; reglas y licencias en 7.4)

Proceso con cada paquete: descargarlo en *Package Manager → My Assets* (con la cuenta de Unity del usuario) → revisar qué trae antes de importarlo → importar solo lo necesario → añadir su carpeta al `.gitignore` si no está en `Assets/ThirdParty/` → probar en PC y WebGL → decidir si se queda.

- [x] G1. `.gitignore` preparado para los assets de terceros (`Assets/ThirdParty/`).
- [ ] G2. **All In 1 Sprite Shader** (UI). *Importado (sin demos, sin carpetas `Resources`).* Diseño y reglas en **9.3**. No sirve para textos TMP ni para el clon de Fantasma (C5).
  - [ ] G2.1. Base: carpeta `Assets/Materials/UI/`, campos de material en `HUDTheme`, ayuda para crear la instancia de cada panel y animar valores por pasos sin GC, y vuelta al aspecto actual si falta el shader.
  - [ ] G2.2. Esfera del especial: glow + shine al llenarse; hue shift y más glow con el poder activo.
  - [ ] G2.3. Panel: destello blanco y sacudida al recibir un golpe. La sacudida tiene **su propia opción** en Opciones, aparte de «Sacudida de cámara» (decidido el 2026-10-13).
  - [ ] G2.4. Iconos de estado sobre la peonza: outline y parpadeo (después de A4).
  - [ ] G2.5. Lobby: gris en las columnas libres; outline o shine en la opción seleccionada y al estar listo.
  - [ ] G2.6. Transiciones de menú y de escena: pixelado creciente + fundido.
  - [ ] G2.7. Pruebas: 4 paneles a la vez, cada uno con sus efectos; 0 KB de GC por frame en el Profiler; aspecto pixel (sin bordes suaves); build de PC y de WebGL; el proyecto compila y se ve bien sin el paquete. Decidir si se queda.
- [ ] G3. **Editor Console Pro:** importar y usar en el editor.
- [ ] G4. **Sonido.** Tabla de eventos y organización en **9.4**.
  - [ ] G4.1. Escuchar los 50 sonidos de FREE Casual Game SFX (ordenados por duración en 9.4) y apuntar en la tabla cuál va a cada evento de menús y flujo. 🎧
  - [ ] G4.2. Copiar los elegidos, renombrados, a `Assets/Audio/SFX/`.
  - [ ] G4.3. Biblioteca de sonidos y reproductor de UI (ver "Pendiente de definir").
  - [ ] G4.4. Conectar los sonidos de menús, lobby, cuenta atrás, pausa y victoria.
  - [ ] G4.5. Asignar los de combate a los campos que ya existen en `FakeBladeController` (choque, ataque, dash, especial, parry, K.O.) y añadir los que faltan (pared, niveles de carga, estados).
  - [ ] G4.6. Descargar y revisar los demás packs: RPG Essentials SFX (Leohpaz) para UI y golpes; Fantasy Sounds Bundle (Cafofo) para los poderes; THOR Thunderstorm (solo los truenos, para Rayos); Monster Sounds & Atmospheres para Fantasma.
  - FREE Casual Game SFX (Dustyroom): *importado entero* (50 sonidos, CC0).
  - Human Vocal Sounds (Cafofo): *revisado, no se importa.* Solo trae voces sueltas de hombre y mujer (gritos, risas, esfuerzos, quejidos) y sonidos sueltos (huesos, comer, latidos). No tiene público ni ánimos, y el juego no tiene personajes.
- [ ] G5. Buscar lo que no cubren los packs: choque metálico de peonzas, zumbido de giro en bucle (tono según las RPM) y música de menú y batalla. Probar también los generadores de sonido y música de Coplay.
- [ ] G6. **POLYGON Prototype Pack (Synty)** para la escena Sandbox (B1).
- [ ] G7. **Decoración low poly** (Low Poly Ultimate Pack, Low Poly Environment de Polytope, Low-Poly Simple Nature): entorno de la arena y fondo del menú. Comprobar cómo quedan pixelados. *Aparcado por ahora:* el Low Poly Ultimate Pack queda como candidato para los escenarios y sus distintos tipos cuando haya más de una arena (sección 4).
- [ ] G8. **UModeler:** probarlo para modelar arenas, hazards y variantes de piezas.
- [ ] G9. **Odin Inspector:** decidir si se usa. Solo en scripts de editor, nunca en el código de runtime (repo público y límite de 200.000 $). Alternativa gratis para atributos de inspector: NaughtyAttributes (MIT).
- [ ] G10. Más adelante, si hacen falta: Dreamteck Splines (hazards con recorrido, cámara del menú), Bitgem Stylized Water o Simple Water Shader URP (arena de agua o hielo) y Fantasy Skybox FREE (si alguna cámara ve el cielo).
- [ ] G11. **Efectos de terceros: Cartoon FX Remaster Free (Jean Moreno) y VFX Impact and Hit Light (Wallcoeur).** Importados (2026-10-06) sin las demos: 65 efectos de Cartoon FX en `Assets/JMO Assets/` (ignorado) y 18 del pack de impactos en `Assets/ThirdParty/VFX/`. Probados 48 en la arena junto a los nuestros:
  - **Muy interesantes:** los **textos de cómic** de Cartoon FX (BOOM!, POW, WHAM!, WOW!, FROZEN, BOING): se leen perfectos sobre el suelo claro y encajan con el tono arcade de la serie. Usos posibles: WHAM! en el golpe fuerte de Rayos, BOOM! en el K.O., WOW! en el parry, FROZEN al congelar, POW en el ataque cargado al máximo y BOING en el rebote de una peonza lanzada contra la pared. Y los **impactos de Wallcoeur con contorno oscuro** (sobre todo Classic_03), que se leen muy bien y duran ~0,4 s, ideales para choques frecuentes. Arcade_01 ya es pixelado.
  - **Interesantes con ajustes:** los golpes de elemento de Cartoon FX (Hit Fire, Hit Ice, Hit Electric), Explosion 1 y WW Explosion (K.O.), Magic Poof (reaparecer) y Ground Hit (Onda de choque). Sus colores HDR y sus luces, con nuestro bloom y el suelo claro, **queman media pantalla**: hay que bajar su multiplicador HDR, quitar las luces y reducir su tamaño (son más grandes que una peonza). Los impactos aditivos beige de Wallcoeur se pierden sobre el suelo claro (el mismo problema que tuvieron los nuestros).
  - **Poco útiles:** brillos y bucles (LightGlow, Ambient Glows, Sun, Bouncing Glows), Cartoon Fight, Sparks Rain, Poison, Souls Escape (casi invisible sobre el suelo claro), estelas de espada, naturaleza y líquidos.
  - **Conclusión:** los nuestros son pequeños y discretos, buenos para lo continuo (estela, auras, estados); los de terceros sirven para los **momentos grandes** (golpe fuerte, K.O., parry, estados) si se ajusta su brillo.
  Análisis previo de su contenido (antes de importarlos):
  - **Licencia:** EULA estándar de la Asset Store en los dos (gratis no es lo mismo que licencia libre): se pueden usar en el juego, pero **no se suben al repo**.
  - **Dónde van:** Cartoon FX trae shaders propios (`.cfxrshader` con un importador que compila la versión de URP) y scripts (`CFXR_Effect`, `CFXR_ParticleText`), así que se queda en su carpeta original `Assets/JMO Assets/` (a añadir al `.gitignore`). El pack de impactos es solo arte (prefabs, materiales y texturas) y va a `Assets/ThirdParty/VFX/`.
  - **Qué no importar:** escenas y carpetas de demo (`CFXRF Demo.unity`, `Demo Assets` con Kino Bloom, `VFXPlayerScene.unity`), la pantalla de bienvenida de JMO, el paquete de efectos antiguos de Cartoon FX y los dos scripts de demo del pack de impactos (ningún prefab los usa). Del resto, solo los efectos elegidos.
  - **Compatibilidad con URP:** Cartoon FX es compatible. Necesita la *Depth Texture* para sus partículas suaves: está activa en el asset de PC, pero **no en `Mobile_RPAsset`**, donde sus efectos serían invisibles (activarla o desactivar las partículas suaves en `CFXR_SETTINGS.cginc`). El pack de impactos usa shaders de partículas antiguos de Unity sin iluminación, que URP suele dibujar; a comprobar al importar. El proyecto ya está en espacio de color lineal, como piden.
  - **Integración en el código:** son prefabs con varios sistemas de partículas, scripts y luces, así que no encajan en el pool actual (un sistema por efecto con `Emit()`). Hace falta un **pool de instancias de prefab** (unas pocas copias precreadas por efecto que se reactivan; sin `Instantiate` en combate). El código del juego no puede usar sus clases: los prefabs se asignan en `VfxLibrary` como efecto opcional y, si faltan (por ejemplo en un clon del repo), se usa nuestro efecto pixel.
  - **Ajustes de cada efecto:** sin la sacudida de cámara de `CFXR_Effect` (ya tenemos la nuestra), sin luces (coste en WebGL), al terminar se desactiva en vez de destruirse (para el pool) y el multiplicador HDR ajustado a nuestro bloom. Como sus `.meta` no se suben, estos ajustes los aplicará un menú de editor nuestro (genérico, sin depender de sus clases) cada vez que alguien importe los paquetes.
  - **Estilo:** texturas dibujadas a mano en alta resolución y con filtro suave, distinto de nuestros sprites pixel. Se puede igualar con filtro Point en sus texturas o con el pixelado de la escena (Otros pendientes). El pack de impactos trae materiales *Pixel*, a probar primero.
  - **Siguiente paso:** importar, probar cada candidato en la arena con capturas, compararlos con nuestros efectos y decidir cuáles se usan.

Descartados en el análisis (se pueden revisar): NodeCanvas (la IA de la fase D se hace en C#), White Mage Spells (efectos realistas y aditivos, en contra de 7.3), GUI Pro Fantasy RPG (estilo pintado) y el resto de la lista (entornos realistas, HDRP, personajes, plantillas de otros géneros y assets deprecated).

**Fase J. Plataformas y builds** (detalles en 7.5)

- [x] J1. Primera build de Windows (DirectX 11, Mono): partida completa, opciones, controles guardados y 4 mandos. 🎮 **Hecha y probada (2026-10-15):** build de 104 MB en `Builds/Windows/` (3 min, sin errores); probada por el usuario en otro ordenador: detecta los mandos y va bien con 3 jugadores y una IA. Para pasarla, la carpeta sin `FakeBlade_BurstDebugInformation_DoNotShip`. Lleva todas las escenas de *Build Settings*, también `TestScene`.
- [ ] J2. WebGL: `Mobile_Renderer` en Forward (o renderer Web aparte), comprobar Depth Texture si se usan partículas suaves y hacer la primera build. Probar en Chrome y Firefox: carga, mandos (aparecen al pulsar), teclas que el navegador no se quede, audio tras el primer clic, guardado de opciones y rendimiento con 4 peonzas. Decidir qué hace Opciones → Calidad. 🎮
- [ ] J3. WebGL: desactivar los CSV del Sandbox y del banco en el navegador (la telemetría de partidas, 6.6, ya no graba en WebGL); revisar el tamaño de la descarga (quitar paquetes sin uso como Visual Scripting).
- [ ] J4. Linux: instalar *Linux Build Support (Mono)*, build con Vulkan y probar aspecto, pantalla completa y mandos (Xbox y PlayStation). Si hay un Steam Deck a mano, también ahí. 🎮
- [ ] J5. Android (**después de completar la versión de PC**): multijugador en red local (6.5) con lobby para crear, buscar y unirse; controles táctiles y menús para pantalla pequeña; orientación solo horizontal, zona segura en el HUD, 60 FPS, botón Atrás, texturas ASTC y build AAB. Antes de empezar, aprender lo básico de Netcode for GameObjects con una prueba aparte.
- [ ] J6. Revisar las reglas de 7.5 y 6.5 al añadir shaders, efectos, código que escriba archivos o cambios en cómo llegan las órdenes a la peonza.
- [ ] J7. Probar una partida a distancia con **Parsec** (anfitrión + 1-3 invitados con mando): asignación en el lobby, latencia y si se puede hacer parry. Steam Remote Play Together, cuando el juego esté en Steam. 🎮

**Fase K. Aspecto visual del combate** (después de terminar la IA, 2026-10-12)

- [ ] K1. **Anticipación de los ataques:** efectos más claros o llamativos al lanzar un ataque rápido (y al empezar un cargado), para verlos venir y poder hacer parry. Jugando contra la IA (2026-10-12) cuesta hacerle parry porque sus ataques no se anticipan. 🎮

**Otros pendientes**

- [ ] Modo por puntos: restar 1 punto por autoeliminación.
- [ ] Asignar la ventana de parry a las piezas (agilidad +0,03 s, ataque −0,01 s, defensa −0,03 s).
- [ ] Asignar sonidos (choque, ataque, dash, especial, parry, K.O.) y música (ver G4 y G5).
- [ ] Fuente pixel para los textos. Candidatas gratis con licencia OFL: Press Start 2P, Silkscreen o Pixelify Sans.
- [ ] **Selección de peonzas: señalar las IA en la interfaz** (tras probar D7 con mando, 2026-10-13). Hoy solo lo dice la ayuda de la columna; falta algún panel o indicador que deje claro que se puede añadir una IA y pasar a su columna con LB/RB (por ejemplo, pestañas sobre las columnas o un aviso en la columna libre). Se hace cuando el usuario quiera refinar el aspecto gráfico y la interfaz.
- [ ] Pixelar la escena 3D de batalla y el fondo del menú (pilar 1.2). Ahora solo lo tiene la vista previa del lobby (`BladePreviewStage`). Renderizar a una RenderTexture de baja resolución con escala entera y filtro Point; prueba rápida: Render Scale de URP entre 0,33 y 0,5 con filtro Nearest-Neighbor. El HUD (Overlay) no se ve afectado.
- [ ] Equilibrio general. Primer ajuste hecho (2026-10-06): daño x0,8, RPM base 400, cargado = rápido × (1 + 10% por nivel) y movimiento más parejo. Siguiente: jugar con mandos y revisar los CSV del sandbox (`Logs/Sandbox/`). Antes, una peonza lanzada que chocaba a 12 m/s contra una de ataque fuerte perdía un 42%. 🎮
- [ ] Probar menús, lobby y combate con mandos reales. 🎮
- [ ] Probar builds de PC y WebGL (ahora en la Fase J: J1 y J2).
- [ ] Probar el ratón en las columnas del lobby.
- [ ] Input handling en "Input System" solamente (ahora está en "Both").
- [ ] Quitar `Assets/Scripts_copiaAntigua.zip` y el stash antiguo de backup cuando ya no hagan falta.
- [ ] Decidir si la dependencia de Coplay se queda en `Packages/manifest.json`.
- [x] Tests automáticos de EditMode (2026-10-10): 28 tests de fórmulas, objetivos de equilibrio y contenido (ver 7.2). Primer resultado: 25 pasan; 3 marcados *Pendiente C12*. Ejecutados desde Claude con la skill `ejecutar-tests`.
- [ ] Tests automáticos de las reglas de combate en Play (choque, parry, dash): PlayMode tests que monten dos peonzas y comprueben quién recibe el daño, el parry y el dash.
- [ ] Al terminar C12: quitar las marcas *Pendiente C12* de `BalanceTargetsTests`.
