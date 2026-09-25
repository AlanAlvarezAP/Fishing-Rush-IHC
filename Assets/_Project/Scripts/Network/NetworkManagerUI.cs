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
    [SerializeField] private string pcIPAddress = "10.87.165.139";
    [SerializeField] private ushort port = 7777;
 
    private void Awake()
    {
        if (uiContainer != null) uiContainer.SetActive(true);
        if (hudContainer != null) hudContainer.SetActive(false);

        serverBtn.onClick.AddListener(() =>
        {
            SetTransportAsServer();
            NetworkManager.Singleton.StartServer();
            ShowGameHUD();
        });
 
        hostBtn.onClick.AddListener(() =>
        {
            SetTransportAsServer();
            NetworkManager.Singleton.StartHost();
            ShowGameHUD();
        });
 
        clientBtn.onClick.AddListener(() =>
        {
            SetTransportAsClient();
            NetworkManager.Singleton.StartClient();
            ShowGameHUD();
        });
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID
        // Esperamos 1.5 segundos a que Cardboard XR e Input System terminen de cargar en Android
        yield return new WaitForSeconds(2.5f);

        if (NetworkManager.Singleton != null)
        {
            SetTransportAsClient();
            NetworkManager.Singleton.StartClient();
            ShowGameHUD();
        }
#else
        yield break;
#endif
    }

    private void ShowGameHUD()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false); // Desaparece el menú de inicio
        }

        if (hudContainer != null)
        {
            hudContainer.SetActive(true);  // Aparece la interfaz del juego
        }
    }
 
    // Server / Host: escucha en todas las interfaces de red disponibles.
    private void SetTransportAsServer()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // transport.SetConnectionData("0.0.0.0", port);
            //transport.SetConnectionData(pcIPAddress, port, "0.0.0.0");
            transport.SetConnectionData(pcIPAddress, port, pcIPAddress);
        }
    }
 
    // Client: se conecta específicamente a la IP de la PC-servidor.
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