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

- **Velocidad angular (RPM):** cada peonza tiene unas revoluciones máximas distintas según sus piezas, sobre una base de **400 RPM** (antes 350, subida para que los combates duren más).
- **Masa e inercia:** dependen de las piezas montadas.
- **Fricción dinámica:** superficie + colisiones.
- **Giroscopio:** efecto de estabilización. Internamente, la peonza se mantiene estable sola hasta que se le termina la "estamina" (RPM).
- **Precisión:** bamboleo cuando pierde velocidad. Por debajo del **30% de RPM** la peonza se bambolea: su inclinación da vueltas (precesión) y crece hasta 12° cerca de 0 RPM. Además echa **humo** y **chispas sueltas**, cada vez más cuanto menos RPM le quedan. Por ahora el bamboleo es solo visual y no afecta al control. Se ajusta en `CombatConfig` → `lowSpinThreshold`, `lowSpinWobbleAngle` y `lowSpinWobbleFrequency`.
- **Estela y chispas:** al moverse deja una estela de píxeles de su color (más grande y opaca durante ataques y dash). Además, mientras dura el acelerón de un ataque o un dash deja una **estela continua** de su color, para que el impulso se note aunque ya fuera rápida. Si va rápido con muchas RPM, la punta saca chispas contra el suelo.
- **Giro visual:** la velocidad a la que se ve girar la peonza no es proporcional a las RPM. Sigue una curva de forma logarítmica: se mantiene casi al máximo durante casi toda la vida y solo se frena de golpe cuando las RPM están muy cerca de 0. Por defecto gira al 90% con un 8% de RPM y al 94% con un 10%. Solo por debajo del 5% se nota que frena. El punto de frenado se ajusta en `CombatConfig` → `visualSpinKnee`.
- **Control:** según la velocidad y la masa, cada peonza tiene un control más o menos estable. Las diferencias entre ligeras y pesadas están **suavizadas** para que ninguna sea incómoda: las velocidades máximas se acercan a una de referencia (`speedSpread` 0,5 y `referenceMaxSpeed` 15,5), el giro y la aceleración se reparten por peso entre `turnByWeight` y `accelerationByWeight`, y la masa física tiene un mínimo (`minPhysicalMass` 0,6) para que las ligeras no se sientan flotantes ni aceleren de golpe. Resultado medido por preset: velocidad Agilidad 19 · Ataque 15,8 · Balanceada 15,4 · Defensa 14,2; giro Agilidad 0,195 · Defensa 0,12; aceleración real Agilidad 173 m/s² (antes ~400) · Defensa 18.
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
- **Cargas de ataque:** cada peonza tiene un número de cargas que se gastan al atacar y se recuperan con el tiempo. La media es **3 cargas**. Las piezas ligeras dan más cargas y las pesadas menos. Se muestran como puntos debajo de la barra de vida.
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
- **Ataque:** fuerza de impacto en las colisiones, que depende de su velocidad al chocar (la masa solo cuenta para el empuje, ver 2.5).
- **Defensa:** resistencia al daño y al empuje.
- **Eliminación:** una peonza queda eliminada **únicamente cuando sus RPM llegan a 0**. No hay ring-out: los bordes de la arena impiden salir.

## 2.7 Objetivos de equilibrio (C10)

Lo que tiene que salir, escrito antes de tocar valores (método en el registro de decisiones). El banco de pruebas lo mide y marca lo que se sale (±10%).

| Objetivo | Valor |
|---|---|
| Duración, Balanceada contra Balanceada | Se aceptan duelos más largos (**~90-120 s**): con ~20 por golpe, 60 s pediría un golpe cada ~2 s |
| Dash | ≈ 75% de un ataque rápido |
| Ataque rápido, Balanceada contra Balanceada | ≈ **20 RPM** |
| Cargado 1 / 2 / 3 | ×1,25 / ×1,5 / ×1,75 del rápido (≈ 25 / 30 / 35) |
| Tope de un solo golpe (cualquier arquetipo, a tope, cargado 3) | **60 RPM** (~10% de una Balanceada) |

| Arquetipo (Balanceada = 1) | Daño que hace | Daño que recibe |
|---|---|---|
| Ataque | 1,25 | 1,10 |
| Defensa | 0,75 | 0,65 (aguanta ~1,5 veces más) |
| Agilidad | 1,10 (y golpea más a menudo) | 1,20 |
| Balanceada | 1 | 1 |

**Banco de pruebas:** menú *FakeBlade → Banco de equilibrio* con Play en la escena Sandbox (necesita un dummy). Cada preset golpea a cada preset con rápido, cargado 1-3 y dash, desde parado (P), saliendo a su velocidad máxima (T) y llegando tarde (L, a 5 m). Mientras dura (~7 min) los dummies están quietos, sin trucos, a velocidad ×1 y sin grabar la sesión; al acabar todo vuelve a como estaba. Informe en `Logs/Balance/` (.md con las tablas y .csv con cada golpe).

3. # Peonzas Modulares

Cada peonza se compone de **piezas intercambiables** que modifican sus estadísticas. Los jugadores montan su peonza antes de cada combate.

| Pieza | Afecta sobre todo a |
|---|---|
| Punta (Tip) | Estabilidad (desgaste de RPM) y velocidad de movimiento |
| Cuerpo (Body) | Peso e inercia |
| Disco (Blade) | Ataque y defensa |
| Núcleo (Core) | RPM máximas y **habilidad especial** |

Estadísticas que modifican las piezas: velocidad de rotación (RPM máx. y desgaste), peso/inercia, potencia de ataque, resistencia/defensa, velocidad de movimiento, fuerza de dash y número de cargas de ataque.

Cada pieza tiene una clase de peso (Ligera / Media / Pesada) y **un arquetipo** (Ataque, Balanceada, Defensa o Agilidad: los mismos tipos A, B, C y D de los modelos). El arquetipo dice a qué estilo pertenece la pieza y se muestra en la selección para elegir sin confusiones. El número de piezas no importa: más adelante habrá piezas con cualidades concretas (por ejemplo, dos características equilibradas y otras muy débiles), y cada una llevará su arquetipo.

| Pieza | Arquetipo |
|---|---|
| Needle Point, Aero Shell, Velocity Core (Rayos) | Agilidad |
| Flat Base, Standard Frame, Balanced Ring, Endurance Core (Spin Boost), Frost Core (Hielo) | Balanceada |
| Wide Ball, Iron Fortress, Crush Wheel, Fortress Core (Defensa), Impact Core (Onda de choque) | Defensa |
| Razor Edge, Blaze Core (Fuego) | Ataque |

**Arquetipos.** Los arquetipos no son peonzas fijas: son el **resultado de combinar las piezas**. Según las estadísticas finales, la peonza encaja en uno:

- **Ataque:** más cargas de ataque, masa media y velocidad media.
- **Defensa:** menos cargas, más masa y velocidad media.
- **Agilidad:** cargas medias, masa baja y velocidad alta.
- **Balanceada:** todo medio.

**Rasgos de las piezas** *(implementados; primeras piezas con rasgos el 2026-10-07)*. Además de sus estadísticas, una pieza puede tener **rasgos**: efectos propios sobre el combate en porcentaje, que hacen piezas con personalidad y con contrapartidas. Por ejemplo:

- Un disco que da **+30% de energía del especial con los ataques cargados**.
- Una punta con **dash con un 25% menos de espera pero un 15% más caro**.
- Un cuerpo que **recarga los ataques más rápido** pero **los encarece**.

Tipos disponibles (`PartTraitType`): energía del especial por golpe rápido, por dash, por cargado, por parry y total; espera y coste del dash; tiempo de recarga y coste de los ataques; y tiempo de carga del ataque cargado. Se ponen en el asset de la pieza (lista *Rasgos*, en tanto por uno: 0,25 = +25%) y se suman entre piezas. Añadir un tipo nuevo es añadirlo al enum y aplicarlo donde toque. Pendiente: más piezas con rasgos y mostrarlos en la selección (ver Quehaceres H8).

Rasgos actuales (equilibrio del 2026-10-07):

| Pieza | Rasgo | Por qué |
|---|---|---|
| Wide Ball, Iron Fortress, Crush Wheel (pesadas) | Espera del dash **+20%** cada una (las tres: +60%, 2,9 s) | La Defensa aguanta y contraataca; no debe vivir del dash. |
| Needle Point | Coste de los ataques **−25%** | La Agilidad ataca mucho y barato. |
| Aero Shell | Recarga de los ataques **−20%** | Ídem: más ataques rápidos, en vez de dashes. |

**Ataque de las piezas** (mismo ajuste): Crush Wheel 15 → **6**, Iron Fortress 5 → **0** (la Defensa pegaba más que el Ataque), Razor Edge 8 → **12** (el disco de Ataque es el que más pega) y Aero Shell −3 → **0**.

**Núcleos** (uno por poder). Deben notarse, pero sin decidir la partida: todos dan **+50 RPM máximas** y, además, una **ventaja y un coste del mismo tamaño** (2 puntos), a juego con su poder. Los poderes más fuertes ya pagan con la energía que necesitan. 1 punto = 25 RPM = 0,5 de velocidad = 0,1 de peso = 2 de ataque = 3 de defensa = 1 de dash = 0,2 de desgaste.

| Núcleo | Poder | Clase | Ventaja | Coste |
|---|---|---|---|---|
| Endurance Core | Spin Boost | Media | +50 RPM (total +100) | −4 ataque |
| Impact Core | Onda de choque | Pesada | +0,2 peso | −1 velocidad |
| Fortress Core | Defensa | Pesada | +6 defensa | −2 dash |
| Velocity Core | Rayos | Ligera | +1 velocidad | −0,2 peso |
| Blaze Core | Fuego | Media | +4 ataque | −6 defensa |
| Frost Core | Hielo | Media | −0,4 desgaste | −50 RPM (total 0) |
| Phantom Core *(con C5)* | Fantasma | Ligera | +2 dash | +0,4 desgaste |

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

Las arenas serán circulares y con forma de cuenco, con un **borde que impide salir** y una fricción normal que no afecta al movimiento.

La primera arena usa el modelo **`Arena 00.fbx`**, escalado al tamaño de juego. Cada arena es un prefab con sus puntos de aparición y un asset de datos (nombre y vista previa) que aparece en la selección de escenario.

Cuando todo funcione, se añadirán **hazards**: suelos con distintas fricciones y efectos (hielo que desliza, suelos que aceleran) y zonas que afectan a las estadísticas. También powerups en el escenario que mejoren los ataques o recuperen algo de RPM.

5. # Sistema de Poderes

El poder especial lo define el **Núcleo** montado: para cambiar de especial se cambia el núcleo de la peonza. Habrá un núcleo por poder.

- **Carga:** el poder se carga con cada **golpe acertado** (ganar el choque) según el **tipo de golpe**, no según el daño: ataque rápido 8%, dash **6%** (antes 10%: el dash ya no es la vía para cargarlo), **ataque cargado 12% + 5% por nivel** (nivel 3 = 27%: gastar cargas se recompensa), ganar un choque sin atacar 3%, parry 20%; en un choque parejo, la mitad. Todo × `specialEnergyMultiplier` (**1,2**; antes 1,5, con el que salía cada 20-30 s), para que el especial llegue hacia la mitad del combate sin repetirse demasiado (efectivo: rápido ~10%, dash ~7%, cargado nivel 3 ≈ 32%). Golpear a una Defensa activa también cuenta; golpear a un aliado, no. También se carga al recoger powerups del escenario. **Cada poder necesita una cantidad distinta de energía** para llenarse: los más fuertes tardan más (columna *Energía* de la tabla; 1 = la barra actual). Mientras el poder está activo **no se gana energía**: al terminar, la barra empieza de 0. Valores en `CombatConfig` → `specialEnergy*`.
- **Activación:** cuando la carga está llena, se pulsa Especial. Mientras está activo, la carga se va vaciando con el tiempo. Todos duran **5 s**. Cuando se vacía, el poder termina. Excepción: Rayos dura hasta 6 s o hasta que da su golpe fuerte (ver tabla).
- **Nombres:** simples, en español e inglés: Defensa / Defense, Fuego / Fire, Hielo / Ice, Rayos / Lightning, Fantasma / Ghost. Spin Boost y Onda de choque mantienen los suyos.

**Efecto común a todos los poderes (al activarse)** *(implementado)*:

- Recupera el **25% de las RPM máximas** (mismo valor para todos, ajustable en `CombatConfig`), **poco a poco en 2 s** (`specialHealTime`), no al momento.
- **Rellena todas las cargas de ataque** disponibles en ese momento.

**Curaciones de los poderes:** todas se aplican con el tiempo (la del efecto común y la de Spin Boost). Si mientras tanto la peonza recibe un **golpe de ataque enemigo** que le quita RPM (ataque rápido, cargado o dash; también en un parry), **se corta lo que quede de curación** hasta el próximo poder: sale un humo, vibra el mando y el sandbox lo apunta ("CURACIÓN CORTADA"). **No la cortan** las paredes, el roce, las peonzas pegadas, la quemadura ni el daño de poderes, ni los choques en los que el rival no atacaba.

Después, durante el tiempo activo, cada poder tiene su efecto propio.

Poderes (se fusionan los que ya había con los nuevos: 7 núcleos en total):

| Poder | Estado | Energía | Efecto propio |
|---|---|---|---|
| Spin Boost | Implementado (se rehará más adelante) | 1 | Recupera RPM progresivamente mientras está activo: **4% de las RPM máximas por segundo** (antes 6%; con el efecto común, 45% en total en vez de 55%). Un golpe de ataque enemigo la corta. |
| Onda de choque | Implementado (se rehará más adelante) | 1 | Al activarse empuja y quita RPM a las peonzas cercanas. |
| Defensa *(sustituye a Storm Breaker)* | Implementado (C1) | 1,2 | Defensa casi al 100%: no pierde RPM por golpes, paredes, quemadura ni desgaste, y el empuje que recibe es casi nulo (**10%**, también el del choque físico). Además, sus ataques se recargan **1,5 veces más rápido**, pero se mueve un **15% más lento**. El dash solo cuesta el **10% de lo normal**, pero su impulso (alcance) baja un **40%**. Atacar sí cuesta RPM. Valores en el asset `Defense`. |
| Fuego *(sustituye a Rastro de fuego)* | Implementado (C2) | 1 | Sus golpes **queman** al enemigo: 1,5% de sus RPM máximas cada 0,5 s durante 3 s (9% en total). Un golpe nuevo reinicia la duración; no se acumula. Cuenta como golpe **cualquier choque en el que le quite RPM al rival**: lo gane o no, también si es el rival quien la embiste, y en un parry. El roce continuo no quema. Valores en el asset `Fire`. |
| Hielo | Implementado (C3) | 1 | Aura de hielo y humo blanco de frío. A los enemigos que golpea los **congela** durante 3 s: −35% de velocidad de movimiento y recarga de ataques a la mitad. Un golpe nuevo reinicia la duración; no se acumula. Cuenta como golpe lo mismo que en Fuego: cualquier choque en el que le quite RPM al rival. Valores en el asset `Ice`. |
| Rayos *(sustituye a Dash eléctrico)* | Implementado (C4) | 0,8 | Entra en modo cargado hasta 6 s, con sus partículas. Solo cuenta **su velocidad hacia el rival**: si la embisten estando quieta, es un choque normal. **Choques pequeños** (de 1 a 6 m/s): empujan al enemigo x1,8, sin daño extra ni lanzada, y no gastan el poder. **Choque fuerte** (6 m/s o más): golpe con +20% de daño y empuje x2,5, deja al rival **lanzado** 0,5 s y gasta el poder, con un destello. Contra una peonza congelada (o invulnerable) es un choque normal y el poder no se gasta. Valores en el asset `Lightning`. |
| Fantasma | En el enum (A5); asset y efecto en C5 (aplazada hasta empezar D1) | 1,3 | Crea **un clon fantasma** de sí misma, invulnerable, que dura lo que el poder y va a la velocidad de su dueño. Persigue al enemigo más cercano; sus golpes quitan el 10% del daño normal, pero empujan y cortan la carga de ataque del rival: sirve para molestar. No hace parry ni se le puede hacer. |

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
| Rayos | Rayos amarillos que chisporrotean alrededor, más chispas que saltan (hecho, heredado de Dash eléctrico). Golpe fuerte: destello con rayos blancos y amarillos, estrellas, sacudida de cámara y un pequeño parón (hecho). |
| Fuego | Corona de llamas rojas y amarillas alrededor de la peonza, más brasas que saltan (hecho). Las peonzas quemadas llevan sus llamitas y el icono de llama (estado Quemadura). |
| Hielo | Cristales que flotan alrededor de la peonza y humo blanco de frío que se arrastra por el suelo (hecho). Las peonzas congeladas llevan su escarcha, vaho frío y el icono de copo (estado Congelación). |
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
- **Sandbox (campo de pruebas):** sustituye al modo Práctica. Tiene **escena propia** y se usa tanto para practicar como de **entorno de debug**. Ver 6.3. *(Implementado en su primera versión, fase B.)*
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
- **Escenas:** `MainMenu` (menú principal, opciones, controles e información, con la arena de fondo), `Assembly` (unirse, montar peonzas y parámetros de partida), `BattleArena` (combate) y `Sandbox` (campo de pruebas: `BattleBootstrap` + `SandboxController` con su panel y sus dummies). La configuración pasa de una escena a otra en `MatchSetup`.
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
| 2026-10-08 | Arquetipo de la peonza | Es el que más se repite entre sus 4 piezas (punta, cuerpo, anilla y núcleo); con empate en cabeza, Balanceada. Antes se deducía de las estadísticas y, con los valores nuevos, el preset Ataque salía como Agilidad. |
| 2026-10-07 | Dispositivo por jugador | En Controles se elige el dispositivo de cada jugador (Teclado J1, Teclado J2, Mando 1..N), al momento desde la pausa y guardado como preferencia, para probar con mando cómodamente (9.2.4). |
| 2026-10-07 | Vista previa de piezas | Al enseñar una pieza en la selección, la peonza se queda quieta para ver bien el cambio (H7). |
| 2026-10-07 | Golpes de pared | Uno por peonza cada 0,1 s; si llega otro más fuerte en ese tiempo, solo se suma la diferencia. El golpe directo de Rayos no se toca hasta volver a medirlo. |

12. # Pendiente de definir

Entre paréntesis, la propuesta por defecto si no se decide otra cosa.

- Valores de equilibrio: coste en RPM de ataque y dash, tiempos de recarga, fracción de daño de la peonza rápida, etc. Se ajustan en el asset `CombatConfig`.
- Qué otros efectos de postprocesado se añaden a Opciones.
- **Estadísticas de las piezas nuevas** (H9): Striker Point, Assault Frame y Gale Ring tienen valores provisionales; se fijan en el pase de equilibrio. También queda por decidir si los presets usan las piezas de su arquetipo (hoy el preset Agilidad lleva Razor Edge, la anilla de Ataque, y el preset Ataque lleva Flat Base, Aero Shell y Crush Wheel).
- **Ataque rápido frente a cargado** (registro del 2026-10-07, Agilidad): el rápido quitaba lo mismo o más que el cargado (rápido 27 de media, hasta 45; cargado 2 ≈ 26; cargado 3 ≈ 30). El daño del cargado no cuenta la velocidad extra de la carga y solo suma +10% por nivel, y mientras se cargaba se iba al 60% de velocidad, así que un rápido lanzado a toda velocidad lo igualaba. **Resuelto:** sin el freno al cargar y con +25% por nivel (nivel 3 = ×1,75, probado en el probe). Queda vigilar los picos: Agilidad con un cargado 3 a velocidad máxima llega a ~107 en el probe; se revisa con el banco de pruebas (C10).
- Collider por pieza: hoy es una esfera fija de 0,6 m para todas; las anillas más anchas sobresalen un poco (propuesta: dejarlo así; si en partida se nota, que el radio dependa del disco).
- Sonido al pulsar un botón (confirmar) y al volver atrás en los menús (alguno de los cortos del pack Casual, distinto de DM-CGS-01, que es el de moverse entre botones).
- Autoeliminación: cuántos segundos sin recibir golpes hacen que cuente como autoeliminación (los mismos 5 s que dan el punto de K.O.).

**Poderes:**

- Nuevo diseño de Spin Boost y Onda de choque (se rehacen más adelante; mientras tanto se quedan como están).
- Lanzada en arenas con forma de cuenco: la peonza lanzada sube por la pendiente (cuenta como suelo) y casi nunca llega a golpear una pared, así que el daño de pared de la lanzada apenas sale en la arena actual (se revisa con las arenas y en C7).
- Rayos: daño real del golpe fuerte (registro del 2026-10-07, 10 golpes contra un dummy de 580 RPM): el golpe en sí quita ~38 (+20%), pero con los choques contra la pared de la lanzada el total sale en **~105 RPM de media (62-161)**, ~18% de la vida. Se nota poco porque casi todo llega después, por la pared. El doble golpe de pared ya está corregido (C9). Con el daño global a ×0,5 (probe, preset Agilidad con dash): golpe directo ~25-28 y un golpe de pared de 12-58, total **39-90** cuando llega a la pared (antes 115-152). Si sigue siendo mucho, la idea es que Rayos tarde más en cargarse: hoy necesita 0,8 de energía, menos que la barra normal (propuesta: subirla a 1-1,2 en C7, después de probar el daño nuevo).
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
- [ ] C5. Fantasma: un clon invulnerable que persigue (necesita la persecución básica de D1), 10% de daño, empuja y corta cargas, sin parry + aspecto translúcido. **Aplazada:** se hace al empezar D1, reutilizando su persecución. Incluye crear su núcleo (Phantom Core, tabla en 3) y añadirlo al catálogo.
- [x] C6. Núcleos en el catálogo: uno por poder, con estadísticas. Hecho: 6 núcleos (Fantasma, con C5) con los valores de la tabla de 3, nuevos `Core_Medium_Fire` y `Core_Medium_Ice`, preset Ataque con Fuego y nombre del núcleo con el color de su poder en la selección. Los 4 presets siguen dando su arquetipo. Falta verlo en la selección con mandos. 🎮
- [ ] C7. Equilibrio de valores de cada poder en partida. 🎮
- [ ] C10. Banco de pruebas de equilibrio (método en el registro de decisiones): herramienta del sandbox o del editor que enfrenta cada preset contra cada preset con golpes de prueba fijos (parado y a velocidad máxima) y saca una tabla de daño dado, daño recibido y golpes hasta el K.O., para compararla con la tabla de objetivos (combate de 60 s en Balanceada contra Balanceada). Hecho: tabla de objetivos (2.7) y banco (`BalanceBench`, menú *FakeBlade → Banco de equilibrio*). Primer informe (2026-10-07, `Logs/Balance/bench_2026-10-07_13-09-31.md`): lo que reciben los arquetipos cumple; lo que hacen no (Ataque 1,5, Defensa 1,02 y Agilidad 1,88, frente a 1,25 / 0,75 / 1,1); 28 golpes pasan de 60 (todos a tope, sobre todo cargados de Agilidad y Ataque, hasta 135); espejo de Balanceada ≈ 86 s. Fórmula común decidida: golpe base + velocidad ±25% (2.5). Segundo informe (`bench_2026-10-07_13-24-37.md`): 11 golpes pasan de 60 (máx. 84, antes 135), Balanceada rápido 17 / 25 y cargado 3 27 / 44 (cumple); lo que hacen los arquetipos sigue alto (Ataque 1,45, Defensa 1,11, Agilidad 1,59) porque sale del ataque de sus piezas (Balanceada tiene el más bajo, 11); el dash ha quedado flojo (Balanceada 9-11) y el espejo de Balanceada sale en ~100 s. Ajustes (decididos con el usuario): dash ×0,5 → ×0,75; duelos más largos aceptados; cada preset con las piezas de su arquetipo (Ataque: Striker Point + Assault Frame + Razor Edge + Blaze Core; Agilidad: Gale Ring en vez de Razor Edge); golpe base 50 → 42; ataque de piezas: Balanced Ring 9, Striker Point 2, Assault Frame 2, Razor Edge 3, Crush Wheel 0, Gale Ring 6; defensa: Striker Point 0, Assault Frame +4, Razor Edge +4, Gale Ring 0, Needle Point −3. El banco además espera a que el ataque salga de verdad antes de medir (antes un rápido a tope a veces salía tarde). Cuarto informe (`bench_2026-10-07_14-00-58.md`): **todos los arquetipos cumplen** (hace / recibe: Ataque 1,28 / 1,1; Defensa 0,71 / 0,67; Agilidad 1,1 / 1,22), Balanceada rápido 17 / 23 y cargado 3 27 / 43, dash ≈ 75% del rápido, espejo de Balanceada ≈ 97 s; solo un golpe pasa de 60 (Ataque cargado 3 a tope contra Agilidad, 67). Falta: probarlo con mando y decidir si se recorta ese último pico.
- [x] C8. Ajuste tras las pruebas del sandbox (2026-10-07): dash con menos daño, más caro y más espera; ataques con más daño; Defensa con menos ataque y dash más lento (rasgos); Agilidad con ataques baratos y rápidos (rasgos); especial más lento; curaciones con el tiempo y cortadas por golpes de ataque; paredes como un choque parejo; peonzas pegadas que se separan; estela del acelerón y vibración. Probado con un probe en el sandbox: daños por preset (tabla en 2.5), 2 separaciones en 2 s con dos peonzas empujándose, curación de 62% a 70% en 0,5 s, cortada por un ataque del dummy (se queda en 69%) y sin golpes llega al 100%. Probado con mando por el usuario (2026-10-07): el dash está bien equilibrado y ya no se abusa, la vibración al atacar se siente muy bien y la Agilidad ya no es débil.
- [x] C9. Daño más bajo y golpe de pared de Rayos. Primera prueba (bonus de masa del ataque a la mitad en el daño): con mando seguían saliendo golpes de 40-60. Ahora: la masa solo cuenta para el empuje, daño global ×0,5 y golpe con dash ×0,5; un solo golpe de pared por peonza cada 0,1 s (`wallHitCooldown`). Probado con un probe en el sandbox: daños de referencia en 2.5 (desde parado 6-27, en marcha 19-36; el Ataque pega más con el cargado que con el dash y solo la Agilidad saca más con el dash que con el rápido) y Rayos 39-90 con la pared (antes 115-152). El doble golpe en la unión de dos tramos no se ha podido reproducir en el probe (72 choques contra la pared sin ninguno), así que esa corrección solo se ha comprobado leyendo el código. Probado con mando el 2026-10-07: el daño está bien en general; el cargado se ajustó después (registro de decisiones, «Daño del cargado»).

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
- [ ] H8. Rasgos de las piezas (ver 3): la base está hecha (`PartTrait`, lista *Rasgos* en cada pieza, aplicados en energía del especial, dash, recarga, costes y carga). Primeros rasgos puestos el 2026-10-07 (dash de las pesadas, ataques de las ágiles; tabla en 3). Falta diseñar más piezas con rasgos, mostrarlos en la selección (bajo el arquetipo, o al tener la pieza seleccionada) y probarlos en el sandbox.
- [ ] H9. Una pieza por arquetipo en punta, cuerpo y anilla (4 de cada, con su modelo A-D). Faltaban los ScriptableObject (el script de piezas solo creaba una por clase de peso): creadas **Striker Point** (punta de Ataque, `Punta_Type_A`), **Assault Frame** (cuerpo de Ataque, `Body_Type_A`) y **Gale Ring** (anilla de Agilidad, `Ring_Type_D`) en `FakeBladeComponentPresets` y en el catálogo, y salen en todos los menús de selección (comprobado con mando el 2026-10-07). Falta: fijar sus estadísticas y decidir los presets en el pase de equilibrio.
- [ ] H7. Selección de piezas: al tener el foco en una fila de pieza (punta, cuerpo, disco o núcleo), la vista previa mueve la cámara o el modelo para enseñar esa pieza (por ejemplo, de lado para la punta, desde arriba para el núcleo). Mientras se enseña una pieza, **la peonza se queda quieta** (sin girar), para que el jugador vea bien cada cambio; girando no se aprecia. Vuelve a girar al salir de las filas de piezas.
- [x] H6. Imagen del núcleo (plano `Imagen de Nucleo` con el icono pixel del poder, teñido con su color). **Aplicada para probarla (2026-10-06)**: `CoreImage`, creada por `BladeModel` con el núcleo; va en el pivote de inclinación (se inclina con la peonza pero no gira) y se orienta a la cámara cada frame. Dos modos en el prefab (`FakeBladeController` → Model Settings → Core Image Mode): **calcomanía** (tumbada sobre el núcleo, por defecto) y **cartel** (de pie, de frente a la cámara). La orientación de la textura sale de las UV de la malla (el FBX del plano necesita Read/Write activado, ya puesto). Iconos en `PowerIcons` (Fuego, Hielo y Rayos usan los de su estado; flecha, ondas, escudo y fantasma para el resto; el icono del asset del poder tiene prioridad). **Se queda como opción**: Opciones → Juego → Icono del núcleo, desactivada por defecto. Falta verla con mandos y a pantalla completa. 🎮
- [x] H4a. Script de Blender para exportar cada pieza a su FBX (`Tools/Blender/export_blade_parts.py`). Probado con Blender 5.0: en Unity llegan con rotación 0, escala 1, Y arriba y montadas en su sitio (la punta, con escala negativa en Blender, sin caras invertidas). No exporta los tipos 0 (modelos base). Exportadas al proyecto el 2026-10-06: 15 piezas en `Assets/3D Models/Bayblade 01/Parts/`.
- Nota sobre las UV de las piezas: no hace falta arreglarlas mientras se pinten con color plano (`_BaseColor`) y sin texturas ni lightmaps. Solo importan en las piezas que lleven textura (como el plano de la imagen del núcleo, que ya las tiene bien).
- [x] H4b. Modelos de las piezas en las peonzas: cada pieza del catálogo apunta a su modelo (el del tipo de su arquetipo) y la peonza se monta con las piezas equipadas, en partida, en la selección, en Opciones y en el fondo del menú; también al cambiar una pieza en caliente. Probado en el sandbox y en la selección con 4 jugadores: escala correcta (los modelos ya están a la escala del juego), apoyadas en el suelo, modelo antiguo oculto y colores por pieza. El collider (esfera de 0,6 m de radio) coincide con el borde de las anillas; solo sobresalen un poco las puntas de algunos anillos (hasta 0,13 m el A). 🎮

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

**Otros pendientes**

- [ ] Modo por puntos: restar 1 punto por autoeliminación.
- [ ] Asignar la ventana de parry a las piezas (agilidad +0,03 s, ataque −0,01 s, defensa −0,03 s).
- [ ] Asignar sonidos (choque, ataque, dash, especial, parry, K.O.) y música (ver G4 y G5).
- [ ] Fuente pixel para los textos. Candidatas gratis con licencia OFL: Press Start 2P, Silkscreen o Pixelify Sans.
- [ ] Pixelar la escena 3D de batalla y el fondo del menú (pilar 1.2). Ahora solo lo tiene la vista previa del lobby (`BladePreviewStage`). Renderizar a una RenderTexture de baja resolución con escala entera y filtro Point; prueba rápida: Render Scale de URP entre 0,33 y 0,5 con filtro Nearest-Neighbor. El HUD (Overlay) no se ve afectado.
- [ ] Equilibrio general. Primer ajuste hecho (2026-10-06): daño x0,8, RPM base 400, cargado = rápido × (1 + 10% por nivel) y movimiento más parejo. Siguiente: jugar con mandos y revisar los CSV del sandbox (`Logs/Sandbox/`). Antes, una peonza lanzada que chocaba a 12 m/s contra una de ataque fuerte perdía un 42%. 🎮
- [ ] Probar menús, lobby y combate con mandos reales. 🎮
- [ ] Probar builds de PC y WebGL.
- [ ] Probar el ratón en las columnas del lobby.
- [ ] Input handling en "Input System" solamente (ahora está en "Both").
- [ ] Quitar `Assets/Scripts_copiaAntigua.zip` y el stash antiguo de backup cuando ya no hagan falta.
- [ ] Decidir si la dependencia de Coplay se queda en `Packages/manifest.json`.
- [ ] Tests automáticos de las reglas de combate (choque, parry, dash).
