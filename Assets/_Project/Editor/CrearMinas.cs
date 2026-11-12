using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Herramienta de editor (no va en las builds).
/// Menu de Unity:  Fishing Rush > Crear minas
///
/// Con la escena Main abierta, crea todo lo que las minas necesitan fuera del codigo:
///   1. Prefab  Assets/_Project/Prefabs/Mina.prefab  (esfera con puas, hecha con primitivas):
///        NetworkObject + Rigidbody cinematico + CapsuleCollider Trigger + MinaExplosiva.
///   2. Lo registra en la lista de Network Prefabs del NetworkManager (si no estaba ya).
///   3. Objeto "MinaSpawner" en la escena, con el prefab y el mismo tamano de lago del FishSpawner.
///   4. "Aviso_BoteInmovil" dentro de Canvas_Boat (pantalla roja + cuenta atras del conductor).
///
/// Se puede ejecutar mas de una vez: lo que ya existe no se duplica.
/// Lo creado en la escena se deshace con Ctrl+Z (el prefab y los materiales se borran a mano).
/// </summary>
public static class CrearMinas
{
    private const string TITULO = "Crear minas";

    private const string CARPETA_PREFABS = "Assets/_Project/Prefabs";
    private const string CARPETA_MATERIALES = "Assets/_Project/Materials";
    private const string RUTA_PREFAB = CARPETA_PREFABS + "/Mina.prefab";
    private const string RUTA_MAT_CUERPO = CARPETA_MATERIALES + "/Mina_Cuerpo.mat";
    private const string RUTA_MAT_PUAS = CARPETA_MATERIALES + "/Mina_Puas.mat";

    private const string NOMBRE_SPAWNER = "MinaSpawner";
    private const string NOMBRE_AVISO = "Aviso_BoteInmovil";

    [MenuItem("Fishing Rush/Crear minas")]
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
            EditorUtility.DisplayDialog(TITULO,
                "No se encontro MenuMismaEscena en la escena abierta. Abre la escena Main.", "OK");
            return;
        }

        string resumen = "";

        // =========================================================
        // 1. PREFAB DE LA MINA
        // =========================================================
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB);
        if (prefab == null)
        {
            prefab = CrearPrefabMina();
            resumen += "- Prefab creado: " + RUTA_PREFAB + "\n";
        }
        else
        {
            resumen += "- El prefab ya existia: " + RUTA_PREFAB + "\n";
        }

        if (prefab == null)
        {
            EditorUtility.DisplayDialog(TITULO, "No se pudo crear el prefab de la mina. Revisa la consola.", "OK");
            return;
        }

        // =========================================================
        // 2. NETWORK PREFABS
        // =========================================================
        resumen += "- " + RegistrarEnNetworkPrefabs(prefab) + "\n";

        Undo.SetCurrentGroupName(TITULO);
        int grupoUndo = Undo.GetCurrentGroup();

        // =========================================================
        // 3. MINASPAWNER EN LA ESCENA
        // =========================================================
        MinaSpawner minaSpawner = Object.FindObjectOfType<MinaSpawner>(true);
        if (minaSpawner == null)
        {
            GameObject go = new GameObject(NOMBRE_SPAWNER);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + NOMBRE_SPAWNER);
            minaSpawner = go.AddComponent<MinaSpawner>();

            // Mismo lago que el de los peces
            FishSpawner fishSpawner = Object.FindObjectOfType<FishSpawner>(true);
            if (fishSpawner != null)
            {
                go.transform.position = fishSpawner.transform.position;
                minaSpawner.lakeCenter = fishSpawner.lakeCenter;
                minaSpawner.lakeSize = fishSpawner.lakeSize;
            }
            resumen += "- Objeto " + NOMBRE_SPAWNER + " creado en la escena\n";
        }
        else
        {
            resumen += "- " + NOMBRE_SPAWNER + " ya existia en la escena\n";
        }

        if (minaSpawner.minaPrefab == null)
        {
            minaSpawner.minaPrefab = prefab;
            EditorUtility.SetDirty(minaSpawner);
        }

        // =========================================================
        // 4. AVISO DEL CONDUCTOR (dentro de Canvas_Boat)
        // =========================================================
        AvisoBoteInmovil aviso = Object.FindObjectOfType<AvisoBoteInmovil>(true);
        if (aviso != null)
        {
            resumen += "- " + NOMBRE_AVISO + " ya existia\n";
        }
        else if (menu.canvasControlesBote == null)
        {
            resumen += "- OJO: MenuMismaEscena no tiene asignado 'canvasControlesBote': no se creo el aviso del conductor\n";
        }
        else
        {
            aviso = CrearAviso(menu.canvasControlesBote.transform, BuscarEstiloTexto(menu));
            resumen += "- " + NOMBRE_AVISO + " creado dentro de " + menu.canvasControlesBote.name + "\n";
        }

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = minaSpawner.gameObject;

        EditorUtility.DisplayDialog(TITULO,
            "Listo:\n\n" + resumen + "\n" +
            "Guarda la escena (Ctrl+S).\n" +
            "En el objeto MinaSpawner puedes ajustar cuantas minas hay y a que altura flotan.", "OK");
    }

    // ---------------------------------------------------------------------
    // Prefab
    // ---------------------------------------------------------------------

    private static GameObject CrearPrefabMina()
    {
        AsegurarCarpeta(CARPETA_PREFABS);
        AsegurarCarpeta(CARPETA_MATERIALES);

        Material matCuerpo = ObtenerMaterial(RUTA_MAT_CUERPO, new Color(0.16f, 0.17f, 0.20f));
        Material matPuas = ObtenerMaterial(RUTA_MAT_PUAS, new Color(0.85f, 0.10f, 0.08f));

        GameObject raiz = new GameObject("Mina");

        // Cuerpo: esfera de 1.2 m
        CrearEsferaVisual("Cuerpo", raiz.transform, Vector3.zero, 1.2f, matCuerpo);

        // Puas rojas: arriba, 4 a los lados y 4 en diagonal hacia arriba (las de abajo no se verian)
        Vector3[] direcciones =
        {
            Vector3.up,
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f)
        };
        for (int i = 0; i < direcciones.Length; i++)
        {
            CrearEsferaVisual("Pua_" + i, raiz.transform, direcciones[i].normalized * 0.62f, 0.28f, matPuas);
        }

        // Fisica: trigger alto (capsula vertical) para que choque con el casco del bote aunque
        // luego subas o bajes la mina. Rigidbody cinematico: necesario para que salte el trigger.
        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CapsuleCollider trigger = raiz.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.direction = 1; // eje Y
        trigger.radius = 0.9f;
        trigger.height = 6f;
        trigger.center = Vector3.zero;

        raiz.AddComponent<NetworkObject>();
        raiz.AddComponent<MinaExplosiva>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA_PREFAB);
        Object.DestroyImmediate(raiz);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }

    private static GameObject CrearEsferaVisual(string nombre, Transform padre, Vector3 posLocal, float diametro, Material material)
    {
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = nombre;
        esfera.transform.SetParent(padre, false);
        esfera.transform.localPosition = posLocal;
        esfera.transform.localScale = Vector3.one * diametro;

        // Solo visual: el unico collider de la mina es el trigger de la raiz
        Collider col = esfera.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        if (material != null) esfera.GetComponent<MeshRenderer>().sharedMaterial = material;
        return esfera;
    }

    private static Material ObtenerMaterial(string ruta, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);

        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        int corte = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, corte);
        string nombre = ruta.Substring(corte + 1);
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    // ---------------------------------------------------------------------
    // Network Prefabs
    // ---------------------------------------------------------------------

    /// <summary>
    /// Agrega el prefab a la primera lista de Network Prefabs del NetworkManager de la escena.
    /// Se hace por SerializedObject (como lo haria el Inspector). Devuelve una linea para el resumen.
    /// </summary>
    private static string RegistrarEnNetworkPrefabs(GameObject prefab)
    {
        const string MANUAL = "OJO: registra tu el prefab Mina a mano: NetworkManager > Network Prefabs Lists > abre la lista > '+'";

        NetworkManager nm = Object.FindObjectOfType<NetworkManager>(true);
        if (nm == null) return "No hay NetworkManager en la escena. " + MANUAL;

        SerializedObject soNm = new SerializedObject(nm);
        SerializedProperty listas = soNm.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
        if (listas == null || !listas.isArray || listas.arraySize == 0) return "No se encontro la lista de Network Prefabs. " + MANUAL;

        Object primeraLista = null;

        for (int i = 0; i < listas.arraySize; i++)
        {
            Object lista = listas.GetArrayElementAtIndex(i).objectReferenceValue;
            if (lista == null) continue;
            if (primeraLista == null) primeraLista = lista;

            SerializedProperty elementos = new SerializedObject(lista).FindProperty("List");
            if (elementos == null || !elementos.isArray) continue;

            for (int j = 0; j < elementos.arraySize; j++)
            {
                SerializedProperty p = elementos.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");
                if (p != null && p.objectReferenceValue == prefab)
                {
                    return "El prefab ya estaba en Network Prefabs (" + lista.name + ")";
                }
            }
        }

        if (primeraLista == null) return "La lista de Network Prefabs esta vacia. " + MANUAL;

        SerializedObject soLista = new SerializedObject(primeraLista);
        SerializedProperty lst = soLista.FindProperty("List");
        if (lst == null || !lst.isArray) return "No se pudo leer la lista de Network Prefabs. " + MANUAL;

        int indice = lst.arraySize;
        lst.InsertArrayElementAtIndex(indice);
        SerializedProperty nuevo = lst.GetArrayElementAtIndex(indice);

        SerializedProperty pPrefab = nuevo.FindPropertyRelative("Prefab");
        if (pPrefab == null) return "No se pudo escribir en la lista de Network Prefabs. " + MANUAL;
        pPrefab.objectReferenceValue = prefab;

        // El elemento nuevo se crea copiando el anterior: se limpian sus otros campos
        SerializedProperty pOverride = nuevo.FindPropertyRelative("Override");
        if (pOverride != null) pOverride.intValue = 0;
        SerializedProperty pSource = nuevo.FindPropertyRelative("SourcePrefabToOverride");
        if (pSource != null) pSource.objectReferenceValue = null;
        SerializedProperty pHash = nuevo.FindPropertyRelative("SourceHashToOverride");
        if (pHash != null) pHash.longValue = 0;
        SerializedProperty pTarget = nuevo.FindPropertyRelative("OverridingTargetPrefab");
        if (pTarget != null) pTarget.objectReferenceValue = null;

        soLista.ApplyModifiedProperties();
        EditorUtility.SetDirty(primeraLista);
        AssetDatabase.SaveAssets();

        return "Prefab registrado en Network Prefabs (" + primeraLista.name + ")";
    }

    // ---------------------------------------------------------------------
    // Aviso del conductor
    // ---------------------------------------------------------------------

    private static AvisoBoteInmovil CrearAviso(Transform canvas, TMP_Text estilo)
    {
        // Contenedor siempre activo (lleva el script) a pantalla completa
        GameObject contenedor = new GameObject(NOMBRE_AVISO, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NOMBRE_AVISO);
        contenedor.transform.SetParent(canvas, false);
        contenedor.transform.SetAsLastSibling(); // se dibuja encima de los botones
        contenedor.layer = canvas.gameObject.layer;
        Estirar((RectTransform)contenedor.transform, Vector2.zero, Vector2.one);

        // Panel rojo semitransparente: NO bloquea los toques (raycastTarget apagado)
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(contenedor.transform, false);
        panel.layer = contenedor.layer;
        Estirar((RectTransform)panel.transform, Vector2.zero, Vector2.one);

        Image fondo = panel.GetComponent<Image>();
        fondo.color = new Color(0.85f, 0.05f, 0.05f, 0.30f);
        fondo.raycastTarget = false;

        // Texto en la parte de arriba, para no tapar los botones del bote
        GameObject goTexto = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        goTexto.transform.SetParent(panel.transform, false);
        goTexto.layer = panel.layer;
        Estirar((RectTransform)goTexto.transform, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.95f));

        TextMeshProUGUI tmp = goTexto.GetComponent<TextMeshProUGUI>();
        if (estilo != null && estilo.font != null)
        {
            tmp.font = estilo.font;
            tmp.fontSharedMaterial = estilo.fontSharedMaterial;
        }
        tmp.text = "MINA ACTIVADA!\nConduccion inhabilitada\n5.0 s";
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 90f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        AvisoBoteInmovil aviso = contenedor.AddComponent<AvisoBoteInmovil>();
        aviso.panel = panel;
        aviso.texto = tmp;

        panel.SetActive(false);
        return aviso;
    }

    private static void Estirar(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>Toma la fuente de algun texto del menu para que el aviso tenga el mismo estilo.</summary>
    private static TMP_Text BuscarEstiloTexto(MenuMismaEscena menu)
    {
        GameObject[] candidatos = { menu.canvasMenuConductor, menu.canvasMenu, menu.canvasGameHUD };
        foreach (GameObject go in candidatos)
        {
            if (go == null) continue;

            TMP_Text mejor = null;
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == "Title") return t;
                if (mejor == null) mejor = t;
            }
            if (mejor != null) return mejor;
        }
        return null;
    }
}
