using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public partial class MenuMismaEscena : NetworkBehaviour
{
    public enum HudPescaAccion
    {
        Potencia = 0,
        Gesto = 1,
        OcultarGesto = 2,
        VentanaTiro = 3,
        OcultarVentanaTiro = 4,
        OcultarTodo = 5
    }

    [Header("UI Controles Conductor Tablet")]
    public GameObject canvasControlesBote;

    [Header("Cámara Bote (Jugador 2 - Tablet)")]
    public GameObject camaraBoteTablet;

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

    [Header("Espera Pescador (Jugador 1) - dentro de Canvas_Menu")]
    [Tooltip("Panel 'Esperando a jugador 2 (conductor)...' que ve el pescador (PC y VR)")]
    public GameObject panelEsperandoConductor;

    [Header("Menus Conductor (Jugador 2) - Screen Space Overlay")]
    public GameObject canvasMenuConductor;
    [Tooltip("Panel con los botones Jugar / Tutorial / Salir del conductor")]
    public GameObject panelMenuConductor;
    [Tooltip("Panel 'Esperando al pescador...' que ve el conductor")]
    public GameObject panelEsperaConductor;
    public GameObject canvasGameOverConductor;
    [SerializeField] private TextMeshProUGUI finalScoreTextConductor;

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

    private Vector3 jugadorLocalPosInicial;
    private Quaternion jugadorLocalRotInicial;

    private NetworkVariable<int> selectedButtonIndexNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    // Momento (en tiempo del servidor) en que termina la partida. Cada dispositivo calcula el
    // tiempo restante con esto, asi un celular que se reconecta ve el mismo tiempo que la PC.
    // Declarada ANTES de isGameStartedNet para que llegue antes que el inicio de partida.
    private NetworkVariable<double> finPartidaNet = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isGameStartedNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Pantalla actual del pescador (PC). Sirve para que el celular VR, si se reconecta,
    // aparezca en la misma pantalla que la PC (los RPCs que se perdio no se repiten).
    public enum PantallaPescador : byte
    {
        Menu = 0,
        Tutorial = 1,
        Wiimote = 2,
        Juego = 3,
        GameOver = 4
    }

    private NetworkVariable<PantallaPescador> pantallaPescadorNet = new NetworkVariable<PantallaPescador>(
        PantallaPescador.Menu, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> tutorialCompletadoNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> tutorialEnPruebaNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Servidor: cliente que marco "listo" como conductor (para liberarlo si se desconecta)
    private ulong clienteConductorListo = ulong.MaxValue;

    // "Listo" de cada jugador (Jugar o Reiniciar). Solo los escribe la PC.
    // La partida arranca cuando los dos estan listos (o con la tecla Y en pruebas).
    private NetworkVariable<bool> pescadorListoNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> conductorListoNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Estado del pescador. Lo calcula la PC (servidor), que es donde corre la logica real
    // del pescador, y los celulares solo lo muestran.
    // Se usa un enum (byte) y NO un FixedString: en Netcode 1.9.1 el FixedString lanza
    // NullReferenceException al serializar el delta, y esa excepcion corta el envio de las
    // demas NetworkVariables del tick (ej. netIsFishing del ThirdPController).
    public enum EstadoJugador : byte
    {
        Caminando = 0,
        Preparado = 1,
        Pescando = 2,
        Recogiendo = 3
    }

    private NetworkVariable<EstadoJugador> estadoJugadorNet = new NetworkVariable<EstadoJugador>(
        EstadoJugador.Caminando, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Handlers con nombre (no lambdas) para poder quitarlos en OnNetworkDespawn: si el celular
        // se reconecta, este objeto vuelve a spawnear y sin esto se suscribirian dos veces.
        estadoJugadorNet.OnValueChanged += OnEstadoJugadorCambio;
        selectedButtonIndexNet.OnValueChanged += OnSeleccionCambio;
        pescadorListoNet.OnValueChanged += OnListoCambio;
        conductorListoNet.OnValueChanged += OnListoCambio;
        isGameStartedNet.OnValueChanged += OnPartidaIniciadaCambio;

        AplicarTextoEstado(TextoEstado(estadoJugadorNet.Value));

        InicializarLogsRed(); // quien se conecta y ping (MenuMismaEscena.Red.cs)

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += OnClienteDesconectado;
        }
        else
        {
            // Cliente (al conectarse por primera vez o al RECONECTARSE): toma el estado de la PC
            if (EsConductor())
            {
                if (isGameStartedNet.Value) OcultarMenuYEmpezarLocal();
            }
            else
            {
                tutorialCompletado = tutorialCompletadoNet.Value;
                RestaurarPantallaPescadorLocal();
            }
        }

        ActualizarEsperaLocal();

        // PlayerNetwork tambien toca las camaras al spawnear: se reaplica el rol de esta build
        ConfigurarCamarasPorRol();
    }

    public override void OnNetworkDespawn()
    {
        estadoJugadorNet.OnValueChanged -= OnEstadoJugadorCambio;
        selectedButtonIndexNet.OnValueChanged -= OnSeleccionCambio;
        pescadorListoNet.OnValueChanged -= OnListoCambio;
        conductorListoNet.OnValueChanged -= OnListoCambio;
        isGameStartedNet.OnValueChanged -= OnPartidaIniciadaCambio;

        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClienteDesconectado;
        FinalizarLogsRed();

        base.OnNetworkDespawn();
    }

    private void OnEstadoJugadorCambio(EstadoJugador anterior, EstadoJugador nuevo)
    {
        AplicarTextoEstado(TextoEstado(nuevo));
    }

    private void OnSeleccionCambio(int anterior, int nuevo)
    {
        if (EsConductor()) return; // el conductor usa el tactil, no la seleccion del Wiimote
        selectedButtonIndex = nuevo;
        ResaltarBotonLocal(nuevo);
    }

    private void OnListoCambio(bool anterior, bool nuevo)
    {
        ActualizarEsperaLocal();
    }

    // Solo el inicio se replica por esta variable. El paso a false (game over) NO cambia
    // pantallas: el game over y el volver al menu tienen su propio flujo.
    private void OnPartidaIniciadaCambio(bool anterior, bool nuevo)
    {
        if (IsServer) return; // la PC ya lo aplico en IniciarPartidaServidor
        if (nuevo) OcultarMenuYEmpezarLocal();
    }

    /// <summary>Servidor: si se desconecta el cliente que estaba "listo" como conductor, se libera.</summary>
    private void OnClienteDesconectado(ulong clientId)
    {
        if (!IsServer) return;
        if (clientId != clienteConductorListo) return;

        clienteConductorListo = ulong.MaxValue;
        if (conductorListoNet.Value && !isGameStartedNet.Value)
        {
            conductorListoNet.Value = false;
            Debug.Log("[LISTOS] El conductor se desconecto: deja de estar listo.");
            ActualizarEsperaLocal();
        }
    }

    /// <summary>Celular VR: muestra la misma pantalla que tiene la PC en este momento.</summary>
    private void RestaurarPantallaPescadorLocal()
    {
        switch (pantallaPescadorNet.Value)
        {
            case PantallaPescador.Juego:
                if (isGameStartedNet.Value) OcultarMenuYEmpezarLocal();
                else MostrarMenuLocal();
                break;
            case PantallaPescador.GameOver:
                MostrarGameOverLocal();
                break;
            case PantallaPescador.Tutorial:
                AbrirTutorialLocal();
                if (tutorialEnPruebaNet.Value) AplicarModoEscenaTutorialLocal(true);
                break;
            case PantallaPescador.Wiimote:
                AbrirWiimoteLocal();
                break;
            default:
                MostrarMenuLocal();
                break;
        }
    }

    private void SetPantallaPescadorServidor(PantallaPescador pantalla)
    {
        if (IsSpawned && IsServer && !EsConductor() && pantallaPescadorNet.Value != pantalla)
        {
            pantallaPescadorNet.Value = pantalla;
        }
    }

    private void MarcarTutorialCompletadoLocal()
    {
        tutorialCompletado = true;
        if (IsSpawned && IsServer && !tutorialCompletadoNet.Value) tutorialCompletadoNet.Value = true;
    }

    // ---------------- TIEMPO DE PARTIDA (lo usa FishingTimerHUD) ----------------
    /// <summary>true si el tiempo lo marca la PC (hay red y la partida tiene hora de fin).</summary>
    public bool TiempoSincronizado => IsSpawned && finPartidaNet.Value > 0d;

    public float TiempoRestanteRed()
    {
        if (!TiempoSincronizado) return 0f;
        double restante = finPartidaNet.Value - NetworkManager.ServerTime.Time;
        return Mathf.Max(0f, (float)restante);
    }

    /// <summary>Solo la PC (o sin red) decide que se acabo el tiempo y muestra el game over a todos.</summary>
    public bool PuedeTerminarPartida => !IsSpawned || IsServer;

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
        if (panelEsperandoConductor != null) panelEsperandoConductor.SetActive(false);

        if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(false);
        if (panelEsperaConductor != null) panelEsperaConductor.SetActive(false);

        // Controles del bote apagados por defecto; solo se encienden para el conductor al empezar
        SetControlesBoteActivos(false);

        // Cada rol ve SOLO sus menus: el pescador (PC/VR) los World Space, el conductor los Overlay
        bool conductor = EsConductor();
        if (canvasMenu != null) canvasMenu.SetActive(!conductor);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(!conductor);
        if (canvasMenuConductor != null) canvasMenuConductor.SetActive(conductor);

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
        ActualizarPingRed();

        // --- ATAJOS DE PRUEBA (funcionan en cualquier dispositivo y se aplican en todos) ---
        // Y: salta la espera del otro jugador y empieza la partida
        if (Input.GetKeyDown(KeyCode.Y)) PedirForzarInicio();
        // T: marca el tutorial como hecho y muestra el menu completo (Jugar, Wiimote, Tutorial, Salir)
        if (Input.GetKeyDown(KeyCode.T)) PedirMenuCompleto();

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

    // =====================================================================
    // INICIO DE PARTIDA: los dos jugadores deben estar "listos"
    //   - Pescador: presiona Jugar/Reiniciar en la PC (Wiimote o teclado).
    //   - Conductor: presiona Jugar/Reiniciar en su celular (se manda a la PC por ServerRpc).
    //   El primero que presiona ve un panel de espera hasta que el otro tambien presione.
    //   La PC decide y arranca la partida en todos con isGameStartedNet.
    // =====================================================================

    /// <summary>Boton Jugar (menu) del pescador y del conductor.</summary>
    public void EmpezarJuego()
    {
        MarcarListoLocal();
    }

    /// <summary>Boton Reiniciar (game over): igual que Jugar, tambien espera al otro jugador.</summary>
    public void ReiniciarJuego()
    {
        MarcarListoLocal();
    }

    /// <summary>
    /// true si aun no hay conexion pero existe NetworkManager (ej. el celular se esta conectando):
    /// en ese caso no se hace nada, para no arrancar una partida local desincronizada.
    /// </summary>
    private bool EsperandoConexion()
    {
        if (IsSpawned) return false;
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[RED] Aun no hay conexion con la PC; se ignora el boton.");
            return true;
        }
        return false;
    }

    private void MarcarListoLocal()
    {
        if (EsperandoConexion()) return;

        // Sin NetworkManager en la escena (pruebas sueltas): empieza directo
        if (!IsSpawned)
        {
            OcultarMenuYEmpezarLocal();
            return;
        }

        int rol = (int)ConfigJugador.RolActual;
        if (IsServer) MarcarListoServidor(rol, true);
        else MarcarListoServerRpc(rol, true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MarcarListoServerRpc(int rol, bool listo, ServerRpcParams rpcParams = default)
    {
        if ((TipoJugador)rol == TipoJugador.Jugador2_ConductorTablet)
        {
            clienteConductorListo = listo ? rpcParams.Receive.SenderClientId : ulong.MaxValue;
        }
        MarcarListoServidor(rol, listo);
    }

    private void MarcarListoServidor(int rol, bool listo)
    {
        if (!IsServer || isGameStartedNet.Value) return;

        if ((TipoJugador)rol == TipoJugador.Jugador2_ConductorTablet) conductorListoNet.Value = listo;
        else pescadorListoNet.Value = listo;

        Debug.Log($"[LISTOS] pescador={pescadorListoNet.Value} conductor={conductorListoNet.Value}");

        if (pescadorListoNet.Value && conductorListoNet.Value)
        {
            IniciarPartidaServidor();
        }
        else
        {
            ActualizarEsperaLocal(); // en la PC se aplica de inmediato
        }
    }

    /// <summary>Solo en la PC: arranca la partida en todos los dispositivos.</summary>
    private void IniciarPartidaServidor()
    {
        if (!IsServer) return;

        pescadorListoNet.Value = false;
        conductorListoNet.Value = false;

        FishSpawner spawner = FindObjectOfType<FishSpawner>();
        if (spawner != null) spawner.ReiniciarSpawning();

        // Hora de fin de la partida en tiempo del servidor (antes de isGameStartedNet)
        FishingTimerHUD timer = FindObjectOfType<FishingTimerHUD>(true);
        float duracion = timer != null ? timer.DuracionPartida : 180f;
        finPartidaNet.Value = NetworkManager.ServerTime.Time + duracion;

        isGameStartedNet.Value = true;   // los clientes empiezan por el OnValueChanged
        OcultarMenuYEmpezarLocal();
    }

    /// <summary>Muestra u oculta el panel de espera segun los "listos" (en cada dispositivo).</summary>
    private void ActualizarEsperaLocal()
    {
        if (!IsSpawned) return;

        bool conductor = EsConductor();
        bool yoListo = conductor ? conductorListoNet.Value : pescadorListoNet.Value;
        bool otroListo = conductor ? pescadorListoNet.Value : conductorListoNet.Value;
        bool esperando = yoListo && !otroListo && !isGameStartedNet.Value;

        if (esperando) MostrarEsperaLocal();
        else OcultarEsperaLocal();
    }

    private void MostrarEsperaLocal()
    {
        if (EsConductor())
        {
            if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(false);
            if (canvasMenuConductor != null) canvasMenuConductor.SetActive(true);
            if (panelMenuConductor != null) panelMenuConductor.SetActive(false);
            if (panelEsperaConductor != null) panelEsperaConductor.SetActive(true);
            return;
        }

        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelIniciarMenu != null) panelIniciarMenu.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);
        if (panelEsperandoConductor != null) panelEsperandoConductor.SetActive(true);

        // Mientras espera no hay botones que navegar con el Wiimote / teclado
        activeButtons = null;
    }

    private void OcultarEsperaLocal()
    {
        if (panelEsperaConductor != null) panelEsperaConductor.SetActive(false);
        if (panelEsperandoConductor != null) panelEsperandoConductor.SetActive(false);
    }

    // --- TECLA Y (pruebas): salta la espera y empieza en todos ---
    private void PedirForzarInicio()
    {
        if (EsperandoConexion()) return;

        if (!IsSpawned)
        {
            OcultarMenuYEmpezarLocal();
            return;
        }

        if (IsServer) ForzarInicioServidor();
        else ForzarInicioServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ForzarInicioServerRpc()
    {
        ForzarInicioServidor();
    }

    private void ForzarInicioServidor()
    {
        if (!IsServer || isGameStartedNet.Value) return;
        Debug.Log("[PRUEBAS] Tecla Y: inicio forzado sin esperar al otro jugador");
        IniciarPartidaServidor();
    }

    // --- TECLA T (pruebas): tutorial marcado como hecho y menu completo en todos ---
    private void PedirMenuCompleto()
    {
        if (IsSpawned && !IsServer)
        {
            MenuCompletoServerRpc();
            return;
        }
        MenuCompletoServidor();
    }

    [ServerRpc(RequireOwnership = false)]
    private void MenuCompletoServerRpc()
    {
        MenuCompletoServidor();
    }

    private void MenuCompletoServidor()
    {
        if (IsSpawned && isGameStartedNet.Value) return; // durante la partida no hace nada

        AplicarMenuCompletoLocal();
        if (IsSpawned && IsServer) MenuCompletoClientRpc();
    }

    [ClientRpc]
    private void MenuCompletoClientRpc()
    {
        if (IsServer) return;
        AplicarMenuCompletoLocal();
    }

    private void AplicarMenuCompletoLocal()
    {
        MarcarTutorialCompletadoLocal();

        if (EsConductor()) return; // el menu del conductor ya es el completo

        // Solo se refresca si se esta viendo el panel inicial (Tutorial / Salir)
        if (panelIniciarMenu != null && panelIniciarMenu.activeInHierarchy)
        {
            MostrarMenuLocal();
        }
    }

    private void OcultarMenuYEmpezarLocal()
    {
        SetPantallaPescadorServidor(PantallaPescador.Juego);
        AjustarVolumenMusica(volumenJuego);
        Time.timeScale = 1f;

        ResetearPosicionesOriginales();

        // Solo el Jugador 2 (conductor) ve los controles del bote
        SetControlesBoteActivos(EsConductor());

        // Solo la PC reinicia el puntaje: si un celular se reconecta a mitad de partida y pasa
        // por aqui, NO debe borrar el puntaje de todos
        if (FishingCounterManager.Instance != null && (!IsSpawned || IsServer))
        {
            FishingCounterManager.Instance.ResetCounter();
        }

        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);
        if (canvasMenuConductor != null) canvasMenuConductor.SetActive(false);
        if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(false);
        OcultarEsperaLocal();

        if (boteObjeto != null)
        {
            SetBoteVisible(true);
            SetMovimientoBote(true);

            GarantizarJugador();
            if (playerController != null) playerController.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (rodObjeto != null) rodObjeto.SetActive(true);

        // HUD (puntaje, estado, tiempo) por ahora solo para el pescador (esta acomodado para VR)
        if (canvasGameHUD != null && !EsConductor())
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

        // --- CÁMARAS SEGÚN ROL ---
        ConfigurarCamarasPorRol();

        if (EsConductor())
        {
            // El ThirdPController se deja habilitado: en el celular su Update no hace nada
            // (no es dueño ni servidor), pero su LateUpdate es el que refleja la caña, el
            // anzuelo y el giro del pescador. Si se apaga, el conductor ve al pescador congelado.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else // Jugador1_PescadorVR (PC y celular VR)
        {
            if (playerController != null)
            {
                playerController.enabled = true;

                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        FishingTimerHUD timerHUD = FindObjectOfType<FishingTimerHUD>();
        if (timerHUD != null)
        {
            timerHUD.StartTimer();
        }
        Debug.Log($"[JUEGO] rol={ConfigJugador.RolActual} player={(playerController != null ? playerController.name + " enabled=" + playerController.enabled : "null")}");
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
        if (!IsServer && !EsConductor()) AbrirWiimoteLocal();
    }

    private void AbrirWiimoteLocal()
    {
        SetPantallaPescadorServidor(PantallaPescador.Wiimote);
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
        if (!IsServer && !EsConductor()) CerrarPanelWiimoteLocal();
    }

    private void CerrarPanelWiimoteLocal()
    {
        if (panelWiimote != null) panelWiimote.SetActive(false);
        MostrarMenuLocal();
    }

    // --- PANEL TUTORIAL ---
    public void AbrirTutorial()
    {
        if (EsConductor())
        {
            // El tutorial del conductor todavia no existe (sera independiente del pescador)
            Debug.Log("[TUTORIAL] Tutorial del conductor pendiente.");
            return;
        }

        AbrirTutorialLocal();
        if (IsSpawned && (IsSpawned && (IsServer || IsHost))) AbrirTutorialClientRpc();
    }

    [ClientRpc]
    private void AbrirTutorialClientRpc()
    {
        if (!IsServer && !EsConductor()) AbrirTutorialLocal();
    }

    private void AbrirTutorialLocal()
    {
        SetPantallaPescadorServidor(PantallaPescador.Tutorial);
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
        if (!IsServer && !EsConductor()) CerrarPanelTutorialLocal();
    }

    private void CerrarPanelTutorialLocal()
    {
        if (panelTutorial != null) panelTutorial.SetActive(false);
        ResetearPosicionesOriginales();
        MarcarTutorialCompletadoLocal();
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
        if (!IsServer && !EsConductor()) MostrarMenuLocal();
    }

    private void MostrarMenuLocal()
    {
        if (EsConductor())
        {
            MostrarMenuConductorLocal();
            return;
        }

        SetPantallaPescadorServidor(PantallaPescador.Menu);
        AjustarVolumenMusica(volumenMenu);
        Time.timeScale = 1f;

        OcultarEsperaLocal();
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

    /// <summary>Menu del conductor (Overlay): Jugar / Tutorial / Salir. Solo en su celular.</summary>
    private void MostrarMenuConductorLocal()
    {
        AjustarVolumenMusica(volumenMenu);
        Time.timeScale = 1f;

        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(false);

        if (canvasMenuConductor != null) canvasMenuConductor.SetActive(true);
        if (panelMenuConductor != null) panelMenuConductor.SetActive(true);
        if (panelEsperaConductor != null) panelEsperaConductor.SetActive(false);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private bool GameOverVisibleLocal()
    {
        GameObject go = EsConductor() ? canvasGameOverConductor : canvasGameOver;
        return go != null && go.activeSelf;
    }

    public void MostrarGameOver()
    {
        if (GameOverVisibleLocal()) return;

        // La PC termina la partida para todos: ya no esta en juego y nadie esta listo
        if (IsSpawned && IsServer)
        {
            isGameStartedNet.Value = false;
            pescadorListoNet.Value = false;
            conductorListoNet.Value = false;
        }

        MostrarGameOverLocal();
        if (IsSpawned && IsServer) SincronizarGameOverClientRpc();
    }

    [ClientRpc]
    private void SincronizarGameOverClientRpc()
    {
        if (IsServer || GameOverVisibleLocal()) return;
        MostrarGameOverLocal();
    }

    private void MostrarGameOverLocal()
    {
        AjustarVolumenMusica(volumenMenu);

        if (EsConductor())
        {
            if (finalScoreTextConductor != null && FishingCounterManager.Instance != null)
            {
                finalScoreTextConductor.text = $"Score final: {FishingCounterManager.Instance.GetFishCount()}";
            }

            if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
            if (canvasMenuConductor != null) canvasMenuConductor.SetActive(false);
            if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(true);

            ApagarJugador();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        SetPantallaPescadorServidor(PantallaPescador.GameOver);

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

    /// <summary>
    /// Volver al menu es INDEPENDIENTE por jugador:
    /// - Conductor: solo su celular vuelve a su menu (y deja de estar listo).
    /// - Pescador: la PC y el celular VR vuelven al menu (el conductor no se entera).
    /// </summary>
    public void VolverAlMenuPrincipal()
    {
        if (EsConductor())
        {
            if (IsSpawned && !IsServer) MarcarListoServerRpc((int)TipoJugador.Jugador2_ConductorTablet, false);
            MostrarMenuLocal();
            return;
        }

        if (IsSpawned && IsServer) pescadorListoNet.Value = false;

        VolverAlMenuPrincipalLocal();
        if (IsSpawned && IsServer) VolverAlMenuPrincipalClientRpc();
    }

    [ClientRpc]
    private void VolverAlMenuPrincipalClientRpc()
    {
        if (!IsServer && !EsConductor()) VolverAlMenuPrincipalLocal();
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

    private static bool EsConductor()
    {
        return ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet;
    }

    private void SetControlesBoteActivos(bool activos)
    {
        // Nunca se muestran en la build del pescador (PC / celular VR)
        if (canvasControlesBote != null) canvasControlesBote.SetActive(activos && EsConductor());
    }

    private void SetMovimientoBote(bool habilitado)
    {
        if (boteObjeto == null) return;
        var bc = boteObjeto.GetComponent<BoatController>();
        if (bc != null) bc.SetMovimientoHabilitado(habilitado);
    }

    /// <summary>
    /// Camaras segun el rol de esta build (se llama al iniciar, al spawnear en red y al empezar):
    /// - Jugador 2 (conductor): SIEMPRE ve por camaraBoteTablet (menu, partida y game over).
    ///   Las camaras del pescador se apagan (solo el componente Camera, no su GameObject,
    ///   por si la caña u otros objetos cuelgan de ellas).
    /// - Jugador 1 (PC y celular VR): camaraBoteTablet SIEMPRE apagada; las camaras del
    ///   pescador las maneja PlayerNetwork / Cardboard.
    /// </summary>
    private void ConfigurarCamarasPorRol()
    {
        bool conductor = EsConductor();

        Camera camBote = null;
        if (camaraBoteTablet != null)
        {
            camaraBoteTablet.SetActive(conductor);
            camBote = camaraBoteTablet.GetComponent<Camera>();
            if (camBote != null)
            {
                camBote.enabled = conductor;
                // Nunca en pantalla dividida (por si el XR de Cardboard sigue activo en el celular)
                if (conductor) camBote.stereoTargetEye = StereoTargetEyeMask.None;
            }
        }

        if (!conductor) return;

        GarantizarJugador();
        if (playerController == null) return;

        AudioListener listenerBote = camaraBoteTablet != null ? camaraBoteTablet.GetComponent<AudioListener>() : null;

        foreach (Camera cam in playerController.GetComponentsInChildren<Camera>(true))
        {
            if (cam == camBote) continue;
            cam.enabled = false;

            // Solo se apaga el listener del pescador si la camara del bote tiene uno propio
            if (listenerBote != null)
            {
                AudioListener al = cam.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }
    }

    private void ApagarJugador()
    {
        SetControlesBoteActivos(false);
        ConfigurarCamarasPorRol();

        if (boteObjeto != null)
        {
            SetBoteVisible(false);
            SetMovimientoBote(false);
        }
        ResetearPosicionesOriginales();
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
            // Guardamos la posición LOCAL exacta que le pusiste en el Inspector dentro del bote
            jugadorLocalPosInicial = playerController.transform.localPosition;
            jugadorLocalRotInicial = playerController.transform.localRotation;
        }
        else
        {
            // Respaldo por defecto sobre la cubierta (3.25m en Y)
            jugadorLocalPosInicial = new Vector3(0f, 3.25f, 0f);
            jugadorLocalRotInicial = Quaternion.identity;
        }

        posicionesGuardadas = true;
    }

    public void ResetearPosicionesOriginales()
    {
        GuardarPosicionesIniciales();

        if (boteObjeto != null)
        {
            // El bote queda quieto hasta que la partida lo habilite de nuevo
            SetMovimientoBote(false);

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

        if (rodObjeto != null) rodObjeto.SetActive(true);

        GarantizarJugador();
        if (playerController != null)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            
            if (cc != null) cc.enabled = false;

            playerController.transform.localPosition = new Vector3(0f, 5.0f, 0f);
            playerController.transform.localRotation = Quaternion.identity;

            Physics.SyncTransforms();

            var netTransform = playerController.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null && netTransform.IsSpawned && (netTransform.IsServer || netTransform.IsOwner))
            {
                netTransform.Teleport(playerController.transform.position, playerController.transform.rotation, playerController.transform.localScale);
            }

            if (cc != null && ConfigJugador.RolActual == TipoJugador.Jugador1_PescadorVR) 
            {
                cc.enabled = true;
            }
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
        if (!IsServer && !EsConductor())
        {
            ResaltarBotonLocal(index);
        }
    }
    
    // =====================================================================
    // HUD DE PESCA: relay PC (servidor) -> celular (visor)
    // FishingHUDManager es un MonoBehaviour normal y no viaja por red. Sus eventos se
    // reenvian por este NetworkBehaviour, cuyos RPCs ya funcionan en el celular.
    // =====================================================================
    public void EnviarHUDPescaAlVisor(HudPescaAccion accion, float valor, string texto, int a, int b)
    {
        if (!IsSpawned || !IsServer) return;
        HudPescaClientRpc((int)accion, valor, texto ?? "", a, b);
    }

    [ClientRpc]
    private void HudPescaClientRpc(int accion, float valor, string texto, int a, int b)
    {
        if (IsServer || EsConductor()) return; // la PC ya lo mostro; el conductor no usa el HUD de pesca
        AplicarHUDPescaVisor((HudPescaAccion)accion, valor, texto, a, b);
    }

    private void AplicarHUDPescaVisor(HudPescaAccion accion, float valor, string texto, int a, int b)
    {
        try
        {
            bool esMostrar = accion == HudPescaAccion.Potencia
                          || accion == HudPescaAccion.Gesto
                          || accion == HudPescaAccion.VentanaTiro;

            // Si el HUD de juego estaba apagado en el celular, se enciende antes de mostrar algo
            if (esMostrar) AsegurarHUDActivoVisor();

            FishingHUDManager hud = FishingHUDManager.Instance;
            if (hud == null) hud = FindObjectOfType<FishingHUDManager>(true);
            if (hud == null) return;

            if (esMostrar && !hud.gameObject.activeInHierarchy) hud.gameObject.SetActive(true);

            switch (accion)
            {
                case HudPescaAccion.Potencia:
                    hud.ShowCastPowerLocal(valor);
                    break;
                case HudPescaAccion.Gesto:
                    hud.ShowGesturePromptLocal(texto, a, b);
                    break;
                case HudPescaAccion.OcultarGesto:
                    hud.HideGesturePromptLocal();
                    break;
                case HudPescaAccion.VentanaTiro:
                    hud.ShowCastWindowTimerLocal(valor);
                    break;
                case HudPescaAccion.OcultarVentanaTiro:
                    hud.HideCastWindowTimerLocal();
                    break;
                case HudPescaAccion.OcultarTodo:
                    hud.HideAllLocal();
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[HUD PESCA][VISOR] Error aplicando accion " + accion + ": " + e.Message);
        }
    }

    // =====================================================================
    // ANUNCIOS (gesto requerido / victoria / derrota): relay PC -> celulares
    // AnunciosUI es un MonoBehaviour y solo lo llama la logica del minijuego (PC).
    // =====================================================================
    public enum AnuncioAccion
    {
        Gesto = 0,
        Victoria = 1,
        Derrota = 2
    }

    public void EnviarAnuncioAlVisor(AnuncioAccion accion, int gesto, float duracion)
    {
        if (!IsSpawned || !IsServer) return;
        AnuncioClientRpc((int)accion, gesto, duracion);
    }

    [ClientRpc]
    private void AnuncioClientRpc(int accion, int gesto, float duracion)
    {
        if (IsServer || EsConductor()) return; // la PC ya lo mostro; avisos por ahora solo para el VR

        try
        {
            AsegurarHUDActivoVisor();

            AnunciosUI anuncios = AnunciosUI.Instance;
            if (anuncios == null) anuncios = FindObjectOfType<AnunciosUI>(true);
            if (anuncios == null) return;

            switch ((AnuncioAccion)accion)
            {
                case AnuncioAccion.Gesto:
                    anuncios.MostrarGestoRequeridoLocal((FishCube.FishingGesture)gesto, duracion);
                    break;
                case AnuncioAccion.Victoria:
                    anuncios.MostrarVictoriaLocal();
                    break;
                case AnuncioAccion.Derrota:
                    anuncios.MostrarDerrotaLocal();
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ANUNCIOS][VISOR] Error aplicando anuncio " + accion + ": " + e.Message);
        }
    }

    private void AsegurarHUDActivoVisor()
    {
        if (canvasGameHUD == null) return;

        if (!canvasGameHUD.activeSelf) canvasGameHUD.SetActive(true);

        CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    // =====================================================================
    // ESCENA DEL TUTORIAL (PC + celular)
    // La PC (servidor) decide si el tutorial esta mostrando un SLIDE o una PRUEBA y lo manda
    // al celular por este NetworkBehaviour (mismo camino de RPC que usa EmpezarJuego).
    //
    // En PRUEBA se deja la escena EXACTAMENTE como EmpezarJuego: bote visible y en conduccion,
    // y el MISMO ThirdPController (playerController) habilitado en la PC y en el celular.
    // Esto es lo importante: el LateUpdate del ThirdPController tambien debe correr en el celular
    // para que (1) envie la rotacion en Y a la PC y (2) el anzuelo siga a netHookPos.
    // Antes el tutorial habilitaba otro objeto (ObtenerJugadorLocalReal) y ademas forzaba
    // cc.enabled = true, cosa que EmpezarJuego nunca hace.
    // =====================================================================
    public void SetModoEscenaTutorial(bool enPrueba)
    {
        if (IsSpawned && IsServer) tutorialEnPruebaNet.Value = enPrueba;
        AplicarModoEscenaTutorialLocal(enPrueba);
        if (IsSpawned && IsServer) SetModoEscenaTutorialClientRpc(enPrueba);
    }

    [ClientRpc]
    private void SetModoEscenaTutorialClientRpc(bool enPrueba)
    {
        if (!IsServer && !EsConductor()) AplicarModoEscenaTutorialLocal(enPrueba);
    }

    private void AplicarModoEscenaTutorialLocal(bool enPrueba)
    {
        // HUD primero (no depende de nada mas y no puede fallar por el bote o el jugador)
        AplicarHUDTutorialLocal(enPrueba);

        try
        {
            // Igual que OcultarMenuYEmpezarLocal: parte siempre de la escena reseteada
            // (detiene la conduccion, reposiciona bote y jugador y apaga el ThirdPController)
            ResetearPosicionesOriginales();

            if (enPrueba)
            {
                if (boteObjeto != null) SetBoteVisible(true);

                // Mismo jugador y mismo enable que EmpezarJuego (sin tocar el CharacterController)
                GarantizarJugador();
                if (playerController != null) playerController.enabled = true;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            // En SLIDE el jugador queda apagado, igual que en el menu

            Debug.Log($"[TUTORIAL][ESCENA] enPrueba={enPrueba} server={IsServer} jugador=" +
                      (playerController != null ? playerController.name + " enabled=" + playerController.enabled : "null"));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TUTORIAL][ESCENA] Error aplicando modo escena: " + e.Message);
        }
    }

    private void AplicarHUDTutorialLocal(bool enJuego)
    {
        if (canvasGameHUD == null) return;

        CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();
        if (cg == null) cg = canvasGameHUD.AddComponent<CanvasGroup>();

        if (enJuego)
        {
            canvasGameHUD.SetActive(true);
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            if (FishingHUDManager.Instance != null) FishingHUDManager.Instance.EnsureCamera();
        }
        else
        {
            cg.alpha = 0f;
            if (FishingHUDManager.Instance != null) FishingHUDManager.Instance.HideAllLocal();
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
        // Cliente (celular VR / conductor): solo refleja lo que manda la PC
        if (IsSpawned && !IsServer)
        {
            AplicarTextoEstado(TextoEstado(estadoJugadorNet.Value));
            return;
        }

        // Servidor (PC) o sin red: calcula el estado
        if (canvasGameHUD == null || !canvasGameHUD.activeInHierarchy) return;

        ThirdPController jugadorActual = ObtenerJugadorLocalReal();
        if (jugadorActual == null) return;

        EstadoJugador estado = CalcularEstadoJugador(jugadorActual);
        AplicarTextoEstado(TextoEstado(estado));

        if (IsSpawned && IsServer && estadoJugadorNet.Value != estado)
        {
            estadoJugadorNet.Value = estado;
        }
    }

    private EstadoJugador CalcularEstadoJugador(ThirdPController jugador)
    {
        // Estados del pescador (Jugador 1): pesca / caminata
        if (!jugador.isFishing) return EstadoJugador.Caminando;
        // isReeling va primero: mientras recoge, alreadyCast sigue en true
        if (jugador.isReeling) return EstadoJugador.Recogiendo;
        if (jugador.alreadyCast) return EstadoJugador.Pescando;
        return EstadoJugador.Preparado;
    }

    private static string TextoEstado(EstadoJugador estado)
    {
        switch (estado)
        {
            case EstadoJugador.Preparado: return "Preparado";
            case EstadoJugador.Pescando: return "Pescando";
            case EstadoJugador.Recogiendo: return "Recogiendo...";
            default: return "Caminando";
        }
    }

    private void AplicarTextoEstado(string estado)
    {
        if (txtEstadoJugador != null && txtEstadoJugador.text != estado)
            txtEstadoJugador.text = estado;
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
        float duracion = 0.8f; // Transici�n suave en 0.8 segundos

        while (tiempo < duracion)
        {
            tiempo += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(inicio, objetivo, tiempo / duracion);
            yield return null;
        }
        bgmSource.volume = objetivo;
    }
}