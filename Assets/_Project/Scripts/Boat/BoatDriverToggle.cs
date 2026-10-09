using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BoatDriverToggle : NetworkBehaviour
{
    [Header("Referencias del Bote")]
    [SerializeField] private Transform timonAnchor;
    [SerializeField] private Transform deckAnchor;

    [Header("Teclas")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

    private BoatController boatController;
    private GameObject playerObject;
    private ThirdPController playerController;
    private CharacterController characterController;

    private bool isDriving = false;

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
            ThirdPController[] players = FindObjectsOfType<ThirdPController>();
            foreach (var p in players)
            {
                if (p.IsOwner)
                {
                    playerController = p;
                    playerObject = p.gameObject;
                    characterController = playerObject.GetComponent<CharacterController>();
                    break;
                }
            }
        }
        return playerObject != null;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (!TryFindPlayer()) return;
            ToggleDrivingMode();
        }
    }

    private void LateUpdate()
    {
        if (IsServer && isDriving && playerObject != null && timonAnchor != null)
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
            playerObject.transform.SetParent(transform);
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