using UnityEngine;
using UnityEngine.UI;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class MenuMismaEscena : MonoBehaviour
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

    [Header("Botones de los 4 Tiles (Para navegación con Wiimote)")]
    public Button btnJugar;
    public Button btnWiimote;
    public Button btnTutorial;
    public Button btnSalir;

    private Button[] menuButtons;
    private int selectedButtonIndex = 0;

    private bool wasWiiA = false;
    private bool wasWiiUp = false;
    private bool wasWiiDown = false;

    void Awake()
    {
        // Forzamos el apagado inmediato mediante CanvasGroup para evitar bugs visuales
        if (canvasGameHUD != null)
        {
            CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvasGameHUD.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // Lo hace invisible
            cg.interactable = false; // Evita clics
            cg.blocksRaycasts = false; // Evita que bloquee otros botones
        }

        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        if (canvasMenu != null) canvasMenu.SetActive(true);
    }

    void Start()
    {
        menuButtons = new Button[] { btnJugar, btnWiimote, btnTutorial, btnSalir };

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ApagarJugador();
        MostrarMenu();
    }

    void Update()
    {
        // BLOQUEO: Si el tutorial o el panel de Wiimote están abiertos, 
        // ignoramos el Update del menú para que la tecla A no haga clics fantasma en el fondo.
        bool subPanelAbierto = (panelTutorial != null && panelTutorial.activeSelf) ||
                              (panelWiimote != null && panelWiimote.activeSelf);

        if (subPanelAbierto || canvasMenu == null || !canvasMenu.activeInHierarchy) return;

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
        if (menuButtons == null || menuButtons.Length == 0) return;

        selectedButtonIndex = (selectedButtonIndex + direccion + menuButtons.Length) % menuButtons.Length;
        if (menuButtons[selectedButtonIndex] != null)
        {
            menuButtons[selectedButtonIndex].Select();
        }
    }

    private void EjecutarBotonSeleccionado()
    {
        if (menuButtons != null && menuButtons[selectedButtonIndex] != null)
        {
            menuButtons[selectedButtonIndex].onClick.Invoke();
        }
    }

    public void EmpezarJuego()
    {
        Time.timeScale = 1f;

        if (canvasMenu != null) canvasMenu.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(false);
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(false);

        if (canvasGameHUD != null)
        {
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
        Debug.Log("[Menu] AbrirTutorial llamado. panelTutorial = " + (panelTutorial != null ? panelTutorial.name : "NULL"));
        if (panelWiimote != null) panelWiimote.SetActive(false);
        if (panelTutorial != null) panelTutorial.SetActive(true);

        ApagarJugador();
    }

    public void CerrarPanelTutorial()
    {
        if (panelTutorial != null) panelTutorial.SetActive(false);
        MostrarMenu();
    }

    public void SalirDelJuego()
    {
        Debug.Log("Cerrando el juego...");
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

        if (menuButtons != null && menuButtons.Length > 0 && menuButtons[0] != null)
        {
            menuButtons[0].Select();
            selectedButtonIndex = 0;
        }
    }

    public void MostrarGameOver()
    {
        if (canvasGameHUD != null) canvasGameHUD.SetActive(false);
        if (canvasGameOver != null) canvasGameOver.SetActive(true);

        ApagarJugador();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
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
        GarantizarJugador();
        ThirdPController[] players = FindObjectsOfType<ThirdPController>();
        foreach (var p in players)
        {
            if (p != null) p.enabled = false;
        }
    }
}