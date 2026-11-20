using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fishing Rush > Crear tutorial del conductor
///
/// Arma en la escena abierta (Main) todo lo que necesita el tutorial del conductor:
///   - Tutorial_Conductor (en la mitad del lago opuesta al pescador) con Paso1..Paso4:
///     punto de inicio del bote, caja de la que el bote no puede salir, pasillos visibles,
///     metas, fila de minas obligatoria, minas, tiburones y circulos de los peces.
///   - Panel_TutorialConductor dentro de Canvas_Menu_Conductor (Overlay) con Slide_1..5,
///     boton Continuar (1..4) y el recuadro de progreso.
///   - Vincula las imagenes de Assets/_Project/Art/Tutorial/Conductor/slide_01..05 (png o jpg).
///   - Asigna las referencias en MenuMismaEscena.
///
/// Si ya existen, NO los vuelve a crear: solo reasigna referencias e imagenes (puedes moverlos
/// a tu gusto y volver a ejecutar la herramienta).
/// </summary>
public static class CrearTutorialConductor
{
    private const string TITULO = "Crear tutorial del conductor";
    private const string NOMBRE_RAIZ = "Tutorial_Conductor";
    private const string NOMBRE_PANEL = "Panel_TutorialConductor";
    private const string CARPETA_IMAGENES = "Assets/_Project/Art/Tutorial/Conductor";
    private const string CARPETA_MATERIALES = "Assets/_Project/Materials/Tutorial";
    private const int CANTIDAD_SLIDES = 5;

    private const float AGUA_VISIBLE_Y = -2f;
    private const float RADIO_META = 6f;
    private const float RADIO_CIRCULO = 5f;

    // Texto provisional de cada panel (se ve mientras no haya imagen)
    private static readonly string[] TEXTOS_SLIDES =
    {
        "1. CONDUCIR\n\nUsa los botones Avanzar y Retroceder de la pantalla.\nInclina la tablet para girar a la izquierda o a la derecha.\n\nHaz los 4 movimientos.",
        "2. MINAS\n\nSi el bote toca una mina, queda inmovilizado unos segundos.\n\nLlega a la meta verde por el pasillo esquivando las minas.",
        "3. TIBURONES\n\nSi un tiburon embiste al bote, lo hace tambalear y cuesta controlarlo.\n\nLlega a la meta verde por el pasillo esquivando los tiburones.",
        "4. ACERCA AL PESCADOR A LOS PECES\n\nEl pescador solo alcanza los peces si el bote esta cerca.\n\nQuedate unos segundos dentro de cada circulo amarillo,\nesquivando minas y tiburones.",
        "TUTORIAL COMPLETADO\n\nYa puedes jugar.",
    };

    [MenuItem("Fishing Rush/Crear tutorial del conductor")]
    private static void Crear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(TITULO, "Sal del modo Play antes de usar esta herramienta.", "OK");
            return;
        }

        MenuMismaEscena menu = Object.FindObjectOfType<MenuMismaEscena>(true);
        if (menu == null)
        {
            EditorUtility.DisplayDialog(TITULO, "No se encontro MenuMismaEscena en la escena abierta. Abre la escena Main.", "OK");
            return;
        }

        if (menu.canvasMenuConductor == null)
        {
            EditorUtility.DisplayDialog(TITULO, "MenuMismaEscena no tiene asignado canvasMenuConductor (Canvas_Menu_Conductor).", "OK");
            return;
        }

        Undo.SetCurrentGroupName(TITULO);
        int grupoUndo = Undo.GetCurrentGroup();
        var informe = new List<string>();

        // =========================================================
        // 1. ESCENA
        // =========================================================
        GameObject raiz = BuscarRaiz(NOMBRE_RAIZ);
        if (raiz == null)
        {
            raiz = CrearEscena(menu);
            informe.Add("Creada la zona del tutorial (" + NOMBRE_RAIZ + ") en la mitad del lago del conductor. " +
                        "Mueve la raiz o los puntos a tu gusto.");
        }
        else
        {
            informe.Add(NOMBRE_RAIZ + " ya existia: no se toco (solo se reasignaron referencias).");
        }

        ZonaTutorialConductor zona = raiz.GetComponent<ZonaTutorialConductor>();
        if (zona == null) zona = Undo.AddComponent<ZonaTutorialConductor>(raiz);
        VincularZona(zona, informe);

        // =========================================================
        // 2. UI EN LA TABLET
        // =========================================================
        Transform canvas = menu.canvasMenuConductor.transform;
        Transform panel = canvas.Find(NOMBRE_PANEL);
        if (panel == null)
        {
            panel = CrearPanel(canvas, menu);
            informe.Add("Creado " + NOMBRE_PANEL + " en Canvas_Menu_Conductor (Slide_1..5, boton Continuar y recuadro de progreso).");
        }
        else
        {
            informe.Add(NOMBRE_PANEL + " ya existia: no se toco.");
        }

        TutorialConductorUI ui = panel.GetComponent<TutorialConductorUI>();
        if (ui == null) ui = Undo.AddComponent<TutorialConductorUI>(panel.gameObject);

        var slides = new GameObject[CANTIDAD_SLIDES];
        for (int i = 0; i < CANTIDAD_SLIDES; i++)
        {
            Transform s = panel.Find("Slide_" + (i + 1));
            slides[i] = s != null ? s.gameObject : null;
            if (s == null) informe.Add("AVISO: falta Slide_" + (i + 1) + " dentro de " + NOMBRE_PANEL + ".");
        }
        VincularImagenes(slides, informe);

        Transform progreso = panel.Find("Progreso");
        TMP_Text textoProgreso = progreso != null ? progreso.GetComponentInChildren<TMP_Text>(true) : null;

        SerializedObject soUi = new SerializedObject(ui);
        AsignarArreglo(soUi.FindProperty("slides"), slides);
        soUi.FindProperty("panelProgreso").objectReferenceValue = progreso != null ? progreso.gameObject : null;
        soUi.FindProperty("textoProgreso").objectReferenceValue = textoProgreso;
        soUi.ApplyModifiedProperties();

        // =========================================================
        // 3. REFERENCIAS EN MenuMismaEscena
        // =========================================================
        SerializedObject soMenu = new SerializedObject(menu);
        soMenu.FindProperty("zonaTutorialConductor").objectReferenceValue = zona;
        soMenu.FindProperty("tutorialConductorUI").objectReferenceValue = ui;

        Transform tileJugar = menu.panelMenuConductor != null ? BuscarHijo(menu.panelMenuConductor.transform, "Tile_jugar") : null;
        soMenu.FindProperty("tileJugarConductor").objectReferenceValue = tileJugar != null ? tileJugar.gameObject : null;
        if (tileJugar == null) informe.Add("AVISO: no se encontro Tile_jugar en Panel_MenuConductor (el boton Jugar no se ocultara).");
        soMenu.ApplyModifiedProperties();

        if (Object.FindObjectOfType<MinaSpawner>(true) == null || Object.FindObjectOfType<MinaSpawner>(true).minaPrefab == null)
            informe.Add("AVISO: no hay MinaSpawner con minaPrefab: el tutorial no podra poner minas.");
        FishSpawner fs = Object.FindObjectOfType<FishSpawner>(true);
        if (fs == null || fs.fishPrefab == null || fs.sharkPrefab == null)
            informe.Add("AVISO: falta FishSpawner con fishPrefab y sharkPrefab: el tutorial no podra poner peces o tiburones.");

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = raiz;

        EditorUtility.DisplayDialog(TITULO, string.Join("\n\n", informe) + "\n\nGuarda la escena (Ctrl+S).", "OK");
    }

    // ---------------------------------------------------------------------
    // ESCENA
    // ---------------------------------------------------------------------
    private static GameObject CrearEscena(MenuMismaEscena menu)
    {
        FishSpawner spawner = Object.FindObjectOfType<FishSpawner>(true);
        MinaSpawner minas = Object.FindObjectOfType<MinaSpawner>(true);

        Vector3 centroLago = spawner != null && spawner.lakeCenter != null ? spawner.lakeCenter.position
                           : (spawner != null ? spawner.transform.position : Vector3.zero);
        float pecesY = spawner != null ? spawner.waterSurfaceY : -3.75f;
        float minasY = minas != null ? minas.alturaMina : AGUA_VISIBLE_Y;

        // Mitad del conductor: la contraria a la del tutorial del pescador
        GameObject pescador = BuscarRaiz("Tutorial_Pescador");
        float ladoPescador;
        if (pescador != null) ladoPescador = pescador.transform.position.x < centroLago.x ? -1f : 1f;
        else
        {
            float xBote = menu.boteObjeto != null ? menu.boteObjeto.transform.position.x : centroLago.x;
            ladoPescador = xBote < centroLago.x - 1f ? 1f : -1f; // misma regla que el tutorial del pescador
        }
        float lado = -ladoPescador;

        GameObject raiz = new GameObject(NOMBRE_RAIZ);
        Undo.RegisterCreatedObjectUndo(raiz, TITULO);
        raiz.transform.position = new Vector3(centroLago.x + lado * 50f, AGUA_VISIBLE_Y, centroLago.z);
        raiz.transform.rotation = Quaternion.Euler(0f, lado > 0f ? 0f : 180f, 0f);
        ZonaTutorialConductor zona = raiz.AddComponent<ZonaTutorialConductor>();
        zona.radioMeta = RADIO_META;
        zona.radioCirculo = RADIO_CIRCULO;

        float yMina = minasY - AGUA_VISIBLE_Y;  // alturas locales respecto del agua
        float yPez = pecesY - AGUA_VISIBLE_Y;

        Material matPasillo = CrearMaterial("TutorialBarrera", new Color(1f, 0.35f, 0.1f));
        Material matMeta = CrearMaterial("TutorialMeta", new Color(0.15f, 0.85f, 0.25f));
        Material matCirculo = CrearMaterial("TutorialCirculo", new Color(1f, 0.85f, 0.1f));

        // --- Paso 1: zona abierta para practicar ---
        Transform p1 = CrearHijo(raiz.transform, "Paso1", Vector3.zero);
        CrearInicio(p1, new Vector3(0f, 0f, -10f));
        CrearArea(p1, new Vector3(0f, 0f, 0f), new Vector3(50f, 30f, 50f));

        // --- Paso 2: pasillo con minas ---
        Transform p2 = CrearHijo(raiz.transform, "Paso2", new Vector3(-20f, 0f, 0f));
        CrearInicio(p2, new Vector3(0f, 0f, -36f));
        CrearArea(p2, new Vector3(0f, 0f, -5f), new Vector3(10f, 30f, 74f));
        CrearPasillo(p2, new Vector3(0f, 0f, -5f), 10f, 74f, matPasillo);
        CrearMeta(p2, "Meta", new Vector3(0f, 0f, 24f), matMeta);
        Transform filaMinas = CrearHijo(p2, "MinasObligatorias", new Vector3(0f, 0f, -20f));
        CrearPunto(filaMinas, "Mina_1", new Vector3(-3f, yMina, 0f));
        CrearPunto(filaMinas, "Mina_2", new Vector3(0f, yMina, 0f));
        CrearPunto(filaMinas, "Mina_3", new Vector3(3f, yMina, 0f));
        Transform minasP2 = CrearHijo(p2, "Minas", Vector3.zero);
        CrearPunto(minasP2, "Mina_1", new Vector3(-1.5f, yMina, -2f));
        CrearPunto(minasP2, "Mina_2", new Vector3(1.5f, yMina, 12f));

        // --- Paso 3: pasillo con tiburones ---
        Transform p3 = CrearHijo(raiz.transform, "Paso3", new Vector3(20f, 0f, 0f));
        CrearInicio(p3, new Vector3(0f, 0f, -36f));
        CrearArea(p3, new Vector3(0f, 0f, -5f), new Vector3(10f, 30f, 74f));
        CrearPasillo(p3, new Vector3(0f, 0f, -5f), 10f, 74f, matPasillo);
        CrearMeta(p3, "Meta", new Vector3(0f, 0f, 24f), matMeta);
        Transform tiburonesP3 = CrearHijo(p3, "Tiburones", Vector3.zero);
        CrearPunto(tiburonesP3, "Tiburon_1", new Vector3(0f, yPez, -8f));
        CrearPunto(tiburonesP3, "Tiburon_2", new Vector3(0f, yPez, 12f));

        // --- Paso 4: zona abierta con 3 circulos, minas y tiburones ---
        Transform p4 = CrearHijo(raiz.transform, "Paso4", Vector3.zero);
        CrearInicio(p4, new Vector3(0f, 0f, -36f));
        CrearArea(p4, new Vector3(0f, 0f, -5f), new Vector3(60f, 30f, 74f));
        Transform circulos = CrearHijo(p4, "Circulos", Vector3.zero);
        CrearCirculo(circulos, "Circulo_1", new Vector3(-15f, yPez, -15f), -yPez, matCirculo);
        CrearCirculo(circulos, "Circulo_2", new Vector3(15f, yPez, -5f), -yPez, matCirculo);
        CrearCirculo(circulos, "Circulo_3", new Vector3(-2f, yPez, 22f), -yPez, matCirculo);
        Transform minasP4 = CrearHijo(p4, "Minas", Vector3.zero);
        CrearPunto(minasP4, "Mina_1", new Vector3(-8f, yMina, -8f));
        CrearPunto(minasP4, "Mina_2", new Vector3(8f, yMina, -18f));
        CrearPunto(minasP4, "Mina_3", new Vector3(7f, yMina, 12f));
        CrearPunto(minasP4, "Mina_4", new Vector3(-12f, yMina, 6f));
        Transform tiburonesP4 = CrearHijo(p4, "Tiburones", Vector3.zero);
        CrearPunto(tiburonesP4, "Tiburon_1", new Vector3(0f, yPez, 2f));
        CrearPunto(tiburonesP4, "Tiburon_2", new Vector3(14f, yPez, 20f));

        p2.gameObject.SetActive(false);
        p3.gameObject.SetActive(false);
        p4.gameObject.SetActive(false);
        raiz.SetActive(false); // MenuMismaEscena la enciende solo durante el tutorial del conductor
        return raiz;
    }

    /// <summary>Asigna los arreglos de ZonaTutorialConductor buscando los hijos por nombre.</summary>
    private static void VincularZona(ZonaTutorialConductor zona, List<string> informe)
    {
        Transform r = zona.transform;
        var inicios = new Transform[4];
        var areas = new BoxCollider[4];
        var visuales = new GameObject[4];

        for (int i = 0; i < 4; i++)
        {
            Transform paso = r.Find("Paso" + (i + 1));
            if (paso == null) { informe.Add("AVISO: falta Paso" + (i + 1) + " en " + NOMBRE_RAIZ + "."); continue; }
            visuales[i] = paso.gameObject;
            inicios[i] = paso.Find("InicioBote");
            Transform area = paso.Find("AreaBote");
            areas[i] = area != null ? area.GetComponent<BoxCollider>() : null;
        }

        Undo.RecordObject(zona, TITULO);
        zona.iniciosBote = inicios;
        zona.areasBote = areas;
        zona.visualesPaso = visuales;
        zona.minasObligatoriasPaso2 = Hijos(r.Find("Paso2/MinasObligatorias"));
        zona.minasPaso2 = Hijos(r.Find("Paso2/Minas"));
        zona.metaPaso2 = r.Find("Paso2/Meta");
        zona.tiburonesPaso3 = Hijos(r.Find("Paso3/Tiburones"));
        zona.metaPaso3 = r.Find("Paso3/Meta");
        zona.circulosPaso4 = Hijos(r.Find("Paso4/Circulos"));
        zona.minasPaso4 = Hijos(r.Find("Paso4/Minas"));
        zona.tiburonesPaso4 = Hijos(r.Find("Paso4/Tiburones"));
        EditorUtility.SetDirty(zona);
    }

    private static Transform[] Hijos(Transform padre)
    {
        if (padre == null) return new Transform[0];
        var lista = new List<Transform>();
        foreach (Transform t in padre) lista.Add(t);
        return lista.ToArray();
    }

    private static Transform CrearHijo(Transform padre, string nombre, Vector3 posLocal)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        return go.transform;
    }

    private static void CrearPunto(Transform padre, string nombre, Vector3 posLocal)
    {
        CrearHijo(padre, nombre, posLocal);
    }

    private static void CrearInicio(Transform paso, Vector3 posLocal)
    {
        Transform t = CrearHijo(paso, "InicioBote", posLocal);
        t.localRotation = Quaternion.identity; // mira hacia +Z local (hacia la meta)
    }

    private static void CrearArea(Transform paso, Vector3 centroLocal, Vector3 tam)
    {
        Transform t = CrearHijo(paso, "AreaBote", centroLocal);
        BoxCollider col = t.gameObject.AddComponent<BoxCollider>();
        col.size = tam;
        col.isTrigger = true;
        col.enabled = false; // solo se usan sus medidas (BoatController limita el bote por codigo)
    }

    private static void CrearPasillo(Transform paso, Vector3 centroLocal, float ancho, float largo, Material mat)
    {
        Transform pasillo = CrearHijo(paso, "Pasillo", centroLocal);
        CrearVisual(pasillo, "Borde_Izq", PrimitiveType.Cube, new Vector3(-ancho * 0.5f, 0.2f, 0f), new Vector3(0.4f, 0.6f, largo), mat);
        CrearVisual(pasillo, "Borde_Der", PrimitiveType.Cube, new Vector3(ancho * 0.5f, 0.2f, 0f), new Vector3(0.4f, 0.6f, largo), mat);
        CrearVisual(pasillo, "Borde_Fondo", PrimitiveType.Cube, new Vector3(0f, 0.2f, -largo * 0.5f), new Vector3(ancho, 0.6f, 0.4f), mat);
    }

    private static void CrearMeta(Transform paso, string nombre, Vector3 posLocal, Material mat)
    {
        Transform meta = CrearHijo(paso, nombre, posLocal);
        CrearVisual(meta, "Disco", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(RADIO_META * 2f, 0.025f, RADIO_META * 2f), mat);
        CrearVisual(meta, "Poste", PrimitiveType.Cylinder, new Vector3(0f, 3f, 0f), new Vector3(0.4f, 3f, 0.4f), mat);
    }

    private static void CrearCirculo(Transform padre, string nombre, Vector3 posLocal, float alturaDisco, Material mat)
    {
        // El punto esta a la altura de los peces (ahi aparece el pez); el disco, sobre el agua
        Transform c = CrearHijo(padre, nombre, posLocal);
        CrearVisual(c, "Disco", PrimitiveType.Cylinder, new Vector3(0f, alturaDisco + 0.05f, 0f), new Vector3(RADIO_CIRCULO * 2f, 0.025f, RADIO_CIRCULO * 2f), mat);
    }

    private static void CrearVisual(Transform padre, string nombre, PrimitiveType tipo, Vector3 posLocal, Vector3 escala, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col); // solo visual: no debe chocar con nada
    }

    // ---------------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------------
    private static Transform CrearPanel(Transform canvas, MenuMismaEscena menu)
    {
        TMP_Text referencia = menu.panelMenuConductor != null ? menu.panelMenuConductor.GetComponentInChildren<TMP_Text>(true) : null;

        GameObject panel = CrearRect(NOMBRE_PANEL, canvas);
        Undo.RegisterCreatedObjectUndo(panel, TITULO);
        Estirar(panel.GetComponent<RectTransform>());
        panel.transform.SetAsLastSibling();

        for (int i = 0; i < CANTIDAD_SLIDES; i++)
        {
            GameObject slide = CrearRect("Slide_" + (i + 1), panel.transform);
            Estirar(slide.GetComponent<RectTransform>());
            Image fondo = slide.AddComponent<Image>();
            fondo.color = new Color(0.05f, 0.08f, 0.12f, 1f); // tapa la pantalla (y los toques)

            GameObject imagen = CrearRect("Imagen", slide.transform);
            Estirar(imagen.GetComponent<RectTransform>());
            Image img = imagen.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false; // se enciende al vincular su imagen

            GameObject provisional = CrearRect("TextoProvisional", slide.transform);
            RectTransform rtProv = provisional.GetComponent<RectTransform>();
            Estirar(rtProv);
            rtProv.offsetMin = new Vector2(120f, 200f);
            rtProv.offsetMax = new Vector2(-120f, -120f);
            CrearTexto(provisional, TEXTOS_SLIDES[i], 48f, Color.white, referencia);

            if (i < CANTIDAD_SLIDES - 1) CrearBotonContinuar(slide.transform, referencia);
            slide.SetActive(false);
        }

        // Recuadro de progreso (arriba al centro, no bloquea los toques)
        GameObject progreso = CrearRect("Progreso", panel.transform);
        RectTransform rtProg = progreso.GetComponent<RectTransform>();
        rtProg.anchorMin = new Vector2(0.5f, 1f);
        rtProg.anchorMax = new Vector2(0.5f, 1f);
        rtProg.pivot = new Vector2(0.5f, 1f);
        rtProg.anchoredPosition = new Vector2(0f, -30f);
        rtProg.sizeDelta = new Vector2(1200f, 220f);
        Image fondoProg = progreso.AddComponent<Image>();
        fondoProg.color = new Color(0f, 0f, 0f, 0.6f);
        fondoProg.raycastTarget = false;

        GameObject textoProg = CrearRect("Texto", progreso.transform);
        RectTransform rtTexto = textoProg.GetComponent<RectTransform>();
        Estirar(rtTexto);
        rtTexto.offsetMin = new Vector2(30f, 15f);
        rtTexto.offsetMax = new Vector2(-30f, -15f);
        CrearTexto(textoProg, "", 40f, Color.white, referencia);
        progreso.SetActive(false);

        panel.SetActive(false); // MenuMismaEscena lo abre con el boton Tutorial del conductor
        return panel.transform;
    }

    private static void CrearBotonContinuar(Transform slide, TMP_Text referencia)
    {
        GameObject boton = CrearRect("Btn_Continuar", slide);
        RectTransform rt = boton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-60f, 60f);
        rt.sizeDelta = new Vector2(420f, 140f);

        Image img = boton.AddComponent<Image>();
        img.color = new Color(0.15f, 0.65f, 0.3f, 1f);
        Button b = boton.AddComponent<Button>();
        b.targetGraphic = img;

        GameObject texto = CrearRect("Texto", boton.transform);
        Estirar(texto.GetComponent<RectTransform>());
        CrearTexto(texto, "Continuar", 52f, Color.white, referencia);
    }

    private static GameObject CrearRect(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        return go;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CrearTexto(GameObject go, string texto, float tam, Color color, TMP_Text referencia)
    {
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (referencia != null && referencia.font != null)
        {
            t.font = referencia.font;
            t.fontSharedMaterial = referencia.fontSharedMaterial;
        }
        t.text = texto;
        t.fontSize = tam;
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
    }

    // ---------------------------------------------------------------------
    // IMAGENES
    // ---------------------------------------------------------------------
    private static void VincularImagenes(GameObject[] slides, List<string> informe)
    {
        CrearCarpeta(CARPETA_IMAGENES);

        var vinculadas = new List<string>();
        var faltan = new List<string>();

        for (int i = 0; i < slides.Length; i++)
        {
            string baseNombre = "slide_" + (i + 1).ToString("00");
            string ruta = BuscarImagen(baseNombre);
            if (ruta == null) { faltan.Add(baseNombre); continue; }
            if (slides[i] == null) continue;

            TextureImporter imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            Transform hijo = slides[i].transform.Find("Imagen");
            Image img = hijo != null ? hijo.GetComponent<Image>() : null;
            if (sprite == null || img == null)
            {
                informe.Add("AVISO: no se pudo poner " + Path.GetFileName(ruta) + " en " + slides[i].name + " (necesita un hijo 'Imagen' con Image).");
                continue;
            }

            Undo.RecordObject(img, TITULO);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.enabled = true;
            EditorUtility.SetDirty(img);

            // Con imagen ya no hace falta el texto provisional
            Transform provisional = slides[i].transform.Find("TextoProvisional");
            if (provisional != null && provisional.gameObject.activeSelf)
            {
                Undo.RecordObject(provisional.gameObject, TITULO);
                provisional.gameObject.SetActive(false);
            }

            vinculadas.Add(slides[i].name + " <- " + Path.GetFileName(ruta));
        }

        if (vinculadas.Count > 0) informe.Add("Imagenes vinculadas:\n" + string.Join("\n", vinculadas));
        if (faltan.Count > 0)
            informe.Add("Sin imagen todavia (se ve el texto provisional): " + string.Join(", ", faltan) +
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
    private static GameObject BuscarRaiz(string nombre)
    {
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == nombre) return go;
        }
        return null;
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

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        string nombre = Path.GetFileName(ruta);
        if (!AssetDatabase.IsValidFolder(padre)) CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    private static void AsignarArreglo(SerializedProperty prop, Object[] valores)
    {
        if (prop == null) return;
        prop.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }
}
