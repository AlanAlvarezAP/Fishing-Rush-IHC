using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerNetwork : NetworkBehaviour
{
    [Header("Cámaras del Personaje")]
    [SerializeField] private GameObject firstPersonCamera;  // Cámara 1ª Persona (Cardboard VR)
    [SerializeField] private GameObject thirdPersonCamera; // Cámara 3ª Persona (Cinemachine)

    [Header("Referencias Visuales del Personaje")]
    [SerializeField] private SkinnedMeshRenderer[] characterMeshRenderers;

    // Propiedad pública para que ThirdPController lea la orientación VR
    public float ClientCameraYaw { get; private set; } = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. Desactivar CameraSwitcher para que no interfiera mediante teclado
        CameraSwitcher switcher = GetComponent<CameraSwitcher>();
        if (switcher != null) switcher.enabled = false;

        if (IsClient && !IsHost)
        {
            // === ROL CLIENTE (Android VR) ===

            // 1. Buscamos la cámara externa flotante del menú/escena y la apagamos
            GameObject externalCam = GameObject.FindWithTag("MainCamera");
            if (externalCam != null && externalCam != firstPersonCamera)
            {
                externalCam.SetActive(false);
            }

            // 2. Apagamos la cámara de 3ra persona y activamos la de 1ra persona
            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(false);

            if (firstPersonCamera != null) 
            {
                firstPersonCamera.tag = "MainCamera"; // Forzamos el Tag
                firstPersonCamera.SetActive(true);   // Activamos la cámara VR
            }

            // 3. Ocultamos el cuerpo en el visor
            //HideBodyOnClient();
        }
        else if (IsServer)
        {
            // === ROL SERVIDOR / HOST (PC) ===
            if (firstPersonCamera != null) firstPersonCamera.SetActive(false);
            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(true);
        }
    }

    private void Update()
    {
        // El Cliente envía constantemente la orientación Y de su mirada al Servidor
        if (IsClient && !IsHost)
        {
            SendHeadRotationToServer();
        }
    }

    private void SendHeadRotationToServer()
    {
        // Leemos la rotación Y de la primera persona activa
        if (firstPersonCamera != null)
        {
            float currentYaw = firstPersonCamera.transform.eulerAngles.y;
            UpdateHeadRotationServerRpc(currentYaw);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateHeadRotationServerRpc(float cameraYaw)
    {
        ClientCameraYaw = cameraYaw;
    }

    private void HideBodyOnClient()
    {
        if (characterMeshRenderers != null)
        {
            foreach (var mesh in characterMeshRenderers)
            {
                if (mesh != null)
                {
                    mesh.enabled = false;
                }
            }
        }
    }
}