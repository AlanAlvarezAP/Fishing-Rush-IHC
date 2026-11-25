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

    [Header("Tiempos de conexion (Unity Transport)")]
    [Tooltip("Sin recibir nada en este tiempo se da la conexion por perdida (antes 30000: tardaba 30 s en notarlo)")]
    [SerializeField] private int disconnectTimeoutMS = 5000;
    [Tooltip("Cada cuanto se manda un latido si no hay otros mensajes (debe ser menor que el de desconexion)")]
    [SerializeField] private int heartbeatTimeoutMS = 1000;
    [Tooltip("Espera de cada intento de conexion (antes 10000)")]
    [SerializeField] private int connectTimeoutMS = 1000;
    [Tooltip("Intentos por cada reconexion: 10 x 1 s = unos 10 s, luego se vuelve a empezar (antes 60 x 10 s = 10 min)")]
    [SerializeField] private int maxConnectAttempts = 10;

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

        RevisarReconexion();
    }

    /// <summary>
    /// Respaldo: si el celular quedo sin conexion y sin ningun intento en marcha (algun fallo
    /// en el que Netcode no aviso), se vuelve a intentar. Asi nunca se queda "muerto".
    /// </summary>
    private void RevisarReconexion()
    {
        if (!modoCliente || rutinaReconexion != null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer || nm.ShutdownInProgress || nm.IsListening) return;

        Debug.Log("[RED] Sin conexion y sin intento en curso: se reintenta.");
        rutinaReconexion = StartCoroutine(ReconectarRutina());
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
        bool inicio = NetworkManager.Singleton.StartClient();
        ShowGameHUD();

        if (inicio)
        {
            Debug.Log("[RED] Conectando como Cliente desde Celular.");
        }
        else
        {
            // Ej. el wifi aun esta apagado: StartClient falla al instante y Netcode NO avisa
            // con OnClientDisconnectCallback, asi que hay que volver a intentarlo desde aqui
            Debug.LogWarning("[RED] No se pudo iniciar la conexion (sin red?). Se reintenta...");
            if (rutinaReconexion == null) rutinaReconexion = StartCoroutine(ReconectarRutina());
        }
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
 
    /// <summary>Los mismos tiempos en la PC y en los celulares (se aplican antes de conectar).</summary>
    private void AplicarTiempos(UnityTransport transport)
    {
        transport.DisconnectTimeoutMS = disconnectTimeoutMS;
        transport.HeartbeatTimeoutMS = Mathf.Min(heartbeatTimeoutMS, disconnectTimeoutMS / 2);
        transport.ConnectTimeoutMS = connectTimeoutMS;
        transport.MaxConnectAttempts = maxConnectAttempts;
    }

    private void SetTransportAsServer()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            AplicarTiempos(transport);
            transport.SetConnectionData(pcIPAddress, port, "0.0.0.0");
        }
    }
 
    private void SetTransportAsClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            Debug.Log("[NETCODE_TEST] Conectando a IP: " + pcIPAddress + " en puerto: " + port);
            AplicarTiempos(transport);
            transport.SetConnectionData(pcIPAddress, port);
        }
    }
}