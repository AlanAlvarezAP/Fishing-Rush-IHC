using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Camaras y visibilidad del cuerpo del pescador segun el dispositivo:
/// - PC (servidor): CameraSwitcher habilitado (tercera persona por defecto).
/// - Celular VR (Jugador 1, cliente): primera persona y cuerpo oculto.
/// - Celular conductor (Jugador 2, cliente): no toca las camaras del pescador ni oculta su cuerpo
///   (el conductor ve al pescador desde camaraBoteTablet; eso lo configura MenuMismaEscena).
/// El giro en Y del celular VR lo envia ThirdPController (TransmitirYawServerRpc).
/// </summary>
public class PlayerNetwork : NetworkBehaviour
{
    [Header("Camaras del Personaje")]
    [SerializeField] private GameObject firstPersonCamera;
    [SerializeField] private GameObject thirdPersonCamera;

    [Header("Referencias Visuales del Personaje")]
    [SerializeField] private SkinnedMeshRenderer[] characterMeshRenderers;

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

            if (ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet)
            {
                // Conductor: el pescador se ve completo desde la camara del bote
                return;
            }

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
