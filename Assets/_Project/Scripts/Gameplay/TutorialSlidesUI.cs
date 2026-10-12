using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class TutorialSlidesUI : MonoBehaviour
{
    [Header("UI del Visor")]
    [SerializeField] private GameObject panelImagenContenedor;   // El objeto contenedor de la imagen/panel
    [SerializeField] private Image displayImage;                 // Componente Image donde se muestra la slide
    [SerializeField] private TextMeshProUGUI slideCounterText;   // Texto "1 / 3"

    [Header("Las Slides del Tutorial")]
    [SerializeField] private Sprite[] tutorialSlides;            // Diapositivas en orden

    [Header("Objetos de Escena")]
    [SerializeField] private GameObject fishSpawnerManager;     // Objeto 'FishSpawnerManager' para apagarlo en el tuto
    [SerializeField] private GameObject boteObjeto;             // Objeto del Bote
    [SerializeField] private ThirdPController playerController;  // Controller del Jugador

    [Header("Configuracion Pesca Cercana")]
    [SerializeField] private GameObject fishPrefab;            // Prefab 'FishNetworkAnchor'
    [SerializeField] private Transform[] puntosDeSpawnCercanos;// 2 objetos en el agua cerca del bote
    [SerializeField] private Transform puntoSpawnLejano;

    private Vector3 posicionInicialJugador;

    private GameObject[] pecesPorPunto;
    private GameObject pezLejano;

    private int conteoPescaInicial = 0;
    private int conteoPescaLejanaInicial = 0;

    private int currentSlideIndex = 0;
    private bool wasWiiA = false;

    private enum EstadoTutorial { MostrandoSlide, ProbandoMovimiento, ProbandoPescaCercana, ProbandoPescaLejana, EsperandoSiguienteSlide }
    private EstadoTutorial estadoActual = EstadoTutorial.MostrandoSlide;

    private bool pressedUp = false;
    private bool pressedDown = false;
    private bool pressedLeft = false;
    private bool pressedRight = false;

    private void OnEnable()
    {
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

    private void Update()
    {
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

        switch (estadoActual)
        {
            case EstadoTutorial.MostrandoSlide:
                if (wiiAJustPressed || keyboardAdvance)
                {
                    if (currentSlideIndex == 0)
                    {
                        IniciarPruebaMovimiento();
                    }
                    else if (currentSlideIndex == 1)
                    {
                        IniciarPruebaPescaCercana();
                    }
                    else if (currentSlideIndex == 2)
                    {
                        IniciarPruebaPescaLejana();
                    }
                    else
                    {
                        SiguienteSlide();
                    }
                }
                break;

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

            case EstadoTutorial.EsperandoSiguienteSlide:
                break;
        }
    }

    private void IniciarPruebaMovimiento()
    {
        estadoActual = EstadoTutorial.ProbandoMovimiento;

        if (panelImagenContenedor != null) panelImagenContenedor.SetActive(false);
        if (displayImage != null) displayImage.gameObject.SetActive(false);

        if (boteObjeto != null) boteObjeto.SetActive(true);
        if (playerController != null) playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[TUTORIAL] Prueba de movimiento iniciada. Presiona W, A, S, D / Cruceta.");
    }

    private IEnumerator CompletadoMovimientoRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        Debug.Log("[TUTORIAL] Movimiento completado. Cargando Slide 2...");

        yield return new WaitForSeconds(2.0f);

        currentSlideIndex = 1;
        MostrarSlideActual();
    }

    private void MostrarSlideActual()
    {
        estadoActual = EstadoTutorial.MostrandoSlide;

        if (panelImagenContenedor != null) panelImagenContenedor.SetActive(true);
        if (displayImage != null) displayImage.gameObject.SetActive(true);

        if (tutorialSlides != null && currentSlideIndex < tutorialSlides.Length)
        {
            if (displayImage != null) displayImage.sprite = tutorialSlides[currentSlideIndex];
            if (slideCounterText != null) slideCounterText.text = $"{currentSlideIndex + 1} / {tutorialSlides.Length}";
        }

        if (currentSlideIndex == 1 || currentSlideIndex == 2)
        {
            if (boteObjeto != null) boteObjeto.SetActive(false);

            if (playerController != null)
            {
                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerController.enabled = false;
                playerController.transform.position = posicionInicialJugador;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SiguienteSlide()
    {
        currentSlideIndex++;

        if (tutorialSlides == null || currentSlideIndex >= tutorialSlides.Length)
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
        if (fishSpawnerManager != null) fishSpawnerManager.SetActive(true);

        MenuMismaEscena menuManager = FindObjectOfType<MenuMismaEscena>();
        if (menuManager != null)
        {
            menuManager.CerrarPanelTutorial();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void IniciarPruebaPescaCercana()
    {
        estadoActual = EstadoTutorial.ProbandoPescaCercana;

        if (panelImagenContenedor != null) panelImagenContenedor.SetActive(false);
        if (displayImage != null) displayImage.gameObject.SetActive(false);

        if (boteObjeto != null) boteObjeto.SetActive(true);

        if (playerController != null)
        {
            playerController.enabled = true;
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
                        Destroy(pecesPorPunto[i]);
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

        currentSlideIndex = 2; // Mostrar Slide 3 (Pesca lejana)
        MostrarSlideActual();
    }

    // --- FASE 2: PESCA LEJANA (OBLIGA A USAR BOTE) ---
    private void IniciarPruebaPescaLejana()
    {
        estadoActual = EstadoTutorial.ProbandoPescaLejana;

        if (panelImagenContenedor != null) panelImagenContenedor.SetActive(false);
        if (displayImage != null) displayImage.gameObject.SetActive(false);

        if (boteObjeto != null) boteObjeto.SetActive(true);

        if (playerController != null)
        {
            playerController.enabled = true;
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        conteoPescaLejanaInicial = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;

        // Spawnear el pez lejano
        Vector3 spawnPos = (puntoSpawnLejano != null) 
            ? puntoSpawnLejano.position 
            : boteObjeto.transform.position + boteObjeto.transform.forward * 45f;

        pezLejano = SpawnearPez(spawnPos);
    }

    private void VerificarEstadoPescaLejana()
    {
        int totalActual = FishingCounterManager.Instance != null ? FishingCounterManager.Instance.GetFishCount() : 0;
        int atrapadosLejanos = totalActual - conteoPescaLejanaInicial;

        // 1. Si capturó el pez lejano -> Completar
        if (atrapadosLejanos >= 1)
        {
            if (pezLejano != null)
            {
                Destroy(pezLejano);
                pezLejano = null;
            }

            StartCoroutine(CompletadoPescaLejanaRoutine());
            return;
        }

        // 2. Si el pez se escapó/despawneó -> Respawnearlo en las coordenadas lejanas
        if (pezLejano == null)
        {
            Vector3 spawnPos = (puntoSpawnLejano != null) 
                ? puntoSpawnLejano.position 
                : boteObjeto.transform.position + boteObjeto.transform.forward * 45f;

            pezLejano = SpawnearPez(spawnPos);
        }
    }

    private IEnumerator CompletadoPescaLejanaRoutine()
    {
        estadoActual = EstadoTutorial.EsperandoSiguienteSlide;
        Debug.Log("[TUTORIAL] Pesca lejana completada. Cargando Slide 4 (Final)...");

        yield return new WaitForSeconds(1.5f);

        currentSlideIndex = 3; // Mostrar Slide 4 ("Tutorial completado")
        MostrarSlideActual();
    }

    private GameObject SpawnearPez(Vector3 posicion)
    {
        if (fishPrefab == null)
        {
            Debug.LogWarning("[TUTORIAL] No se ha asignado el 'fishPrefab' en el Inspector.");
            return null;
        }

        GameObject pez = Instantiate(fishPrefab, posicion, Quaternion.identity);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            NetworkObject netObj = pez.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        return pez;
    }
}