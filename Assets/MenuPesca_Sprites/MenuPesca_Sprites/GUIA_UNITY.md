# Menú de pesca: guía para armarlo en Unity

Todas las medidas están en píxeles de un Canvas de **1920×1080** (el mockup era 1280×720, aquí todo está multiplicado por 1.5).

---

## 0. Configuración base (una sola vez)

**Canvas**
- Render Mode: `Screen Space - Overlay`
- Canvas Scaler → UI Scale Mode: `Scale With Screen Size`
- Reference Resolution: `1920 x 1080`, Screen Match Mode: `Match Width Or Height`, Match = `0.5`
- Reference Pixels Per Unit: `100` (déjalo así)

**Importar los PNG** (seleccionas todos en el Project → Inspector):
| Campo | Valor |
|---|---|
| Texture Type | `Sprite (2D and UI)` |
| Sprite Mode | `Single` |
| Pixels Per Unit | `100` |
| Mesh Type | `Full Rect` |
| Generate Mip Maps | desactivado |
| Compression | `None` |
| Filter Mode | `Bilinear` |

**Sprites 9-slice** (tienen que estirarse sin deformar las esquinas): abre `Sprite Editor` y pon los bordes:
| Sprite | Border L / R / T / B |
|---|---|
| `tile_fill` | 12 / 12 / 12 / 12 |
| `ui_round_9` | 12 / 12 / 12 / 12 |
| `outline_btn` | 12 / 12 / 12 / 12 |
| `ui_round_6` | 8 / 8 / 8 / 8 |

Luego, en cada `Image` que use uno de esos sprites: `Image Type = Sliced`.
Ejemplo: `tile_fill` mide 64×64, pero tu tile mide 354×354. Con Sliced las esquinas siguen midiendo 12 px y solo se estira el centro.

**Los íconos y formas blancas se pintan con `Image → Color`.** Ese es el truco: un solo sprite blanco sirve para todos los colores.

---

## 1. Fuentes (TextMeshPro)

Descarga de fonts.google.com: **Rye**, **Roboto Slab** (Medium y Bold) y **Lato** (Regular y Bold). Pon los `.ttf` en `Assets/Fonts`.

Por cada `.ttf`: `Window → TextMeshPro → Font Asset Creator`
- Source Font File: el .ttf
- Sampling Point Size: `Auto Sizing`
- Atlas Resolution: `1024 x 1024`
- Character Set: `Unicode Range (Hex)` → escribe `20-7E,A0-FF`
- Render Mode: `SDFAA`
- `Generate Font Atlas` → `Save`

El rango `20-7E,A0-FF` es el que incluye **ñ, á, é, í, ó, ú, ¿, ¡**. Sin eso "Colección" se ve roto.

| Uso | Fuente | Tamaño |
|---|---|---|
| Título "Pesca" | Rye | 198 (pantalla de colección: 96) |
| Frase bajo el título | Roboto Slab Medium | 36 |
| Nombre de tile / de pez | Roboto Slab Bold | 45 (tile) / 24 (pez) |
| Subtítulos, chips, hints | Lato Bold / Regular | 20–22 |

**Sombra dura del título** (la del mockup): en el Material del TMP, sección `Underlay` activada → Color `#1C0F08`, Offset Y `-0.6`, Dilate `0.3`, Softness `0`. Ajusta hasta que se parezca.

---

## 2. Colores

| Nombre | Hex | Dónde |
|---|---|---|
| Madera oscura | `#3B2316` | fondo base, texto sobre beige |
| Madera media | `#6B4226` | borde de la carta bloqueada |
| Beige | `#D9C7A3` | tile Opciones, cartas, textos secundarios |
| Crema | `#F1E7D0` | títulos, texto sobre fondo oscuro |
| Naranja | `#D9742B` | tile Jugar |
| Verde lima | `#9BC53D` | chips ("Wiimote conectado", "Nuevo", contador) |
| Verde camuflaje | `#5E6B3A` | tile Colección |
| Gris metal | `#8A9199` | tile Wiimote, bordes de botones |
| Marrón casi negro | `#24140C` | texto sobre naranja y gris |
| Sombra | `#1C0F08` | sombra dura de tiles y cartas |

En Unity se escribe el hex en el color picker, campo `Hex Color`, sin el `#` si el campo no lo acepta.

---

## 3. Los elementos, uno por uno

### 3.1 Fondo de madera
- Sprite: `bg_wood_1920x1080.png`
- `Image` anclado a **stretch/stretch** (Alt+Shift al elegir el ancla) con Left/Right/Top/Bottom = 0, Image Type `Simple`, Color blanco.
- Es el **primer hijo del Canvas**, así queda detrás de todo.

### 3.2 Tile cuadrado (el elemento principal)
Tamaño final: **354×354**. Cada tile son 2 imágenes apiladas, y por eso necesita esta jerarquía:

```
Tile_Jugar            (RectTransform 354x354, sin Image)
├─ Shadow             (Image: ui_round_9, Color #1C0F08, Sliced)
└─ Face               (Image: tile_fill, Color = color del tile, Sliced) + Button + TileHover
   ├─ Icon            (Image: icon_play, 96x96, Color del texto)
   ├─ Label           (TMP: "Jugar")
   └─ Sub             (TMP: "Empezar a pescar")
```

**Shadow:** anclas stretch/stretch, Left 0, Right 0, Top 0, **Bottom 0**, y luego `Pos Y = -12` (la sombra queda 12 px más abajo que la cara). Va **antes** de Face en la jerarquía para dibujarse detrás.

**Face:** anclas stretch/stretch con offsets en 0. Esta es la que tiene el `Button` (Target Graphic = su propia Image).

**Icon:** ancla arriba-izquierda, Pos X `33`, Pos Y `-33`, Width/Height `96`.

**Label:** ancla abajo-izquierda, Pos X `33`, Pos Y `66`, Roboto Slab Bold 45, Alignment izquierda.

**Sub:** ancla abajo-izquierda, Pos X `33`, Pos Y `33`, Lato Bold 22.

**Colores de los 4 tiles:**
| Tile | Color de Face | Color de Icon y textos | Icono |
|---|---|---|---|
| Jugar | `#D9742B` | `#24140C` | `icon_play` |
| Colección | `#5E6B3A` | `#F1E7D0` | `icon_fish` |
| Wiimote | `#8A9199` | `#24140C` | `icon_wiimote` |
| Opciones | `#D9C7A3` | `#3B2316` | `icon_gear` |

**Button → Transition = `Color Tint`** (multiplica sobre el color del tile):
- Normal `#FFFFFF`, Highlighted `#F2F2F2`, Pressed `#BDBDBD`, Selected `#FFFFFF`, Disabled `#808080`, Fade Duration `0.1`

**Efecto de "levantar" al pasar el mouse:** añade el script `TileHover.cs` (incluido en esta carpeta) al objeto **Face**. Sube la cara 6 px (los 4 px del mockup × 1.5) y la sombra se queda abajo.

**Contador "7 / 12"** (solo en Colección): hijo de Face, `ui_round_6` Sliced Color `#3B2316`, ancla arriba-derecha, Pos X `-27`, Pos Y `-27`. Dentro un TMP Lato Bold 21 `#F1E7D0`. Con `Horizontal Layout Group` (padding 14 izq/der, 8 arriba/abajo) + `Content Size Fitter` (Horizontal y Vertical = Preferred Size) se ajusta solo al texto.

### 3.3 Cuadrícula de 4 tiles
Un objeto vacío `TilesGrid` con `Grid Layout Group`:
- Cell Size `354 x 354`, Spacing `36 x 36`
- Constraint: `Fixed Column Count` = `2`
- Child Alignment: `Upper Left`

Resultado: 354×2 + 36 = **744 px** de ancho y alto. Ancla a la derecha-centro con Pos X = `-132` (margen derecho de 88 × 1.5). Los hijos de la grilla son los 4 `Tile_*` (los objetos raíz vacíos).

### 3.4 Título y frase
- `Title`: TMP Rye 198, `#F1E7D0`, con Underlay (ver sección 1). Texto: "Pesca".
- `Tagline`: TMP Roboto Slab Medium 36, `#D9C7A3`.
- Ponlos en un `Vertical Layout Group` (Spacing 30, Child Alignment `Upper Left`) a la izquierda, junto con el chip, el botón Salir y los hints.

### 3.5 Chip "Wiimote conectado"
- `Image`: `ui_round_6`, Sliced, Color `#9BC53D`
- Hijos en un `Horizontal Layout Group` (padding 21 izq/der, 12 arriba/abajo, Spacing 15):
  - Punto: `circle`, 14×14, Color `#3B2316`
  - TMP: Lato Bold 21, `#3B2316`, MAYÚSCULAS, Character Spacing `2`
- `Content Size Fitter`: Horizontal y Vertical en `Preferred Size`

### 3.6 Botón "Salir" y botón "Volver" (contorno)
- `Image`: `outline_btn`, Sliced, **Fill Center desactivado**, Color `#8A9199`
- Salir: 168×66, texto Roboto Slab Bold 27 `#D9C7A3`
- Volver: 190×66, con `icon_arrow_left` de 30×30 (Color `#D9C7A3`) a la izquierda del texto
- Button con Color Tint: Normal `#FFFFFF`, Highlighted `#F2F2F2`, Pressed `#BDBDBD`

### 3.7 Hints de controles ("A Elegir", "B Volver")
Un `Horizontal Layout Group` (Spacing 33) con 2 grupos; cada grupo es otro Horizontal Layout (Spacing 12):
- Círculo: `circle`, 39×39, Color `#F1E7D0`, con TMP hijo centrado ("A" o "B") Lato Bold 21 `#3B2316`
- Texto: Lato Regular 22 `#D9C7A3`

### 3.8 Hilo y señuelo decorativos
- `lure_line.png`, 60×285, ancla arriba-derecha, Pos X `-60`, Pos Y `0`. **No tiene `Raycast Target`** (desmárcalo, es solo decoración).
- Tiene sus colores propios: déjalo con Color blanco.
- Extra: si lo meces con un script de rotación pequeña (±2°) se siente vivo.

---

## 4. Pantalla "Colección"

### 4.1 Cabecera
Un `Horizontal Layout Group` con `Child Force Expand Width` y `Space Between` (o 3 objetos con anclas izq/centro/der):
- Izquierda: botón **Volver** (3.6)
- Centro: TMP "Colección", Rye 96, `#F1E7D0`
- Derecha: chip `#9BC53D` con texto Lato Bold 24 "Descubiertos 7 / 12" (mismo método que 3.5)

### 4.2 Carta de pez
Tamaño **251×251**. Misma idea que el tile:

```
Card_Trucha           (RectTransform 251x251, sin Image)
├─ Shadow             (ui_round_9, Color #1C0F08, Sliced, Pos Y -9)
└─ Face               (tile_fill, Color = color de la carta, Sliced)
   ├─ NewChip         (ui_round_6 #9BC53D + TMP "NUEVO" Lato Bold 18, arriba-derecha, X -12, Y -12)
   ├─ FishIcon        (fish_silhouette, 144x81, ancla arriba-centro, Pos Y -33, Color = color del texto)
   ├─ Name            (TMP Roboto Slab Bold 24, abajo-centro, Pos Y 42)
   └─ Weight          (TMP Lato Bold 20, abajo-centro, Pos Y 18)
```

Colores de ejemplo:
| Pez | Color de Face | Color de pez y texto |
|---|---|---|
| Trucha arcoíris | `#D9742B` | `#24140C` |
| Pejerrey | `#D9C7A3` | `#3B2316` |
| Carpa | `#5E6B3A` | `#F1E7D0` |
| Bagre | `#8A9199` | `#24140C` |

El ojo del pez está **recortado** (transparente), así que se ve del color de la carta. Es normal.

### 4.3 Carta bloqueada
```
Card_Locked           (RectTransform 251x251)
├─ Face               (Image: card_locked_251, Image Type = Simple, Color blanco)
├─ Lock               (icon_lock, 81x81, Color #B7BDC4, arriba-centro, Pos Y -33)
├─ Name               (TMP "???" Roboto Slab Bold 24 #B7BDC4)
└─ Weight             (TMP "Sin descubrir" Lato Bold 20 #B7BDC4)
```
Esta no lleva Shadow ni 9-slice: el borde punteado se deformaría si se estirara, por eso se exporta ya al tamaño exacto.

### 4.4 Cuadrícula de cartas
`Grid Layout Group`: Cell Size `251 x 251`, Spacing `30 x 30`, Constraint `Fixed Column Count` = `6`.
Ancho total: 6 × 251 + 5 × 30 = **1656 px** (justo el ancho útil con 132 px de margen a cada lado).

Haz un **Prefab** de `Card` y de `Card_Locked` y en vez de copiar y pegar 12 veces, instancia desde un script.

---

## 5. Orden de trabajo recomendado

1. Importar sprites + configurar 9-slice (sección 0)
2. Crear los Font Assets (sección 1)
3. Fondo + 1 tile completo (3.1 y 3.2) → convertirlo en Prefab `Tile`
4. Duplicar el Prefab 4 veces con los colores y textos de la tabla (3.3)
5. Título, chip, Salir y hints (3.4 a 3.7)
6. Pantalla de Colección con 1 carta → Prefab → 12 instancias (sección 4)

Si algo se ve "borroso" en las esquinas, revisa que el sprite tenga `Compression = None` y `Mesh Type = Full Rect`.
