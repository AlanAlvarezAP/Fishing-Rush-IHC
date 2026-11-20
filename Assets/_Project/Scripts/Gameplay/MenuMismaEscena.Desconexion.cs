using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// PAUSA POR DESCONEXION (solo durante la partida)
/// - Cada celular le dice a la PC su rol al conectarse. La PC recuerda que roles estaban al empezar
///   la partida; si uno de ellos se desconecta, la partida se pausa para todos:
///     * Time.timeScale = 0 en todos (temporizador, vida y nado de peces y tiburones, animaciones).
///       Netcode usa tiempo real, asi que la red sigue funcionando.
///     * Se pierde la pesca en curso y el bote se detiene.
///     * Aviso: "Pescador / Conductor desconectado... esperando su reconexion" (y en la consola de la PC).
/// - Al reconectarse se reanuda: el tiempo de la partida, la mina y el tambaleo se corren lo que
///   duro la pausa. Si no vuelve en segundosMaxEsperaReconexion, game over para todos.
/// - El dispositivo que perdio la conexion ve "Reconectando..." mientras reintenta.
/// </summary>
public partial class MenuMismaEscena
{
    [Header("Desconexion en plena partida")]
    [Tooltip("Segundos que se espera a que vuelva el jugador desconectado; despues, game over")]
    public float segundosMaxEsperaReconexion = 60f;

    private const byte FALTA_PESCADOR = 1;
    private const byte FALTA_CONDUCTOR = 2;

    // Que jugador falta (FALTA_PESCADOR | FALTA_CONDUCTOR). 0 = sin pausa.
    private NetworkVariable<byte> desconexionFaltanNet = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    // Hora del servidor en que empezo la pausa (para congelar el temporizador y la cuenta de 1 minuto)
    private NetworkVariable<double> desconexionInicioNet = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    /// <summary>true mientras la partida esta en pausa porque falta un jugador.</summary>
    public bool PausaPorDesconexion => IsSpawned && isGameStartedNet.Value && desconexionFaltanNet.Value != 0;

    // ---------------- servidor ----------------
    private readonly Dictionary<ulong, TipoJugador> rolesClientes = new Dictionary<ulong, TipoJugador>();
    private readonly HashSet<TipoJugador> rolesEnPartida = new HashSet<TipoJugador>();

    // ---------------- local ----------------
    private bool pausaAplicadaLocal = false;
    private bool vioPartidaEnCurso = false; // cliente: la partida estaba en curso la ultima vez que tuvo red

    // =====================================================================
    // ROLES DE LOS CLIENTES
    // =====================================================================
    /// <summary>
    /// Servidor: un celular dijo su rol al conectarse o reconectarse. Lo llama
    /// RegistrarRolServerRpc de MenuMismaEscena.Red.cs (ahi esta el aviso en la consola).
    /// </summary>
    private void RolRegistradoServidor(ulong id, TipoJugador tipo)
    {
        rolesClientes[id] = tipo;

        // Si entra con la partida ya empezada (ej. se unio tarde), desde ahora tambien cuenta
        if (isGameStartedNet.Value) rolesEnPartida.Add(tipo);

        ActualizarPausaServidor();
    }

    /// <summary>Servidor: lo llama OnClienteDesconectado.</summary>
    private void QuitarRolCliente(ulong clientId)
    {
        if (!rolesClientes.TryGetValue(clientId, out TipoJugador tipo)) return;
        rolesClientes.Remove(clientId);

        // El conductor se fue: el bote no debe seguir con su ultimo input (avanzar / girar)
        if (tipo == TipoJugador.Jugador2_ConductorTablet && boteObjeto != null)
        {
            BoatController bc = boteObjeto.GetComponent<BoatController>();
            if (bc != null) bc.DetenerInputRemoto();
        }

        ActualizarPausaServidor();
    }

    // (NombreRol esta en MenuMismaEscena.Red.cs)
    private bool RolConectado(TipoJugador tipo) => rolesClientes.ContainsValue(tipo);

    /// <summary>Servidor, al empezar la partida: los roles conectados son los que se esperan si se caen.</summary>
    private void FijarRolesEnPartidaServidor()
    {
        rolesEnPartida.Clear();
        foreach (TipoJugador tipo in rolesClientes.Values) rolesEnPartida.Add(tipo);
        if (desconexionFaltanNet.Value != 0) desconexionFaltanNet.Value = 0;
    }

    // =====================================================================
    // PAUSA (servidor)
    // =====================================================================
    private void ActualizarPausaServidor()
    {
        if (!IsSpawned || !IsServer) return;

        byte faltan = 0;
        if (isGameStartedNet.Value)
        {
            if (rolesEnPartida.Contains(TipoJugador.Jugador1_PescadorVR) && !RolConectado(TipoJugador.Jugador1_PescadorVR)) faltan |= FALTA_PESCADOR;
            if (rolesEnPartida.Contains(TipoJugador.Jugador2_ConductorTablet) && !RolConectado(TipoJugador.Jugador2_ConductorTablet)) faltan |= FALTA_CONDUCTOR;
        }

        byte antes = desconexionFaltanNet.Value;
        if (faltan == antes) return;

        if (antes == 0) EmpezarPausaServidor(faltan);
        else if (faltan == 0) ReanudarPartidaServidor();
        else
        {
            desconexionFaltanNet.Value = faltan; // cambio quien falta (ej. se fue el otro tambien)
            Debug.Log($"[RED] Sigue la pausa. Faltan: {TextoQuienFalta(faltan)}.");
        }
    }

    private void EmpezarPausaServidor(byte faltan)
    {
        desconexionInicioNet.Value = NetworkManager.ServerTime.Time;
        desconexionFaltanNet.Value = faltan;

        PerderPescaPorDesconexion();

        if (boteObjeto != null)
        {
            BoatController bc = boteObjeto.GetComponent<BoatController>();
            if (bc != null) bc.DetenerInputRemoto();
        }

        Debug.LogWarning($"[RED] PARTIDA EN PAUSA: {TextoQuienFalta(faltan)} desconectado. " +
                         $"Se espera su reconexion {segundosMaxEsperaReconexion:0} s.");
    }

    private void ReanudarPartidaServidor()
    {
        double duracion = NetworkManager.ServerTime.Time - desconexionInicioNet.Value;
        if (duracion < 0d) duracion = 0d;

        // La hora de fin de la partida, la mina y el tambaleo se corren lo que duro la pausa
        if (finPartidaNet.Value > 0d) finPartidaNet.Value += duracion;
        if (boteObjeto != null)
        {
            BoatController bc = boteObjeto.GetComponent<BoatController>();
            if (bc != null) bc.AplazarEfectos(duracion);
        }

        desconexionFaltanNet.Value = 0;
        Debug.Log($"[RED] Jugador reconectado: la partida se reanuda (pausa de {duracion:0.0} s).");
    }

    /// <summary>Se pierde lo que se estaba pescando y el pescador sale del modo pesca.</summary>
    private void PerderPescaPorDesconexion()
    {
        foreach (FishCube pez in FindObjectsOfType<FishCube>()) pez.CancelarPorDesconexion();

        SalirModoPescaJugadores();
        GarantizarJugador();
        if (playerController != null) playerController.CancelarBloqueoTiburon();
        if (FishingHUDManager.Instance != null) FishingHUDManager.Instance.HideAll();
    }

    /// <summary>Servidor, cada frame: si se acabo la espera, game over para todos.</summary>
    private void RevisarEsperaReconexionServidor()
    {
        if (!IsSpawned || !IsServer || !PausaPorDesconexion) return;

        double esperado = NetworkManager.ServerTime.Time - desconexionInicioNet.Value;
        if (esperado < segundosMaxEsperaReconexion) return;

        Debug.LogWarning($"[RED] {TextoQuienFalta(desconexionFaltanNet.Value)} no volvio en {segundosMaxEsperaReconexion:0} s: game over.");
        desconexionFaltanNet.Value = 0; // sin correr el tiempo: la partida termina aqui
        Time.timeScale = 1f;
        pausaAplicadaLocal = false;
        MostrarGameOver();
    }

    /// <summary>Tiempo restante de la partida congelado durante la pausa (lo usa TiempoRestanteRed).</summary>
    private double AhoraParaTiempoDePartida()
    {
        return PausaPorDesconexion ? desconexionInicioNet.Value : NetworkManager.ServerTime.Time;
    }

    // =====================================================================
    // EN CADA DISPOSITIVO: congelar y avisar
    // =====================================================================
    private void ActualizarPausaLocal()
    {
        bool pausa = PausaPorDesconexion;

        if (pausa)
        {
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            pausaAplicadaLocal = true;
        }
        else if (pausaAplicadaLocal)
        {
            pausaAplicadaLocal = false;
            Time.timeScale = 1f;
        }

        // Cliente: recuerda si estaba en partida, para avisar "Reconectando..." si pierde la red
        if (IsSpawned) vioPartidaEnCurso = isGameStartedNet.Value;

        string mensaje = null;
        if (pausa)
        {
            mensaje = TextoPausa();
        }
        else if (!IsSpawned && vioPartidaEnCurso && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            mensaje = "SE PERDIO LA CONEXION\nReconectando con la PC...";
        }

        AvisosPeligroUI.MensajeDesconexion = mensaje;
    }

    private string TextoPausa()
    {
        byte faltan = desconexionFaltanNet.Value;
        double esperado = NetworkManager.ServerTime.Time - desconexionInicioNet.Value;
        int restante = Mathf.Max(0, Mathf.CeilToInt(segundosMaxEsperaReconexion - (float)esperado));

        string quien = TextoQuienFalta(faltan);
        string verbo = faltan == (FALTA_PESCADOR | FALTA_CONDUCTOR) ? "desconectados" : "desconectado";
        return $"PARTIDA EN PAUSA\n{quien} {verbo}\nEsperando su reconexion... {restante} s";
    }

    private static string TextoQuienFalta(byte faltan)
    {
        bool p = (faltan & FALTA_PESCADOR) != 0;
        bool c = (faltan & FALTA_CONDUCTOR) != 0;
        if (p && c) return "Pescador y conductor";
        if (p) return "Pescador";
        if (c) return "Conductor";
        return "Nadie";
    }
}
