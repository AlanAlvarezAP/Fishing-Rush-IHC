using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Anuncios en pantalla (imagen del gesto, victoria, derrota).
/// - La PC (servidor) es quien los dispara desde el minijuego: los muestra local y los reenvia
///   a los celulares a traves de MenuMismaEscena (igual que FishingHUDManager).
/// - Los metodos *Local solo pintan en este dispositivo (los usa el relay en el celular).
/// </summary>
public class AnunciosUI : MonoBehaviour
{
    public static AnunciosUI Instance;

    [Header("Referencia al Tile de Mensajes (Solo Victoria/Derrota)")]
    public GameObject tileAnuncios;
    public TextMeshProUGUI txtAnuncio;

    [Header("Imagen Central del Gesto (Independiente)")]
    public Image imgGestoCentral;
    public Sprite iconHorizontal;
    public Sprite iconVertical;
    public Sprite iconCircle;
    public Sprite iconCross;

    [Header("Colores del Texto")]
    public Color colorVictoria = Color.green;
    public Color colorDerrota = Color.red;

    private Coroutine rutinaAnuncio;

    // Relay hacia los celulares
    private MenuMismaEscena relay;

    private bool EsServidorEnRed =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (tileAnuncios != null) tileAnuncios.SetActive(false);
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);
    }

    [Header("HUD en VR")]
    [Tooltip("Tamano del HUD tal como se diseno en pantalla 16:9")]
    public Vector2 tamanoHudVR = new Vector2(141.3f, 79.5f);
    [Tooltip("Metros entre los ojos y el HUD (igual al Plane Distance del canvas en el editor)")]
    public float distanciaHudVR = 1f;
    [Tooltip("Campo de vision vertical de la camara con la que se diseno el HUD en el editor (FirstPersonCamera = 60)")]
    public float fovVerticalDisenoHud = 60f;
    [Tooltip("0 = automatico: el HUD mide lo mismo que en el editor (a 1 m con 60 grados, unos 2.05 m de ancho), " +
             "asi los paneles quedan donde se acomodaron usando VR_limits. Pon un valor en metros solo para forzar otro ancho.")]
    public float anchoHudVRManual = 0f;

    private float AnchoHudVR
    {
        get
        {
            if (anchoHudVRManual > 0f) return anchoHudVRManual;
            float alto = 2f * distanciaHudVR * Mathf.Tan(fovVerticalDisenoHud * 0.5f * Mathf.Deg2Rad);
            float aspecto = tamanoHudVR.y > 0f ? tamanoHudVR.x / tamanoHudVR.y : 16f / 9f;
            return alto * aspecto;
        }
    }

    private Canvas canvasHUD;
    private Camera camaraOriginalHUD;
    private bool canvasHUDBuscado = false;
    private bool hudEnMundo = false;
    private float proximoChequeoCamara = 0f;

    private void OnEnable()
    {
        Application.onBeforeRender += SeguirCamaraVR;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= SeguirCamaraVR;
    }

    private void SeguirCamaraVR()
    {
        if (!hudEnMundo || canvasHUD == null) return;

        Camera cam = CamaraActivaUtil.Buscar(camaraOriginalHUD);
        if (cam == null) return;

        CamaraActivaUtil.ColocarFrente(canvasHUD, cam, tamanoHudVR, distanciaHudVR, AnchoHudVR);
    }

    private void LateUpdate()
    {
        if (!canvasHUDBuscado)
        {
            canvasHUDBuscado = true;
            canvasHUD = GetComponent<Canvas>();
            if (canvasHUD != null)
            {
                camaraOriginalHUD = canvasHUD.worldCamera;

                if (CamaraActivaUtil.EnVR && canvasHUD.isRootCanvas)
                {
                    hudEnMundo = true;
                    CanvasScaler scaler = canvasHUD.GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.dynamicPixelsPerUnit = 20f;
                }
            }
        }

        if (canvasHUD == null) return;

        if (hudEnMundo)
        {
            SeguirCamaraVR();
            return;
        }

        if (Time.unscaledTime < proximoChequeoCamara) return;
        proximoChequeoCamara = Time.unscaledTime + 0.5f;

        if (canvasHUD.renderMode != RenderMode.ScreenSpaceCamera) return;

        Camera deseada = CamaraActivaUtil.Buscar(camaraOriginalHUD);
        if (deseada != null && canvasHUD.worldCamera != deseada) canvasHUD.worldCamera = deseada;
    }

    private void Relay(MenuMismaEscena.AnuncioAccion accion, int gesto, float duracion)
    {
        if (!EsServidorEnRed) return;

        if (relay == null) relay = FindObjectOfType<MenuMismaEscena>();
        if (relay == null) return;

        relay.EnviarAnuncioAlVisor(accion, gesto, duracion);
    }

    // --- 1. MOSTRAR SOLO LA IMAGEN DEL GESTO (El Tile/Recuadro se queda APAGADO) ---
    public void MostrarGestoRequerido(FishCube.FishingGesture gesto, float duracion = 1.5f)
    {
        MostrarGestoRequeridoLocal(gesto, duracion);
        Relay(MenuMismaEscena.AnuncioAccion.Gesto, (int)gesto, duracion);
    }

    public void MostrarGestoRequeridoLocal(FishCube.FishingGesture gesto, float duracion = 1.5f)
    {
        Sprite spriteGesto = ObtenerSpriteGesto(gesto);
        if (spriteGesto == null || imgGestoCentral == null) return;
        if (!isActiveAndEnabled) return; // no se pueden lanzar corrutinas en un objeto apagado

        if (rutinaAnuncio != null) StopCoroutine(rutinaAnuncio);
        rutinaAnuncio = StartCoroutine(RutinaMostrarGesto(spriteGesto, duracion));
    }

    private bool GestoDentroDelTile =>
        tileAnuncios != null && imgGestoCentral != null &&
        imgGestoCentral.transform != tileAnuncios.transform &&
        imgGestoCentral.transform.IsChildOf(tileAnuncios.transform);

    private GameObject RecuadroTexto()
    {
        if (tileAnuncios == null || txtAnuncio == null) return null;
        if (!txtAnuncio.transform.IsChildOf(tileAnuncios.transform)) return null;

        Transform actual = txtAnuncio.transform;
        while (actual.parent != null && actual.parent != tileAnuncios.transform) actual = actual.parent;
        return actual.parent == tileAnuncios.transform ? actual.gameObject : null;
    }

    private IEnumerator RutinaMostrarGesto(Sprite spriteGesto, float duracion)
    {
        bool dentro = GestoDentroDelTile;

        if (dentro)
        {
            GameObject recuadro = RecuadroTexto();
            if (recuadro != null) recuadro.SetActive(false);
            tileAnuncios.SetActive(true);
        }
        else if (tileAnuncios != null)
        {
            // Garantizar que el recuadro de texto este apagado
            tileAnuncios.SetActive(false);
        }

        // Encender UNICAMENTE la imagen del gesto
        imgGestoCentral.sprite = spriteGesto;
        imgGestoCentral.transform.SetAsLastSibling();
        imgGestoCentral.gameObject.SetActive(true);

        yield return new WaitForSeconds(duracion);

        // Apagar la imagen
        imgGestoCentral.gameObject.SetActive(false);
        if (dentro) tileAnuncios.SetActive(false);
    }

    // --- 2. MOSTRAR MENSAJES DE TEXTO (AQUI SI SE ENCIENDE EL RECUADRO) ---
    public void MostrarMensaje(string texto, Color colorTexto, float duracion = 1.5f)
    {
        if (tileAnuncios == null) return;
        if (!isActiveAndEnabled) return;

        if (rutinaAnuncio != null) StopCoroutine(rutinaAnuncio);
        rutinaAnuncio = StartCoroutine(RutinaMostrarTexto(texto, colorTexto, duracion));
    }

    private IEnumerator RutinaMostrarTexto(string texto, Color colorTexto, float duracion)
    {
        // Apagar la imagen del gesto
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);

        if (txtAnuncio != null)
        {
            txtAnuncio.text = texto;
            txtAnuncio.color = colorTexto;
            txtAnuncio.gameObject.SetActive(true);
        }

        GameObject recuadro = RecuadroTexto();
        if (recuadro != null) recuadro.SetActive(true);

        // Encender el cuadro con el mensaje final
        tileAnuncios.transform.SetAsLastSibling();
        tileAnuncios.SetActive(true);

        yield return new WaitForSeconds(duracion);

        tileAnuncios.SetActive(false);
    }

    private Sprite ObtenerSpriteGesto(FishCube.FishingGesture gesto)
    {
        switch (gesto)
        {
            case FishCube.FishingGesture.Horizontal: return iconHorizontal;
            case FishCube.FishingGesture.Vertical: return iconVertical;
            case FishCube.FishingGesture.Circle: return iconCircle;
            case FishCube.FishingGesture.Cross: return iconCross;
            default: return null;
        }
    }

    // Llamados por la logica (PC): muestran local y reenvian a los celulares
    public void MostrarVictoria()
    {
        MostrarVictoriaLocal();
        Relay(MenuMismaEscena.AnuncioAccion.Victoria, 0, 2.0f);
    }

    public void MostrarDerrota()
    {
        MostrarDerrotaLocal();
        Relay(MenuMismaEscena.AnuncioAccion.Derrota, 0, 2.0f);
    }

    // Solo pintan en este dispositivo
    public void MostrarVictoriaLocal() => MostrarMensaje("PEZ CAPTURADO!", colorVictoria, 2.0f);
    public void MostrarDerrotaLocal() => MostrarMensaje("EL PEZ ESCAPO!", colorDerrota, 2.0f);
}
