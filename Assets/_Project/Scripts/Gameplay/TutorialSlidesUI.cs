using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

/// <summary>
/// Tutorial con slides.
/// - PC (servidor/host): ejecuta TODA la logica (input, pruebas, peces) y decide el estado.
/// - Celular (cliente/visor): no envia nada. Refleja 2 NetworkVariables (indice del slide y si el
///   slide es visible) y la escena (bote, jugador, HUD) llega por MenuMismaEscena.SetModoEscenaTutorial,
///   que usa el mismo camino que EmpezarJuego.
/// </summary>
public class TutorialSlidesUI : NetworkBehaviour
{
    [Header("Paneles de las Slides del Tutorial")]
    [SerializeField] private GameObject[] panelesTutorial;

    [Header("Objetos de Escena")]
    [SerializeField] private GameObject fishSpawnerManager;
    [SerializeField] private GameObject boteObjeto;
    [SerializeField] private ThirdPController playerController;
    [SerializeField] private GameObject canvasGameHUD;

    [Header("Configuracion Pesca Cercana")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private Transform[] puntosDeSpawnCercanos;
    [SerializeField] private Transform puntoSpawnLejano;

    private Vector3 posicionInicialJugador;

    private GameObject[] pecesPorPunto;
    private GameObject pezLejano;

    private int conteoPescaInicial = 0;
    private int conteoPescaLejanaInicial = 0;

    private int currentSlideIndex = 0;
    private bool slideVisible = true;   // verdad local del servidor
    private bool wasWiiA = false;

    private enum EstadoTutorial { MostrandoSlide, ProbandoMovimiento, ProbandoPescaCercana, ProbandoPescaLejana, EsperandoSiguienteSlide }
    private EstadoTutorial estadoActual = EstadoTutorial.MostrandoSlide;

    private bool pressedUp = false;
    private bool pressedDown = false;
    private bool pressedLeft = false;
    private bool pressedRight = false;

    private MenuMismaEscena menuRef;

    // ---------------- ESTADO DE RED (solo escribe el servidor) ----------------
    private NetworkVariable<int> slideNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> slideVisibleNet = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ---------------- ROLES ----------------
    private bool RedActiva => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool EsVisor => RedActiva && !NetworkManager.Singleton.IsServer;      // celular
    private bool PuedoEscribirRed => RedActiva && NetworkManager.Singleton.IsServer && IsSpawned;

    // ---------------- CICLO DE VIDA ----------------
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        slideNet.OnValueChanged += OnSlideNetCambio;
        slideVisibleNet.OnValueChanged += OnVisibleNetCambio;

        if (IsServer)
        {
            // Sincroniza lo que el servidor ya tenia antes de spawnear
            slideNet.Value = currentSlideIndex;
            slideVisibleNet.Value = slideVisible;
        }
        else
        {
            currentSlideIndex = slideNet.Value;
            AplicarVista(slideNet.Value, slideVisibleNet.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        slideNet.OnValueChanged -= OnSlideNetCambio;
        slideVisibleNet.OnValueChanged -= OnVisibleNetCambio;
        base.OnNetworkDespawn();
    }

    private void OnSlideNetCambio(int previo, int nuevo)
    {
        if (IsServer) return; // el servidor ya aplico su vista en SetEstado
        currentSlideIndex = nuevo;
        AplicarVista(nuevo, slideVisibleNet.Value);
    }

    private void OnVisibleNetCambio(bool previo, bool nuevo)
    {
        if (IsServer) return;
        AplicarVista(slideNet.Value, nuevo);
    }

    private void OnEnable()
    {
        // CELULAR: solo refleja lo que diga el servidor
        if (EsVisor)
        {
            AplicarVista(slideNet.Value, slideVisibleNet.Value);
            return;
        }

        // PC / modo sin red: arranca el tutorial
        currentSlideIndex = 0;
        estadoActual = EstadoTutorial.MostrandoSlide;

        if (playerController != null)
        {
            posicionInicialJugador = playerController.transform.position;
        }

        pressedUp = false;
        pressedDown = false;
        pressedLeft = false;
        pressedRight = false;

        if (fishSpawnerManager != null) fishSpawnerManager.SetActive(false);

        MostrarSlideActual();
    }

    private void OnDisable()
    {
        OcultarUIAllSlides();

        if (EsVisor) return;

        if (fishSpawnerManager != null)
        {
            fishSpawnerManager.SetActive(true);

            FishSpawner spawner = fishSpawnerManager.GetComponent<FishSpawner>();
            if (spawner != null)
            {
                spawner.ReiniciarSpawning();
            }
        }

        LimpiarPecesDePrueba();
    }

    private void AplicarVista(int index, bool visible)
    {
        if (!isActiveAndEnabled) return;

        OcultarUIAllSlides();

        if (visible && panelesTutorial != null && index >= 0 && index < panelesTutorial.Length)
        {
            if (panelesTutorial[index] != null)
            {
                panelesTutorial[index].SetActive(true);
            }
        }

        AplicarHUD(!visible);

        if (!EsVisor)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }
    }

    private void AplicarHUD(bool enJuego)
    {
        if (canvasGameHUD == null) return;

        CanvasGroup cg = canvasGameHUD.GetComponent<CanvasGroup>();

        if (enJuego)
        {
            canvasGameHUD.SetActive(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
        else
        {
            if (cg != null) cg.alpha = 0f;
            else canvasGameHUD.SetActive(false);
        }
    }

    // ---------------- ESTADO (solo PC / sin red) ----------------
    private void SetEstado(int index, bool visible)
    {
        currentSlideIndex = index;
        slideVisible = visible;

        if (PuedoEscribirRed)
        {
            slideNet.Value = index;
            slideVisibleNet.Value = visible;
        }

        AplicarVista(index, visible);

        // Escena (bote, jugador, HUD) en PC y celular: se hace en MenuMismaEscena, que usa
        // el mismo camino que EmpezarJuego. visible = slide, !visible = prueba.
        MenuMismaEscena menu = ObtenerMenu();
        if (menu != null) menu.SetModoEscenaTutorial(!visible);
    }

    // ---------------- INPUT (solo PC) ----------------
    private void Update()
    {
        if (EsVisor) return; // el celular no procesa nada

        bool currentWiiA = false;
        bool wiiUp = false, wiiDown = false, wiiLeft = false, wiiRight = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];
            int ret;
            do { ret = wiimote.ReadWiimoteData(); } while (ret > 0);

            currentWiiA = wiimote.Button.a;
            wiiUp = wiimote.Button.d_up;
            wiiDown = wiimote.Button.d_down;
            wiiLeft = wiimote.Button.d_left;
            wiiRight = wiimote.Button.d_right;
        }
#endif

        bool wiiAJustPressed = currentWiiA && !wasWiiA;
        wasWiiA = currentWiiA;

        bool keyboardAdvance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

        if (wiiAJustPressed || keyboardAdvance)
        {
            ProcesarAvance();
            return;
        }

        switch (estadoActual)
        {
            case EstadoTutorial.ProbandoMovimiento:
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || wiiUp) pressedUp = true;
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || wiiDown) pressedDown = true;
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || wiiLeft) pressedLeft = true;
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || wiiRight) pressedRight = true;

                if (pressedUp && pressedDown && pressedLeft && pressedRight)
                {
                    StartCoroutine(CompletadoMovimientoRoutine());
                }
                break;

            case EstadoTutorial.ProbandoPescaCercana:
                VerificarEstadoPescaCercana();
                break;

            case EstadoTutorial.ProbandoPescaLejana:
                VerificarEstadoPescaLejana();
                break;
        }
    }

    private void ProcesarAvance()
    {
        switch (estadoActual)
        {
            case EstadoTutorial.MostrandoSlide:
                if (currentSlideIndex == 0) IniciarPruebaMovimiento();
                else if (currentSlideIndex == 1) IniciarPruebaPescaCercana();
                else if (currentSlideIndex == 2) IniciarPruebaPescaLejana();
                else SiguienteSlide();
                break;

            case EstadoTutorial.ProbandoMovimiento:
                StartCoroutine(CompletadoMovimientoRoutine());
                break;

            case EstadoTutorial.ProbandoPescaCercana:
                StartCoroutine(CompletadoPescaCercanaRoutine());
                break;

            case EstadoTutorial.ProbandoPescaLejana:
                StartCoroutine(CompletadoPescaLejanaRoutine());
                break;
        }
    }

    private void MostrarSlideActual()
    {
        if (panelesTutorial == null || currentSlideIndex >= panelesTutorial.Length)
        {
            FinalizarTutorial();
            return;
        }

        estadoActual = EstadoTutorial.MostrandoSlide;

        SetEstado(currentSlideIndex, true);

        if (ObtenerMenu() == null) PrepararEscenaSlideSinMenu();
    }

    private void SiguienteSlide()
    {
        currentSlideIndex++;

        if (panelesTutorial == null || currentSlideIndex >= panelesTutorial.Length)
        {
            FinalizarTutorial();
        }
        else
        {
            MostrarSlideActual();
        }
    }

    private void FinalizarTutorial()
    {
        LimpiarPecesDePrueba();

        if (fishSpawnerManager != null) fishSpawnerManager.SetActive(true);

        MenuMismaEscena menuManager = ObtenerMenu();
        if (menuManager != null)
        {
            menuManager.CerrarPanelTutorial();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void PrepararEscenaSlideSinMenu()
    {
        try
        {
            if (boteObjeto != null)
            {
                var toggle = boteObjeto.GetComponent<BoatDriverToggle>();
                if (toggle != null) toggle.DesactivarConduccionMenu();
            }

            if (playerController != null)
            {
                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = new Vector3(0f, 1f, 0f);
                Physics.SyncTransforms();
                playerController.enabled = false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TUTORIAL] Error preparando escena del slide: " + e.Message);
        }
    }

    private void PrepararEscenaPruebaSinMenu()
    {
        if (boteObjeto != null)
        {
            boteObjeto.SetActive(true);
            var toggle = boteObjeto.GetComponent<BoatDriverToggle>();
            if (toggle != null) toggle.ActivarModoConduccionPermanente();
        }

        if (playerController != null)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            playerController.enabled = true;
        }
    }

    private void IniciarPruebaMovimiento()
    {
        estadoActual = EstadoTutorial.ProbandoMovimiento;

        SetEstado(currentSlideIndex, false);

        if (ObtenerMenu() == null) PrepararEscenaPruebaSinMenu();
    }

    private IEnumerator CompletadoMovimientoRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        Debug.Log("[TUTORIAL] Movimiento completado. Cargando Slide 2...");

        yield return new WaitForSeconds(2.0f);

        currentSlideIndex = 1;
        MostrarSlideActual();
    }

    private void IniciarPruebaPescaCercana()
    {
        estadoActual = EstadoTutorial.ProbandoPescaCercana;

        SetEstado(currentSlideIndex, false);

        if (ObtenerMenu() == null) PrepararEscenaPruebaSinMenu();

        if (FishingHUDManager.Instance != null)
        {
            FishingHUDManager.Instance.EnsureCamera();
        }

        conteoPescaInicial = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;

        if (puntosDeSpawnCercanos != null && puntosDeSpawnCercanos.Length > 0)
        {
            pecesPorPunto = new GameObject[puntosDeSpawnCercanos.Length];

            for (int i = 0; i < puntosDeSpawnCercanos.Length; i++)
            {
                if (puntosDeSpawnCercanos[i] != null)
                {
                    pecesPorPunto[i] = SpawnearPez(puntosDeSpawnCercanos[i].position);
                }
            }
        }
    }

    private void VerificarEstadoPescaCercana()
    {
        int totalActual = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;
        int atrapadosEnTutorial = totalActual - conteoPescaInicial;

        if (atrapadosEnTutorial >= 2)
        {
            if (pecesPorPunto != null)
            {
                for (int i = 0; i < pecesPorPunto.Length; i++)
                {
                    if (pecesPorPunto[i] != null)
                    {
                        DestruirPez(pecesPorPunto[i]);
                        pecesPorPunto[i] = null;
                    }
                }
            }

            StartCoroutine(CompletadoPescaCercanaRoutine());
            return;
        }

        if (pecesPorPunto != null)
        {
            int pecesActivosCount = 0;
            for (int i = 0; i < pecesPorPunto.Length; i++)
            {
                if (pecesPorPunto[i] != null) pecesActivosCount++;
            }

            for (int i = 0; i < pecesPorPunto.Length; i++)
            {
                if (pecesPorPunto[i] == null)
                {
                    if (atrapadosEnTutorial + pecesActivosCount < 2)
                    {
                        if (puntosDeSpawnCercanos[i] != null)
                        {
                            pecesPorPunto[i] = SpawnearPez(puntosDeSpawnCercanos[i].position);
                            pecesActivosCount++;
                        }
                    }
                }
            }
        }
    }

    private IEnumerator CompletadoPescaCercanaRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        Debug.Log("[TUTORIAL] Pesca cercana completada. Cargando Slide 3...");

        yield return new WaitForSeconds(1.5f);

        currentSlideIndex = 2;
        MostrarSlideActual();
    }

    // ---------------- PRUEBA 3: PESCA LEJANA ----------------
    private void IniciarPruebaPescaLejana()
    {
        estadoActual = EstadoTutorial.ProbandoPescaLejana;

        SetEstado(currentSlideIndex, false);

        if (ObtenerMenu() == null) PrepararEscenaPruebaSinMenu();

        if (FishingHUDManager.Instance != null)
        {
            FishingHUDManager.Instance.EnsureCamera();
        }

        conteoPescaLejanaInicial = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;

        pezLejano = SpawnearPez(PosicionPezLejano());
    }

    private void VerificarEstadoPescaLejana()
    {
        int totalActual = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;
        int atrapadosLejanos = totalActual - conteoPescaLejanaInicial;

        if (atrapadosLejanos >= 1)
        {
            if (pezLejano != null)
            {
                DestruirPez(pezLejano);
                pezLejano = null;
            }

            StartCoroutine(CompletadoPescaLejanaRoutine());
            return;
        }

        if (pezLejano == null)
        {
            pezLejano = SpawnearPez(PosicionPezLejano());
        }
    }

    private Vector3 PosicionPezLejano()
    {
        if (puntoSpawnLejano != null) return puntoSpawnLejano.position;
        if (boteObjeto != null) return boteObjeto.transform.position + boteObjeto.transform.forward * 45f;
        return Vector3.zero;
    }

    private IEnumerator CompletadoPescaLejanaRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        Debug.Log("[TUTORIAL] Pesca lejana completada. Cargando Slide 4 (Final)...");

        yield return new WaitForSeconds(1.5f);

        currentSlideIndex = 3;
        MostrarSlideActual();
    }

    // ---------------- PECES ----------------
    private GameObject SpawnearPez(Vector3 posicion)
    {
        if (fishPrefab == null)
        {
            Debug.LogWarning("[TUTORIAL] No se ha asignado el 'fishPrefab' en el Inspector.");
            return null;
        }

        GameObject pez = Instantiate(fishPrefab, posicion, Quaternion.identity);

        if (RedActiva && NetworkManager.Singleton.IsServer)
        {
            NetworkObject netObj = pez.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        return pez;
    }

    private void DestruirPez(GameObject pez)
    {
        if (pez == null) return;

        NetworkObject netObj = pez.GetComponent<NetworkObject>();
        if (RedActiva && NetworkManager.Singleton.IsServer && netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true); // despawnea en el celular tambien y destruye
        }
        else
        {
            Destroy(pez);
        }
    }

    private void LimpiarPecesDePrueba()
    {
        if (pecesPorPunto != null)
        {
            for (int i = 0; i < pecesPorPunto.Length; i++)
            {
                if (pecesPorPunto[i] != null)
                {
                    DestruirPez(pecesPorPunto[i]);
                    pecesPorPunto[i] = null;
                }
            }
        }

        if (pezLejano != null)
        {
            DestruirPez(pezLejano);
            pezLejano = null;
        }
    }

    private void OcultarUIAllSlides()
    {
        if (panelesTutorial != null)
        {
            for (int i = 0; i < panelesTutorial.Length; i++)
            {
                if (panelesTutorial[i] != null)
                    panelesTutorial[i].SetActive(false);
            }
        }
    }

    private MenuMismaEscena ObtenerMenu()
    {
        if (menuRef == null) menuRef = FindObjectOfType<MenuMismaEscena>();
        return menuRef;
    }
}
