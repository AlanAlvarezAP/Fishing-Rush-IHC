using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Botones de Red")]
    [SerializeField] private Button serverBtn;
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;

    [Header("Contenedores de UI (Canvases)")]
    [SerializeField] private GameObject uiContainer;
    [SerializeField] private GameObject hudContainer;
 
    [Header("Configuración de Red")]
    [SerializeField] private string pcIPAddress = "10.55.25.139";
    [SerializeField] private ushort port = 7777;
 
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
        SetTransportAsClient();
        NetworkManager.Singleton.StartClient();
        ShowGameHUD();
        Debug.Log("[RED] Conectando como Cliente desde Celular.");
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