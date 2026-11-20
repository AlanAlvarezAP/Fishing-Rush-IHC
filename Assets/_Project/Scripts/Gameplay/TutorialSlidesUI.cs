using System.Collections;
using UnityEngine;
using TMPro;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

/// <summary>
/// Tutorial del PESCADOR (independiente del conductor). Se hace sobre una plataforma en su mitad
/// del lago (MenuMismaEscena.zonaTutorialPescador), no en el bote.
///
/// Secuencia (cada slide es Panel_Tutorial_N; al avanzar empieza su prueba):
///   1. Movimiento  : moverse en las 4 direcciones.
///   2. Lanzamientos: equipar la cana y lanzar 3 veces (no hay peces).
///   3. Peces       : 3 peces a distancias distintas; hay que capturar los 3 (si uno escapa, reaparece).
///   4. Tiburones   : 2 tiburones; hay que zafarse de su mordida 2 veces.
///   Fin: vuelve al menu con el tutorial completado.
///
/// - PC (servidor/host): ejecuta TODA la logica (input, pruebas, peces) y decide el estado.
/// - Celular VR (cliente): solo refleja NetworkVariables (slide visible, prueba y progreso) y la
///   escena (plataforma, jugador, HUD) llega por MenuMismaEscena.
/// </summary>
public class TutorialSlidesUI : NetworkBehaviour
{
    [Header("Paneles de las Slides del Tutorial (Panel_Tutorial_1..4)")]
    [SerializeField] private GameObject[] panelesTutorial;

    [Header("Objetos de Escena")]
    [SerializeField] private GameObject fishSpawnerManager;
    [SerializeField] private GameObject boteObjeto;
    [SerializeField] private ThirdPController playerController;
    [SerializeField] private GameObject canvasGameHUD;

    [Header("Peces y tiburones de las pruebas")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private GameObject sharkPrefab;
    [Tooltip("3 puntos a distancias distintas (cerca, medio, lejos). Los crea Fishing Rush > Crear tutorial del pescador")]
    [SerializeField] private Transform[] puntosPeces;
    [Tooltip("Puntos donde aparecen los tiburones")]
    [SerializeField] private Transform[] puntosTiburones;

    [Header("Metas de las pruebas")]
    [SerializeField] private int lanzamientosRequeridos = 3;
    [SerializeField] private int escapesTiburonRequeridos = 2;
    [Tooltip("Segundos antes de que reaparezca un pez que escapo (o un tiburon que se fue)")]
    [SerializeField] private float segundosReaparicion = 1.5f;
    [Tooltip("Segundos que se muestra 'Completado!' antes del siguiente slide")]
    [SerializeField] private float segundosEntrePruebas = 1.5f;
    [Tooltip("Segundos que se muestra el slide final (tutorial completado) antes de volver al menu")]
    [SerializeField] private float segundosSlideFinal = 3f;

    [Header("Progreso en el HUD (durante las pruebas)")]
    [SerializeField] private GameObject tileProgreso;
    [SerializeField] private TMP_Text textoProgreso;

    // ---------------- ESTADO LOCAL (servidor) ----------------
    private int currentSlideIndex = 0;
    private bool slideVisible = true;
    private bool wasWiiA = false;

    private enum EstadoTutorial { MostrandoSlide, Probando, EsperandoSiguienteSlide }
    private EstadoTutorial estadoActual = EstadoTutorial.MostrandoSlide;

    public enum Prueba : byte { Ninguna = 0, Movimiento = 1, Lanzamientos = 2, Peces = 3, Tiburones = 4 }
    private Prueba pruebaActual = Prueba.Ninguna;
    private int progresoActual = 0;

    // Movimiento
    private bool movArriba, movAbajo, movIzquierda, movDerecha;

    // Lanzamientos
    private int lanzamientosBase = 0;

    // Peces
    private GameObject[] pecesPorPunto;
    private bool[] pezCapturado;
    private float[] vacioDesde;          // -1 = el pez sigue vivo
    private int conteoPecesConocido = 0; // FishingCounterManager ya contabilizado

    // Tiburones
    private GameObject[] tiburonesPorPunto;
    private float[] tiburonVacioDesde;
    private bool[] tiburonUsado;        // ya se zafo de este: no vuelve a aparecer
    private int tiburonMordiendo = -1;  // punto del tiburon que mordio el anzuelo
    private int escapesTiburon = 0;
    private bool estabaBloqueado = false;

    private MenuMismaEscena menuRef;

    // ---------------- ESTADO DE RED (solo escribe el servidor) ----------------
    private NetworkVariable<int> slideNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> slideVisibleNet = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Prueba y progreso para el texto del HUD (enum byte + int, nunca FixedString)
    private NetworkVariable<Prueba> pruebaNet = new NetworkVariable<Prueba>(
        Prueba.Ninguna, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> progresoNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ---------------- ROLES ----------------
    private bool RedActiva => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool EsVisor => RedActiva && !NetworkManager.Singleton.IsServer;      // celular
    private bool PuedoEscribirRed => RedActiva && NetworkManager.Singleton.IsServer && IsSpawned;

    // Estado que llega por red al celular. Se lee de MenuMismaEscena (siempre spawneado): el
    // NetworkObject de Panel_Tutorial empieza apagado y Netcode no lo spawnea.
    private int SlideRed { get { var m = ObtenerMenu(); return m != null && m.IsSpawned ? m.TutorialSlideRed : slideNet.Value; } }
    private bool VisibleRed { get { var m = ObtenerMenu(); return m != null && m.IsSpawned ? m.TutorialSlideVisibleRed : slideVisibleNet.Value; } }
    private Prueba PruebaRed { get { var m = ObtenerMenu(); return m != null && m.IsSpawned ? (Prueba)m.TutorialPruebaRed : pruebaNet.Value; } }
    private int ProgresoRed { get { var m = ObtenerMenu(); return m != null && m.IsSpawned ? m.TutorialProgresoRed : progresoNet.Value; } }

    /// <summary>Celular: aplica el slide que manda la PC (lo llama MenuMismaEscena).</summary>
    public void AplicarEstadoRed()
    {
        if (!EsVisor) return;
        currentSlideIndex = SlideRed;
        AplicarVista(SlideRed, VisibleRed);
    }

    /// <summary>Celular: actualiza el texto de progreso que manda la PC.</summary>
    public void AplicarProgresoRed()
    {
        if (!EsVisor) return;
        ActualizarTextoProgreso(PruebaRed, ProgresoRed);
    }

    // ---------------- CICLO DE VIDA ----------------
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        slideNet.OnValueChanged += OnSlideNetCambio;
        slideVisibleNet.OnValueChanged += OnVisibleNetCambio;
        pruebaNet.OnValueChanged += OnPruebaNetCambio;
        progresoNet.OnValueChanged += OnProgresoNetCambio;

        if (IsServer)
        {
            // Sincroniza lo que el servidor ya tenia antes de spawnear
            slideNet.Value = currentSlideIndex;
            slideVisibleNet.Value = slideVisible;
            pruebaNet.Value = pruebaActual;
            progresoNet.Value = progresoActual;
        }
        else
        {
            AplicarEstadoRed();
        }
    }

    public override void OnNetworkDespawn()
    {
        slideNet.OnValueChanged -= OnSlideNetCambio;
        slideVisibleNet.OnValueChanged -= OnVisibleNetCambio;
        pruebaNet.OnValueChanged -= OnPruebaNetCambio;
        progresoNet.OnValueChanged -= OnProgresoNetCambio;
        base.OnNetworkDespawn();
    }

    private void OnSlideNetCambio(int previo, int nuevo)
    {
        if (IsServer) return; // el servidor ya aplico su vista en SetEstado
        AplicarEstadoRed();
    }

    private void OnVisibleNetCambio(bool previo, bool nuevo)
    {
        if (IsServer) return;
        AplicarEstadoRed();
    }

    private void OnPruebaNetCambio(Prueba previo, Prueba nuevo)
    {
        if (IsServer) return;
        AplicarProgresoRed();
    }

    private void OnProgresoNetCambio(int previo, int nuevo)
    {
        if (IsServer) return;
        AplicarProgresoRed();
    }

    private void OnEnable()
    {
        // CELULAR: solo refleja lo que diga el servidor
        if (EsVisor)
        {
            AplicarEstadoRed();
            return;
        }

        // PC / modo sin red: arranca el tutorial
        currentSlideIndex = 0;
        estadoActual = EstadoTutorial.MostrandoSlide;
        SetPrueba(Prueba.Ninguna, 0);

        if (playerController == null) playerController = FindObjectOfType<ThirdPController>();
        if (fishSpawnerManager != null) fishSpawnerManager.SetActive(false);

        MostrarSlideActual();
    }

    private void OnDisable()
    {
        OcultarUIAllSlides();
        if (tileProgreso != null) tileProgreso.SetActive(false);

        if (EsVisor) return;

        StopAllCoroutines();
        LimpiarPecesDePrueba();

        if (fishSpawnerManager != null)
        {
            fishSpawnerManager.SetActive(true);

            // ReiniciarSpawning borra TODOS los peces: si el conductor sigue en su tutorial
            // se llevaria sus peces y tiburones (la partida igual lo reinicia al empezar)
            MenuMismaEscena menuSpawn = ObtenerMenu();
            FishSpawner spawner = fishSpawnerManager.GetComponent<FishSpawner>();
            if (spawner != null && (menuSpawn == null || !menuSpawn.EnTutorialConductor)) spawner.ReiniciarSpawning();
        }
    }

    // ---------------- VISTA (PC y celular) ----------------
    private void AplicarVista(int index, bool visible)
    {
        if (!isActiveAndEnabled) return;

        OcultarUIAllSlides();

        if (visible && panelesTutorial != null && index >= 0 && index < panelesTutorial.Length)
        {
            if (panelesTutorial[index] != null) panelesTutorial[index].SetActive(true);
        }

        AplicarHUD(!visible);

        if (EsVisor) ActualizarTextoProgreso(PruebaRed, ProgresoRed);
        else ActualizarTextoProgreso(pruebaActual, progresoActual);

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

    private int MetaDe(Prueba prueba)
    {
        switch (prueba)
        {
            case Prueba.Movimiento: return 4;
            case Prueba.Lanzamientos: return Mathf.Max(1, lanzamientosRequeridos);
            case Prueba.Peces: return puntosPeces != null && puntosPeces.Length > 0 ? puntosPeces.Length : 3;
            case Prueba.Tiburones: return Mathf.Max(1, escapesTiburonRequeridos);
            default: return 0;
        }
    }

    /// <summary>Texto del HUD durante las pruebas (se arma en cada dispositivo).</summary>
    private void ActualizarTextoProgreso(Prueba prueba, int progreso)
    {
        bool mostrar = prueba != Prueba.Ninguna && isActiveAndEnabled;
        if (tileProgreso != null && tileProgreso.activeSelf != mostrar) tileProgreso.SetActive(mostrar);
        if (!mostrar || textoProgreso == null) return;

        int meta = MetaDe(prueba);
        if (progreso == PROGRESO_RECOGER_ANZUELO)
        {
            textoProgreso.text = "Recoge el anzuelo";
            return;
        }

        if (progreso >= meta)
        {
            textoProgreso.text = "Completado!";
            return;
        }

        switch (prueba)
        {
            case Prueba.Movimiento: textoProgreso.text = $"Direcciones {progreso}/{meta}"; break;
            case Prueba.Lanzamientos: textoProgreso.text = $"Lanzamientos {progreso}/{meta}"; break;
            case Prueba.Peces: textoProgreso.text = $"Peces {progreso}/{meta}"; break;
            case Prueba.Tiburones: textoProgreso.text = $"Escapes {progreso}/{meta}"; break;
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

        // Lo que de verdad llega al celular (ver SlideRed)
        MenuMismaEscena menuRed = ObtenerMenu();
        if (menuRed != null) menuRed.PublicarSlideTutorial(index, visible);

        AplicarVista(index, visible);

        // Escena (plataforma, jugador, HUD) en PC y celular: se hace en MenuMismaEscena.
        // visible = slide (jugador quieto), !visible = prueba (jugador habilitado).
        MenuMismaEscena menu = ObtenerMenu();
        if (menu != null) menu.SetModoEscenaTutorial(!visible);
    }

    private void SetPrueba(Prueba prueba, int progreso)
    {
        pruebaActual = prueba;
        progresoActual = progreso;

        if (PuedoEscribirRed)
        {
            pruebaNet.Value = prueba;
            progresoNet.Value = progreso;
        }

        MenuMismaEscena menuRed = ObtenerMenu();
        if (menuRed != null) menuRed.PublicarPruebaTutorial((byte)prueba, progreso);

        ActualizarTextoProgreso(prueba, progreso);
    }

    private void SetProgreso(int progreso)
    {
        if (progreso == progresoActual) return;
        SetPrueba(pruebaActual, progreso);
    }

    // ---------------- INPUT Y PRUEBAS (solo PC) ----------------
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

        // En un SLIDE: A del Wiimote (o Espacio / Enter) empieza su prueba
        if (estadoActual == EstadoTutorial.MostrandoSlide)
        {
            bool teclado = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            if (wiiAJustPressed || teclado) IniciarPruebaDelSlide();
            return;
        }

        if (estadoActual != EstadoTutorial.Probando) return;

        // Pruebas en PC: N salta la prueba actual (en una prueba la A del Wiimote es de la cana)
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("[TUTORIAL] Tecla N: prueba saltada.");
            CompletarPrueba();
            return;
        }

        switch (pruebaActual)
        {
            case Prueba.Movimiento:
                ActualizarPruebaMovimiento(wiiUp, wiiDown, wiiLeft, wiiRight);
                break;
            case Prueba.Lanzamientos:
                ActualizarPruebaLanzamientos();
                break;
            case Prueba.Peces:
                ActualizarPruebaPeces();
                break;
            case Prueba.Tiburones:
                ActualizarPruebaTiburones();
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

        SetPrueba(Prueba.Ninguna, 0);

        // Slides 1-4 tienen prueba. Los siguientes (el 5: "tutorial completado") no:
        // se muestran unos segundos y se vuelve solo al menu.
        if (currentSlideIndex >= CANTIDAD_PRUEBAS)
        {
            estadoActual = EstadoTutorial.EsperandoSiguienteSlide; // ignora A / Espacio / Enter
            SetEstado(currentSlideIndex, true);
            StartCoroutine(SlideFinalRoutine());
            return;
        }

        estadoActual = EstadoTutorial.MostrandoSlide;
        SetEstado(currentSlideIndex, true);
    }

    private const int CANTIDAD_PRUEBAS = 4;

    private IEnumerator SlideFinalRoutine()
    {
        yield return new WaitForSeconds(segundosSlideFinal);
        SiguienteSlide(); // si era el ultimo, FinalizarTutorial vuelve al menu
    }

    private void IniciarPruebaDelSlide()
    {
        Prueba prueba;
        switch (currentSlideIndex)
        {
            case 0: prueba = Prueba.Movimiento; break;
            case 1: prueba = Prueba.Lanzamientos; break;
            case 2: prueba = Prueba.Peces; break;
            case 3: prueba = Prueba.Tiburones; break;
            default:
                return; // slides sin prueba: avanzan solos (SlideFinalRoutine)
        }

        estadoActual = EstadoTutorial.Probando;
        SetEstado(currentSlideIndex, false);
        SetPrueba(prueba, 0);

        if (FishingHUDManager.Instance != null) FishingHUDManager.Instance.EnsureCamera();
        if (playerController == null) playerController = FindObjectOfType<ThirdPController>();

        switch (prueba)
        {
            case Prueba.Movimiento:
                movArriba = movAbajo = movIzquierda = movDerecha = false;
                break;

            case Prueba.Lanzamientos:
                lanzamientosBase = playerController != null ? playerController.LanzamientosHechos : 0;
                break;

            case Prueba.Peces:
                IniciarPruebaPeces();
                break;

            case Prueba.Tiburones:
                IniciarPruebaTiburones();
                break;
        }

        Debug.Log($"[TUTORIAL] Empieza la prueba {prueba} (slide {currentSlideIndex + 1}).");
    }

    private void CompletarPrueba()
    {
        if (estadoActual != EstadoTutorial.Probando) return;
        StartCoroutine(PruebaCompletadaRoutine());
    }

    private IEnumerator PruebaCompletadaRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        SetProgreso(MetaDe(pruebaActual)); // muestra "Completado!"
        Debug.Log($"[TUTORIAL] Prueba {pruebaActual} completada.");

        yield return new WaitForSeconds(segundosEntrePruebas);

        LimpiarPecesDePrueba();
        SiguienteSlide();
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
        Debug.Log("[TUTORIAL] Tutorial del pescador terminado.");
        LimpiarPecesDePrueba();
        SetPrueba(Prueba.Ninguna, 0);

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

    // ---------------- PRUEBA 1: MOVIMIENTO ----------------
    private void ActualizarPruebaMovimiento(bool wiiUp, bool wiiDown, bool wiiLeft, bool wiiRight)
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || wiiUp) movArriba = true;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || wiiDown) movAbajo = true;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || wiiLeft) movIzquierda = true;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || wiiRight) movDerecha = true;

        int hechas = (movArriba ? 1 : 0) + (movAbajo ? 1 : 0) + (movIzquierda ? 1 : 0) + (movDerecha ? 1 : 0);
        SetProgreso(hechas);

        if (hechas >= 4) CompletarPrueba();
    }

    // ---------------- PRUEBA 2: LANZAMIENTOS ----------------
    private void ActualizarPruebaLanzamientos()
    {
        if (playerController == null) return;

        int hechos = Mathf.Max(0, playerController.LanzamientosHechos - lanzamientosBase);
        int meta = MetaDe(Prueba.Lanzamientos);

        if (hechos < meta)
        {
            SetProgreso(hechos);
            return;
        }

        // Ultimo lanzamiento hecho: espera a que recoja el anzuelo antes de pasar al slide
        bool anzueloAfuera = playerController.alreadyCast || playerController.isReeling || playerController.isCastingInProcess;
        if (anzueloAfuera)
        {
            SetProgreso(PROGRESO_RECOGER_ANZUELO);
            return;
        }

        CompletarPrueba();
    }

    // Valor especial de progreso: "Recoge el anzuelo" (se manda por red igual que el progreso)
    private const int PROGRESO_RECOGER_ANZUELO = -1;

    // ---------------- PRUEBA 3: CAPTURAR 3 PECES ----------------
    private int ConteoPeces => FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;

    private void IniciarPruebaPeces()
    {
        int n = puntosPeces != null ? puntosPeces.Length : 0;
        pecesPorPunto = new GameObject[n];
        pezCapturado = new bool[n];
        vacioDesde = new float[n];
        conteoPecesConocido = ConteoPeces;

        for (int i = 0; i < n; i++)
        {
            vacioDesde[i] = -1f;
            if (puntosPeces[i] != null) pecesPorPunto[i] = SpawnearPez(fishPrefab, puntosPeces[i].position, false);
        }

        if (n == 0) Debug.LogWarning("[TUTORIAL] No hay puntosPeces asignados: usa Fishing Rush > Crear tutorial del pescador.");
    }

    private void ActualizarPruebaPeces()
    {
        if (pecesPorPunto == null || pecesPorPunto.Length == 0) return;

        int capturados = 0;

        for (int i = 0; i < pecesPorPunto.Length; i++)
        {
            if (pezCapturado[i]) { capturados++; continue; }
            if (pecesPorPunto[i] != null) continue;

            // El pez de este punto desaparecio: se espera un momento a que el contador de
            // capturas se actualice y se decide si lo capturo o si escapo.
            if (vacioDesde[i] < 0f) { vacioDesde[i] = Time.time; continue; }
            if (Time.time - vacioDesde[i] < 0.5f) continue;

            if (ConteoPeces > conteoPecesConocido)
            {
                conteoPecesConocido++;
                pezCapturado[i] = true;
                capturados++;
                Debug.Log($"[TUTORIAL] Pez {i + 1} capturado.");
            }
            else if (Time.time - vacioDesde[i] >= segundosReaparicion)
            {
                // Escapo (o se le acabo el tiempo de vida): reaparece en su mismo punto
                vacioDesde[i] = -1f;
                if (puntosPeces[i] != null) pecesPorPunto[i] = SpawnearPez(fishPrefab, puntosPeces[i].position, false);
                Debug.Log($"[TUTORIAL] Pez {i + 1} escapo: reaparece.");
            }
        }

        SetProgreso(capturados);
        if (capturados >= pecesPorPunto.Length) CompletarPrueba();
    }

    // ---------------- PRUEBA 4: ZAFARSE DE 2 TIBURONES ----------------
    private void IniciarPruebaTiburones()
    {
        int n = puntosTiburones != null ? puntosTiburones.Length : 0;
        tiburonesPorPunto = new GameObject[n];
        tiburonVacioDesde = new float[n];
        tiburonUsado = new bool[n];
        tiburonMordiendo = -1;
        escapesTiburon = 0;
        estabaBloqueado = playerController != null && playerController.BloqueadoPorTiburon;

        for (int i = 0; i < n; i++)
        {
            tiburonVacioDesde[i] = -1f;
            if (puntosTiburones[i] != null) tiburonesPorPunto[i] = SpawnearPez(sharkPrefab, puntosTiburones[i].position, true);
        }

        if (n == 0) Debug.LogWarning("[TUTORIAL] No hay puntosTiburones asignados: usa Fishing Rush > Crear tutorial del pescador.");
    }

    private void ActualizarPruebaTiburones()
    {
        // Un escape = la cana estaba bloqueada por una mordida y se libero
        bool bloqueado = playerController != null && playerController.BloqueadoPorTiburon;

        // Empieza una mordida: se anota cual tiburon fue (el mas cercano al anzuelo)
        if (!estabaBloqueado && bloqueado) tiburonMordiendo = TiburonMasCercanoAlAnzuelo();

        if (estabaBloqueado && !bloqueado)
        {
            escapesTiburon++;
            Debug.Log($"[TUTORIAL] Escape del tiburon {escapesTiburon}.");

            // Ese tiburon se va y ya no vuelve (esperando o con patrones, da igual)
            if (tiburonMordiendo >= 0 && tiburonMordiendo < tiburonesPorPunto.Length)
            {
                tiburonUsado[tiburonMordiendo] = true;
                if (tiburonesPorPunto[tiburonMordiendo] != null)
                {
                    DestruirPez(tiburonesPorPunto[tiburonMordiendo]);
                    tiburonesPorPunto[tiburonMordiendo] = null;
                }
            }
            tiburonMordiendo = -1;
        }
        estabaBloqueado = bloqueado;

        int meta = MetaDe(Prueba.Tiburones);
        SetProgreso(Mathf.Min(escapesTiburon, meta));

        if (escapesTiburon >= meta)
        {
            CompletarPrueba();
            return;
        }

        // Despues de morder, el tiburon se va (o se le acaba el tiempo): vuelve a aparecer
        if (tiburonesPorPunto == null) return;
        for (int i = 0; i < tiburonesPorPunto.Length; i++)
        {
            if (tiburonesPorPunto[i] != null) continue;
            if (tiburonUsado != null && tiburonUsado[i]) continue; // ya se zafo de este

            if (tiburonVacioDesde[i] < 0f) { tiburonVacioDesde[i] = Time.time; continue; }
            if (Time.time - tiburonVacioDesde[i] < segundosReaparicion) continue;

            tiburonVacioDesde[i] = -1f;
            if (puntosTiburones[i] != null) tiburonesPorPunto[i] = SpawnearPez(sharkPrefab, puntosTiburones[i].position, true);
        }
    }

    private int TiburonMasCercanoAlAnzuelo()
    {
        if (tiburonesPorPunto == null) return -1;

        Transform anzuelo = playerController != null ? playerController.Anzuelo : null;
        Vector3 referencia = anzuelo != null ? anzuelo.position
                           : (playerController != null ? playerController.transform.position : Vector3.zero);

        int mejor = -1;
        float mejorDist = float.MaxValue;
        for (int i = 0; i < tiburonesPorPunto.Length; i++)
        {
            if (tiburonesPorPunto[i] == null) continue;
            float d = (tiburonesPorPunto[i].transform.position - referencia).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = i; }
        }
        return mejor;
    }

    // ---------------- PECES ----------------
    private GameObject SpawnearPez(GameObject prefab, Vector3 posicion, bool tiburon)
    {
        if (prefab == null)
        {
            Debug.LogWarning(tiburon
                ? "[TUTORIAL] No se ha asignado el 'sharkPrefab' en el Inspector."
                : "[TUTORIAL] No se ha asignado el 'fishPrefab' en el Inspector.");
            return null;
        }

        GameObject pez = Instantiate(prefab, posicion, Quaternion.identity);

        // Los tiburones del tutorial no van tras el bote: el conductor puede estar en su propio tutorial
        FishCube fc = pez.GetComponent<FishCube>();
        if (fc != null) fc.PermitirEmbestida = false;

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

    private void DestruirTodos(GameObject[] lista)
    {
        if (lista == null) return;
        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] != null)
            {
                DestruirPez(lista[i]);
                lista[i] = null;
            }
        }
    }

    private void LimpiarPecesDePrueba()
    {
        DestruirTodos(pecesPorPunto);
        DestruirTodos(tiburonesPorPunto);

        // Si se corta en plena mordida, la cana no debe quedar bloqueada
        if (playerController != null && playerController.BloqueadoPorTiburon) playerController.CancelarBloqueoTiburon();
    }

    private void OcultarUIAllSlides()
    {
        if (panelesTutorial == null) return;
        for (int i = 0; i < panelesTutorial.Length; i++)
        {
            if (panelesTutorial[i] != null) panelesTutorial[i].SetActive(false);
        }
    }

    private MenuMismaEscena ObtenerMenu()
    {
        if (menuRef == null) menuRef = FindObjectOfType<MenuMismaEscena>();
        return menuRef;
    }
}
