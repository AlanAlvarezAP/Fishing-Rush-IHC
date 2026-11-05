using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Parte de MenuMismaEscena dedicada a los LOGS DE RED (mismo objeto de red, sin tocar el Inspector):
/// - Quien se conecta / desconecta: pescador (celular VR) o conductor, con su id de cliente.
///   Cada celular le dice su rol a la PC al conectarse (RegistrarRolServerRpc).
/// - Ping (RTT en ms) cada 'segundosEntrePing':
///     PC      : el ping de cada celular conectado.
///     Celular : su ping hacia la PC.
/// </summary>
public partial class MenuMismaEscena
{
    [Header("Red: logs de conexion")]
    [Tooltip("Cada cuantos segundos se escribe el ping en la consola (0 = desactivado)")]
    [SerializeField] private float segundosEntrePing = 30f;

    // Servidor: rol de cada cliente conectado
    private readonly Dictionary<ulong, TipoJugador> rolesPorCliente = new Dictionary<ulong, TipoJugador>();
    private float proximoPing = 0f;

    private static string NombreRol(TipoJugador rol)
    {
        return rol == TipoJugador.Jugador2_ConductorTablet ? "CONDUCTOR" : "PESCADOR (celular VR)";
    }

    /// <summary>Se llama desde OnNetworkSpawn.</summary>
    private void InicializarLogsRed()
    {
        proximoPing = Time.unscaledTime + segundosEntrePing;

        if (IsServer)
        {
            rolesPorCliente.Clear();
            NetworkManager.OnClientConnectedCallback += OnClienteConectadoLog;
            NetworkManager.OnClientDisconnectCallback += OnClienteDesconectadoLog;
            Debug.Log("[RED] PC lista como servidor. Esperando celulares...");
        }
        else
        {
            Debug.Log($"[RED] Conectado a la PC como {NombreRol(ConfigJugador.RolActual)} (cliente {NetworkManager.LocalClientId}).");
            RegistrarRolServerRpc((int)ConfigJugador.RolActual);
        }
    }

    /// <summary>Se llama desde OnNetworkDespawn.</summary>
    private void FinalizarLogsRed()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClienteConectadoLog;
            NetworkManager.OnClientDisconnectCallback -= OnClienteDesconectadoLog;
        }

        if (!IsServer)
        {
            Debug.LogWarning($"[RED] {NombreRol(ConfigJugador.RolActual)}: se perdio la conexion con la PC.");
        }

        rolesPorCliente.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegistrarRolServerRpc(int rol, ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        rolesPorCliente[id] = (TipoJugador)rol;
        Debug.Log($"[RED] Se conecto el {NombreRol((TipoJugador)rol)} (cliente {id}). {ResumenConectados()}");
    }

    private void OnClienteConectadoLog(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) return;
        Debug.Log($"[RED] Cliente {clientId} conectado, esperando que indique su rol...");
    }

    private void OnClienteDesconectadoLog(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId) return;

        string quien = rolesPorCliente.TryGetValue(clientId, out TipoJugador rol)
            ? NombreRol(rol)
            : "cliente sin rol registrado";
        rolesPorCliente.Remove(clientId);

        Debug.LogWarning($"[RED] Se desconecto el {quien} (cliente {clientId}). {ResumenConectados()}");
    }

    private string ResumenConectados()
    {
        bool pescador = false, conductor = false;
        foreach (TipoJugador rol in rolesPorCliente.Values)
        {
            if (rol == TipoJugador.Jugador2_ConductorTablet) conductor = true;
            else pescador = true;
        }
        return $"Conectados -> Pescador VR: {(pescador ? "SI" : "no")} | Conductor: {(conductor ? "SI" : "no")}";
    }

    /// <summary>Se llama desde Update: escribe el ping cada 'segundosEntrePing'.</summary>
    private void ActualizarPingRed()
    {
        if (!IsSpawned || segundosEntrePing <= 0f) return;
        if (Time.unscaledTime < proximoPing) return;
        proximoPing = Time.unscaledTime + segundosEntrePing;

        NetworkTransport transporte = NetworkManager.NetworkConfig.NetworkTransport;
        if (transporte == null) return;

        if (IsServer)
        {
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                if (id == NetworkManager.ServerClientId) continue;

                string quien = rolesPorCliente.TryGetValue(id, out TipoJugador rol) ? NombreRol(rol) : "cliente sin rol";
                Debug.Log($"[RED][PING] {quien} (cliente {id}): {transporte.GetCurrentRtt(id)} ms");
            }
        }
        else
        {
            ulong rtt = transporte.GetCurrentRtt(NetworkManager.ServerClientId);
            Debug.Log($"[RED][PING] {NombreRol(ConfigJugador.RolActual)} -> PC: {rtt} ms");
        }
    }
}
