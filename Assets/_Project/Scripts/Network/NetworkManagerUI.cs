using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Gestion de Menu")]
    [SerializeField] private MenuMismaEscena menuMismaEscena;

    [Header("Botones de Red")]
    [SerializeField] private Button serverBtn;
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;

    [Header("Contenedores de UI (Canvases)")]
    [SerializeField] private GameObject uiContainer;
    [SerializeField] private GameObject hudContainer;
 
    [Header("Configuracion de Red")]
    [SerializeField] private string pcIPAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Reconexion (celulares)")]
    [Tooltip("Segundos entre intentos de reconexion cuando el celular pierde la conexion con la PC")]
    [SerializeField] private float segundosEntreReintentos = 3f;

    private bool modoCliente = false;
    private Coroutine rutinaReconexion;

    private void Awake()
    {
        if (uiContainer != null) uiContainer.SetActive(true);
        if (hudContainer != null) hudContainer.SetActive(false);

        if (serverBtn != null)
        {
            serverBtn.onClick.AddListener(() => StartServerMode());
        }
 
        if (hostBtn != null)
        {
            hostBtn.onClick.AddListener(() =>
            {
                SetTransportAsServer();
                NetworkManager.Singleton.StartHost();
                ShowGameHUD();
            });
        }
 
        if (clientBtn != null)
        {
            clientBtn.onClick.AddListener(() => StartClientMode());
        }
    }

    private IEnumerator Start()
    {
        UnlockCursor();
#if UNITY_ANDROID
        yield return new WaitForSeconds(2.5f);

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient)
        {
            StartClientMode();
        }
#else
        yield return new WaitForSeconds(0.2f);

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            StartServerMode();
        }
#endif
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }
    }

    private void StartServerMode()
    {
        SetTransportAsServer();
        NetworkManager.Singleton.StartServer();
        ShowGameHUD();
        Debug.Log("[RED] Servidor iniciado automaticamente en PC.");
    }

    private void StartClientMode()
    {
        if (!modoCliente)
        {
            modoCliente = true;
            // Se avisa tanto si se cae una conexion establecida como si falla el intento de conectar
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDesconectadoDelServidor;
        }

        SetTransportAsClient();
        NetworkManager.Singleton.StartClient();
        ShowGameHUD();
        Debug.Log("[RED] Conectando como Cliente desde Celular.");
    }

    private void OnDestroy()
    {
        if (modoCliente && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDesconectadoDelServidor;
        }
    }

    // ---------------- RECONEXION AUTOMATICA (solo celulares) ----------------
    // Si el celular pierde la conexion con la PC (o la PC aun no estaba lista), reintenta solo.
    // Al reconectarse, MenuMismaEscena y el resto de NetworkBehaviours toman el estado actual de la PC.
    private void OnDesconectadoDelServidor(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer) return;
        if (clientId != nm.LocalClientId && clientId != NetworkManager.ServerClientId) return;

        Debug.Log("[RED] Se perdio la conexion con la PC. Reintentando...");
        if (rutinaReconexion == null) rutinaReconexion = StartCoroutine(ReconectarRutina());
    }

    private IEnumerator ReconectarRutina()
    {
        var nm = NetworkManager.Singleton;

        // Espera a que Netcode termine de cerrar la conexion anterior
        while (nm != null && (nm.ShutdownInProgress || nm.IsListening))
        {
            if (!nm.ShutdownInProgress && nm.IsListening) nm.Shutdown();
            yield return null;
        }

        yield return new WaitForSecondsRealtime(segundosEntreReintentos);

        rutinaReconexion = null;
        if (nm != null && !nm.IsListening)
        {
            StartClientMode();
        }
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowGameHUD()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false);
        }

        if (hudContainer != null)
        {
            hudContainer.SetActive(true);
        }

        //if (menuMismaEscena != null)
        //{
        //    menuMismaEscena.EmpezarJuego();
        //}
    }
 
    private void SetTransportAsServer()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(pcIPAddress, port, "0.0.0.0");
        }
    }
 
    private void SetTransportAsClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            Debug.Log("[NETCODE_TEST] Conectando a IP: " + pcIPAddress + " en puerto: " + port);
            transport.SetConnectionData(pcIPAddress, port);
        }
    }
}