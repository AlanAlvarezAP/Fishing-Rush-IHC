using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Herramienta de editor (no va en las builds).
/// Menu de Unity:  Fishing Rush > Crear tutorial del pescador
///
/// Crea (solo si no existe todavia) la raiz "Tutorial_Pescador" con:
///   - Plataforma sobre el agua (con su BoxCollider) y 4 paredes invisibles para no caerse.
///   - PuntoInicio_Pescador: donde aparece el pescador (su flecha azul = hacia donde mira).
///   - PuntoPez_Cerca / Medio / Lejos y PuntoTiburon_1 / 2.
///   - Barrera: linea visible que marca la mitad del lago del pescador.
///   - Zona_BloqueoBote: BoxCollider (trigger) con la mitad del pescador; el bote no puede entrar.
/// y en el HUD el "Tile_ProgresoTutorial" (copia de Tile_Estado).
///
/// SIEMPRE (tambien si ya existe) vuelve a:
///   - Asignar los campos de MenuMismaEscena y TutorialSlidesUI.
///   - Vincular las imagenes de Assets/_Project/Art/Tutorial/Pescador/slide_01..05 (png o jpg)
///     al hijo "Image" de Panel_Tutorial_1..5.
/// Asi, despues de mover la plataforma o cambiar las imagenes, basta con volver a ejecutarla.
/// Todo se puede deshacer con Ctrl+Z.
/// </summary>
public static class CrearTutorialPescador
{
    private const string TITULO = "Crear tutorial del pescador";
    private const string NOMBRE_RAIZ = "Tutorial_Pescador";
    private const string NOMBRE_TILE_PROGRESO = "Tile_ProgresoTutorial";
    private const string NOMBRE_PUNTO_CANVAS = "PuntoCanvas_Menu";
    private const string CARPETA_IMAGENES = "Assets/_Project/Art/Tutorial/Pescador";
    private const string CARPETA_MATERIALES = "Assets/_Project/Materials/Tutorial";
    private const int CANTIDAD_SLIDES = 5; // 4 con prueba + 1 final (tutorial completado)

    // Medidas (metros)
    private const float DISTANCIA_LINEA_AL_CENTRO = 10f; // la barrera queda a 10 m del centro del lago
    private const float LADO_PLATAFORMA = 8f;
    private const float GROSOR_PLATAFORMA = 0.6f;
    private const float AGUA_VISIBLE_Y = -2f;            // superficie del agua que se ve (los peces nadan mas abajo, en waterSurfaceY)
    private const float ALTURA_SOBRE_AGUA = 1.25f;       // cara de arriba de la plataforma sobre el agua visible
    private const float ALTURA_CANVAS_SOBRE_PLATAFORMA = 1.95f; // Canvas_Menu en Y = 1.2 (mundo)
    private const float ALTURA_PAREDES = 1.0f;           // bajas: no deben frenar el anzuelo al lanzar
    private const float GROSOR_PAREDES = 0.3f;

    [MenuItem("Fishing Rush/Crear tutorial del pescador")]
    private static void Crear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(TITULO, "Sal del modo Play antes de usar esta herramienta.", "OK");
            return;
        }

        MenuMismaEscena menu = Object.FindObjectOfType<MenuMismaEscena>(true);
        TutorialSlidesUI tutorial = Object.FindObjectOfType<TutorialSlidesUI>(true);
        FishSpawner spawner = Object.FindObjectOfType<FishSpawner>(true);

        if (menu == null || tutorial == null)
        {
            EditorUtility.DisplayDialog(TITULO,
                "No se encontro MenuMismaEscena o TutorialSlidesUI en la escena abierta. Abre la escena Main.", "OK");
            return;
        }

        Undo.SetCurrentGroupName(TITULO);
        int grupoUndo = Undo.GetCurrentGroup();
        var informe = new List<string>();

        // =========================================================
        // 1. ESCENA: plataforma, paredes, puntos, barrera y zona
        // =========================================================
        GameObject raiz = BuscarRaiz();
        if (raiz == null)
        {
            raiz = CrearEscena(menu, spawner);
            informe.Add("Creada la plataforma del tutorial (" + NOMBRE_RAIZ + "). Muevela a tu gusto: todo lo demas va con ella.");
        }
        else
        {
            if (CorregirAltura(raiz))
                informe.Add(NOMBRE_RAIZ + " estaba bajo el agua: se subio la plataforma (los puntos de peces y tiburones se dejaron a su altura).");
            else
                informe.Add(NOMBRE_RAIZ + " ya existia: no se toco (solo se reasignaron referencias).");
        }

        Transform puntoCanvas = BuscarHijo(raiz.transform, NOMBRE_PUNTO_CANVAS);
        if (puntoCanvas == null)
        {
            puntoCanvas = CrearPuntoCanvas(menu, raiz.transform);
            if (puntoCanvas != null)
                informe.Add("Creado " + NOMBRE_PUNTO_CANVAS + ": ahi se ponen los slides durante el tutorial (Canvas_Menu se mueve a ese punto). Muevelo si se ven mal.");
        }

        Transform puntoInicio = BuscarHijo(raiz.transform, "PuntoInicio_Pescador");
        Transform[] puntosPeces =
        {
            BuscarHijo(raiz.transform, "PuntoPez_Cerca"),
            BuscarHijo(raiz.transform, "PuntoPez_Medio"),
            BuscarHijo(raiz.transform, "PuntoPez_Lejos"),
        };
        Transform[] puntosTiburones =
        {
            BuscarHijo(raiz.transform, "PuntoTiburon_1"),
            BuscarHijo(raiz.transform, "PuntoTiburon_2"),
        };
        Transform zona = BuscarHijo(raiz.transform, "Zona_BloqueoBote");
        BoxCollider zonaCollider = zona != null ? zona.GetComponent<BoxCollider>() : null;

        // =========================================================
        // 2. HUD: tile de progreso de las pruebas
        // =========================================================
        GameObject tileProgreso = null;
        TMP_Text textoProgreso = null;
        if (menu.canvasGameHUD != null)
        {
            Transform existente = BuscarHijo(menu.canvasGameHUD.transform, NOMBRE_TILE_PROGRESO);
            if (existente == null)
            {
                Transform tileEstado = BuscarHijo(menu.canvasGameHUD.transform, "Tile_Estado");
                if (tileEstado != null)
                {
                    existente = CrearTileProgreso(tileEstado);
                    informe.Add("Creado " + NOMBRE_TILE_PROGRESO + " en Canvas_GameHUD (copia de Tile_Estado, a su izquierda). Acomodalo dentro de VR_limits.");
                }
                else
                {
                    informe.Add("AVISO: no se encontro Tile_Estado en Canvas_GameHUD; no se creo el tile de progreso.");
                }
            }

            if (existente != null)
            {
                tileProgreso = existente.gameObject;
                Transform status = BuscarHijo(existente, "StatusText");
                textoProgreso = status != null ? status.GetComponent<TMP_Text>() : existente.GetComponentInChildren<TMP_Text>(true);
            }
        }

        // =========================================================
        // 3. SLIDES: Panel_Tutorial_1..5 e imagenes
        // =========================================================
        Transform panelTutorial = menu.panelTutorial != null ? menu.panelTutorial.transform : tutorial.transform;
        GameObject[] slides = AsegurarSlides(panelTutorial, informe);
        VincularImagenes(slides, informe);

        // =========================================================
        // 4. REFERENCIAS
        // =========================================================
        SerializedObject soMenu = new SerializedObject(menu);
        soMenu.FindProperty("zonaTutorialPescador").objectReferenceValue = raiz;
        soMenu.FindProperty("puntoInicioTutorialPescador").objectReferenceValue = puntoInicio;
        soMenu.FindProperty("zonaBloqueoBoteTutorial").objectReferenceValue = zonaCollider;
        soMenu.FindProperty("puntoCanvasTutorialPescador").objectReferenceValue = puntoCanvas;
        soMenu.ApplyModifiedProperties();

        SerializedObject soTut = new SerializedObject(tutorial);
        AsignarArreglo(soTut.FindProperty("panelesTutorial"), slides);
        AsignarArreglo(soTut.FindProperty("puntosPeces"), puntosPeces);
        AsignarArreglo(soTut.FindProperty("puntosTiburones"), puntosTiburones);
        soTut.FindProperty("tileProgreso").objectReferenceValue = tileProgreso;
        soTut.FindProperty("textoProgreso").objectReferenceValue = textoProgreso;

        AsignarSiVacio(soTut, "boteObjeto", menu.boteObjeto);
        AsignarSiVacio(soTut, "playerController", menu.playerController);
        AsignarSiVacio(soTut, "canvasGameHUD", menu.canvasGameHUD);
        if (spawner != null)
        {
            AsignarSiVacio(soTut, "fishSpawnerManager", spawner.gameObject);
            AsignarSiVacio(soTut, "fishPrefab", spawner.fishPrefab);
            AsignarSiVacio(soTut, "sharkPrefab", spawner.sharkPrefab);
        }
        soTut.ApplyModifiedProperties();

        if (soTut.FindProperty("sharkPrefab").objectReferenceValue == null)
            informe.Add("AVISO: TutorialSlidesUI no tiene sharkPrefab (asigna el prefab Tiburon).");
        if (soTut.FindProperty("fishPrefab").objectReferenceValue == null)
            informe.Add("AVISO: TutorialSlidesUI no tiene fishPrefab.");

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = raiz;

        EditorUtility.DisplayDialog(TITULO, string.Join("\n\n", informe) + "\n\nGuarda la escena (Ctrl+S).", "OK");
    }

    // ---------------------------------------------------------------------
    // ESCENA
    // ---------------------------------------------------------------------
    private static GameObject BuscarRaiz()
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == NOMBRE_RAIZ) return go;
        }
        return null;
    }

    private static GameObject CrearEscena(MenuMismaEscena menu, FishSpawner spawner)
    {
        float aguaY = spawner != null ? spawner.waterSurfaceY : -3.75f;
        Vector3 centroLago = spawner != null && spawner.lakeCenter != null ? spawner.lakeCenter.position
                           : (spawner != null ? spawner.transform.position : Vector3.zero);
        Vector2 tamLago = spawner != null ? spawner.lakeSize : new Vector2(180f, 180f);

        // Mitad del pescador: la contraria a donde esta el bote (si el bote esta al centro, la de x negativa)
        float xBote = menu.boteObjeto != null ? menu.boteObjeto.transform.position.x : centroLago.x;
        float lado = xBote < centroLago.x - 1f ? 1f : -1f;

        float mitad = tamLago.x * 0.5f;
        float lineaX = centroLago.x + lado * DISTANCIA_LINEA_AL_CENTRO;
        float centroMitadX = centroLago.x + lado * (DISTANCIA_LINEA_AL_CENTRO + (mitad - DISTANCIA_LINEA_AL_CENTRO) * 0.5f);
        float arribaPlataforma = AGUA_VISIBLE_Y + ALTURA_SOBRE_AGUA;

        Vector3 centroPlataforma = new Vector3(centroMitadX, arribaPlataforma, centroLago.z);
        Vector3 haciaAgua = new Vector3(lado, 0f, 0f); // el pescador mira hacia el borde del lago
        Vector3 costado = Vector3.forward;

        GameObject raiz = new GameObject(NOMBRE_RAIZ);
        Undo.RegisterCreatedObjectUndo(raiz, TITULO);
        raiz.transform.position = centroPlataforma;

        Material matPlataforma = CrearMaterial("TutorialPlataforma", new Color(0.55f, 0.36f, 0.2f));
        Material matBarrera = CrearMaterial("TutorialBarrera", new Color(1f, 0.35f, 0.1f));

        // --- Plataforma (cubo con BoxCollider) ---
        GameObject plataforma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plataforma.name = "Plataforma";
        plataforma.transform.SetParent(raiz.transform, false);
        plataforma.transform.localPosition = new Vector3(0f, -GROSOR_PLATAFORMA * 0.5f, 0f);
        plataforma.transform.localScale = new Vector3(LADO_PLATAFORMA, GROSOR_PLATAFORMA, LADO_PLATAFORMA);
        plataforma.GetComponent<Renderer>().sharedMaterial = matPlataforma;

        // --- Paredes invisibles (solo colliders) ---
        GameObject paredes = new GameObject("Paredes");
        paredes.transform.SetParent(raiz.transform, false);
        float m = LADO_PLATAFORMA * 0.5f;
        CrearPared(paredes.transform, "Pared_Norte", new Vector3(0f, ALTURA_PAREDES * 0.5f, m), new Vector3(LADO_PLATAFORMA, ALTURA_PAREDES, GROSOR_PAREDES));
        CrearPared(paredes.transform, "Pared_Sur", new Vector3(0f, ALTURA_PAREDES * 0.5f, -m), new Vector3(LADO_PLATAFORMA, ALTURA_PAREDES, GROSOR_PAREDES));
        CrearPared(paredes.transform, "Pared_Este", new Vector3(m, ALTURA_PAREDES * 0.5f, 0f), new Vector3(GROSOR_PAREDES, ALTURA_PAREDES, LADO_PLATAFORMA));
        CrearPared(paredes.transform, "Pared_Oeste", new Vector3(-m, ALTURA_PAREDES * 0.5f, 0f), new Vector3(GROSOR_PAREDES, ALTURA_PAREDES, LADO_PLATAFORMA));

        // --- Punto de inicio (1.2 m sobre la plataforma, mirando al agua) ---
        GameObject inicio = new GameObject("PuntoInicio_Pescador");
        inicio.transform.SetParent(raiz.transform, false);
        inicio.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        inicio.transform.rotation = Quaternion.LookRotation(haciaAgua, Vector3.up);

        // --- Puntos de peces (en el agua, frente al pescador, a distancias distintas) ---
        float dy = aguaY - arribaPlataforma;
        CrearPunto(raiz.transform, "PuntoPez_Cerca", haciaAgua * 9f + costado * 3f + Vector3.up * dy);
        CrearPunto(raiz.transform, "PuntoPez_Medio", haciaAgua * 16f - costado * 5f + Vector3.up * dy);
        CrearPunto(raiz.transform, "PuntoPez_Lejos", haciaAgua * 25f + costado * 2f + Vector3.up * dy);

        CrearPunto(raiz.transform, "PuntoTiburon_1", haciaAgua * 14f + costado * 8f + Vector3.up * dy);
        CrearPunto(raiz.transform, "PuntoTiburon_2", haciaAgua * 14f - costado * 8f + Vector3.up * dy);

        // --- Barrera visible: marca la mitad del pescador (el bote se frena por codigo) ---
        GameObject barrera = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrera.name = "Barrera";
        barrera.transform.SetParent(raiz.transform, false);
        barrera.transform.position = new Vector3(lineaX, AGUA_VISIBLE_Y + 0.1f, centroLago.z);
        barrera.transform.localScale = new Vector3(0.4f, 0.5f, tamLago.y);
        barrera.GetComponent<Renderer>().sharedMaterial = matBarrera;
        BoxCollider colBarrera = barrera.GetComponent<BoxCollider>();
        colBarrera.size = new Vector3(1f, 40f, 1f); // muro alto (0.5 x 40 = 20 m)

        // --- Zona que el bote no puede pisar (desde la barrera hasta mas alla del borde del lago) ---
        GameObject zona = new GameObject("Zona_BloqueoBote");
        zona.transform.SetParent(raiz.transform, false);
        float ancho = (mitad - DISTANCIA_LINEA_AL_CENTRO) + 50f;
        zona.transform.position = new Vector3(lineaX + lado * ancho * 0.5f, aguaY, centroLago.z);
        BoxCollider colZona = zona.AddComponent<BoxCollider>();
        colZona.isTrigger = true;
        colZona.size = new Vector3(ancho, 30f, tamLago.y + 100f);

        raiz.SetActive(false); // MenuMismaEscena la enciende solo durante el tutorial del pescador
        return raiz;
    }

    /// <summary>
    /// Plataformas creadas con la version anterior quedaron bajo el agua. Sube la raiz hasta la
    /// altura correcta, pero deja los puntos de peces/tiburones y la zona a su altura (en el agua).
    /// Solo actua si la plataforma esta bajo el agua visible (no deshace un ajuste tuyo).
    /// </summary>
    private static bool CorregirAltura(GameObject raiz)
    {
        float deseada = AGUA_VISIBLE_Y + ALTURA_SOBRE_AGUA;
        float delta = deseada - raiz.transform.position.y;
        if (raiz.transform.position.y > AGUA_VISIBLE_Y + 0.2f || delta <= 0.01f) return false;

        var fijos = new List<Transform>();
        foreach (Transform hijo in raiz.transform)
        {
            if (hijo.name.StartsWith("PuntoPez") || hijo.name.StartsWith("PuntoTiburon") || hijo.name == "Zona_BloqueoBote")
                fijos.Add(hijo);
        }

        Undo.RecordObject(raiz.transform, TITULO);
        foreach (Transform t in fijos) Undo.RecordObject(t, TITULO);

        var posFijas = new List<Vector3>();
        foreach (Transform t in fijos) posFijas.Add(t.position);

        raiz.transform.position += Vector3.up * delta;
        for (int i = 0; i < fijos.Count; i++) fijos[i].position = posFijas[i];

        Transform barrera = BuscarHijo(raiz.transform, "Barrera");
        if (barrera != null)
        {
            Undo.RecordObject(barrera, TITULO);
            Vector3 p = barrera.position;
            barrera.position = new Vector3(p.x, AGUA_VISIBLE_Y + 0.1f, p.z);
        }
        return true;
    }

    /// <summary>
    /// Punto donde va Canvas_Menu en el tutorial: la misma posicion relativa que tiene el canvas
    /// con el pescador en el bote, pero tomando como referencia PuntoInicio_Pescador.
    /// </summary>
    private static Transform CrearPuntoCanvas(MenuMismaEscena menu, Transform raiz)
    {
        Transform inicio = BuscarHijo(raiz, "PuntoInicio_Pescador");
        if (menu.canvasMenu == null || inicio == null || menu.boteObjeto == null) return null;

        Transform bote = menu.boteObjeto.transform;
        Transform canvas = menu.canvasMenu.transform;

        Vector3 refBote = bote.TransformPoint(menu.posicionInicialPescador);
        Quaternion yawBote = Quaternion.Euler(0f, bote.eulerAngles.y, 0f);
        Quaternion yawPunto = Quaternion.Euler(0f, inicio.eulerAngles.y, 0f);
        Quaternion giro = yawPunto * Quaternion.Inverse(yawBote);

        GameObject p = new GameObject(NOMBRE_PUNTO_CANVAS);
        Undo.RegisterCreatedObjectUndo(p, TITULO);
        p.transform.SetParent(raiz, false);
        Vector3 pos = inicio.position + giro * (canvas.position - refBote);
        pos.y = AGUA_VISIBLE_Y + ALTURA_SOBRE_AGUA + ALTURA_CANVAS_SOBRE_PLATAFORMA; // a la altura de la vista
        p.transform.SetPositionAndRotation(pos, giro * canvas.rotation);
        return p.transform;
    }

    private static void CrearPared(Transform padre, string nombre, Vector3 posLocal, Vector3 tam)
    {
        GameObject pared = new GameObject(nombre);
        pared.transform.SetParent(padre, false);
        pared.transform.localPosition = posLocal;
        BoxCollider col = pared.AddComponent<BoxCollider>();
        col.size = tam;
    }

    private static void CrearPunto(Transform padre, string nombre, Vector3 posLocal)
    {
        GameObject p = new GameObject(nombre);
        p.transform.SetParent(padre, false);
        p.transform.localPosition = posLocal;
    }

    private static Material CrearMaterial(string nombre, Color color)
    {
        CrearCarpeta(CARPETA_MATERIALES);
        string ruta = CARPETA_MATERIALES + "/" + nombre + ".mat";

        Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null) return existente;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = nombre };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    // ---------------------------------------------------------------------
    // HUD
    // ---------------------------------------------------------------------
    private static Transform CrearTileProgreso(Transform tileEstado)
    {
        GameObject copia = Object.Instantiate(tileEstado.gameObject, tileEstado.parent);
        copia.name = NOMBRE_TILE_PROGRESO;
        Undo.RegisterCreatedObjectUndo(copia, TITULO);

        foreach (NetworkObject no in copia.GetComponentsInChildren<NetworkObject>(true)) Object.DestroyImmediate(no);

        RectTransform rtOriginal = (RectTransform)tileEstado;
        RectTransform rt = (RectTransform)copia.transform;
        rt.anchoredPosition = rtOriginal.anchoredPosition + new Vector2(-45f, 0f);

        Transform label = BuscarHijo(copia.transform, "Label");
        if (label != null && label.GetComponent<TMP_Text>() != null) label.GetComponent<TMP_Text>().text = "Tutorial:";

        Transform status = BuscarHijo(copia.transform, "StatusText");
        if (status != null && status.GetComponent<TMP_Text>() != null) status.GetComponent<TMP_Text>().text = "";

        copia.SetActive(false); // TutorialSlidesUI lo muestra solo durante las pruebas
        return copia.transform;
    }

    // ---------------------------------------------------------------------
    // SLIDES E IMAGENES
    // ---------------------------------------------------------------------
    private static GameObject[] AsegurarSlides(Transform panelTutorial, List<string> informe)
    {
        var slides = new GameObject[CANTIDAD_SLIDES];
        Transform ultimo = null;

        for (int i = 0; i < CANTIDAD_SLIDES; i++)
        {
            string nombre = "Panel_Tutorial_" + (i + 1);
            Transform t = panelTutorial.Find(nombre);

            if (t == null && ultimo != null)
            {
                // Falta este slide: se copia el anterior (los slides no tienen objetos de red)
                GameObject copia = Object.Instantiate(ultimo.gameObject, panelTutorial);
                copia.name = nombre;
                copia.SetActive(false);
                Undo.RegisterCreatedObjectUndo(copia, TITULO);
                t = copia.transform;
                informe.Add("Creado " + nombre + " (copia de " + ultimo.name + ").");
            }

            if (t == null)
            {
                informe.Add("AVISO: no existe " + nombre + " dentro de " + panelTutorial.name + ".");
                continue;
            }

            slides[i] = t.gameObject;
            ultimo = t;
        }

        return slides;
    }

    private static void VincularImagenes(GameObject[] slides, List<string> informe)
    {
        CrearCarpeta(CARPETA_IMAGENES);

        var vinculadas = new List<string>();
        var faltan = new List<string>();

        for (int i = 0; i < slides.Length; i++)
        {
            string baseNombre = "slide_" + (i + 1).ToString("00");
            string ruta = BuscarImagen(baseNombre);

            if (ruta == null)
            {
                faltan.Add(baseNombre);
                continue;
            }

            if (slides[i] == null) continue;

            // La imagen tiene que importarse como Sprite para usarla en un Image de UI
            TextureImporter imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            Transform hijoImagen = slides[i].transform.Find("Image");
            Image img = hijoImagen != null ? hijoImagen.GetComponent<Image>() : null;

            if (sprite == null || img == null)
            {
                informe.Add("AVISO: no se pudo poner " + Path.GetFileName(ruta) + " en " + slides[i].name + " (necesita un hijo 'Image' con componente Image).");
                continue;
            }

            Undo.RecordObject(img, TITULO);
            img.sprite = sprite;
            img.preserveAspect = true;
            EditorUtility.SetDirty(img);
            vinculadas.Add(slides[i].name + " <- " + Path.GetFileName(ruta));
        }

        if (vinculadas.Count > 0) informe.Add("Imagenes vinculadas:\n" + string.Join("\n", vinculadas));
        if (faltan.Count > 0)
            informe.Add("Sin imagen todavia (se dejan las actuales): " + string.Join(", ", faltan) +
                        "\nPonlas en " + CARPETA_IMAGENES + " (png o jpg) y vuelve a ejecutar la herramienta.");
    }

    private static string BuscarImagen(string baseNombre)
    {
        foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            string ruta = CARPETA_IMAGENES + "/" + baseNombre + ext;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ruta) != null) return ruta;
        }
        return null;
    }

    // ---------------------------------------------------------------------
    // UTILIDADES
    // ---------------------------------------------------------------------
    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static Transform BuscarHijo(Transform raiz, string nombre)
    {
        if (raiz == null) return null;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }
        return null;
    }

    private static void AsignarArreglo(SerializedProperty prop, Object[] valores)
    {
        if (prop == null) return;
        prop.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }

    private static void AsignarSiVacio(SerializedObject so, string campo, Object valor)
    {
        SerializedProperty p = so.FindProperty(campo);
        if (p != null && p.objectReferenceValue == null && valor != null) p.objectReferenceValue = valor;
    }
}
