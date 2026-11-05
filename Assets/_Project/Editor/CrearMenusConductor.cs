using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Herramienta de editor (no va en las builds).
/// Menu de Unity:  Fishing Rush > Crear menus del conductor
///
/// A partir de los canvas que ya existen (asignados en MenuMismaEscena) crea:
///   1. Canvas_Menu_Conductor     : copia de Canvas_Menu con SOLO el panel principal
///                                  (Jugar / Tutorial / Salir, sin el boton del Wiimote),
///                                  en Screen Space - Overlay, + panel "Esperando al pescador...".
///   2. Canvas_GameOver_Conductor : copia de Canvas_GameOver en Screen Space - Overlay.
///   3. Panel_EsperandoConductor  : panel nuevo dentro de Canvas_Menu (el del pescador, World Space)
///                                  con "Esperando a jugador 2 (conductor)...".
/// y los asigna en los campos nuevos de MenuMismaEscena.
///
/// Los botones copiados conservan sus onClick (apuntan al mismo MenuMismaEscena), y el script
/// decide segun el rol que hacer. Todo se puede deshacer con Ctrl+Z.
/// </summary>
public static class CrearMenusConductor
{
    private const string NOMBRE_MENU_CONDUCTOR = "Canvas_Menu_Conductor";
    private const string NOMBRE_PANEL_MENU_CONDUCTOR = "Panel_MenuConductor";
    private const string NOMBRE_GAMEOVER_CONDUCTOR = "Canvas_GameOver_Conductor";
    private const string NOMBRE_ESPERA_CONDUCTOR = "Panel_EsperaConductor";
    private const string NOMBRE_ESPERA_PESCADOR = "Panel_EsperandoConductor";

    private const string TEXTO_ESPERA_CONDUCTOR = "Esperando al pescador...";
    private const string TEXTO_ESPERA_PESCADOR = "Esperando a jugador 2 (conductor)...";

    // Ayudas visuales de botones del Wiimote ("A", "B", barra de pistas): el conductor usa tactil
    private const bool QUITAR_AYUDAS_WIIMOTE = true;
    private static readonly string[] NOMBRES_AYUDAS_WIIMOTE = { "HintsBar", "BotonA", "BotonB" };

    [MenuItem("Fishing Rush/Crear menus del conductor")]
    private static void Crear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Crear menus del conductor", "Sal del modo Play antes de usar esta herramienta.", "OK");
            return;
        }

        MenuMismaEscena menu = Object.FindObjectOfType<MenuMismaEscena>(true);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Crear menus del conductor",
                "No se encontro MenuMismaEscena en la escena abierta. Abre la escena Main.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(menu);

        // --- Validaciones ---
        if (menu.canvasMenu == null || menu.canvasGameOver == null || menu.panelMenuPrincipal == null)
        {
            EditorUtility.DisplayDialog("Crear menus del conductor",
                "MenuMismaEscena necesita tener asignados canvasMenu, canvasGameOver y panelMenuPrincipal.", "OK");
            return;
        }

        if (menu.canvasMenuConductor != null || menu.canvasGameOverConductor != null || menu.panelEsperandoConductor != null)
        {
            EditorUtility.DisplayDialog("Crear menus del conductor",
                "Ya hay menus del conductor asignados en MenuMismaEscena.\n\n" +
                "Si quieres volver a generarlos, borra Canvas_Menu_Conductor, Canvas_GameOver_Conductor y " +
                "Panel_EsperandoConductor, y deja vacios esos campos en el Inspector.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Crear menus del conductor");
        int grupoUndo = Undo.GetCurrentGroup();

        TMP_Text estiloTexto = BuscarEstiloTexto(menu.panelMenuPrincipal.transform);

        // =========================================================
        // 1. MENU DEL CONDUCTOR
        // =========================================================
        // Solo se copia el panel principal: Panel_Tutorial / Calibracion / IniciarMenu NO se copian
        // (Panel_Tutorial tiene un NetworkObject y no debe duplicarse nunca)
        GameObject menuConductor = CrearCanvasOverlay(menu.canvasMenu, NOMBRE_MENU_CONDUCTOR);
        Transform panelConductor = CopiarHijo(menu.panelMenuPrincipal.transform, menuConductor.transform);

        // Fuera el boton del Wiimote (y su "Tile" contenedor si solo lo tenia a el)
        if (menu.btnWiimote != null)
        {
            Transform wii = Equivalente(menu.panelMenuPrincipal.transform, panelConductor, menu.btnWiimote.transform);
            if (wii != null)
            {
                if (wii.parent != null && wii.parent != panelConductor && wii.parent.childCount == 1) wii = wii.parent;
                Object.DestroyImmediate(wii.gameObject);
            }
        }

        if (QUITAR_AYUDAS_WIIMOTE) QuitarAyudasWiimote(panelConductor);

        panelConductor.name = NOMBRE_PANEL_MENU_CONDUCTOR;
        panelConductor.gameObject.SetActive(true);

        GameObject esperaConductor = CrearPanelEspera(menuConductor.transform, NOMBRE_ESPERA_CONDUCTOR, TEXTO_ESPERA_CONDUCTOR, estiloTexto);

        QuitarComponentesDeRed(menuConductor);

        // =========================================================
        // 2. GAME OVER DEL CONDUCTOR
        // =========================================================
        GameObject gameOverConductor = CrearCanvasOverlay(menu.canvasGameOver, NOMBRE_GAMEOVER_CONDUCTOR);
        foreach (Transform hijo in menu.canvasGameOver.transform)
        {
            if (hijo.GetComponentInChildren<NetworkObject>(true) != null) continue; // nunca duplicar objetos de red
            CopiarHijo(hijo, gameOverConductor.transform);
        }

        if (QUITAR_AYUDAS_WIIMOTE)
        {
            foreach (Transform hijo in gameOverConductor.transform) QuitarAyudasWiimote(hijo);
        }

        TMP_Text scoreOriginal = so.FindProperty("finalScoreText").objectReferenceValue as TMP_Text;
        Transform scoreConductor = scoreOriginal != null
            ? Equivalente(menu.canvasGameOver.transform, gameOverConductor.transform, scoreOriginal.transform)
            : null;

        QuitarComponentesDeRed(gameOverConductor);

        // =========================================================
        // 3. ESPERA DEL PESCADOR (dentro de Canvas_Menu, World Space)
        // =========================================================
        GameObject esperaPescador = CrearPanelEspera(menu.canvasMenu.transform, NOMBRE_ESPERA_PESCADOR, TEXTO_ESPERA_PESCADOR, estiloTexto);

        // =========================================================
        // 4. ASIGNAR EN MenuMismaEscena
        // =========================================================
        so.Update();
        so.FindProperty("canvasMenuConductor").objectReferenceValue = menuConductor;
        so.FindProperty("panelMenuConductor").objectReferenceValue = panelConductor.gameObject;
        so.FindProperty("panelEsperaConductor").objectReferenceValue = esperaConductor;
        so.FindProperty("canvasGameOverConductor").objectReferenceValue = gameOverConductor;
        so.FindProperty("finalScoreTextConductor").objectReferenceValue =
            scoreConductor != null ? scoreConductor.GetComponent<TextMeshProUGUI>() : null;
        so.FindProperty("panelEsperandoConductor").objectReferenceValue = esperaPescador;
        so.ApplyModifiedProperties();

        // Los canvas nuevos quedan apagados: MenuMismaEscena los enciende segun el rol
        menuConductor.SetActive(false);
        gameOverConductor.SetActive(false);

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = menuConductor;

        EditorUtility.DisplayDialog("Crear menus del conductor",
            "Listo. Se crearon y asignaron en MenuMismaEscena:\n\n" +
            "- " + NOMBRE_MENU_CONDUCTOR + " (con " + NOMBRE_ESPERA_CONDUCTOR + ")\n" +
            "- " + NOMBRE_GAMEOVER_CONDUCTOR + (scoreConductor == null ? "  (OJO: no se encontro el texto del score)" : "") + "\n" +
            "- " + NOMBRE_ESPERA_PESCADOR + " dentro de " + menu.canvasMenu.name + "\n\n" +
            "Revisa como se ven, ajusta lo que quieras y guarda la escena (Ctrl+S).\n" +
            "Si algo no te gusta: Ctrl+Z lo deshace todo.", "OK");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Crea un canvas NUEVO (vacio) al lado del original, en Screen Space - Overlay.
    /// No se clona el canvas original entero para no arrastrar objetos de red (Panel_Tutorial).
    /// </summary>
    private static GameObject CrearCanvasOverlay(GameObject original, string nombre)
    {
        GameObject copia = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(copia, "Crear " + nombre);
        copia.layer = original.layer;
        copia.transform.SetParent(original.transform.parent, false);
        copia.transform.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);

        Canvas canvasOriginal = original.GetComponent<Canvas>();
        Canvas canvas = copia.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (canvasOriginal != null)
        {
            canvas.sortingOrder = canvasOriginal.sortingOrder;
            canvas.pixelPerfect = canvasOriginal.pixelPerfect;
        }

        // Misma resolucion de referencia que el canvas World Space original, para que el
        // contenido mantenga sus proporciones en la pantalla del celular
        RectTransform rtOriginal = original.transform as RectTransform;
        Vector2 referencia = (rtOriginal != null && rtOriginal.sizeDelta.x > 1f && rtOriginal.sizeDelta.y > 1f)
            ? rtOriginal.sizeDelta
            : new Vector2(1280f, 720f);

        CanvasScaler scaler = copia.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = copia.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referencia;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (copia.GetComponent<GraphicRaycaster>() == null) copia.AddComponent<GraphicRaycaster>();

        return copia;
    }

    /// <summary>Copia un hijo (con todo lo que tiene dentro) bajo el canvas nuevo, mismo nombre y layout.</summary>
    private static Transform CopiarHijo(Transform original, Transform nuevoPadre)
    {
        GameObject copia = Object.Instantiate(original.gameObject, nuevoPadre, false);
        copia.name = original.name;
        copia.SetActive(original.gameObject.activeSelf);
        return copia.transform;
    }

    /// <summary>Panel a pantalla completa con fondo oscuro y un texto centrado. Queda apagado.</summary>
    private static GameObject CrearPanelEspera(Transform padre, string nombre, string texto, TMP_Text estilo)
    {
        GameObject panel = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Crear " + nombre);
        panel.transform.SetParent(padre, false);
        panel.transform.SetAsLastSibling();
        panel.layer = padre.gameObject.layer;

        RectTransform rt = (RectTransform)panel.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        GameObject goTexto = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        goTexto.transform.SetParent(panel.transform, false);
        goTexto.layer = panel.layer;

        RectTransform rtTexto = (RectTransform)goTexto.transform;
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(80f, 80f);
        rtTexto.offsetMax = new Vector2(-80f, -80f);

        TextMeshProUGUI tmp = goTexto.GetComponent<TextMeshProUGUI>();
        if (estilo != null && estilo.font != null)
        {
            tmp.font = estilo.font;
            tmp.fontSharedMaterial = estilo.fontSharedMaterial;
        }
        tmp.text = texto;
        tmp.fontSize = 56f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        panel.SetActive(false);
        return panel;
    }

    /// <summary>Quita NetworkObject / NetworkBehaviour de la copia (no puede haber objetos de red duplicados).</summary>
    private static void QuitarComponentesDeRed(GameObject raiz)
    {
        foreach (NetworkBehaviour nb in raiz.GetComponentsInChildren<NetworkBehaviour>(true))
            Object.DestroyImmediate(nb);

        foreach (NetworkObject no in raiz.GetComponentsInChildren<NetworkObject>(true))
            Object.DestroyImmediate(no);
    }

    private static void QuitarAyudasWiimote(Transform panel)
    {
        foreach (string nombre in NOMBRES_AYUDAS_WIIMOTE)
        {
            Transform t = panel.Find(nombre);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }

    /// <summary>Busca en la copia el objeto que corresponde a 'original' (misma ruta de nombres).</summary>
    private static Transform Equivalente(Transform raizOriginal, Transform raizCopia, Transform original)
    {
        if (original == raizOriginal) return raizCopia;

        string ruta = original.name;
        Transform t = original.parent;
        while (t != null && t != raizOriginal)
        {
            ruta = t.name + "/" + ruta;
            t = t.parent;
        }
        if (t == null) return null; // no esta dentro de raizOriginal

        return raizCopia.Find(ruta);
    }

    /// <summary>Toma la fuente del titulo del menu para que los textos nuevos tengan el mismo estilo.</summary>
    private static TMP_Text BuscarEstiloTexto(Transform panel)
    {
        TMP_Text mejor = null;
        foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "Title") return t;
            if (mejor == null) mejor = t;
        }
        return mejor;
    }
}
