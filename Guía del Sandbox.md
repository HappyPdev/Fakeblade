# Guía del Sandbox

El sandbox es el campo de pruebas de FakeBlade: una partida sin final en la que puedes cambiar casi todo **sin salir** (rivales, piezas, trucos, velocidad del juego, valores de combate) y ver qué pasa en cada choque. Esta guía explica cada opción y cómo leer el archivo de datos que guarda cada sesión.

## 1. Entrar

1. En el menú principal, **JUGAR**.
2. Únete en la selección de peonzas (mantén ataque) y elige tu peonza. Pueden entrar hasta 4 personas.
3. En los parámetros de partida elige el modo **Sandbox** (con un solo jugador es el único modo).

## 2. El panel

- **Abrir y cerrar:** **Tab** en teclado o **Select / Back** en mando (se cambia en Opciones → Controles). Al abrirlo el juego se pausa; al cerrarlo sigue al momento, sin cuenta atrás.
- **Moverse:** arriba/abajo para elegir fila, izquierda/derecha para cambiar el valor, aceptar para los botones. También con ratón.
- **Atrás** (Esc / B): en una sección vuelve al panel principal; en el principal, cierra el panel.

El panel principal:

| Fila | Qué hace |
|---|---|
| **Rivales** | Abre la sección de rivales (dummies). |
| **Mi peonza** | Abre la sección para cambiar las piezas en caliente. |
| **Trucos** | Abre la sección de trucos. |
| **Debug** | Abre la sección de ayudas visuales y de guardado de datos. |
| **Ajustes** | Abre la sección de arena y valores de combate. |
| **Velocidad** | x1, x0,5, x0,25 o PAUSA (se aplica al cerrar el panel). |
| **Reiniciar todo** | Devuelve todas las peonzas a su sitio con RPM y cargas llenas, energía a 0 y sin estados, y cierra el panel. |

Todo lo que eliges se mantiene mientras sigas en el sandbox (también si reinicias o cambias de arena). Al **salir** del sandbox, la velocidad y los valores de combate vuelven a los normales.

## 3. Rivales

| Fila | Opciones |
|---|---|
| **Rivales** | Ninguno o de 1 a 3 dummies (máximo 4 peonzas en total). Cambiarlo reinicia la partida al cerrar el panel. |
| **Dummies** | Qué hacen todos: **Quietos**, **Se mueven**, **Atacan** (ataque rápido hacia ti: sirve para practicar el parry), **Dash hacia ti** o **Especial** (llenan la esfera y usan su poder). |
| **Cada** | Cada cuántos segundos hacen su acción: 0,5 / 1 / 2 / 3 s. |

## 4. Mi peonza

Cambia la **punta**, el **cuerpo**, el **disco** y el **núcleo** al momento: cambian el modelo, las estadísticas y las cargas. Debajo de cada pieza se ve su arquetipo; el núcleo sale con el color de su poder. Al cambiar el núcleo, el poder pasa a ser el nuevo y la esfera empieza vacía.

Si hay varios jugadores, la primera fila (**Jugador**) elige de quién son las piezas.

## 5. Trucos

Cada truco se aplica a **nadie**, a los **jugadores**, a los **dummies** o a **todos**:

| Truco | Efecto |
|---|---|
| **RPM infinitas** | Las RPM se rellenan siempre: no se pierde nunca. |
| **Especial lleno** | La esfera se rellena cada vez que se vacía (el poder se puede usar sin parar). |
| **Cargas infinitas** | Las cargas de ataque no se gastan. |
| **Dash sin espera** | El dash está siempre disponible. |
| **Invulnerable** | No recibe daño ni estados. |

Ejemplo: *RPM infinitas → Dummies* para golpear un saco que no cae; *Invulnerable → Jugadores* para estudiar los ataques de los dummies sin perder.

## 6. Debug

Todo empieza apagado. Cada fila es un Sí/No:

| Fila | Qué se ve | Cómo leerlo |
|---|---|---|
| **Ventana de parry** | Un **anillo cian** alrededor de la peonza. | Aparece en los primeros instantes de un ataque rápido: si en ese momento te golpea un ataque enemigo, haces **parry**. |
| **Velocidades** | Una **flecha** del color del jugador. | Apunta hacia donde va la peonza; su largo es lo que recorrerá en 0,25 s. Sirve para ver quién llega más rápido a un choque (el más rápido gana). |
| **Números de daño** | Números que suben sobre la peonza. | Las RPM que pierde: **blanco** (menos de 10), **naranja** (10-29) y **rojo** (30 o más). El roce continuo no saca números. |
| **Estados** | Texto bajo la peonza. | "QUEMADA 2,1 S", "CONGELADA", "LANZADA" o "INVULNERABLE", con el tiempo que le queda. |
| **FPS** | Contador arriba a la derecha. | Fotogramas por segundo. |
| **Registro** | Los últimos 6 eventos abajo a la izquierda. | Choques ("CHOQUE J2 -2 · J1 -56": lo que pierde cada uno), parrys, especiales, estados, K.O. y curaciones cortadas por un golpe ("CURACIÓN CORTADA"). Cada jugador va con su color; desaparecen a los 6 s. |
| **Guardar datos** | Nada en pantalla. | Guarda la sesión en un archivo (ver punto 9). **Activado por defecto.** |

Las ayudas usan tiempo real: a cámara lenta se leen igual.

## 7. Velocidad (cámara lenta y frame a frame)

En el panel principal, **Velocidad**:

- **x0,5 / x0,25:** cámara lenta, para ver un choque con calma.
- **PAUSA:** al cerrar el panel el juego se queda congelado. Pulsa **«.»** (teclado) o el **clic del stick derecho** (mando) para **avanzar un fotograma**. Abajo se recuerda cómo.

La pausa normal del juego (Esc / Start) sigue funcionando: al reanudar, vuelve a la velocidad elegida.

## 8. Ajustes

| Fila | Qué hace |
|---|---|
| **Arena** | Cambia de arena. Al cerrar el panel se recarga el sandbox en ella, con todo lo demás igual. |
| **Daño global** | Multiplica todo el daño de golpes, paredes y roce (no la quemadura). |
| **Daño por carga** | Daño extra del ataque cargado por cada nivel (+10% = nivel 3 hace ×1,3). |
| **Ventana parry** | Cuánto dura la ventana de parry. |
| **Coste ataque** | RPM que cuesta un ataque rápido (% de las máximas). |
| **Coste dash** | RPM que cuesta el dash (% de las máximas). |
| **Daño ataque** | Daño de los golpes con ataque (rápido o cargado) frente a un choque sin atacar (x1,2). |
| **Daño dash** | Daño de los golpes con dash (x0,75: el dash ya llega muy rápido). |
| **Daño choque** | Daño por velocidad de choque. |
| **Empuje** | Empuje base del choque. |
| **Energía especial** | Ritmo de carga del especial: multiplica la energía de cada golpe acertado (rápido, dash, cargado...). |

Cada valor va de x0,5 a x2 sobre el suyo (se ve el valor real). Son **temporales**: el sandbox juega con una copia y el archivo de configuración no se toca. **Restaurar valores** vuelve todo a x1. Si un ajuste convence, hay que pasarlo a mano a `Assets/Settings/CombatConfig.asset`.

## 9. El archivo de datos

Con **Guardar datos** activado, cada vez que entras al sandbox se crea un archivo:

- **En el editor:** `Logs/Sandbox/sandbox_AAAA-MM-DD_HH-MM-SS.csv`, en la carpeta del proyecto (junto a `Assets`; no se sube al repositorio).
- **En una build:** en la carpeta de datos del juego, subcarpeta `SandboxLogs`.

Cambiar de arena empieza un archivo nuevo. Se puede abrir con Excel u otra hoja de cálculo (separador **punto y coma**, decimales con **punto**) o pasárselo a Claude para que lo analice.

### Columnas

`t ; real ; event ; player ; other ; v1 ; v2 ; v3 ; v4 ; info`

- **t:** segundos de juego desde que empezó la escena (a cámara lenta avanza más despacio).
- **real:** segundos reales.
- **event:** qué ha pasado (tabla de abajo).
- **player / other:** jugadores implicados (J1, J2...). Los dummies también tienen número (J2, J3...).
- **v1-v4:** números del evento.
- **info:** datos extra en forma `clave=valor|clave=valor`.

### Eventos

| Evento | player / other | v1 | v2 | v3 | v4 | info |
|---|---|---|---|---|---|---|
| **SESSION** | — | — | — | — | — | arena, fecha y versión |
| **CONFIG** | — | — | — | — | — | velocidad y valores de combate en uso (se repite al cambiarlos) |
| **PLAYER** | jugador | RPM máx. | peso | ataque | defensa | humano o dummy, piezas, poder, arquetipo, velocidad, cargas, dash, desgaste (se repite al cambiar piezas) |
| **CLASH** | A / B | velocidad de A hacia B (m/s) | velocidad de B hacia A | RPM que pierde A | RPM que pierde B | tipo (Normal, Even = parejo, Parry, DoubleParry, Launched, Stuck = peonzas pegadas que se separan), empuje a cada una, nivel de carga, dash, % de RPM tras el choque |
| **DAMAGE** | quien pierde / quien lo causa | RPM perdidas | RPM que le quedan | — | — | estado si tiene alguno (la quemadura sale aquí, también los choques) |
| **ATTACK** | atacante | nivel de carga (0 = rápido) | % de RPM | — | — | — |
| **DASH** | jugador | — | % de RPM | — | — | — |
| **SPECIAL_ON / SPECIAL_OFF** | jugador | — | % de RPM | — | — | poder |
| **STATUS** | afectado / causante | — | — | — | — | estado nuevo (None = se le quita) |
| **KO** | quien cae / último que le golpeó | — | — | — | — | — |
| **HEAL_CUT** | jugador | — | % de RPM | — | — | un golpe de ataque enemigo ha cortado la curación de su poder |
| **RESET** | — | — | — | — | — | se usó "Reiniciar todo" |
| **SNAPSHOT** | jugador | % de RPM | velocidad (m/s) | energía del especial (0-1) | cargas | estado, poder activo o "ko" (una línea por peonza y segundo) |

### Preguntas que responde

- **¿Cuánto dura un combate?** Tiempo entre el inicio (o un RESET) y el KO.
- **¿Cuánto quita cada cosa?** CLASH (por tipo y nivel de carga), DAMAGE con `status=Burning` para la quemadura.
- **¿Se usa mucho el especial o el dash?** Cuenta SPECIAL_ON, DASH y ATTACK por jugador.
- **¿Qué arquetipo gana?** PLAYER dice la build de cada uno; KO dice quién cae y quién le golpeó.

## 10. Recetas rápidas

- **Practicar el parry:** Rivales → 1 dummy, Dummies → Atacan, Cada → 1 s. Debug → Ventana de parry: ataca cuando su ataque esté a punto de llegar y fíjate en tu anillo cian.
- **Probar un poder:** Mi peonza → núcleo del poder. Trucos → Especial lleno → Jugadores. Debug → Estados y Registro.
- **Ver un choque a cámara lenta:** Velocidad → x0,25 (o PAUSA y avanza con «.»). Debug → Velocidades y Números de daño.
- **Medir la duración de los combates:** Rivales → 1 dummy, Dummies → Atacan. Deja que se pegue hasta el K.O. y mira el archivo.
- **Probar un cambio de equilibrio:** Ajustes → cambia el valor, juega, compara los archivos de antes y de después. Restaurar valores al terminar.
