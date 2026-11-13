using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Netcode;

public static class CrearTiburon
{
    private const string TITULO = "Crear tiburon";

    private const string RUTA_PREFAB = "Assets/_Project/Prefabs/Tiburon.prefab";
    private const string RUTA_MODELO = "Assets/3D Shark pack/3D shark - Character Pack/Prefabs/shark_modeling.prefab";

    private const string RUTA_ICONO = "Assets/_Project/Materials/Shark_icon_1.png";

    private const float LARGO_DESEADO = 3.5f;
    private const float ALTURA_MODELO = 1.1f;
    private const float ESCALA_POR_DEFECTO = 4f;

    private const string NOMBRE_AVISO_PESCADOR = "Aviso_Peligro_Pescador";
    private const string NOMBRE_AVISO_CONDUCTOR = "Aviso_Peligro_Conductor";

    [MenuItem("Fishing Rush/Crear tiburon")]
    private static void Crear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(TITULO, "Sal del modo Play antes de usar esta herramienta.", "OK");
            return;
        }

        MenuMismaEscena menu = Object.FindObjectOfType<MenuMismaEscena>(true);
        FishSpawner spawner = Object.FindObjectOfType<FishSpawner>(true);
        if (menu == null || spawner == null)
        {
            EditorUtility.DisplayDialog(TITULO,
                "No se encontro MenuMismaEscena o FishSpawner en la escena abierta. Abre la escena Main.", "OK");
            return;
        }
        if (spawner.fishPrefab == null)
        {
            EditorUtility.DisplayDialog(TITULO, "El FishSpawner no tiene asignado su prefab de pez (Fish Prefab).", "OK");
            return;
        }

        string resumen = "";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB);
        if (prefab != null)
        {
            resumen += "- El prefab ya existia: " + RUTA_PREFAB + "\n";
        }
        else
        {
            GameObject modelo = BuscarModelo();
            if (modelo == null)
            {
                EditorUtility.DisplayDialog(TITULO,
                    "No se encontro el modelo del tiburon (shark_modeling.prefab).\n\n" +
                    "Importa el asset '3D shark - Character Free' y vuelve a ejecutar.", "OK");
                return;
            }

            string detalle;
            prefab = CrearPrefab(spawner.fishPrefab, modelo, out detalle);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(TITULO, "No se pudo crear el prefab del tiburon:\n\n" + detalle, "OK");
                return;
            }
            resumen += "- Prefab creado: " + RUTA_PREFAB + " (" + detalle + ")\n";
        }

        resumen += "- " + RegistrarEnNetworkPrefabs(prefab) + "\n";

        Undo.SetCurrentGroupName(TITULO);
        int grupoUndo = Undo.GetCurrentGroup();

        if (spawner.sharkPrefab != prefab)
        {
            Undo.RecordObject(spawner, "Asignar tiburon");
            spawner.sharkPrefab = prefab;
            EditorUtility.SetDirty(spawner);
            resumen += "- Prefab asignado en FishSpawner (Shark Prefab)\n";
        }
        else
        {
            resumen += "- FishSpawner ya tenia el prefab asignado\n";
        }

        resumen += "- " + CrearAvisoSiFalta(menu.canvasGameHUD, NOMBRE_AVISO_PESCADOR, false, menu) + "\n";
        resumen += "- " + CrearAvisoSiFalta(menu.canvasControlesBote, NOMBRE_AVISO_CONDUCTOR, true, menu) + "\n";

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeObject = prefab;

        EditorUtility.DisplayDialog(TITULO,
            "Resultado:\n\n" + resumen + "\n" +
            "Guarda la escena (Ctrl+S).\n" +
            "Si alguna linea empieza con OJO, copiala y enviala.", "OK");
    }

    private static GameObject BuscarModelo()
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_MODELO);
        if (modelo != null) return modelo;

        foreach (string guid in AssetDatabase.FindAssets("shark_modeling t:Prefab"))
        {
            modelo = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (modelo != null) return modelo;
        }
        return null;
    }

    private static GameObject CrearPrefab(GameObject prefabPez, GameObject modelo, out string detalle)
    {
        detalle = "";

        GameObject raiz = (GameObject)PrefabUtility.InstantiatePrefab(prefabPez);
        if (raiz == null)
        {
            detalle = "no se pudo instanciar el prefab del pez";
            return null;
        }

        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(raiz))
            {
                PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }
            raiz.name = "Tiburon";

            FishCube pez = raiz.GetComponent<FishCube>();
            if (pez == null)
            {
                detalle = "el prefab del pez no tiene FishCube en la raiz";
                return null;
            }

            SerializedObject soPez = new SerializedObject(pez);
            SerializedProperty pEsTiburon = soPez.FindProperty("esTiburon");
            if (pEsTiburon == null)
            {
                detalle = "FishCube no tiene el campo 'esTiburon' (los scripts no estan actualizados?)";
                return null;
            }
            pEsTiburon.boolValue = true;
            soPez.ApplyModifiedPropertiesWithoutUndo();

            FishAlive.FishMotion motor = raiz.GetComponentInChildren<FishAlive.FishMotion>(true);
            if (motor == null)
            {
                detalle = "el prefab del pez no tiene FishMotion";
                return null;
            }

            foreach (Renderer r in motor.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }

            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, motor.transform);
            if (m == null)
            {
                detalle = "no se pudo instanciar el modelo del tiburon";
                return null;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(m))
            {
                PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            m.name = "ModeloTiburon";
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one;

            foreach (Collider c in m.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            Material matCuerpo = BuscarMaterial("01 - Default");
            Material matOjos = BuscarMaterial("02 - Default");
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
            {
                Material nuevo = (r is SkinnedMeshRenderer) ? matCuerpo : matOjos;
                if (nuevo == null) continue;

                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = nuevo;
                r.sharedMaterials = mats;
            }

            Animator anim = m.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            float largo = MedirLargo(m);
            float escala = largo > 0.0001f ? LARGO_DESEADO / largo : ESCALA_POR_DEFECTO;
            m.transform.localScale = Vector3.one * escala;

            float escalaPadreY = motor.transform.lossyScale.y;
            if (escalaPadreY < 0.0001f) escalaPadreY = 1f;
            m.transform.localPosition = new Vector3(0f, ALTURA_MODELO / escalaPadreY, 0f);

            TiburonVisual visual = m.AddComponent<TiburonVisual>();
            visual.animator = anim;

            Sprite icono = AssetDatabase.LoadAssetAtPath<Sprite>(RUTA_ICONO);
            foreach (Image img in raiz.GetComponentsInChildren<Image>(true))
            {
                if (img.name != "FishIcon") continue;

                if (icono != null)
                {
                    img.sprite = icono;
                    img.color = Color.white;
                }
                else
                {
                    img.color = new Color(1f, 0.2f, 0.15f, 1f);
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA_PREFAB);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            detalle = largo > 0.0001f
                ? "modelo medido " + largo.ToString("0.00") + " m, escala x" + escala.ToString("0.00")
                : "OJO: no se pudo medir el modelo, se uso escala x" + ESCALA_POR_DEFECTO;
            if (anim == null) detalle += ", OJO: el modelo no tiene Animator";

            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }

    private static Material BuscarMaterial(string nombre)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/3D Shark pack" }))
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null && mat.name == nombre) return mat;
        }
        return null;
    }

    private static float MedirLargo(GameObject modelo)
    {
        bool hay = false;
        Bounds b = new Bounds();
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true))
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hay) return 0f;
        return Mathf.Max(b.size.x, b.size.z);
    }

    private static string RegistrarEnNetworkPrefabs(GameObject prefab)
    {
        const string MANUAL = "OJO: el tiburon NO quedo en Network Prefabs. Agregalo a mano: selecciona DefaultNetworkPrefabs y pulsa '+'. ";

        NetworkManager nm = Object.FindObjectOfType<NetworkManager>(true);
        if (nm == null) return MANUAL + "(no hay NetworkManager en la escena)";

        SerializedObject soNm = new SerializedObject(nm);
        SerializedProperty listas = soNm.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
        if (listas == null || !listas.isArray || listas.arraySize == 0) return MANUAL + "(no se encontro la lista)";

        Object lista = null;
        for (int i = 0; i < listas.arraySize && lista == null; i++)
        {
            lista = listas.GetArrayElementAtIndex(i).objectReferenceValue;
        }
        if (lista == null) return MANUAL + "(la lista esta vacia)";

        SerializedObject soLista = new SerializedObject(lista);
        SerializedProperty elementos = soLista.FindProperty("List");
        if (elementos == null || !elementos.isArray) return MANUAL + "(no se pudo leer la lista)";

        bool yaEstaba = false;
        for (int j = 0; j < elementos.arraySize; j++)
        {
            SerializedProperty p = elementos.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");
            if (p != null && p.objectReferenceValue == prefab) { yaEstaba = true; break; }
        }

        if (!yaEstaba)
        {
            int indice = elementos.arraySize;
            elementos.InsertArrayElementAtIndex(indice);
            SerializedProperty nuevo = elementos.GetArrayElementAtIndex(indice);

            SerializedProperty pPrefab = nuevo.FindPropertyRelative("Prefab");
            if (pPrefab == null) return MANUAL + "(no se pudo escribir en la lista)";
            pPrefab.objectReferenceValue = prefab;

            SerializedProperty pOverride = nuevo.FindPropertyRelative("Override");
            if (pOverride != null) pOverride.intValue = 0;
            SerializedProperty pSource = nuevo.FindPropertyRelative("SourcePrefabToOverride");
            if (pSource != null) pSource.objectReferenceValue = null;
            SerializedProperty pHash = nuevo.FindPropertyRelative("SourceHashToOverride");
            if (pHash != null) pHash.longValue = 0;
            SerializedProperty pTarget = nuevo.FindPropertyRelative("OverridingTargetPrefab");
            if (pTarget != null) pTarget.objectReferenceValue = null;

            soLista.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(lista);
        AssetDatabase.SaveAssets();

        string rutaLista = AssetDatabase.GetAssetPath(lista);
        string guidPrefab = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        bool enDisco = false;
        try
        {
            enDisco = !string.IsNullOrEmpty(rutaLista) && !string.IsNullOrEmpty(guidPrefab)
                      && File.ReadAllText(rutaLista).Contains(guidPrefab);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Crear tiburon] No se pudo leer " + rutaLista + ": " + e.Message);
        }

        if (!enDisco) return MANUAL + "(no se guardo en " + rutaLista + ")";
        return (yaEstaba ? "Ya estaba" : "Registrado") + " en Network Prefabs y guardado en disco (" + lista.name + ")";
    }

    private static string CrearAvisoSiFalta(GameObject canvas, string nombre, bool esConductor, MenuMismaEscena menu)
    {
        foreach (AvisosPeligroUI existente in Object.FindObjectsOfType<AvisosPeligroUI>(true))
        {
            if (existente.esConductor == esConductor) return nombre + " ya existia";
        }

        if (canvas == null)
        {
            return "OJO: no se creo " + nombre + " (MenuMismaEscena no tiene asignado su canvas)";
        }

        Transform padre = canvas.transform;

        GameObject contenedor = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + nombre);
        contenedor.transform.SetParent(padre, false);
        contenedor.transform.SetAsLastSibling();
        contenedor.layer = canvas.layer;
        Estirar((RectTransform)contenedor.transform, Vector2.zero, Vector2.one);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(contenedor.transform, false);
        panel.layer = contenedor.layer;
        Estirar((RectTransform)panel.transform, Vector2.zero, Vector2.one);

        Image fondo = panel.GetComponent<Image>();
        fondo.color = new Color(0.85f, 0.05f, 0.05f, 0.30f);
        fondo.raycastTarget = false;

        GameObject goTexto = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        goTexto.transform.SetParent(panel.transform, false);
        goTexto.layer = panel.layer;
        if (esConductor) Estirar((RectTransform)goTexto.transform, new Vector2(0.2f, 0.36f), new Vector2(0.8f, 0.54f));
        else Estirar((RectTransform)goTexto.transform, new Vector2(0.2f, 0.62f), new Vector2(0.8f, 0.92f));

        TextMeshProUGUI tmp = goTexto.GetComponent<TextMeshProUGUI>();
        TMP_Text estilo = BuscarEstiloTexto(menu);
        if (estilo != null && estilo.font != null)
        {
            tmp.font = estilo.font;
            tmp.fontSharedMaterial = estilo.fontSharedMaterial;
        }
        tmp.text = "TIBURON!\nPescador inhabilitado\n8.0 s";
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 90f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        AvisosPeligroUI aviso = contenedor.AddComponent<AvisosPeligroUI>();
        aviso.panel = panel;
        aviso.fondo = fondo;
        aviso.texto = tmp;
        aviso.esConductor = esConductor;

        panel.SetActive(false);
        return nombre + " creado dentro de " + canvas.name;
    }

    private static void Estirar(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
    }

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
