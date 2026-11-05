using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Movimiento del bote.
/// - Celular conductor (Jugador 2, cliente): lee los botones tactiles (UIBoteInput) y manda el input
///   al servidor SOLO cuando cambia (incluido el soltar = Vector2.zero).
/// - PC (servidor): guarda el ultimo input recibido y lo aplica en su propio Update con su deltaTime,
///   asi la velocidad no depende de los FPS del celular. Publica posicion/rotacion por NetworkVariables.
/// - Resto de clientes (celular VR): solo interpolan hacia la posicion de red.
/// El movimiento solo esta habilitado durante la partida (lo controla MenuMismaEscena).
/// </summary>
public class BoatController : NetworkBehaviour
{
    [Header("Ajustes de Movimiento")]
    [SerializeField] private float moveSpeed = 8.0f;
    [SerializeField] private float turnSpeed = 40.0f;
    [SerializeField] private float syncSmoothing = 15f;

    private bool movimientoHabilitado = false;

    // Servidor: ultimo input recibido del conductor
    private Vector2 inputRemoto = Vector2.zero;

    // Cliente conductor: ultimo input enviado (para mandar solo cuando cambia)
    private Vector2 ultimoInputEnviado = Vector2.zero;

    private NetworkVariable<Vector3> netBoatPos = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<Quaternion> netBoatRot = new NetworkVariable<Quaternion>(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    /// <summary>Habilita o bloquea el movimiento del bote (menu / partida).</summary>
    public void SetMovimientoHabilitado(bool habilitado)
    {
        movimientoHabilitado = habilitado;
        if (!habilitado) inputRemoto = Vector2.zero;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
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

        // Celular conductor: envia su input al servidor cuando cambia
        if (!offline && !IsServer && IsSpawned && ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet)
        {
            Vector2 inputTablet = ReadInput();
            if ((inputTablet - ultimoInputEnviado).sqrMagnitude > 0.0001f)
            {
                ultimoInputEnviado = inputTablet;
                EnviarInputBoteServerRpc(inputTablet);
            }
        }

        if (IsServer || offline)
        {
            if (movimientoHabilitado)
            {
                // Input del conductor remoto; sin red (pruebas) se usa el tactil local
                Vector2 input = inputRemoto.sqrMagnitude > 0.0001f ? inputRemoto : ReadInput();
                ApplyMovement(input);
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
        if (UIBoteInput.InputTactil.sqrMagnitude > 0.01f)
        {
            return UIBoteInput.InputTactil.normalized;
        }
        return Vector2.zero;
    }

    private void ApplyMovement(Vector2 input)
    {
        bool seMovio = false;

        if (Mathf.Abs(input.y) > 0.1f)
        {
            transform.position += transform.forward * input.y * moveSpeed * Time.deltaTime;
            seMovio = true;
        }

        if (Mathf.Abs(input.x) > 0.1f)
        {
            transform.Rotate(Vector3.up, input.x * turnSpeed * Time.deltaTime);
            seMovio = true;
        }

        // IMPORTANTE: el jugador (hijo del bote) usa CharacterController, que guarda su propia
        // posicion interna en el motor de fisica y NO se entera cuando se mueve el padre por
        // transform. Sin esto, en su siguiente Move() vuelve a su posicion vieja en el mundo:
        // se "queda en su sitio", la cubierta lo arrastra y puede atravesar el bote y caerse.
        // SyncTransforms actualiza esa posicion interna para que viaje con el bote.
        if (seMovio) Physics.SyncTransforms();
    }

    public void SnapTo(Vector3 pos, Quaternion rot)
    {
        inputRemoto = Vector2.zero;
        transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
        if (IsServer && IsSpawned) { netBoatPos.Value = pos; netBoatRot.Value = rot; }
    }

    [ServerRpc(RequireOwnership = false)]
    private void EnviarInputBoteServerRpc(Vector2 input)
    {
        inputRemoto = Vector2.ClampMagnitude(input, 1f);
    }
}
