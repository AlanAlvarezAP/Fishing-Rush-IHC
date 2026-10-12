using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class MenuMismaEscena : NetworkBehaviour
{
    [Header("UI Canvases Principales")]
    public GameObject canvasMenu;         // Objeto 'Canvas_Menu'
    public GameObject canvasGameHUD;      // Objeto 'Canvas_GameHUD'
    public GameObject canvasGameOver;     // Objeto 'Canvas_GameOver'

    [Header("Sub-Paneles Opcionales")]
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

    [SerializeField] private TextMeshProUGUI finalScoreText;

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
        if (activeButtons[selectedButtonIndex] != null)
        {
            activeButtons[selectedButtonIndex].Select();
        }
    }

    private void EjecutarBotonSeleccionado()
    {
        if (activeButtons != null && activeButtons[selectedButtonIndex] != null)
        {
            activeButtons[selectedButtonIndex].onClick.Invoke();
        }
    }

    public void EmpezarJuego()
    {
        OcultarMenuYEmpezarLocal();

        if (IsServer || IsHost)
        {
            EmpezarJuegoClientRpc();
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

        if (boteObjeto != null) boteObjeto.SetActive(true);

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

    public void AbrirWiimote()
    {
        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(true);

        ApagarJugador();
    }

    public void CerrarPanelWiimote()
    {
        if (panelWiimote != null) panelWiimote.SetActive(false);
        MostrarMenu();
    }

    public void AbrirTutorial()
    {
        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(true);

        ApagarJugador();
    }

    public void CerrarPanelTutorial()
    {
        if (panelTutorial != null) panelTutorial.SetActive(false);
        ResetearPosicionesOriginales();
        MostrarMenu();
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
        Time.timeScale = 1f;

        if (canvasMenu != null) canvasMenu.SetActive(true);
        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);

        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        activeButtons = new Button[] { btnJugar, btnWiimote, btnTutorial, btnSalir };
        selectedButtonIndex = 0;

        if (activeButtons.Length > 0 && activeButtons[0] != null)
        {
            activeButtons[0].Select();
        }
    }

    public void MostrarGameOver()
    {
        if (finalScoreText != null && FishingCounterManager.Instance != null)
        {
            int score = FishingCounterManager.Instance.GetFishCount();
            finalScoreText.text = $"Score final: {score}";
        }

        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(true);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;

        activeButtons = new Button[] { btnReiniciar, btnVolverMenu };
        selectedButtonIndex = 0;

        if (activeButtons.Length > 0 && activeButtons[0] != null)
        {
            activeButtons[0].Select();
        }
    }

    public void ReiniciarJuego()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;

        if (canvasGameOver != null) canvasGameOver.SetActive(false);

        MostrarMenu();
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
        if (boteObjeto != null) boteObjeto.SetActive(false);
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
            boteObjeto.transform.position = botePosInicial;
            boteObjeto.transform.rotation = boteRotInicial;

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
}