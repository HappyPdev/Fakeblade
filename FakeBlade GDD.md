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

Plataformas: PC (Windows) y navegador con WebGL.
Audiencia: a partir de 8 años.

2. # Mecánicas Core

## 2.1 Sistema de Física de la peonza

- **Velocidad angular (RPM):** cada peonza tiene unas revoluciones máximas distintas según sus piezas.
- **Masa e inercia:** dependen de las piezas montadas.
- **Fricción dinámica:** superficie + colisiones.
- **Giroscopio:** efecto de estabilización. Internamente, la peonza se mantiene estable sola hasta que se le termina la "estamina" (RPM).
- **Precisión:** bamboleo cuando pierde velocidad. Por debajo del **30% de RPM** la peonza se bambolea: su inclinación da vueltas (precesión) y crece hasta 12° cerca de 0 RPM. Además echa **humo** y **chispas sueltas**, cada vez más cuanto menos RPM le quedan. Por ahora el bamboleo es solo visual y no afecta al control. Se ajusta en `CombatConfig` → `lowSpinThreshold`, `lowSpinWobbleAngle` y `lowSpinWobbleFrequency`.
- **Estela y chispas:** al moverse deja una estela de píxeles de su color (más grande y opaca durante ataques y dash). Si va rápido con muchas RPM, la punta saca chispas contra el suelo.
- **Giro visual:** la velocidad a la que se ve girar la peonza no es proporcional a las RPM. Sigue una curva de forma logarítmica: se mantiene casi al máximo durante casi toda la vida y solo se frena de golpe cuando las RPM están muy cerca de 0. Por defecto gira al 90% con un 8% de RPM y al 94% con un 10%. Solo por debajo del 5% se nota que frena. El punto de frenado se ajusta en `CombatConfig` → `visualSpinKnee`.
- **Control:** según la velocidad y la masa, cada peonza tiene un control más o menos estable.
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

Hasta **2 jugadores pueden compartir el teclado** (J1 con WASD y J2 con las flechas). El resto usa mando.

## 2.3 Sistema de Ataque

- **Ataque rápido:** al pulsar el botón, la peonza hace una pequeña aceleración en la dirección del joystick. Gana menos velocidad que un dash o un ataque cargado, pero durante el ataque tiene una **bonificación de masa**. Si no hay dirección marcada, ataca hacia el enemigo más cercano.
- **Cargas de ataque:** cada peonza tiene un número de cargas que se gastan al atacar y se recuperan con el tiempo. La media es **3 cargas**. Las piezas ligeras dan más cargas y las pesadas menos. Se muestran como puntos debajo de la barra de vida.
- **Ataques rápidos seguidos (combos):** pulsando repetidamente se encadenan varios ataques, cada uno con su coste en cargas y RPM. Los golpes consecutivos acumulan una pequeña bonificación de daño y de empuje.
- **Ataque cargado:** al mantener pulsado el botón de ataque, aumentan la potencia, la velocidad máxima del acelerón y la bonificación de masa. El nivel máximo de carga depende de las cargas restantes, y el ataque gasta tantas cargas como niveles se hayan cargado. Si se mantiene en el máximo un tiempo, el ataque se lanza solo. Los ataques cargados empujan más al objetivo.
- **Efecto de la carga:** mientras se carga, unas partículas convergen hacia la peonza. En cada nivel son más, más grandes y pasan del color del jugador a un blanco dorado, y sale un anillo en el suelo. Al llegar al máximo hay un destello de estrellas y salen llamas de la base hasta que se lanza.
- **Coste:** los ataques cuestan RPM, igual que el dash.
- **Cooldown:** entre ataques cargados hay un tiempo de enfriamiento.

## 2.4 Sistema de Dash

El dash es un acelerón **mayor que el de los ataques** que se hace con un botón propio. Sirve para dos cosas:

- **Esquivar o reposicionarse:** para salir de un ataque o buscar una posición mejor contra el objetivo.
- **Atacar:** si durante el dash la peonza choca contra otra, **cuenta como un ataque** y tiene la misma prioridad que un ataque, aplicando la regla de velocidad del sistema de choque (2.5).

Cuesta RPM y tiene cooldown. **No tiene bonificación de masa ni parámetros extra** (ni armadura ni bonus de daño): su ventaja es solo la velocidad que da.

**Dash acertado:** si durante la ventana del dash la peonza **gana un choque contra un enemigo** (llega más rápida, según la regla de 2.5), recupera el **50% de las RPM que le costó el dash**. Solo una vez por dash. Si pierde el choque, choca con un compañero o no toca a nadie, no recupera nada. La fracción se ajusta en `CombatConfig` → `dashHitRefundFraction`.

**Dirección (sin autoapuntado con dirección):** el dash y los ataques van exactamente hacia donde apunta el jugador. Solo si no pulsa ninguna dirección van al enemigo más cercano (ver 2.1).

## 2.5 Sistema de Choque

Cuando dos peonzas chocan, se comparan sus velocidades en el momento del choque (la componente de la velocidad con la que cada una va hacia la otra):

- **La más lenta** recibe la penalización: pierde RPM en función del impacto y de la **diferencia de velocidad**, y sale empujada según la relación de masas (incluida la bonificación de masa del ataque).
- **La más rápida** recibe solo una **fracción del daño** (por defecto un 25%, ajustable). Así cualquier choque tiene algún coste.
- **Impacto directo:** se transmite la energía entre las peonzas.
- **Impacto oblicuo:** desvía la trayectoria y hace perder velocidad. Hace menos daño porque la velocidad de aproximación es menor.
- **Defensa:** sincronizar bien los ataques para llegar con más velocidad que el rival es la forma de defenderse ("timing").
- **Paredes:** chocar contra el borde quita muy pocas RPM. Solo cuenta la velocidad perpendicular a la pared, así que rozarla no quita RPM. El suelo nunca quita RPM.

### Parry

Un ataque **rápido** lanzado justo antes de recibir un ataque enemigo lo bloquea por completo.

- **Ventana:** los primeros **0,12 s** desde que se lanza el ataque rápido (al soltar el botón). Solo vale el ataque rápido: ni el cargado ni el dash hacen parry.
- **Condición:** la otra peonza tiene que venir atacando (ataque rápido, cargado o dash). El parry tiene prioridad sobre la regla de la más rápida.
- **Resultado:**
  - Quien hace el parry **no recibe daño**.
  - El atacante recibe solo **su fracción del impacto** (la misma que recibe normalmente la peonza más rápida).
  - El atacante **sale despedido**, su ataque o dash queda cortado y queda **aturdido 0,3 s** (sin control, ataques ni dash) para que el rebote se note.
  - Quien hace el parry frena su embestida y casi no se mueve.
- **Recompensas para quien hace el parry:**
  - recupera la carga de ataque gastada;
  - gana un 20% de la energía del especial.
- **Aviso:** partículas especiales (estallido blanco y cian), texto "¡PARRY!" en pantalla y una pausa muy breve del juego (hit-stop de 0,08 s).
- **Doble parry:** si las dos peonzas están en su ventana a la vez, se anulan. Ninguna recibe daño ni recompensa, y las dos rebotan aturdidas.
- **Piezas:** cada pieza suma o resta ventana de parry (`parryWindowModifier`): las de agilidad **+0,03 s**, las balanceadas 0, las de ataque **−0,01 s** y las de defensa **−0,03 s**. Se suman todas las piezas montadas; la ventana nunca baja de 0,02 s. *(Pendiente de asignar en los assets de las piezas.)*
- **Ajustes:** todo se configura en `CombatConfig` → Parry.

## 2.6 Gestión de Energía

- **Vida o resistencia (RPM):** la vida de la peonza son sus revoluciones por minuto. Cuantas más RPM, más energía para seguir luchando. Las RPM bajan poco a poco solas (desgaste) y al recibir golpes.
- **Masa:** resistencia a perder velocidad y a ser empujada.
- **Ataque:** fuerza de impacto en las colisiones, que depende de la masa de la peonza y de su velocidad al chocar.
- **Defensa:** resistencia al daño y al empuje.
- **Eliminación:** una peonza queda eliminada **únicamente cuando sus RPM llegan a 0**. No hay ring-out: los bordes de la arena impiden salir.

3. # Peonzas Modulares

Cada peonza se compone de **piezas intercambiables** que modifican sus estadísticas. Los jugadores montan su peonza antes de cada combate.

| Pieza | Afecta sobre todo a |
|---|---|
| Punta (Tip) | Estabilidad (desgaste de RPM) y velocidad de movimiento |
| Cuerpo (Body) | Peso e inercia |
| Disco (Blade) | Ataque y defensa |
| Núcleo (Core) | RPM máximas y **habilidad especial** |

Estadísticas que modifican las piezas: velocidad de rotación (RPM máx. y desgaste), peso/inercia, potencia de ataque, resistencia/defensa, velocidad de movimiento, fuerza de dash y número de cargas de ataque.

Cada pieza tiene una clase de peso (Ligera / Media / Pesada).

**Arquetipos.** Los arquetipos no son peonzas fijas: son el **resultado de combinar las piezas**. Según las estadísticas finales, la peonza encaja en uno:

- **Ataque:** más cargas de ataque, masa media y velocidad media.
- **Defensa:** menos cargas, más masa y velocidad media.
- **Agilidad:** cargas medias, masa baja y velocidad alta.
- **Balanceada:** todo medio.

El resto de valores se irán probando y equilibrando. El sistema es modular y ampliable: añadir una pieza nueva es crear un asset de datos.

4. # Diseño de Arenas

Al principio habrá un solo tipo de arena. En las opciones antes del duelo se podrá elegir escenario, para que el diseño sea modular y ampliable.

Las arenas serán circulares y con forma de cuenco, con un **borde que impide salir** y una fricción normal que no afecta al movimiento.

La primera arena usa el modelo **`Arena 00.fbx`**, escalado al tamaño de juego. Cada arena es un prefab con sus puntos de aparición y un asset de datos (nombre y vista previa) que aparece en la selección de escenario.

Cuando todo funcione, se añadirán **hazards**: suelos con distintas fricciones y efectos (hielo que desliza, suelos que aceleran) y zonas que afectan a las estadísticas. También powerups en el escenario que mejoren los ataques o recuperen algo de RPM.

5. # Sistema de Poderes

El poder especial lo define el **Núcleo** montado: para cambiar de especial se cambia el núcleo de la peonza. Habrá un núcleo por poder.

- **Carga:** el poder se carga al **golpear a los enemigos con éxito** (sobre todo al ganar choques) o al recoger powerups del escenario. **Cada poder necesita una cantidad distinta de energía** para llenarse: los más fuertes tardan más (columna *Energía* de la tabla; 1 = la barra actual). Mientras el poder está activo **no se gana energía**: al terminar, la barra empieza de 0.
- **Activación:** cuando la carga está llena, se pulsa Especial. Mientras está activo, la carga se va vaciando con el tiempo. Todos duran **5 s**. Cuando se vacía, el poder termina. Excepción: Rayos dura hasta 6 s o hasta que da su golpe fuerte (ver tabla).
- **Nombres:** simples, en español e inglés: Defensa / Defense, Fuego / Fire, Hielo / Ice, Rayos / Lightning, Fantasma / Ghost. Spin Boost y Onda de choque mantienen los suyos.

**Efecto común a todos los poderes (al activarse)** *(implementado)*:

- Recupera el **25% de las RPM máximas** (mismo valor para todos, ajustable en `CombatConfig`).
- **Rellena todas las cargas de ataque** disponibles en ese momento.

Después, durante el tiempo activo, cada poder tiene su efecto propio.

Poderes (se fusionan los que ya había con los nuevos: 7 núcleos en total):

| Poder | Estado | Energía | Efecto propio |
|---|---|---|---|
| Spin Boost | Implementado (se rehará más adelante) | 1 | Recupera RPM progresivamente mientras está activo. |
| Onda de choque | Implementado (se rehará más adelante) | 1 | Al activarse empuja y quita RPM a las peonzas cercanas. |
| Defensa *(sustituye a Storm Breaker)* | Pendiente | 1,2 | Defensa casi al 100%: no pierde RPM por golpes, paredes ni desgaste, y el empuje que recibe es casi nulo. Además, sus ataques se recargan **1,5 veces más rápido**, pero se mueve un **15% más lento**. El dash solo cuesta el **10% de lo normal**, pero su alcance baja un **40%**. Atacar sí cuesta RPM. |
| Fuego *(sustituye a Rastro de fuego)* | Pendiente | 1 | Sus golpes **queman** al enemigo: 1,5% de sus RPM máximas cada 0,5 s durante 3 s (9% en total). Un golpe nuevo reinicia la duración; no se acumula. |
| Hielo | Pendiente | 1 | Aura de hielo y humo blanco de frío. A los enemigos que golpea los **congela** durante 3 s: −35% de velocidad de movimiento y recarga de ataques a la mitad. Un golpe nuevo reinicia la duración; no se acumula. |
| Rayos *(sustituye a Dash eléctrico)* | Pendiente | 0,8 | Entra en modo cargado hasta 6 s, con sus partículas. **Choques pequeños:** empujan fuerte al enemigo, pero no gastan el poder. **Choque fuerte** (por encima de una velocidad mínima): golpe con +20% de daño y empuje x2,5, deja al rival **lanzado** 0,5 s y gasta el poder. |
| Fantasma | Pendiente | 1,3 | Crea **un clon fantasma** de sí misma, invulnerable, que dura lo que el poder y va a la velocidad de su dueño. Persigue al enemigo más cercano; sus golpes quitan el 10% del daño normal, pero empujan y cortan la carga de ataque del rival: sirve para molestar. No hace parry ni se le puede hacer. |

**Estados alterados** (implementado el sistema; los aplicarán Fuego, Hielo y Rayos):

- **Quemadura** (Fuego): pierde RPM por tic durante unos segundos. Es **daño fijo**: no lo reduce la defensa de las piezas (sí un poder defensivo activo).
- **Congelación** (Hielo): menos velocidad de movimiento y recarga de ataques más lenta durante unos segundos.
- **Lanzada** (Rayos): durante 0,5 s tras el golpe fuerte, la peonza **no tiene prioridad por velocidad**. Si choca contra otra peonza, recibe el daño como la más lenta, y la otra recibe un choque parejo. Si choca contra una **pared**, la pared la golpea como una peonza parada con la fuerza del usuario de Rayos (daño base + diferencia de velocidad; en la prueba, a 30 m/s: 13,8% de las RPM frente al 1,1% de un choque normal). **Todo** ese daño (el de la lanzada y el de la peonza contra la que choca, como en los bolos) cuenta como golpe del usuario de Rayos para el punto de K.O. Así se premia empujar enemigos contra paredes o contra otros enemigos.
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
| Rayos | Rayos amarillos que chisporrotean alrededor, más chispas que saltan (hecho, heredado de Dash eléctrico). Falta el destello del golpe fuerte. |
| Fuego | Pendiente: llamas alrededor; llamitas sobre las peonzas quemadas. |
| Hielo | Pendiente: cristales de hielo y humo blanco de frío; escarcha sobre las peonzas congeladas. |
| Fantasma | Pendiente: clon translúcido con estela. |

6. # Sistema de Multijugador

Multijugador local para PC Windows y WebGL, con 2-4 jugadores usando mandos (y hasta 2 en teclado). Los huecos se pueden completar con **rivales CPU** (ver 6.4): el máximo de 4 cuenta jugadores humanos y CPU juntos.

**Escalable a 8:** en el futuro se quiere llegar a **8 jugadores**. Por ahora el máximo es 4, pero el código debe prepararse para que ese número sea un único valor configurable (lobby, HUD, colores, puntos de aparición, equipos...).

- **Asignación de controles flexible:** cada jugador se une en la pantalla de selección **manteniendo pulsado** el botón de ataque de su dispositivo (ver 9.2). El orden en que se unen fija el número de jugador (J1, J2...) y el dispositivo que usa cada uno.
- **Cámara dinámica** que encuadra a todos los jugadores vivos en todo momento.
- Si el proyecto funciona, se estudiará portarlo a móvil con partidas en la misma red local.

## 6.1 Modos de juego

- **Último en pie:** sin reaparición. Gana la última peonza (o equipo) con RPM. Sirve tanto para duelos 1vs1 como para batallas de 2-4 jugadores.
- **Todos contra todos (por puntos):** partida con tiempo límite. Cada eliminación da un punto al último jugador que golpeó a la peonza eliminada, y la peonza eliminada **reaparece**. Si una peonza se queda sin RPM **sin que nadie la haya golpeado** en los últimos segundos (desgaste o paredes), **pierde 1 punto** (autoeliminación). Gana quien tenga más puntos al acabar el tiempo o quien llegue antes a los puntos objetivo.
- **Por equipos (2vs2):** los equipos se enfrentan con las mismas reglas. El **fuego amigo es configurable**. Cada jugador elige su equipo en su columna de la pantalla de selección.
- **Sandbox (campo de pruebas):** sustituye al modo Práctica. Tiene **escena propia** y se usa tanto para practicar como de **entorno de debug**. Ver 6.3. *(Ahora mismo sigue existiendo la Práctica simple contra un dummy, dentro de la escena de batalla, hasta que se haga el sandbox.)*
- **Contra la IA:** la arquitectura separa quién controla la peonza (jugador, dummy o IA). La IA se usará primero en el sandbox y después como rival CPU en partidas normales. Ver 6.4.

## 6.2 Reglas personalizables

Como en Super Smash Bros, cada modo tiene reglas por defecto y el jugador puede personalizarlas:

- **Vidas por jugador** (reaparición al perder una vida).
- **Límite de tiempo** (por defecto **sin límite**, salvo en el modo por puntos).
- **Puntos objetivo.**
- **Fuego amigo** (activado o desactivado).

**Desempates** cuando se acaba el tiempo: gana quien tenga más puntos o vidas. Si siguen empatados, gana quien tenga más % de RPM.

## 6.3 Sandbox (campo de pruebas y debug)

Escena propia (`Sandbox`) para probar mecánicas sin salir de la partida: parrys, especiales, piezas, etc. Todo se puede cambiar **en ejecución**.

- **Jugadores:** pueden entrar **hasta 4 jugadores humanos**, que se unen manteniendo pulsado ataque, como en la selección de peonzas. Sirve, por ejemplo, para practicar parrys entre dos personas con mando.
- **Panel del sandbox:** se abre con un **botón propio** (Select/Back en mando, Tab en teclado; reasignable), que **pausa** el juego. Se navega con mando y con ratón. Al cerrarlo, el juego sigue con los cambios aplicados.
- **Primera versión del panel:**
  - **Rivales:** ninguno, dummy o IA (nivel y arquetipo), de 1 a 3 rivales.
  - **Comportamiento del dummy:** quieto, moverse, **atacar cada X segundos** (para practicar parry), hacer dash hacia el jugador, usar el especial.
- **Más adelante** (se irá ampliando):
  - **Mi peonza:** cambiar piezas y **núcleo (especial)** en caliente.
  - **Trucos:** RPM infinitas (mías o del rival), especial siempre lleno, cargas infinitas, dash sin cooldown, invulnerable.
  - **Reiniciar:** posiciones, RPM, cargas y energía.
  - **Tiempo:** cámara lenta (x0,25 / x0,5 / x1) y avance frame a frame.
  - **Debug visual:** indicador de la ventana de parry, vectores de velocidad, números de daño, estados alterados, info de choques, FPS y un registro de eventos (choques, parrys, especiales).
  - Cambiar de arena y retocar valores clave de `CombatConfig` (ventana de parry, costes...) sin salir.

## 6.4 IA rival

La IA controla la peonza con la misma interfaz que un jugador (`IBladeInputSource`), así que no hace trampas: pulsa los mismos botones.

**Niveles de dificultad (4):** Fácil, Normal, Difícil y Experto. Cambian el tiempo de reacción, la puntería, la probabilidad de hacer parry, el uso del dash para esquivar y cómo usa el especial.

| Nivel | Reacción | Parry | Especial |
|---|---|---|---|
| Fácil | Lenta (~0,5 s) | Nunca | Al azar |
| Normal | Media (~0,3 s) | A veces | Cuando está lleno |
| Difícil | Rápida (~0,18 s) | A menudo | En buen momento |
| Experto | Muy rápida (~0,1 s) | Casi siempre que puede | Óptimo |

*(Valores orientativos, por ajustar.)*

**Comportamiento según el arquetipo de su peonza:**

- **Agilidad:** agresiva. Entra y sale con dash y **busca parrys** (ataque rápido justo cuando el rival lanza el suyo).
- **Ataque:** presión constante, combos y ataques cargados; usa el especial en cuanto lo tiene.
- **Defensa:** aguanta en el centro y contraataca cuando el rival gasta sus cargas o queda aturdido. Busca empujar al rival contra las paredes.
- **Balanceada:** mezcla de los anteriores según la situación.

**Rivales CPU en partidas normales:** se podrán añadir CPU a las partidas, hasta completar el máximo de jugadores (4 ahora; 8 en el futuro).

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
- **Escenas:** `MainMenu` (menú principal, opciones, controles e información, con la arena de fondo), `Assembly` (unirse, montar peonzas y parámetros de partida) y `BattleArena` (combate). La configuración pasa de una escena a otra en `MatchSetup`.
- **Catálogo** (`FakeBladeCatalog`): piezas, presets, paleta de colores, arenas y prefab de jugador. Añadir contenido es añadir entradas al catálogo.
- **HUD dirigido por eventos:** no busca objetos en la escena ni formatea strings cada frame.
- **Assembly definitions** (`FakeBlade.Runtime` y `FakeBlade.Editor`) para compilar más rápido y separar el código de editor.

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
  - `Assets/ThirdParty/UnityTemplate/`: el readme de la plantilla URP de Unity (`TutorialInfo` y `Readme.asset`). No lo usa nada; no se sube.
  - `Assets/Plugins/AllIn1SpriteShader/`: All In 1 Sprite Shader (sin demos). En su carpeta original y en `.gitignore`.
  - Se quedan en su sitio aunque no sean nuestros: `TextMesh Pro` (recursos de Unity que usa el HUD; hacen falta para que el repo funcione al clonarlo) e `InputSystem_Actions` (registrado como acciones globales del proyecto en la configuración de Input System).
- El código del juego **no depende de clases de paquetes de pago**: los shaders se usan a través de materiales y las herramientas solo en el editor. Así el repo compila aunque falten.
- **Qué entra en la build:** lo que usan las escenas de la build (y sus dependencias), todo lo que esté en carpetas `Resources` y todo el código de runtime. Lo que no se usa no entra, pero hay que vigilar las carpetas `Resources` y los scripts de cada paquete (se compilan siempre, también en WebGL).

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
  - Vista previa 3D de la peonza girando, pixelada.
  - Arquetipo resultante y barras de estadísticas.
  - **Preset** (Ataque, Defensa, Agilidad, Balanceada o Aleatorio).
  - **Personalizar:** cambiar Punta, Cuerpo, Disco y Núcleo una a una. Al tocar una pieza, el preset pasa a "Personalizada".
  - **Color:** paleta fija de colores retro. Un color elegido por un jugador no lo puede coger otro.
  - **Equipo** (A o B): solo se usa si la partida es por equipos.
  - **Confirmar.**
- **Atrás:** si el jugador ha confirmado, deja de estar listo. Si no, al mantener Atrás abandona su columna. Con todas las columnas libres, Atrás vuelve al menú principal.
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
- **Juego:** idioma, vibración del mando y sacudida de cámara.

### 9.2.4 Controles

Reasignación completa de los controles de **Teclado J1**, **Teclado J2** y **Mando** (el mapeo de mando es común a todos los mandos):

- Se selecciona una acción y se pulsa la tecla o el botón nuevo.
- Si la tecla ya estaba en uso, se **intercambia** con la acción que la tenía, para que nunca quede un control duplicado ni vacío.
- "Restablecer" vuelve a los controles por defecto.

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
- Hazards y powerups de arena (ver sección 4).
- Más efectos de postprocesado en Opciones.
- Portar a móvil con partidas en red local.

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
| 2026-10-05 | Carpeta ThirdParty | Todo lo que no es nuestro va en `Assets/ThirdParty/` (ignorado); los paquetes libres (CC0, MIT...) en `ThirdParty/Free/`, que sí se sube. Los paquetes de código o shaders se quedan en su carpeta original (ignorada). |

12. # Pendiente de definir

Entre paréntesis, la propuesta por defecto si no se decide otra cosa.

- Estadísticas de cada núcleo (uno por poder: RPM máximas, peso...).
- Valores de equilibrio: coste en RPM de ataque y dash, tiempos de recarga, fracción de daño de la peonza rápida, etc. Se ajustan en el asset `CombatConfig`.
- Qué otros efectos de postprocesado se añaden a Opciones.
- Modelos o skins distintos por pieza. Ahora las piezas solo cambian estadísticas y el color.
- Sonido al pulsar un botón (confirmar) y al volver atrás en los menús (alguno de los cortos del pack Casual, distinto de DM-CGS-01, que es el de moverse entre botones).
- Autoeliminación: cuántos segundos sin recibir golpes hacen que cuente como autoeliminación (los mismos 5 s que dan el punto de K.O.).

**Poderes:**

- Nuevo diseño de Spin Boost y Onda de choque (se rehacen más adelante; mientras tanto se quedan como están).
- Rayos: velocidad mínima del choque fuerte (velocidad de cierre de 6 m/s) y empuje de los choques pequeños (x1,8, sin daño extra ni lanzada).
- Rayos: si más adelante cualquiera que toque a la peonza (atacando o en movimiento normal) sale impulsado (por ahora, solo sus propios choques).
- Fantasma: agresividad del clon (busca al rival más cercano y ataca en cuanto puede, como mucho una vez por segundo).

**Efectos de UI y sonido:**

- Biblioteca de sonidos (un asset `SfxLibrary` en `Resources`, como `VfxLibrary`, con clip, volumen y variación de tono por evento).
- Cómo suenan los menús (un reproductor de UI único que no se destruye entre escenas, con el volumen de efectos de Opciones).
- Mezcla de audio (sin AudioMixer por ahora; volúmenes desde `SettingsService`).
- Si la sacudida del panel al recibir un golpe depende de la opción "Sacudida de cámara" (sí, la misma opción).

**Sandbox e IA:**

- Parámetros exactos de cada nivel de IA (tabla orientativa en 6.4).
- Desde dónde se añaden las CPU en las partidas normales (en una columna libre de la selección de peonzas, eligiendo nivel y peonza).
- Con varios jugadores en el sandbox, quién maneja el panel (cualquiera puede abrirlo; lo controla quien lo abre).

13. # Quehaceres

Lista de trabajo por fases, para ir añadiendo poco a poco. Se marca `[x]` al terminar. 🎮 = conviene probarlo con mandos reales. 🎧 = lo tiene que escuchar el usuario.

Orden propuesto: primero la base común de los poderes, después un sandbox básico (para poder probar cada poder según se hace), luego los poderes uno a uno y por último la IA completa.

**Fase A. Base común de los especiales**

- [x] A1. Efecto común al activar: +25% de RPM máximas y rellenar todas las cargas de ataque (`CombatConfig.specialActivationSpinPct` + `AttackSystem.RefillCharges`).
- [x] A2. Poderes como datos + comportamiento: un asset por poder (energía necesaria, duración, color, icono, nombre ES/EN) y una clase con su efecto. Migrados los 4 actuales (Spin Boost, Onda de choque, Storm Breaker y Dash eléctrico).
- [x] A3. Energía necesaria por poder (Spin Boost 1, Onda 1, Fuego 1, Hielo 1, Defensa 1,2, Fantasma 1,3, Rayos 0,8) y esfera del HUD según ese valor. Storm Breaker y Dash eléctrico ya usan la de Defensa (1,2) y Rayos (0,8). Fuego, Hielo y Fantasma la tendrán al crear su asset.
- [x] A4. Estados alterados en la peonza (quemadura, congelación, lanzada): uno a la vez, sustitución, triángulo de bloqueos (Fuego > Hielo > Rayos > Fuego), partículas que salen del panel del jugador, partículas sobre la peonza e icono encima de ella (`StatusEffectSystem`; los aplicarán los poderes con `TryBurn`, `TryFreeze` y `TryLaunch`).
- [ ] A5. Fusión: Storm Breaker → Defensa, Dash eléctrico → Rayos, Rastro de fuego → Fuego. Actualizar enum, núcleos, textos (Defensa/Defense, Fuego/Fire, Hielo/Ice, Rayos/Lightning, Fantasma/Ghost) y auras.

**Fase B. Sandbox básico** (sustituye a Práctica)

- [ ] B1. Escena `Sandbox`; la opción de practicar del menú lleva aquí.
- [ ] B2. Botón propio del sandbox (Select/Back y Tab, reasignable) que pausa y abre el panel. 🎮
- [ ] B3. Panel navegable con mando y ratón (reutilizando los widgets pixel del menú). 🎮
- [ ] B4. Rivales: ninguno o dummy, de 1 a 3 (la IA se añade al panel en D6).
- [ ] B5. Comportamientos del dummy: quieto, moverse, atacar cada X s (practicar parry), dash hacia el jugador, usar especial.
- [ ] B6. Hasta 4 jugadores humanos, que se unen manteniendo ataque. 🎮

**Fase C. Poderes, uno a uno** (cada uno con su aura, sus textos y su prueba en el sandbox)

- [ ] C1. Defensa: sin pérdida de RPM por golpes, paredes ni desgaste; empuje casi nulo; recarga de ataques x1,5; movimiento −15%; dash al 10% de coste y −40% de alcance.
- [ ] C2. Fuego: quemadura (1,5% cada 0,5 s durante 3 s) + llamas.
- [ ] C3. Hielo: congelación (−35% de movimiento, recarga a la mitad, 3 s) + cristales y humo blanco.
- [ ] C4. Rayos: modo cargado hasta 6 s; choques pequeños empujan fuerte sin gastar; choque fuerte con +20% de daño, empuje x2,5 y lanzada 0,5 s, que gasta el poder; contra una peonza congelada, choque normal sin gastar; regla de daño de la peonza lanzada + destello del golpe.
- [ ] C5. Fantasma: un clon invulnerable que persigue (necesita la persecución básica de D1), 10% de daño, empuja y corta cargas, sin parry + aspecto translúcido.
- [ ] C6. Núcleos en el catálogo: uno por poder, con estadísticas.
- [ ] C7. Equilibrio de valores de cada poder en partida. 🎮

**Fase D. IA**

- [ ] D1. Base de la IA: percepción (rivales, paredes, ataques enemigos) y decisión por prioridades, sobre `IBladeInputSource` (partiendo de `SimpleAIBrain`).
- [ ] D2. 4 niveles de dificultad como asset de datos (reacción, puntería, parry, esquiva con dash, uso del especial).
- [ ] D3. Perfiles por arquetipo: Agilidad agresiva y con parrys; Ataque con presión y cargados; Defensa que aguanta, contraataca y estampa contra paredes; Balanceada mixta.
- [ ] D4. Parry de la IA: lanzar un ataque rápido justo cuando el rival ataca.
- [ ] D5. Uso de los especiales por la IA (cada poder en su buen momento).
- [ ] D6. IA en el panel del sandbox: 1-3 rivales, nivel y arquetipo.
- [ ] D7. Rivales CPU en partidas normales (hasta completar 4 jugadores).
- [ ] D8. Menú de fondo con la IA nueva en vez de la actual.

**Fase E. Sandbox completo**

- [ ] E1. Cambiar núcleo (especial) y piezas en caliente.
- [ ] E2. Trucos: RPM infinitas, especial siempre lleno, cargas infinitas, dash sin cooldown, invulnerable.
- [ ] E3. Reiniciar posiciones, RPM, cargas y energía.
- [ ] E4. Debug visual: ventana de parry, vectores de velocidad, números de daño, estados y FPS.
- [ ] E5. Registro de eventos en pantalla (choques, parrys, especiales).
- [ ] E6. Cámara lenta (x0,25 / x0,5) y avance frame a frame.
- [ ] E7. Cambiar de arena y retocar valores clave de `CombatConfig` sin salir.

**Fase F. Más adelante**

- [ ] F1. Rehacer Spin Boost y Onda de choque.
- [ ] F2. Escalar a 8 jugadores: que el máximo sea un único valor configurable (hoy está repetido en `LobbyController.MaxPlayers`, `GameManager.maxPlayers`, `FakeBladeController.ClashMemory` y `MenuArenaBackground`), HUD para más de 4 paneles, paleta de 8 colores o más, puntos de aparición, columnas del lobby y equipos.

**Fase G. Assets de la Asset Store** (importar y analizar; reglas y licencias en 7.4)

Proceso con cada paquete: descargarlo en *Package Manager → My Assets* (con la cuenta de Unity del usuario) → revisar qué trae antes de importarlo → importar solo lo necesario → añadir su carpeta al `.gitignore` si no está en `Assets/ThirdParty/` → probar en PC y WebGL → decidir si se queda.

- [x] G1. `.gitignore` preparado para los assets de terceros (`Assets/ThirdParty/`).
- [ ] G2. **All In 1 Sprite Shader** (UI). *Importado (sin demos, sin carpetas `Resources`).* Diseño y reglas en **9.3**. No sirve para textos TMP ni para el clon de Fantasma (C5).
  - [ ] G2.1. Base: carpeta `Assets/Materials/UI/`, campos de material en `HUDTheme`, ayuda para crear la instancia de cada panel y animar valores por pasos sin GC, y vuelta al aspecto actual si falta el shader.
  - [ ] G2.2. Esfera del especial: glow + shine al llenarse; hue shift y más glow con el poder activo.
  - [ ] G2.3. Panel: destello blanco y sacudida al recibir un golpe.
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

Descartados en el análisis (se pueden revisar): NodeCanvas (la IA de la fase D se hace en C#), White Mage Spells (efectos realistas y aditivos, en contra de 7.3), GUI Pro Fantasy RPG (estilo pintado) y el resto de la lista (entornos realistas, HDRP, personajes, plantillas de otros géneros y assets deprecated).

**Otros pendientes**

- [ ] Modo por puntos: restar 1 punto por autoeliminación.
- [ ] Asignar la ventana de parry a las piezas (agilidad +0,03 s, ataque −0,01 s, defensa −0,03 s).
- [ ] Asignar sonidos (choque, ataque, dash, especial, parry, K.O.) y música (ver G4 y G5).
- [ ] Fuente pixel para los textos. Candidatas gratis con licencia OFL: Press Start 2P, Silkscreen o Pixelify Sans.
- [ ] Pixelar la escena 3D de batalla y el fondo del menú (pilar 1.2). Ahora solo lo tiene la vista previa del lobby (`BladePreviewStage`). Renderizar a una RenderTexture de baja resolución con escala entera y filtro Point; prueba rápida: Render Scale de URP entre 0,33 y 0,5 con filtro Nearest-Neighbor. El HUD (Overlay) no se ve afectado.
- [ ] Equilibrio general (el ataque cargado de nivel 3 quita ~29%, quizá demasiado; una peonza lanzada que choca a 12 m/s contra una peonza de ataque fuerte perdió un 42%). 🎮
- [ ] Probar menús, lobby y combate con mandos reales. 🎮
- [ ] Probar builds de PC y WebGL.
- [ ] Probar el ratón en las columnas del lobby.
- [ ] Input handling en "Input System" solamente (ahora está en "Both").
- [ ] Quitar `Assets/Scripts_copiaAntigua.zip` y el stash antiguo de backup cuando ya no hagan falta.
- [ ] Decidir si la dependencia de Coplay se queda en `Packages/manifest.json`.
- [ ] Tests automáticos de las reglas de combate (choque, parry, dash).
