using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerNetwork : NetworkBehaviour
{
    [Header("Camaras del Personaje")]
    [SerializeField] private GameObject firstPersonCamera;
    [SerializeField] private GameObject thirdPersonCamera;

    [Header("Referencias Visuales del Personaje")]
    [SerializeField] private SkinnedMeshRenderer[] characterMeshRenderers;

    public float ClientCameraYaw { get; private set; } = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CameraSwitcher switcher = GetComponent<CameraSwitcher>();

        GameObject externalCam = GameObject.FindWithTag("MainCamera");
        if (externalCam != null && externalCam != firstPersonCamera && externalCam != thirdPersonCamera)
        {
            externalCam.SetActive(false);
        }

        if (IsClient && !IsHost)
        {
            if (switcher != null) switcher.enabled = false;

            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(false);

            if (firstPersonCamera != null) 
            {
                firstPersonCamera.tag = "MainCamera";
                firstPersonCamera.SetActive(true);

                AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
                if (fpListener != null) fpListener.enabled = true;
            }

            HideBodyOnClient();
        }
        else if (IsServer)
        {
            if (switcher != null)
            {
                if (switcher.characterMeshRenderers == null || switcher.characterMeshRenderers.Length == 0)
                {
                    switcher.characterMeshRenderers = characterMeshRenderers;
                }

                switcher.enabled = true;
                switcher.SetCameraMode(false);
            }
        }

        /*
        if (!IsOwner)
        {
            AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
            AudioListener tpListener = thirdPersonCamera.GetComponent<AudioListener>();
            if (fpListener != null) fpListener.enabled = false;
            if (tpListener != null) tpListener.enabled = false;

            if (firstPersonCamera != null) firstPersonCamera.SetActive(false);
            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(false);
            if (switcher != null) switcher.enabled = false;
            return;
        }
        */
    }

    private void Update()
    {
        if (IsClient && !IsHost)
        {
            SendHeadRotationToServer();
        }
    }

    private void SendHeadRotationToServer()
    {
        if (firstPersonCamera != null && firstPersonCamera.activeInHierarchy)
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