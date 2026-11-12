using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NUEVO (tiburon). Avisos de peligro en pantalla. Hay dos copias en la escena (las crea el menu
/// "Fishing Rush > Crear tiburon"):
///   - Pescador (Canvas_GameHUD, PC y celular VR):
///       tiburon mordio  -> pantalla roja + "Pescador inhabilitado 6.2 s"
///       bote tambalea   -> "El barco se tambalea, los patrones no cuentan"
///       mina            -> "Mina! Bote detenido 4.1 s"
///   - Conductor (Canvas_Boat, tablet), con 'esConductor' marcado:
///       bote tambalea   -> "Choque con tiburon!"
///       tiburon mordio  -> "Pescador inhabilitado 6.2 s" (para que sepa por que no pesca)
///     (el aviso de la mina del conductor ya lo muestra AvisoBoteInmovil)
/// No usa RPCs: lee los segundos restantes de las NetworkVariables del jugador y del bote.
/// Va en un objeto SIEMPRE activo; lo que se enciende y apaga es 'panel'.
/// Textos sin tildes ni signos de apertura: la fuente del juego puede no tenerlos.
///
/// OJO 1: este script NO tiene Awake a proposito. Si Awake lanza una excepcion, Unity apaga el
///        componente entero y no se muestra ningun aviso (paso con una version anterior).
/// OJO 2: se revisan todos los jugadores (ThirdPController) de la escena, no uno solo.
/// OJO 3: el aviso del PESCADOR no puede quedarse dentro de Canvas_GameHUD: ese canvas esta
///        atado a la camara de primera persona, que en la PC esta apagada (se juega en tercera
///        persona), y un canvas atado a una camara apagada no se dibuja. Por eso la primera vez
///        se crea un canvas propio ("Canvas_AvisosPeligro") y el panel se muda ahi:
///          - PC / tablet: canvas tipo Overlay (siempre encima, con cualquier camara).
///          - Celular VR: canvas atado a la camara que este encendida, a 0.5 m de la vista.
/// </summary>
public class AvisosPeligroUI : MonoBehaviour
{
    public GameObject panel;
    public Image fondo;
    public TextMeshProUGUI texto;
    [Tooltip("Marcado en la copia de la tablet (Canvas_Boat)")]
    public bool esConductor = false;

    [Header("Color del fondo")]
    public Color colorTiburon = new Color(0.85f, 0.05f, 0.05f, 0.30f);
    public Color colorTambaleo = new Color(0.90f, 0.45f, 0.05f, 0.18f);
    public Color colorSinFondo = new Color(0f, 0f, 0f, 0f);

    private ThirdPController[] jugadores = new ThirdPController[0];
    private BoatController bote;
    private float proximaBusqueda = 0f;
    private bool cajaCreada = false;

    private bool canvasPropioIntentado = false;
    private Canvas canvasPropio;
    private float proximoChequeoCanvas = 0f;

    private void OnEnable()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnDisable()
    {
        // El panel puede vivir en otro canvas: al apagarse el HUD (fin de partida) no debe quedar a la vista
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null) return;

        if (!esConductor)
        {
            if (!canvasPropioIntentado) CrearCanvasPropio();
            ActualizarCanvasPropio();
        }

        if (Time.unscaledTime >= proximaBusqueda)
        {
            proximaBusqueda = Time.unscaledTime + 1f;
            jugadores = FindObjectsOfType<ThirdPController>();
            if (bote == null) bote = FindObjectOfType<BoatController>();
        }

        // El bloqueo mas largo entre todos los jugadores de la escena
        float tiburon = 0f;
        for (int i = 0; i < jugadores.Length; i++)
        {
            if (jugadores[i] == null) continue;
            float s = jugadores[i].SegundosBloqueoTiburon;
            if (s > tiburon) tiburon = s;
        }

        float tambaleo = bote != null ? bote.SegundosTambaleoRestantes : 0f;
        float mina = bote != null ? bote.SegundosInmovilRestantes : 0f;

        string mensaje = null;
        Color color = colorSinFondo;

        if (!esConductor)
        {
            if (tiburon > 0f)
            {
                mensaje = $"TIBURON!\nPescador inhabilitado\n{tiburon:0.0} s";
                color = colorTiburon;
            }
            else if (tambaleo > 0f)
            {
                mensaje = "EL BARCO SE TAMBALEA!\nLos patrones no cuentan";
                color = colorTambaleo;
            }
            else if (mina > 0f)
            {
                mensaje = $"MINA! Bote detenido\n{mina:0.0} s";
            }
        }
        else
        {
            if (tambaleo > 0f)
            {
                mensaje = "CHOQUE CON TIBURON!\nEl barco se tambalea";
                color = colorTambaleo;
            }
            else if (tiburon > 0f)
            {
                mensaje = $"Tiburon! Pescador inhabilitado\n{tiburon:0.0} s";
            }
        }

        bool mostrar = mensaje != null;
        if (panel.activeSelf != mostrar) panel.SetActive(mostrar);
        if (!mostrar) return;

        if (!cajaCreada) CrearCajaOscura();

        if (texto != null && texto.text != mensaje) texto.text = mensaje;
        if (fondo != null && fondo.color != color) fondo.color = color;
    }

    // Canvas propio para el aviso del pescador (ver OJO 3). Protegido con try/catch: si falla,
    // el panel se queda donde estaba.
    private void CrearCanvasPropio()
    {
        canvasPropioIntentado = true;

        try
        {
            GameObject go = new GameObject("Canvas_AvisosPeligro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = gameObject.layer;

            canvasPropio = go.GetComponent<Canvas>();
            canvasPropio.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasPropio.sortingOrder = 50;

            // Misma apariencia en cualquier resolucion (se disena como si la pantalla fuera 1280 x 720)
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rtPanel = (RectTransform)panel.transform;
            rtPanel.SetParent(go.transform, false);
            rtPanel.anchorMin = Vector2.zero;
            rtPanel.anchorMax = Vector2.one;
            rtPanel.offsetMin = Vector2.zero;
            rtPanel.offsetMax = Vector2.zero;
            rtPanel.localScale = Vector3.one;
            rtPanel.localPosition = new Vector3(rtPanel.localPosition.x, rtPanel.localPosition.y, 0f);

            if (texto != null)
            {
                texto.fontSizeMin = 22f;
                texto.fontSizeMax = 80f;
            }
        }
        catch (Exception e)
        {
            canvasPropio = null;
            Debug.LogWarning("[AVISOS] No se pudo crear el canvas propio: " + e.Message);
        }
    }

    // Cada medio segundo: Overlay en pantalla normal; en VR, atado a la camara encendida
    private void ActualizarCanvasPropio()
    {
        if (canvasPropio == null || Time.unscaledTime < proximoChequeoCanvas) return;
        proximoChequeoCanvas = Time.unscaledTime + 0.5f;

        if (!CamaraActivaUtil.EnVR)
        {
            if (canvasPropio.renderMode != RenderMode.ScreenSpaceOverlay) canvasPropio.renderMode = RenderMode.ScreenSpaceOverlay;
            return;
        }

        Camera cam = CamaraActivaUtil.Buscar(canvasPropio.worldCamera);
        if (cam == null) return;

        if (canvasPropio.renderMode != RenderMode.ScreenSpaceCamera) canvasPropio.renderMode = RenderMode.ScreenSpaceCamera;
        if (canvasPropio.worldCamera != cam) canvasPropio.worldCamera = cam;
        canvasPropio.planeDistance = 0.5f;
    }

    // Caja oscura detras del texto, para que se lea sobre el agua o el cielo. Se crea la primera
    // vez que se muestra un aviso (con el panel ya encendido) y protegida con try/catch: si algo
    // falla, el aviso se sigue mostrando sin la caja.
    private void CrearCajaOscura()
    {
        cajaCreada = true;
        if (texto == null) return;

        try
        {
            RectTransform rtTexto = texto.rectTransform;

            if (esConductor)
            {
                // En la tablet: franja mas grande y letra con un tamano minimo
                rtTexto.anchorMin = new Vector2(0.08f, 0.26f);
                rtTexto.anchorMax = new Vector2(0.92f, 0.54f);
                rtTexto.offsetMin = Vector2.zero;
                rtTexto.offsetMax = Vector2.zero;
                texto.fontSizeMin = 30f;
            }

            GameObject caja = new GameObject("CajaTexto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            caja.layer = texto.gameObject.layer;

            RectTransform rtCaja = (RectTransform)caja.transform;
            rtCaja.SetParent(rtTexto.parent, false);
            rtCaja.anchorMin = rtTexto.anchorMin;
            rtCaja.anchorMax = rtTexto.anchorMax;
            rtCaja.offsetMin = Vector2.zero;
            rtCaja.offsetMax = Vector2.zero;
            rtCaja.SetSiblingIndex(rtTexto.GetSiblingIndex()); // justo detras del texto

            Image imgCaja = caja.GetComponent<Image>();
            imgCaja.color = new Color(0f, 0f, 0f, 0.65f);
            imgCaja.raycastTarget = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AVISOS] No se pudo crear la caja del texto: " + e.Message);
        }
    }
}
