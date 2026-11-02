using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class BoatController : NetworkBehaviour
{
    [Header("Ajustes de Movimiento")]
    [SerializeField] private float moveSpeed = 8.0f;
    [SerializeField] private float turnSpeed = 40.0f;
    [SerializeField] private float syncSmoothing = 15f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool isBeingDriven = false;
    private ThirdPController playerController;

    private NetworkVariable<Vector3> netBoatPos = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    //private NetworkVariable<Vector3> netBoatPos = new NetworkVariable<Vector3>(
    //    new Vector3(0f, -2.25f, 0.25f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    //);
    private NetworkVariable<Quaternion> netBoatRot = new NetworkVariable<Quaternion>(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public void SetDrivingState(bool driving)
    {
        isBeingDriven = driving;
        if (debugLogs) Debug.Log("[BOTE] BoatController.SetDrivingState(" + driving + ")");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isBeingDriven = true;
        if (IsServer)
        {
            netBoatPos.Value = transform.position;
            netBoatRot.Value = transform.rotation;
        }
        else
        {
            transform.position = netBoatPos.Value;
            transform.rotation = netBoatRot.Value;
        }
    }

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        bool offline = nm == null || !nm.IsListening;

        if (!IsServer && ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet)
        {
            Vector2 inputTablet = ReadInput();
            if (inputTablet.sqrMagnitude > 0.01f)
            {
                EnviarInputBoteServerRpc(inputTablet);
            }
        }

        if (IsServer || offline)
        {
            if (isBeingDriven)
            {
                ApplyMovement(ReadInput());
            }

            if (!offline && IsSpawned)
            {
                netBoatPos.Value = transform.position;
                netBoatRot.Value = transform.rotation;
            }
        }

        else if (IsSpawned)
        {
            float t = Time.deltaTime * syncSmoothing;
            transform.position = Vector3.Lerp(transform.position, netBoatPos.Value, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, netBoatRot.Value, t);
        }
    }
 
    private Vector2 ReadInput()
    {
        //float vertical = Input.GetAxis("Vertical");
        //float horizontal = Input.GetAxis("Horizontal");

        if (UIBoteInput.InputTactil.sqrMagnitude > 0.01f)
        {
            return UIBoteInput.InputTactil.normalized;
        }
 
        //return new Vector2(horizontal, vertical);
        return Vector2.zero;
    }
 
    private void ApplyMovement(Vector2 input)
    {
        if (Mathf.Abs(input.y) > 0.1f)
        {
            transform.position += transform.forward * input.y * moveSpeed * Time.deltaTime;
        }
 
        if (Mathf.Abs(input.x) > 0.1f)
        {
            transform.Rotate(Vector3.up, input.x * turnSpeed * Time.deltaTime);
        }
    }

    public void SnapTo(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        if (IsServer && IsSpawned) { netBoatPos.Value = pos; netBoatRot.Value = rot; }
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnviarInputBoteServerRpc(Vector2 input)
    {
        ApplyMovement(input);
    }
}