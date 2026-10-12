using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class BoatDriverToggle : NetworkBehaviour
{
    [Header("Referencias del Bote")]
    [SerializeField] private Transform timonAnchor;
    [SerializeField] private Transform deckAnchor;

    [Header("Teclas")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

    [Header("Referencia al Jugador")]
    [SerializeField] private ThirdPController playerController;
    [SerializeField] private GameObject playerObject;

    private BoatController boatController;
    private CharacterController characterController;

    private bool isDriving = false;
    private bool wasWiiOne = false;
    private void Awake()
    {
        boatController = GetComponent<BoatController>();
    }

    private void Start()
    {
        TryFindPlayer();
    }

    private bool TryFindPlayer()
    {
        if (playerObject == null)
        {
            Debug.LogWarning("[DEBUG BOTE] Falla: 'playerObject' es NULL");
            return false;
        }

        if (playerController == null)
            playerController = playerObject.GetComponent<ThirdPController>();

        if (characterController == null)
            characterController = playerObject.GetComponent<CharacterController>();

        return true;
    }

    private void Update()
    {
        bool wiiToggleTrigger = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            bool currentWiiOne = wiimote.Button.one;
            if (currentWiiOne && !wasWiiOne)
            {
                wiiToggleTrigger = true;
            }
            wasWiiOne = currentWiiOne;
        }
#endif
        if (Input.GetKeyDown(toggleKey) || wiiToggleTrigger)
        {
            Debug.Log("[DEBUG BOTE] Entrante detectado (Teclado o Wiimote).");

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    Debug.LogWarning("[DEBUG BOTE] Bloqueado: La accion proviene de un Cliente.");
                    return;
                }
            }

            if (!TryFindPlayer())
            {
                Debug.LogWarning("[DEBUG BOTE] Bloqueado: TryFindPlayer() devolvio false.");
                return;
            }

            Debug.Log("[DEBUG BOTE] Modo conduccion alternado exitosamente");
            ToggleDrivingMode();
        }
    }

    private void LateUpdate()
    {
        bool isAllowed = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) || NetworkManager.Singleton.IsServer;

        if (isAllowed && isDriving && playerObject != null && timonAnchor != null)
        {
            playerObject.transform.position = timonAnchor.position;
            playerObject.transform.rotation = timonAnchor.rotation;
        }
    }

    private void ToggleDrivingMode()
    {
        isDriving = !isDriving;

        if (isDriving)
        {
            //playerObject.transform.SetParent(transform);
            TeleportPlayer(timonAnchor);

            if (playerController != null) playerController.enabled = false;
            if (characterController != null) characterController.enabled = false;
            if (boatController != null) boatController.SetDrivingState(true);

            Debug.Log("[BOTE - SERVIDOR] Modo Conduccion Activado");
        }
        else
        {
            TeleportPlayer(deckAnchor);

            if (characterController != null) characterController.enabled = true;
            if (playerController != null) playerController.enabled = true;
            if (boatController != null) boatController.SetDrivingState(false);

            Debug.Log("[BOTE - SERVIDOR] Modo Conduccion Desactivado");
        }
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null || playerObject == null) return;

        if (characterController != null) characterController.enabled = false;

        playerObject.transform.position = target.position;
        playerObject.transform.rotation = target.rotation;

        Physics.SyncTransforms();

        if (playerController != null)
        {
            playerController.playerVelocity = Vector3.zero;
        }
    }
}