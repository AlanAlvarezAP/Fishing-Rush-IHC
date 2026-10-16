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
    public bool IsDriving => isDriving;
    //private bool wasWiiOne = false;

    //private NetworkVariable<bool> isDrivingNet = new NetworkVariable<bool>(
    //    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    //);

    /*
    private static bool HasAuthority
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }
    }
    */

    private void Awake()
    {
        boatController = GetComponentInParent<BoatController>();
        if (boatController == null) boatController = GetComponentInChildren<BoatController>();
        if (boatController == null) boatController = GetComponent<BoatController>();
    }

    //public override void OnNetworkSpawn()
    //{
    //    base.OnNetworkSpawn();
    //    ActivarModoConduccionPermanente();
    //}

    /*
    private void OnDrivingStateChanged(bool previousValue, bool newValue)
    {
        if (isDriving != newValue)
        {
            ApplyDrivingStateLocally(newValue);
        }
    }
    */

    //private void Start()
    //{
    //    ActivarModoConduccionPermanente();
    //}

    private bool TryFindPlayer()
    {
        if (playerObject == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
                playerObject = nm.LocalClient.PlayerObject.gameObject;
            else
                playerObject = GameObject.FindWithTag("Player");
 
            if (playerObject == null)
            {
                Debug.LogWarning("[BOTE] No se encontro al jugador (tag 'Player' / PlayerObject).");
                return false;
            }
        }
 
        if (playerController == null) playerController = playerObject.GetComponent<ThirdPController>();
        if (characterController == null) characterController = playerObject.GetComponent<CharacterController>();
        return true;
    }

    private void Update()
    {
        //if (playerObject == null)
        //{
        //    ActivarModoConduccionPermanente();
        //}

        /*
        if (!HasAuthority) return; // los clientes solo visualizan
 
        bool wiiToggleTrigger = false;
 
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];
 
            int ret;
            do { ret = wiimote.ReadWiimoteData(); } while (ret > 0);
 
            bool currentWiiOne = wiimote.Button.one;
            if (currentWiiOne && !wasWiiOne) wiiToggleTrigger = true;
            wasWiiOne = currentWiiOne;
        }
#endif
 
        if (Input.GetKeyDown(toggleKey) || wiiToggleTrigger)
        {
            if (!TryFindPlayer()) return;
            SetDriving(!isDriving);
        }
        */
    }
 
    private void LateUpdate()
    {
        if (isDriving && playerObject != null && timonAnchor != null)
        {
            playerObject.transform.position = timonAnchor.position;
            //playerObject.transform.SetPositionAndRotation(timonAnchor.position, timonAnchor.rotation);
        }
    }
 
    /*
    private void SetDriving(bool driving)
    {
        isDriving = driving;
 
        if (isDriving)
        {
            if (characterController != null) characterController.enabled = false;
            if (playerController != null) playerController.enabled = false;
 
            foreach (var col in playerObject.GetComponentsInChildren<Collider>())
                col.enabled = false;
 
            if (timonAnchor != null)
                playerObject.transform.SetPositionAndRotation(timonAnchor.position, timonAnchor.rotation);
 
            if (boatController != null) boatController.SetDrivingState(true);
            Debug.Log("[BOTE] Modo Conduccion Activado");
        }
        else
        {
            if (deckAnchor != null)
                playerObject.transform.SetPositionAndRotation(deckAnchor.position, deckAnchor.rotation);
 
            foreach (var col in playerObject.GetComponentsInChildren<Collider>())
                col.enabled = true;
 
            Physics.SyncTransforms();
 
            if (characterController != null) characterController.enabled = true;
            if (playerController != null)
            {
                playerController.playerVelocity = Vector3.zero;
                playerController.enabled = true;
            }
 
            if (boatController != null) boatController.SetDrivingState(false);
            Debug.Log("[BOTE] Modo Conduccion Desactivado");
        }
    }
    */

    public void ForceStopDriving()
    {
        DesactivarConduccionMenu();
        //if (isDriving && playerObject != null) SetDriving(false);
        //if (playerObject != null && timonAnchor != null)
        //{
        //    playerObject.transform.SetPositionAndRotation(timonAnchor.position, timonAnchor.rotation);
        //}
    }

    public void ActivarModoConduccionPermanente()
    {
        if (!TryFindPlayer()) return;

        isDriving = true;

        if (characterController != null) characterController.enabled = false;

        if (playerController != null) playerController.enabled = true;

        foreach (var col in playerObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (timonAnchor != null)
            playerObject.transform.SetPositionAndRotation(timonAnchor.position, timonAnchor.rotation);

        if (boatController != null) boatController.SetDrivingState(true);

        Debug.Log("[BOTE] Modo Conduccion Permanente Activado.");
    }

    public void DesactivarConduccionMenu()
    {
        isDriving = false;
        if (boatController != null) boatController.SetDrivingState(false);
        Debug.Log("[BOTE] Modo Conduccion DESACTIVADO por Menu.");
    }
}