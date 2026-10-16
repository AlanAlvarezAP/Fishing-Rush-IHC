using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class MenuMismaEscena : NetworkBehaviour
{
    [Header("UI Canvases Principales")]
    public GameObject canvasMenu;
    public GameObject canvasGameHUD;
    public GameObject canvasGameOver;

    [Header("Sub-Paneles Opcionales")]
    public GameObject panelMenuPrincipal;
    public GameObject panelWiimote;
    public GameObject panelTutorial;

    [Header("Referencia al Jugador")]
    public ThirdPController playerController;

    [Header("Objetos para ocultar")]
    public GameObject boteObjeto;
    public GameObject rodObjeto;

    [Header("Botones Menu Principal")]
    public Button btnJugar;
    public Button btnWiimote;
    public Button btnTutorial;
    public Button btnSalir;

    [Header("Botones GameOver")]
    public Button btnReiniciar;
    public Button btnVolverMenu;

    [Header("Nuevo Panel Inicial")]
    public GameObject panelIniciarMenu;
    public Button btnIniciarTutorial;
    public Button btnIniciarSalir;

    private bool tutorialCompletado = false;


    [SerializeField] private TextMeshProUGUI finalScoreText;

    [Header("UI Estado Jugador")]
    public TextMeshProUGUI txtEstadoJugador;

    [Header("Control de Audio BGM")]
    public AudioSource bgmSource;
    [Range(0f, 1f)] public float volumenMenu = 1.0f;
    [Range(0f, 1f)] public float volumenJuego = 0.3f;
    private Coroutine rutinaVolumen;

    private Vector3 botePosInicial;
    private Quaternion boteRotInicial;
    private Vector3 jugadorPosInicial;
    private Quaternion jugadorRotInicial;
    private bool posicionesGuardadas = false;

    private Button[] activeButtons;
    private int selectedButtonIndex = 0;

    private bool wasWiiA = false;
    private bool wasWiiUp = false;
    private bool wasWiiDown = false;

    private NetworkVariable<int> selectedButtonIndexNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<bool> isMenuVisibleNet = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        selectedButtonIndexNet.OnValueChanged += (oldVal, newVal) =>
        {
            selectedButtonIndex = newVal;
            ResaltarBotonLocal(newVal);
        };

        isMenuVisibleNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (canvasMenu != null) canvasMenu.SetActive(newVal);
        };
    }

    void Awake()
    {
        if (canvasGameHUD != null)
        {
            CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvasGameHUD.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);

        // for less resolution
        var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        if (urpAsset != null)
        {
            urpAsset.renderScale = 0.8f;
        }
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        // end
    }

    void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GuardarPosicionesIniciales();

        ApagarJugador();
        MostrarMenu();
    }

    void Update()
    {
        ActualizarTextoEstadoHUD();
        if (Input.GetKeyDown(KeyCode.T))
        {
            tutorialCompletado = !tutorialCompletado;
            MostrarMenuLocal();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (panelTutorial != null && panelTutorial.activeSelf)
            {
                CerrarPanelTutorial();
                return;
            }

            if (panelWiimote != null && panelWiimote.activeSelf)
            {
                CerrarPanelWiimote();
                return;
            }

            bool jugando = canvasGameHUD != null && canvasGameHUD.activeInHierarchy 
                        && (canvasMenu == null || !canvasMenu.activeInHierarchy)
                        && (canvasGameOver == null || !canvasGameOver.activeInHierarchy);

            if (jugando)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
        bool subPanelAbierto = (panelTutorial != null && panelTutorial.activeSelf) ||
                              (panelWiimote != null && panelWiimote.activeSelf);

        bool menuActivo = canvasMenu != null && canvasMenu.activeInHierarchy;
        bool gameOverActivo = canvasGameOver != null && canvasGameOver.activeInHierarchy;

        if (subPanelAbierto || (!menuActivo && !gameOverActivo)) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            bool currentWiiDown = wiimote.Button.d_down || wiimote.Button.d_right;
            bool currentWiiUp = wiimote.Button.d_up || wiimote.Button.d_left;
            bool currentWiiA = wiimote.Button.a;

            if (currentWiiDown && !wasWiiDown) CambiarSeleccion(1);
            wasWiiDown = currentWiiDown;

            if (currentWiiUp && !wasWiiUp) CambiarSeleccion(-1);
            wasWiiUp = currentWiiUp;

            if (currentWiiA && !wasWiiA) EjecutarBotonSeleccionado();
            wasWiiA = currentWiiA;
        }
#endif

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) CambiarSeleccion(1);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) CambiarSeleccion(-1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) EjecutarBotonSeleccionado();

        
    }

    private void CambiarSeleccion(int direccion)
    {
        if (activeButtons == null || activeButtons.Length == 0) return;

        selectedButtonIndex = (selectedButtonIndex + direccion + activeButtons.Length) % activeButtons.Length;
        ResaltarBotonLocal(selectedButtonIndex);

        if (IsOwner || IsServer)
        {
            selectedButtonIndexNet.Value = selectedButtonIndex;
        }
    }

    private void EjecutarBotonSeleccionado()
    {
        if (activeButtons != null && selectedButtonIndex >= 0 && selectedButtonIndex < activeButtons.Length && activeButtons[selectedButtonIndex] != null)
        {
            activeButtons[selectedButtonIndex].onClick.Invoke();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void EmpezarJuegoServerRpc()
    {
        OcultarMenuYEmpezarLocal();
        EmpezarJuegoClientRpc();
    }
    public void EmpezarJuego()
    {
        OcultarMenuYEmpezarLocal();

        if (IsSpawned)
        {
            if (IsServer || IsHost)
                EmpezarJuegoClientRpc();
            else
                EmpezarJuegoServerRpc();
        }
    }

    [ClientRpc]
    private void EmpezarJuegoClientRpc()
    {
        if (!IsServer)
        {
            OcultarMenuYEmpezarLocal();
        }
    }

    private void OcultarMenuYEmpezarLocal()
    {
        AjustarVolumenMusica(volumenJuego);
        Time.timeScale = 1f;

        ResetearPosicionesOriginales();

        if (FishingCounterManager.Instance != null)
        {
            FishingCounterManager.Instance.ResetCounter();
        }

        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        if (boteObjeto != null) SetBoteVisible(true);

        if (canvasGameHUD != null)
        {
            canvasGameHUD.SetActive(true);
            CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        GarantizarJugador();
        if (playerController != null)
        {
            playerController.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        FishingTimerHUD timerHUD = FindObjectOfType<FishingTimerHUD>();
        if (timerHUD != null)
        {
            timerHUD.StartTimer();
        }
    }

    // --- PANEL WIIMOTE ---
    public void AbrirWiimote()
    {
        AbrirWiimoteLocal();
        if (IsSpawned && (IsSpawned && (IsServer || IsHost))) AbrirWiimoteClientRpc();
    }

    [ClientRpc]
    private void AbrirWiimoteClientRpc()
    {
        if (!IsServer) AbrirWiimoteLocal();
    }

    private void AbrirWiimoteLocal()
    {
        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (panelIniciarMenu != null) panelIniciarMenu.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(true);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CerrarPanelWiimote()
    {
        CerrarPanelWiimoteLocal();
        if (IsSpawned && (IsServer || IsHost)) CerrarPanelWiimoteClientRpc();
    }

    [ClientRpc]
    private void CerrarPanelWiimoteClientRpc()
    {
        if (!IsServer) CerrarPanelWiimoteLocal();
    }

    private void CerrarPanelWiimoteLocal()
    {
        if (panelWiimote != null) panelWiimote.SetActive(false);
        MostrarMenuLocal();
    }

    // --- PANEL TUTORIAL ---
    public void AbrirTutorial()
    {
        AbrirTutorialLocal();
        if (IsSpawned && (IsSpawned && (IsServer || IsHost))) AbrirTutorialClientRpc();
    }

    [ClientRpc]
    private void AbrirTutorialClientRpc()
    {
        if (!IsServer) AbrirTutorialLocal();
    }

    private void AbrirTutorialLocal()
    {
        AjustarVolumenMusica(volumenJuego);

        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (panelIniciarMenu != null) panelIniciarMenu.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(true);

        ApagarJugador();

        if (boteObjeto != null) SetBoteVisible(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CerrarPanelTutorial()
    {
        CerrarPanelTutorialLocal();
        if (IsSpawned && (IsServer || IsHost)) CerrarPanelTutorialClientRpc();
    }

    [ClientRpc]
    private void CerrarPanelTutorialClientRpc()
    {
        if (!IsServer) CerrarPanelTutorialLocal();
    }

    private void CerrarPanelTutorialLocal()
    {
        if (panelTutorial != null) panelTutorial.SetActive(false);
        ResetearPosicionesOriginales();
        tutorialCompletado = true;
        MostrarMenuLocal();
    }

    public void SalirDelJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void MostrarMenu()
    {
        MostrarMenuLocal();
        if (IsSpawned && (IsServer || IsHost)) SincronizarMenuClientRpc();
    }

    [ClientRpc]
    private void SincronizarMenuClientRpc()
    {
        if (!IsServer) MostrarMenuLocal();
    }

    private void MostrarMenuLocal()
    {
        AjustarVolumenMusica(volumenMenu);
        Time.timeScale = 1f;

        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true); // Restaura panel de botones principales
        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);

        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!tutorialCompletado && panelIniciarMenu != null)
        {
            if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
            panelIniciarMenu.SetActive(true);
            activeButtons = new Button[] { btnIniciarTutorial, btnIniciarSalir };
        }
        else
        {
            if (panelIniciarMenu != null) panelIniciarMenu.SetActive(false);
            if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
            activeButtons = new Button[] { btnJugar, btnWiimote, btnTutorial, btnSalir };
        }

        selectedButtonIndex = 0;

        if (activeButtons.Length > 0 && activeButtons[0] != null)
        {
            activeButtons[0].Select();
            if (IsSpawned && (IsServer || IsHost))
            {
                SincronizarSeleccionBotonClientRpc(0);
            }
        }
    }

    public void MostrarGameOver()
    {
        if (canvasGameOver != null && canvasGameOver.activeSelf) return;
        MostrarGameOverLocal();
        if (IsSpawned && (IsServer || IsHost)) SincronizarGameOverClientRpc();
    }

    [ClientRpc]
    private void SincronizarGameOverClientRpc()
    {
        if (!IsServer) MostrarGameOverLocal();
    }

    private void MostrarGameOverLocal()
    {
        AjustarVolumenMusica(volumenMenu);
        if (finalScoreText != null && FishingCounterManager.Instance != null)
        {
            int score = FishingCounterManager.Instance.GetFishCount();
            finalScoreText.text = $"Score final: {score}";
        }

        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(true);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 1f; // 0f

        activeButtons = new Button[] { btnReiniciar, btnVolverMenu };
        selectedButtonIndex = 0;

        if (activeButtons.Length > 0 && activeButtons[0] != null)
        {
            activeButtons[0].Select();
            if (IsSpawned && (IsServer || IsHost))
            {
                SincronizarSeleccionBotonClientRpc(0);
            }
        }
    }

    public void ReiniciarJuego()
    {
        ReiniciarJuegoLocal();
        if (IsSpawned && (IsServer || IsHost)) ReiniciarJuegoClientRpc();
    }

    [ClientRpc]
    private void ReiniciarJuegoClientRpc()
    {
        if (!IsServer) ReiniciarJuegoLocal();
    }

    private void ReiniciarJuegoLocal()
    {
        Time.timeScale = 1f;

        FishSpawner spawner = FindObjectOfType<FishSpawner>();
        if (spawner != null && (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer))
        {
            spawner.ReiniciarSpawning();
        }

        //EmpezarJuego();
        OcultarMenuYEmpezarLocal();
    }

    public void VolverAlMenuPrincipal()
    {
        VolverAlMenuPrincipalLocal();
        if (IsSpawned && (IsServer || IsHost)) VolverAlMenuPrincipalClientRpc();
    }

    [ClientRpc]
    private void VolverAlMenuPrincipalClientRpc()
    {
        if (!IsServer) VolverAlMenuPrincipalLocal();
    }

    private void VolverAlMenuPrincipalLocal()
    {
        Time.timeScale = 1f;

        if (canvasGameOver != null) canvasGameOver.SetActive(false);

        FishSpawner spawner = FindObjectOfType<FishSpawner>();
        if (spawner != null && (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer))
        {
            spawner.ClearAllFish();
        }

        MostrarMenuLocal();
    }

    private void GarantizarJugador()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<ThirdPController>();
        }
    }

    private void ApagarJugador()
    {
        ResetearPosicionesOriginales();
        if (boteObjeto != null) SetBoteVisible(false);
        if (rodObjeto != null) rodObjeto.SetActive(false);
        GarantizarJugador();
        ThirdPController[] players = FindObjectsOfType<ThirdPController>();
        foreach (var p in players)
        {
            if (p != null) p.enabled = false;
        }
    }
    private void GuardarPosicionesIniciales()
    {
        if (posicionesGuardadas) return;

        if (boteObjeto != null)
        {
            botePosInicial = boteObjeto.transform.position;
            boteRotInicial = boteObjeto.transform.rotation;
        }

        GarantizarJugador();
        if (playerController != null)
        {
            jugadorPosInicial = playerController.transform.position;
            jugadorRotInicial = playerController.transform.rotation;
        }

        posicionesGuardadas = true;
    }

    public void ResetearPosicionesOriginales()
    {
        GuardarPosicionesIniciales();

        if (boteObjeto != null)
        {
            var toggle = boteObjeto.GetComponent<BoatDriverToggle>();
            if (toggle != null) toggle.ForceStopDriving();

            //boteObjeto.transform.position = botePosInicial;
            //boteObjeto.transform.rotation = boteRotInicial;
            
            var bc = boteObjeto.GetComponent<BoatController>();
            if (bc != null) bc.SnapTo(botePosInicial, boteRotInicial);
            else { boteObjeto.transform.position = botePosInicial; boteObjeto.transform.rotation = boteRotInicial; }

            Rigidbody rbBote = boteObjeto.GetComponent<Rigidbody>();
            if (rbBote != null)
            {
                rbBote.velocity = Vector3.zero;
                rbBote.angularVelocity = Vector3.zero;
            }
        }

        GarantizarJugador();
        if (playerController != null)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerController.transform.position = jugadorPosInicial;
            playerController.transform.rotation = jugadorRotInicial;

            if (cc != null) cc.enabled = true;
        }
    }
    private void ResaltarBotonLocal(int index)
    {
        if (activeButtons != null && index >= 0 && index < activeButtons.Length)
        {
            if (activeButtons[index] != null)
            {
                activeButtons[index].Select();
                Debug.Log($"[UI DEBUG] Botron {index} ({activeButtons[index].name}) seleccionado localmente.");
            }
        }
    }

    [ClientRpc]
    private void SincronizarSeleccionBotonClientRpc(int index)
    {
        Debug.Log($"[UI DEBUG] ClientRpc recibido en celular. indice: {index}");
        if (!IsServer)
        {
            ResaltarBotonLocal(index);
        }
    }
    
    private void SetBoteVisible(bool visible)
    {
        if (boteObjeto == null) return;
        foreach (var r in boteObjeto.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    private ThirdPController ObtenerJugadorLocalReal()
    {
        // 1. Si estamos en red, obtenemos el personaje asignado a NUESTRO cliente
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var pc = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ThirdPController>();
                if (pc != null) return pc;
            }
        }

        // 2. Si no hay red o es singleplayer, buscamos el jugador local activo en la escena
        ThirdPController[] jugadores = FindObjectsOfType<ThirdPController>();
        foreach (var j in jugadores)
        {
            if (j != null && j.gameObject.activeInHierarchy && (j.IsOwner || !j.IsSpawned))
                return j;
        }

        return playerController;
    }

    private void ActualizarTextoEstadoHUD()
    {
        if (txtEstadoJugador == null || canvasGameHUD == null || !canvasGameHUD.activeInHierarchy) return;

        // 1. Detección directa del Bote (evalúa primero, sin depender del Inspector ni del jugador activo)
        BoatDriverToggle boatToggle = FindObjectOfType<BoatDriverToggle>();
        if (boatToggle != null && boatToggle.IsDriving)
        {
            txtEstadoJugador.text = "En el bote";
            return;
        }

        // 2. Si no está en el bote, busca al personaje para revisar estados de pesca / caminata
        ThirdPController jugadorActual = ObtenerJugadorLocalReal();
        if (jugadorActual == null) return;

        if (jugadorActual.isFishing)
        {
            if (jugadorActual.alreadyCast)
                txtEstadoJugador.text = "Pescando";
            else if (jugadorActual.isReeling)
                txtEstadoJugador.text = "Recogiendo...";
            else
                txtEstadoJugador.text = "Preparado";
        }
        else
        {
            txtEstadoJugador.text = "Caminando";
        }
    }

    private void AjustarVolumenMusica(float objetivo)
    {
        if (bgmSource == null) return;
        if (rutinaVolumen != null) StopCoroutine(rutinaVolumen);
        rutinaVolumen = StartCoroutine(TransicionVolumenCorrutina(objetivo));
    }

    private System.Collections.IEnumerator TransicionVolumenCorrutina(float objetivo)
    {
        float inicio = bgmSource.volume;
        float tiempo = 0f;
        float duracion = 0.8f; // Transición suave en 0.8 segundos

        while (tiempo < duracion)
        {
            tiempo += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(inicio, objetivo, tiempo / duracion);
            yield return null;
        }
        bgmSource.volume = objetivo;
    }
}