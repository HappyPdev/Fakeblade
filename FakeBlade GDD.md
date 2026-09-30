# FakeBlade

# Game Design Document

Juego arcade de duelos de peonzas que recrea la serie de dibujos de Beyblade, para hasta 4 jugadores en local.

> **Documento consolidado.** La fuente prioritaria es *"JUEGO DE PEONZAS v.2"*: si algo de este documento contradice al v2, manda el v2 y se ha eliminado de aquí.
> Las decisiones tomadas durante el desarrollo están en la sección **11. Registro de decisiones**.
> Última actualización: 2026-09-30.

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

- **Ventana:** los primeros **0,12 s** desde que se lanza el ataque rápido (al soltar el botón). Solo vale el ataque rápido: ni el cargado ni el dash hacen parry. Con Storm Breaker, un ataque rápido cuenta como cargado para el daño, pero sigue pudiendo hacer parry.
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
- **Piezas:** cada pieza puede sumar o restar ventana de parry (`parryWindowModifier`). La idea es que las de agilidad la amplíen y las de defensa la reduzcan.
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

El poder especial lo define el **Núcleo** montado.

- **Carga:** el poder se carga al **golpear a los enemigos con éxito** (sobre todo al ganar choques) o al recoger powerups del escenario.
- **Activación:** cuando la carga está llena, se pulsa Especial. Mientras está activo, la carga se va vaciando con el tiempo. Cuando se vacía, el poder termina.

Poderes:

| Poder | Estado | Efecto |
|---|---|---|
| Spin Boost | Implementado | Recupera RPM progresivamente mientras está activo. |
| Onda de choque | Implementado | Al activarse empuja y quita RPM a las peonzas cercanas. |
| Storm Breaker (escudo) | Implementado | Para builds defensivas. Pequeña bonificación de movimiento, gran resistencia al empuje, menos daño recibido y los ataques rápidos cuentan como cargados. |
| Dash eléctrico | Implementado | Dashes de mayor alcance, sin apenas cooldown, y los ataques cargados se cargan más rápido. |
| Rastro de fuego | Pendiente | Deja un rastro que quita RPM a quien lo pisa y da velocidad al usuario mientras dura. |

**Aura de cada poder** (mientras está activo, con el color del poder):

| Poder | Aura |
|---|---|
| Spin Boost | Doble espiral de cruces verdes que sube alrededor de la peonza. |
| Onda de choque | Ondas naranjas que se expanden por el suelo cada 0,4 s, más chispas a ras de suelo. |
| Storm Breaker | Anillo protector azul que gira alrededor del cuerpo, más destellos en una cúpula. |
| Dash eléctrico | Rayos amarillos que chisporrotean alrededor, más chispas que saltan. |

6. # Sistema de Multijugador

Multijugador local para PC Windows y WebGL, con 2-4 jugadores usando mandos (y hasta 2 en teclado).

- **Asignación de controles flexible:** cada jugador se une en la pantalla de selección **manteniendo pulsado** el botón de ataque de su dispositivo (ver 9.2). El orden en que se unen fija el número de jugador (J1, J2...) y el dispositivo que usa cada uno.
- **Cámara dinámica** que encuadra a todos los jugadores vivos en todo momento.
- Si el proyecto funciona, se estudiará portarlo a móvil con partidas en la misma red local.

## 6.1 Modos de juego

- **Último en pie:** sin reaparición. Gana la última peonza (o equipo) con RPM. Sirve tanto para duelos 1vs1 como para batallas de 2-4 jugadores.
- **Todos contra todos (por puntos):** partida con tiempo límite. Cada eliminación da un punto al último jugador que golpeó a la peonza eliminada, y la peonza eliminada **reaparece**. Gana quien tenga más puntos al acabar el tiempo o quien llegue antes a los puntos objetivo.
- **Por equipos (2vs2):** los equipos se enfrentan con las mismas reglas. El **fuego amigo es configurable**. Cada jugador elige su equipo en su columna de la pantalla de selección.
- **Práctica (1 jugador):** si solo se une un jugador, la partida es de práctica contra un **dummy** (una peonza que no se controla). No termina sola y sirve para probar peonzas y controles. Se sale desde la pausa.
- **Futuro, contra la IA:** la arquitectura separa quién controla la peonza (jugador, dummy o IA), para poder añadir rivales controlados por IA más adelante.

## 6.2 Reglas personalizables

Como en Super Smash Bros, cada modo tiene reglas por defecto y el jugador puede personalizarlas:

- **Vidas por jugador** (reaparición al perder una vida).
- **Límite de tiempo** (por defecto **sin límite**, salvo en el modo por puntos).
- **Puntos objetivo.**
- **Fuego amigo** (activado o desactivado).

**Desempates** cuando se acaba el tiempo: gana quien tenga más puntos o vidas. Si siguen empatados, gana quien tenga más % de RPM.

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

10. # Ideas futuras

- **Rivales controlados por IA** (ver 6.1). También permitirían el modo Survival.
- **Survival:** 1 contra oleadas de enemigos con dificultad progresiva (necesita IA; no está en el v2).
- Hazards y powerups de arena (ver sección 4).
- Poder "Rastro de fuego".
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

12. # Pendiente de definir

- Qué poder concreto lleva cada Núcleo en el catálogo final de piezas.
- Valores de equilibrio: coste en RPM de ataque y dash, tiempos de recarga, fracción de daño de la peonza rápida, etc. Se ajustan en el asset `CombatConfig`.
- Si en el modo por puntos se resta un punto al quedarse sin RPM sin que nadie te haya golpeado (autoeliminación). Por defecto no se resta.
- Si la peonza puede moverse mientras carga un ataque. Por defecto se mueve al 60% de su velocidad (ajustable).
- Comportamiento de la IA rival (niveles de dificultad, estilos por arquetipo).
- Qué otros efectos de postprocesado se añaden a Opciones.
- Modelos o skins distintos por pieza. Ahora las piezas solo cambian estadísticas y el color.
- Valores de ventana de parry de cada pieza (ahora todas en 0) y si la IA debe intentar hacer parry.
- Si el bamboleo con RPM bajas debe afectar también al control (menos precisión al moverse o atacar). Ahora es solo visual.
- Efectos del poder Rastro de fuego cuando se implemente.
