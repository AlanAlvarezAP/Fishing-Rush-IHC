using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// HUD de pesca (potencia, gestos, ventana de tiro).
/// - La PC (servidor) es la unica que decide que se muestra: muestra local y reenvia al celular
///   a traves de MenuMismaEscena (que ya tiene un NetworkObject spawneado y RPCs funcionando).
/// - El celular (visor) ignora las llamadas directas y solo muestra lo que le llega por el relay.
/// - Los metodos publicos conservan el mismo nombre/firma, asi que ThirdPController no cambia.
/// </summary>
public class FishingHUDManager : MonoBehaviour
{
    public static FishingHUDManager Instance;

    [Header("Panel de Potencia de Lanzamiento")]
    [SerializeField] private GameObject powerPanel;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private float powerDisplayDuration = 2.0f;

    [Header("Panel de Gestos Requeridos")]
    [SerializeField] private GameObject gesturePanel;
    [SerializeField] private TextMeshProUGUI gestureText;

    [Header("Panel Ventana de Tiro")]
    [SerializeField] private GameObject castWindowPanel;
    [SerializeField] private TextMeshProUGUI castWindowText;

    private Coroutine powerCoroutine;

    // Relay hacia el celular
    private MenuMismaEscena relay;
    private string ultimoGestoEnviado = null;
    private bool gestoVisibleEnviado = false;
    private float ultimaVentanaEnviada = -1f;
    private bool ventanaVisibleEnviada = false;

    // Camara
    private float proximoChequeoCamara = 0f;
    private float proximoReintentoCamara = 0f;

    // true si ya se le mostro algo al celular (evita mandar un OcultarTodo por RPC en cada frame)
    private bool hudVisorPendiente = false;

    private bool EsVisor =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        !NetworkManager.Singleton.IsServer;

    private bool EsServidorEnRed =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        HideAllLocal();
    }

    private void Start()
    {
        EnsureCamera();
    }

    private void OnEnable()
    {
        EnsureCamera();
    }

    // Si la camara enlazada al canvas se apaga o desaparece (muy comun en el celular), se vuelve a buscar sola
    private void LateUpdate()
    {
        if (Time.unscaledTime < proximoChequeoCamara) return;
        proximoChequeoCamara = Time.unscaledTime + 0.5f;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        if (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled)
        {
            EnsureCamera();
        }
    }

    // ---------------- CAMARA ----------------
    public void EnsureCamera()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        Camera targetCam = BuscarCamaraLocal();
        if (targetCam == null) return;

        if (canvas.worldCamera != targetCam)
        {
            canvas.worldCamera = targetCam;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            canvas.planeDistance = 0.5f; // HUD a 0.5m de la vista en 1ra persona
        }
    }

    // Version barata para llamar en cada Show*: solo busca de nuevo si la camara actual no sirve
    private void AsegurarCamaraLigero()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        if (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled)
        {
            // Con limite de frecuencia: si no hay camara valida, no buscar en cada frame
            if (Time.unscaledTime < proximoReintentoCamara) return;
            proximoReintentoCamara = Time.unscaledTime + 0.5f;
            EnsureCamera();
        }
    }

    private Camera BuscarCamaraLocal()
    {
        ThirdPController player = null;

        // 1. El jugador de ESTE dispositivo (en el celular no es el de la PC)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ThirdPController>();
        }

        // 2. Algun ThirdPController propio (o sin red)
        if (player == null)
        {
            ThirdPController[] jugadores = FindObjectsOfType<ThirdPController>();
            foreach (var j in jugadores)
            {
                if (j != null && j.gameObject.activeInHierarchy && (j.IsOwner || !j.IsSpawned))
                {
                    player = j;
                    break;
                }
            }
        }

        Camera cam = CamaraDe(player);
        if (cam != null && cam.isActiveAndEnabled) return cam;

        // 3. Respaldo: camara principal activa
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;

        // 4. Ultimo recurso: la del jugador aunque este apagada (LateUpdate la corrige cuando se active)
        return cam != null ? cam : Camera.main;
    }

    private Camera CamaraDe(ThirdPController p)
    {
        if (p == null || p.camera == null) return null;

        Camera c = p.camera.GetComponent<Camera>();
        if (c == null) c = p.camera.GetComponentInChildren<Camera>(true);
        return c;
    }

    // ---------------- RELAY AL CELULAR ----------------
    private void Relay(MenuMismaEscena.HudPescaAccion accion, float valor = 0f, string texto = "", int a = 0, int b = 0)
    {
        if (!EsServidorEnRed) return;

        if (relay == null) relay = FindObjectOfType<MenuMismaEscena>();
        if (relay == null) return;

        if (accion == MenuMismaEscena.HudPescaAccion.Potencia ||
            accion == MenuMismaEscena.HudPescaAccion.Gesto ||
            accion == MenuMismaEscena.HudPescaAccion.VentanaTiro)
        {
            hudVisorPendiente = true;
        }

        relay.EnviarHUDPescaAlVisor(accion, valor, texto, a, b);
    }

    // ---------------- POTENCIA DE LANZAMIENTO ----------------
    public void ShowCastPower(float force)
    {
        if (EsVisor) return; // el celular solo recibe por el relay
        ShowCastPowerLocal(force);
        Relay(MenuMismaEscena.HudPescaAccion.Potencia, force);
    }

    public void ShowCastPowerLocal(float force)
    {
        AsegurarCamaraLigero();
        if (powerPanel == null || powerText == null) return;

        if (powerCoroutine != null) StopCoroutine(powerCoroutine);

        powerText.text = force.ToString("F1");
        powerPanel.SetActive(true);

        powerCoroutine = StartCoroutine(HidePowerPanelRoutine());
    }

    private IEnumerator HidePowerPanelRoutine()
    {
        yield return new WaitForSeconds(powerDisplayDuration);
        if (powerPanel != null) powerPanel.SetActive(false);
    }

    // ---------------- GESTOS REQUERIDOS ----------------
    public void ShowGesturePrompt(string gestureName, int hits, int attempts)
    {
        if (EsVisor) return;
        ShowGesturePromptLocal(gestureName, hits, attempts);

        if (string.IsNullOrEmpty(gestureName) || gestureName == "None")
        {
            EnviarOcultarGesto();
            return;
        }

        // Solo se envia si cambio algo (evita un RPC por frame)
        string clave = gestureName + "|" + hits + "|" + attempts;
        if (clave == ultimoGestoEnviado && gestoVisibleEnviado) return;

        ultimoGestoEnviado = clave;
        gestoVisibleEnviado = true;
        Relay(MenuMismaEscena.HudPescaAccion.Gesto, 0f, gestureName, hits, attempts);
    }

    public void ShowGesturePromptLocal(string gestureName, int hits, int attempts)
    {
        AsegurarCamaraLigero();
        if (gesturePanel == null || gestureText == null) return;

        if (string.IsNullOrEmpty(gestureName) || gestureName == "None")
        {
            HideGesturePromptLocal();
            return;
        }

        gestureText.text = $"{gestureName.ToUpper()} {hits}/{attempts}";
        gesturePanel.SetActive(true);
    }

    public void HideGesturePrompt()
    {
        if (EsVisor) return;
        HideGesturePromptLocal();
        EnviarOcultarGesto();
    }

    public void HideGesturePromptLocal()
    {
        if (gesturePanel != null) gesturePanel.SetActive(false);
    }

    private void EnviarOcultarGesto()
    {
        if (!gestoVisibleEnviado) return;
        gestoVisibleEnviado = false;
        ultimoGestoEnviado = null;
        Relay(MenuMismaEscena.HudPescaAccion.OcultarGesto);
    }

    // ---------------- VENTANA DE TIRO ----------------
    public void ShowCastWindowTimer(float timeLeft)
    {
        if (EsVisor) return;
        ShowCastWindowTimerLocal(timeLeft);

        // Solo se envia cuando cambia el decimal que se ve en pantalla
        float redondeado = Mathf.Round(timeLeft * 10f) / 10f;
        if (ventanaVisibleEnviada && Mathf.Approximately(redondeado, ultimaVentanaEnviada)) return;

        ultimaVentanaEnviada = redondeado;
        ventanaVisibleEnviada = true;
        Relay(MenuMismaEscena.HudPescaAccion.VentanaTiro, redondeado);
    }

    public void ShowCastWindowTimerLocal(float timeLeft)
    {
        AsegurarCamaraLigero();
        if (castWindowPanel == null || castWindowText == null) return;

        castWindowText.text = timeLeft.ToString("F1");
        if (!castWindowPanel.activeSelf) castWindowPanel.SetActive(true);
    }

    public void HideCastWindowTimer()
    {
        if (EsVisor) return;
        HideCastWindowTimerLocal();

        if (ventanaVisibleEnviada)
        {
            ventanaVisibleEnviada = false;
            ultimaVentanaEnviada = -1f;
            Relay(MenuMismaEscena.HudPescaAccion.OcultarVentanaTiro);
        }
    }

    public void HideCastWindowTimerLocal()
    {
        if (castWindowPanel != null && castWindowPanel.activeSelf)
        {
            castWindowPanel.SetActive(false);
        }
    }

    // ---------------- OCULTAR TODO ----------------
    public void HideAll()
    {
        if (EsVisor) return;
        HideAllLocal();

        gestoVisibleEnviado = false;
        ultimoGestoEnviado = null;
        ventanaVisibleEnviada = false;
        ultimaVentanaEnviada = -1f;

        if (hudVisorPendiente)
        {
            hudVisorPendiente = false;
            Relay(MenuMismaEscena.HudPescaAccion.OcultarTodo);
        }
    }

    public void HideAllLocal()
    {
        if (powerPanel != null) powerPanel.SetActive(false);
        if (gesturePanel != null) gesturePanel.SetActive(false);
        if (castWindowPanel != null) castWindowPanel.SetActive(false);
    }
}
