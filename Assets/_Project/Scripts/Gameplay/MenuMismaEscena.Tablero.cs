using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public partial class MenuMismaEscena
{
    [Header("Tablero de puntajes")]
    [Tooltip("Cuantos equipos se muestran en el celular y la tablet")]
    [SerializeField] private int filasTablero = 5;
    [Tooltip("Cuantos equipos se muestran en la tabla completa de la PC")]
    [SerializeField] private int filasTableroPC = 10;
    [Tooltip("Escala de la grilla del menu del pescador al pasar a 3 columnas (antes 0.8 con 2 columnas)")]
    [SerializeField] private float escalaGrillaConTablero = 0.56f;
    [SerializeField] private Vector2 posicionGrillaConTablero = new Vector2(-62f, 188f);
    [SerializeField] private float tamanoLetraTablero = 32f;

    private Button btnTablero;
    private GameObject panelTablero;
    private TextMeshProUGUI[] columnasTablero;
    private bool tableroAbierto = false;
    private string tableroRed = "";

    private bool capturandoNombres = false;
    private bool enfocarCampoNombre = false;
    private string nombrePescador = "";
    private string nombreConductor = "";

    private float momentoAperturaTablero = 0f;
    private bool enterPendienteTablero = false;
    private float ignorarConfirmarHasta = 0f;
    private bool wasWiiLeft = false;
    private bool wasWiiRight = false;

    private string textoEsperaPescadorOriginal;
    private string textoEsperaConductorOriginal;

    private GUIStyle estiloTitulo;
    private GUIStyle estiloTexto;
    private GUIStyle estiloCampo;
    private GUIStyle estiloBoton;
    private GUIStyle estiloCelda;
    private GUIStyle estiloCeldaIzquierda;
    private Texture2D texturaFondo;

    private static readonly string[] titulosTablero = { "#", "Equipo", "Peces", "Capturas", "Patrones", "Minas", "Tiburon" };
    private static readonly float[] bordesTablero = { 0.03f, 0.08f, 0.40f, 0.51f, 0.64f, 0.77f, 0.86f, 0.98f };

    private bool EsAutoridadLocal => !IsSpawned || IsServer;

    private bool TableroCompletoEnPantalla => tableroAbierto && EsAutoridadLocal && !Application.isMobilePlatform;

    private Button[] BotonesMenuPrincipal()
    {
        if (btnTablero != null) return new Button[] { btnJugar, btnWiimote, btnTutorial, btnTablero, btnSalir };
        return new Button[] { btnJugar, btnWiimote, btnTutorial, btnSalir };
    }

    private int ColumnasMenuActual()
    {
        if (activeButtons == null || btnTablero == null) return 1;
        return System.Array.IndexOf(activeButtons, btnTablero) >= 0 ? 3 : 1;
    }

    private void MoverSeleccion(int dx, int dy)
    {
        if (activeButtons == null || activeButtons.Length == 0) return;

        int columnas = ColumnasMenuActual();
        if (columnas <= 1)
        {
            CambiarSeleccion(dx != 0 ? dx : dy);
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            GameObject seleccionado = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            for (int i = 0; seleccionado != null && i < activeButtons.Length; i++)
            {
                if (activeButtons[i] != null && activeButtons[i].gameObject == seleccionado)
                {
                    selectedButtonIndex = i;
                    break;
                }
            }
        }

        int total = activeButtons.Length;
        int filas = (total + columnas - 1) / columnas;
        int indice = Mathf.Clamp(selectedButtonIndex, 0, total - 1);
        int fila = indice / columnas;
        int columna = indice % columnas;

        if (dx != 0) columna = (columna + dx + columnas) % columnas;
        if (dy != 0) fila = (fila + dy + filas) % filas;

        int nuevo = fila * columnas + columna;
        if (nuevo >= total) nuevo = dx > 0 ? fila * columnas : total - 1;

        SeleccionarBotonMenu(nuevo);
    }

    private void SeleccionarBotonMenu(int indice)
    {
        if (activeButtons == null || indice < 0 || indice >= activeButtons.Length) return;

        selectedButtonIndex = indice;
        ResaltarBotonLocal(indice);

        if (IsSpawned && (IsOwner || IsServer))
        {
            selectedButtonIndexNet.Value = indice;
            if (IsServer) SincronizarSeleccionBotonClientRpc(indice);
        }
    }

    private void PrepararMenuTablero()
    {
        try
        {
            if (EsConductor()) PrepararTileConductor();
            else PrepararTilePescador();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TABLERO] No se pudo agregar el boton al menu: " + e.Message);
        }
    }

    private void PrepararTilePescador()
    {
        if (btnTablero != null || btnTutorial == null) return;

        Transform tileBase = btnTutorial.transform.parent;
        if (tileBase == null || tileBase.parent == null) return;
        Transform grilla = tileBase.parent;

        btnTablero = ClonarTile(tileBase, grilla);
        if (btnTablero == null) return;

        if (btnSalir != null && btnSalir.transform.parent != null && btnSalir.transform.parent.parent == grilla)
        {
            btnTablero.transform.parent.SetSiblingIndex(btnSalir.transform.parent.GetSiblingIndex());
        }

        GridLayoutGroup grid = grilla.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
        }

        RectTransform rt = grilla as RectTransform;
        if (rt != null)
        {
            rt.localScale = new Vector3(escalaGrillaConTablero, escalaGrillaConTablero, 1f);
            rt.anchoredPosition = posicionGrillaConTablero;
        }
    }

    private void PrepararTileConductor()
    {
        if (btnTablero != null || panelMenuConductor == null) return;

        GridLayoutGroup grid = panelMenuConductor.GetComponentInChildren<GridLayoutGroup>(true);
        if (grid == null || grid.transform.childCount == 0) return;

        Transform grilla = grid.transform;
        Transform tileBase = grilla.GetChild(0);
        Transform tileSalir = null;

        for (int i = 0; i < grilla.childCount; i++)
        {
            string nombre = grilla.GetChild(i).name.ToLowerInvariant();
            if (nombre.Contains("tutorial")) tileBase = grilla.GetChild(i);
            if (nombre.Contains("salir")) tileSalir = grilla.GetChild(i);
        }

        btnTablero = ClonarTile(tileBase, grilla);
        if (btnTablero == null) return;

        if (tileSalir != null) btnTablero.transform.parent.SetSiblingIndex(tileSalir.GetSiblingIndex());
    }

    private Button ClonarTile(Transform tileBase, Transform grilla)
    {
        GameObject copia = Instantiate(tileBase.gameObject, grilla);
        copia.name = "Tile_tablero";

        Button boton = copia.GetComponentInChildren<Button>(true);
        if (boton == null)
        {
            Destroy(copia);
            return null;
        }

        if (boton.transform.parent != copia.transform)
        {
            Destroy(copia);
            return null;
        }

        boton.name = "FaceTablero";
        boton.onClick = new Button.ButtonClickedEvent();
        boton.onClick.AddListener(AbrirTablero);

        Image fondo = boton.GetComponent<Image>();
        if (fondo != null) fondo.color = new Color(0.20f, 0.52f, 0.60f, 1f);

        foreach (TMP_Text t in boton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "Label") t.text = "Tablero";
            else if (t.name == "Sub") t.text = "Mejores puntajes";
        }

        TMP_FontAsset fuente = null;
        foreach (TMP_Text t in boton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "Label") fuente = t.font;
        }

        RectTransform icono = boton.transform.Find("Icono") as RectTransform;
        if (icono != null)
        {
            Image img = icono.GetComponent<Image>();
            if (img != null) img.enabled = false;

            GameObject go = new GameObject("TextoIcono", typeof(RectTransform));
            go.layer = icono.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(boton.transform, false);
            rt.SetSiblingIndex(icono.GetSiblingIndex() + 1);
            rt.anchorMin = icono.anchorMin;
            rt.anchorMax = icono.anchorMax;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(220f, icono.sizeDelta.y);
            rt.anchoredPosition = new Vector2(icono.anchoredPosition.x - icono.sizeDelta.x * 0.5f, icono.anchoredPosition.y);

            TextMeshProUGUI top = go.AddComponent<TextMeshProUGUI>();
            if (fuente != null) top.font = fuente;
            top.text = "TOP";
            top.fontSize = 56f;
            top.fontStyle = FontStyles.Bold;
            top.alignment = TextAlignmentOptions.MidlineLeft;
            top.enableWordWrapping = false;
            top.overflowMode = TextOverflowModes.Overflow;
            top.margin = Vector4.zero;
            top.color = Color.white;
            top.raycastTarget = false;
        }

        return boton;
    }

    private void InicializarTableroRed()
    {
        registrandoNombresNet.OnValueChanged += OnRegistrandoNombresCambio;

        if (IsServer)
        {
            RegistroPartida.Cargar();
        }
        else
        {
            PedirTableroServerRpc();
        }
    }

    private void FinalizarTableroRed()
    {
        registrandoNombresNet.OnValueChanged -= OnRegistrandoNombresCambio;
        capturandoNombres = false;
    }

    private void OnRegistrandoNombresCambio(bool anterior, bool nuevo)
    {
        ActualizarEsperaLocal();
    }

    private void AplicarTextoEspera()
    {
        bool nombres = IsSpawned && registrandoNombresNet.Value;
        AplicarTextoEsperaEn(panelEsperandoConductor, ref textoEsperaPescadorOriginal, nombres);
        AplicarTextoEsperaEn(panelEsperaConductor, ref textoEsperaConductorOriginal, nombres);
    }

    private static void AplicarTextoEsperaEn(GameObject panel, ref string original, bool nombres)
    {
        if (panel == null) return;

        TMP_Text texto = panel.GetComponentInChildren<TMP_Text>(true);
        if (texto == null) return;

        if (original == null) original = texto.text;

        string deseado = nombres ? "Registrando nombres en la PC..." : original;
        if (texto.text != deseado) texto.text = deseado;
    }

    private void PedirNombresServidor()
    {
        if (!IsServer) return;

        capturandoNombres = true;
        enfocarCampoNombre = true;
        registrandoNombresNet.Value = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ActualizarEsperaLocal();
    }

    private void ConfirmarNombres()
    {
        if (!capturandoNombres) return;

        nombrePescador = RegistroPartida.LimpiarNombre(nombrePescador);
        nombreConductor = RegistroPartida.LimpiarNombre(nombreConductor);

        capturandoNombres = false;
        if (IsSpawned && IsServer) registrandoNombresNet.Value = false;

        IniciarPartidaServidor();
    }

    private void CancelarNombres()
    {
        if (!capturandoNombres) return;

        capturandoNombres = false;

        if (IsSpawned && IsServer)
        {
            registrandoNombresNet.Value = false;
            pescadorListoNet.Value = false;
            conductorListoNet.Value = false;
            clienteConductorListo = ulong.MaxValue;
            MenuConductorClientRpc();
        }

        MostrarMenu();
    }

    [ClientRpc]
    private void MenuConductorClientRpc()
    {
        if (IsServer || !EsConductor()) return;
        MostrarMenuLocal();
    }

    private void IniciarRegistroPartida(float duracion)
    {
        RegistroPartida.Iniciar(nombrePescador, nombreConductor, duracion);
    }

    private void GuardarResultadoPartida()
    {
        if (!EsAutoridadLocal || !RegistroPartida.Activo) return;

        int peces = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;
        RegistroPartida.Terminar(peces);
        EnviarTableroAClientes();
    }

    private void EnviarTableroAClientes()
    {
        if (IsSpawned && IsServer) TableroDatosClientRpc(RegistroPartida.TextoRed(filasTablero));
    }

    [ServerRpc(RequireOwnership = false)]
    private void PedirTableroServerRpc()
    {
        EnviarTableroAClientes();
    }

    [ClientRpc]
    private void TableroDatosClientRpc(string datos)
    {
        if (IsServer) return;

        tableroRed = datos ?? "";
        if (tableroAbierto) RellenarTablero();
    }

    public void AbrirTablero()
    {
        AbrirTableroLocal();
        if (!EsConductor() && IsSpawned && IsServer) AbrirTableroClientRpc();
    }

    [ClientRpc]
    private void AbrirTableroClientRpc()
    {
        if (!IsServer && !EsConductor()) AbrirTableroLocal();
    }

    public void CerrarTablero()
    {
        CerrarTableroLocal();
        if (!EsConductor() && IsSpawned && IsServer) CerrarTableroClientRpc();
    }

    [ClientRpc]
    private void CerrarTableroClientRpc()
    {
        if (!IsServer && !EsConductor()) CerrarTableroLocal();
    }

    private void AbrirTableroLocal()
    {
        bool conductor = EsConductor();
        GameObject padre = conductor ? canvasMenuConductor : canvasMenu;
        if (padre == null) return;

        if (panelTablero == null)
        {
            try
            {
                CrearPanelTablero(padre);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TABLERO] No se pudo crear el panel: " + e.Message);
                return;
            }
        }

        padre.SetActive(true);

        if (conductor)
        {
            if (panelMenuConductor != null) panelMenuConductor.SetActive(false);
        }
        else
        {
            if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
            if (panelIniciarMenu != null) panelIniciarMenu.SetActive(false);
            activeButtons = null;
        }

        panelTablero.transform.SetAsLastSibling();
        panelTablero.SetActive(true);
        tableroAbierto = true;
        momentoAperturaTablero = Time.unscaledTime;
        enterPendienteTablero = false;

        RellenarTablero();
    }

    private void CerrarTableroLocal()
    {
        OcultarPanelTablero();
        ignorarConfirmarHasta = Time.unscaledTime + 0.3f;
        MostrarMenuLocal();

        if (EsConductor() || activeButtons == null || btnTablero == null) return;

        int indice = System.Array.IndexOf(activeButtons, btnTablero);
        if (indice < 0) return;

        selectedButtonIndex = indice;
        ResaltarBotonLocal(indice);
        if (IsSpawned && IsServer)
        {
            selectedButtonIndexNet.Value = indice;
            SincronizarSeleccionBotonClientRpc(indice);
        }
    }

    private void OcultarPanelTablero()
    {
        tableroAbierto = false;
        if (panelTablero != null && panelTablero.activeSelf) panelTablero.SetActive(false);
    }

    private void ActualizarEntradaTablero()
    {
        if (EsConductor() || !EsAutoridadLocal) return;

        bool recienAbierto = Time.unscaledTime - momentoAperturaTablero < 0.3f;

        if (!recienAbierto && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            enterPendienteTablero = true;
        }

        bool soltoEnter = Input.GetKeyUp(KeyCode.Return) || Input.GetKeyUp(KeyCode.KeypadEnter);
        bool cerrar = !recienAbierto &&
                      ((enterPendienteTablero && soltoEnter) ||
                       Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace));

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            bool pulsado = wiimote.Button.a || wiimote.Button.b;
            if (pulsado && !wasWiiA && !recienAbierto) cerrar = true;
            wasWiiA = pulsado;
        }
#endif

        if (cerrar) CerrarTablero();
    }

    private TextMeshProUGUI CrearTextoTablero(Transform padre, string nombre, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;

        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI texto = go.AddComponent<TextMeshProUGUI>();
        texto.raycastTarget = false;
        texto.enableWordWrapping = false;
        texto.overflowMode = TextOverflowModes.Ellipsis;
        texto.color = Color.white;
        return texto;
    }

    private void CrearPanelTablero(GameObject padre)
    {
        GameObject panel = new GameObject("Panel_Tablero", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = padre.layer;

        RectTransform rtPanel = (RectTransform)panel.transform;
        rtPanel.SetParent(padre.transform, false);
        rtPanel.anchorMin = Vector2.zero;
        rtPanel.anchorMax = Vector2.one;
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;

        panel.GetComponent<Image>().color = new Color(0.13f, 0.08f, 0.05f, 0.97f);

        TextMeshProUGUI titulo = CrearTextoTablero(rtPanel, "Titulo", new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f));
        titulo.text = "TABLERO  -  MEJORES EQUIPOS";
        titulo.fontSize = 54f;
        titulo.fontStyle = FontStyles.Bold;
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.color = new Color(1f, 0.85f, 0.4f, 1f);

        columnasTablero = new TextMeshProUGUI[titulosTablero.Length];
        for (int i = 0; i < titulosTablero.Length; i++)
        {
            TextMeshProUGUI col = CrearTextoTablero(rtPanel, "Columna_" + titulosTablero[i],
                new Vector2(bordesTablero[i], 0.17f), new Vector2(bordesTablero[i + 1], 0.83f));
            col.fontSize = tamanoLetraTablero;
            col.lineSpacing = 35f;
            col.alignment = i == 1 ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
            if (i != 1) col.overflowMode = TextOverflowModes.Overflow;
            columnasTablero[i] = col;
        }

        GameObject goBoton = new GameObject("Boton_Volver", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        goBoton.layer = padre.layer;

        RectTransform rtBoton = (RectTransform)goBoton.transform;
        rtBoton.SetParent(rtPanel, false);
        rtBoton.anchorMin = new Vector2(0.38f, 0.04f);
        rtBoton.anchorMax = new Vector2(0.62f, 0.14f);
        rtBoton.offsetMin = Vector2.zero;
        rtBoton.offsetMax = Vector2.zero;

        goBoton.GetComponent<Image>().color = new Color(0.85f, 0.45f, 0.17f, 1f);
        goBoton.GetComponent<Button>().onClick.AddListener(CerrarTablero);

        TextMeshProUGUI textoBoton = CrearTextoTablero(rtBoton, "Texto", Vector2.zero, Vector2.one);
        textoBoton.text = "VOLVER";
        textoBoton.fontSize = 36f;
        textoBoton.fontStyle = FontStyles.Bold;
        textoBoton.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);
        panelTablero = panel;
    }

    private List<string[]> FilasTableroLocal()
    {
        List<string[]> filas = new List<string[]>();

        if (EsAutoridadLocal)
        {
            foreach (ResultadoPartida r in RegistroPartida.Mejores(filasTablero))
            {
                filas.Add(new[]
                {
                    r.Equipo, r.peces.ToString(),
                    RegistroPartida.Porcentaje(r.RatioCapturas), RegistroPartida.Porcentaje(r.PrecisionPatrones),
                    r.minas.ToString(), r.AtaquesTiburon.ToString()
                });
            }
            return filas;
        }

        if (string.IsNullOrEmpty(tableroRed)) return filas;

        foreach (string linea in tableroRed.Split('\n'))
        {
            string[] partes = linea.Split('|');
            if (partes.Length >= 6) filas.Add(partes);
        }
        return filas;
    }

    private void RellenarTablero()
    {
        if (columnasTablero == null) return;

        List<string[]> filas = FilasTableroLocal();
        var textos = new System.Text.StringBuilder[columnasTablero.Length];

        for (int c = 0; c < textos.Length; c++)
        {
            textos[c] = new System.Text.StringBuilder();
            textos[c].Append("<color=#FFD966><b>").Append(titulosTablero[c]).Append("</b></color>");
        }

        if (filas.Count == 0)
        {
            textos[1].Append("\nAun no hay partidas");
        }

        for (int f = 0; f < filas.Count; f++)
        {
            textos[0].Append('\n').Append(f + 1);
            for (int c = 1; c < textos.Length; c++)
            {
                textos[c].Append('\n').Append("<noparse>").Append(filas[f][c - 1]).Append("</noparse>");
            }
        }

        for (int c = 0; c < columnasTablero.Length; c++)
        {
            if (columnasTablero[c] != null) columnasTablero[c].text = textos[c].ToString();
        }
    }

    private void PrepararEstilosGUI()
    {
        if (estiloTitulo != null) return;

        texturaFondo = new Texture2D(1, 1);
        texturaFondo.SetPixel(0, 0, Color.white);
        texturaFondo.Apply();

        estiloTitulo = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estiloTitulo.normal.textColor = new Color(1f, 0.85f, 0.4f, 1f);

        estiloTexto = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
        estiloTexto.normal.textColor = Color.white;

        estiloCampo = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
        estiloBoton = new GUIStyle(GUI.skin.button) { fontSize = 22 };

        estiloCelda = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, wordWrap = false };
        estiloCelda.normal.textColor = Color.white;

        estiloCeldaIzquierda = new GUIStyle(estiloCelda) { alignment = TextAnchor.MiddleLeft };
    }

    private void DibujarFondoGUI(Rect r, Color color)
    {
        Color previo = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, texturaFondo);
        GUI.color = previo;
    }

    private void OnGUI()
    {
        if (!capturandoNombres && !TableroCompletoEnPantalla) return;

        PrepararEstilosGUI();

        Matrix4x4 matrizPrevia = GUI.matrix;

        if (capturandoNombres) DibujarPanelNombres();
        else DibujarTableroCompleto();

        GUI.matrix = matrizPrevia;
    }

    private void DibujarPanelNombres()
    {
        float escala = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        float ancho = Screen.width / escala;

        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.Use();
                ConfirmarNombres();
                return;
            }
            if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                CancelarNombres();
                return;
            }
        }

        Rect caja = new Rect((ancho - 640f) * 0.5f, 170f, 640f, 380f);
        DibujarFondoGUI(new Rect(0f, 0f, ancho, 720f), new Color(0f, 0f, 0f, 0.55f));
        DibujarFondoGUI(caja, new Color(0.13f, 0.08f, 0.05f, 0.98f));

        GUI.Label(new Rect(caja.x, caja.y + 15f, caja.width, 50f), "NOMBRES DEL EQUIPO", estiloTitulo);

        GUI.Label(new Rect(caja.x + 40f, caja.y + 90f, 220f, 44f), "Pescador (P1)", estiloTexto);
        GUI.SetNextControlName("campoPescador");
        nombrePescador = GUI.TextField(new Rect(caja.x + 260f, caja.y + 90f, 340f, 44f), nombrePescador, RegistroPartida.MAX_LETRAS_NOMBRE, estiloCampo);

        GUI.Label(new Rect(caja.x + 40f, caja.y + 155f, 220f, 44f), "Conductor (P2)", estiloTexto);
        GUI.SetNextControlName("campoConductor");
        nombreConductor = GUI.TextField(new Rect(caja.x + 260f, caja.y + 155f, 340f, 44f), nombreConductor, RegistroPartida.MAX_LETRAS_NOMBRE, estiloCampo);

        GUI.Label(new Rect(caja.x + 40f, caja.y + 215f, 560f, 60f), "Tab cambia de campo. Si los dejas vacios se guarda como Equipo N.", estiloTexto);

        if (GUI.Button(new Rect(caja.x + 40f, caja.y + 300f, 270f, 54f), "Empezar (Enter)", estiloBoton))
        {
            ConfirmarNombres();
            return;
        }
        if (GUI.Button(new Rect(caja.x + 330f, caja.y + 300f, 270f, 54f), "Cancelar (Esc)", estiloBoton))
        {
            CancelarNombres();
            return;
        }

        if (enfocarCampoNombre && e.type == EventType.Repaint)
        {
            enfocarCampoNombre = false;
            GUI.FocusControl(string.IsNullOrEmpty(nombrePescador) ? "campoPescador" : "campoConductor");
        }
    }

    private static string Seg(float valor)
    {
        return valor < 0f ? "-" : valor.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void DibujarTableroCompleto()
    {
        const float anchoDiseno = 1600f;
        float escala = Screen.width / anchoDiseno;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        float alto = Screen.height / escala;

        string[] titulos =
        {
            "#", "Equipo", "Peces", "Intent.", "Capt.", "Pat. OK", "Pat. X", "Prec.",
            "Horiz.", "Vert.", "Circ.", "Cruz", "s/pat.", "1er pez",
            "Minas", "s inm.", "Mord.", "Emb.", "s bloq.", "Dist. m", "s quieto", "Inclin."
        };

        const float anchoNumero = 34f;
        const float anchoEquipo = 250f;
        float margen = 20f;
        float anchoCelda = (anchoDiseno - margen * 2f - anchoNumero - anchoEquipo) / (titulos.Length - 2);
        float altoFila = 34f;

        List<ResultadoPartida> filas = RegistroPartida.Mejores(filasTableroPC);

        float altoCaja = 70f + altoFila * (Mathf.Max(1, filas.Count) + 1) + 60f;
        Rect caja = new Rect(margen * 0.5f, Mathf.Max(10f, (alto - altoCaja) * 0.5f), anchoDiseno - margen, altoCaja);

        DibujarFondoGUI(new Rect(0f, 0f, anchoDiseno, alto), new Color(0f, 0f, 0f, 0.6f));
        DibujarFondoGUI(caja, new Color(0.13f, 0.08f, 0.05f, 0.98f));

        GUI.Label(new Rect(caja.x, caja.y + 8f, caja.width, 44f), "TABLERO COMPLETO (solo PC)", estiloTitulo);

        float y = caja.y + 62f;
        DibujarFondoGUI(new Rect(caja.x, y, caja.width, altoFila), new Color(1f, 1f, 1f, 0.10f));
        DibujarFilaGUI(titulos, y, margen, anchoNumero, anchoEquipo, anchoCelda, altoFila);

        for (int i = 0; i < filas.Count; i++)
        {
            ResultadoPartida r = filas[i];
            y += altoFila;

            if (i % 2 == 1) DibujarFondoGUI(new Rect(caja.x, y, caja.width, altoFila), new Color(1f, 1f, 1f, 0.04f));

            string[] celdas =
            {
                (i + 1).ToString(), r.Equipo, r.peces.ToString(), r.intentos.ToString(),
                RegistroPartida.Porcentaje(r.RatioCapturas),
                r.patronesOk.ToString(), r.patronesFallados.ToString(),
                RegistroPartida.Porcentaje(r.PrecisionPatrones),
                RegistroPartida.Porcentaje(r.PrecisionGesto(1)), RegistroPartida.Porcentaje(r.PrecisionGesto(2)),
                RegistroPartida.Porcentaje(r.PrecisionGesto(3)), RegistroPartida.Porcentaje(r.PrecisionGesto(4)),
                Seg(r.patronesOk > 0 ? r.SegundosPorPatron : -1f), Seg(r.segundosPrimerPez),
                r.minas.ToString(), Seg(r.segundosInmovil),
                r.mordidas.ToString(), r.embestidas.ToString(), Seg(r.segundosBloqueado),
                Seg(r.distanciaBote), Seg(r.segundosQuieto),
                RegistroPartida.Porcentaje(r.PorcentajeInclinacion)
            };

            DibujarFilaGUI(celdas, y, margen, anchoNumero, anchoEquipo, anchoCelda, altoFila);
        }

        if (filas.Count == 0)
        {
            GUI.Label(new Rect(caja.x, y + altoFila, caja.width, altoFila), "Aun no hay partidas registradas", estiloCelda);
        }

        GUI.Label(new Rect(caja.x + 12f, caja.yMax - 46f, caja.width - 24f, 40f),
            "Enter / A: volver    |    Datos: " + RegistroPartida.CarpetaVisible, estiloCeldaIzquierda);
    }

    private void DibujarFilaGUI(string[] celdas, float y, float margen, float anchoNumero, float anchoEquipo, float anchoCelda, float altoFila)
    {
        float x = margen;
        for (int c = 0; c < celdas.Length; c++)
        {
            float ancho = c == 0 ? anchoNumero : (c == 1 ? anchoEquipo : anchoCelda);
            GUI.Label(new Rect(x, y, ancho, altoFila), celdas[c], c == 1 ? estiloCeldaIzquierda : estiloCelda);
            x += ancho;
        }
    }
}
