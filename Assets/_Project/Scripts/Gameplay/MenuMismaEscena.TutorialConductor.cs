using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// TUTORIAL DEL CONDUCTOR
/// - La tablet lleva los paneles y revisa las pruebas (TutorialConductorUI). En cada paso le pide
///   a la PC que lo prepare (PedirPasoTutorialConductor) y al final le avisa (fin).
/// - La PC (servidor) mueve el bote, pone las minas / tiburones / peces del paso, limita el bote al
///   pasillo del paso y publica el paso en conductorTutorialPasoNet (todos ven la zona).
/// - Mientras dure, el pescador va FUERA del bote (frente a su menu o en su propio tutorial):
///   el pescador solo va dentro del bote cuando ningun jugador esta en su tutorial.
/// </summary>
public partial class MenuMismaEscena
{
    [Header("Tutorial del Conductor (lo crea Fishing Rush > Crear tutorial del conductor)")]
    [Tooltip("Raiz Tutorial_Conductor (pasillos, metas, circulos y puntos). Solo se activa durante el tutorial del conductor")]
    public ZonaTutorialConductor zonaTutorialConductor;
    [Tooltip("Panel_TutorialConductor dentro de Canvas_Menu_Conductor (solo lo usa la tablet)")]
    public TutorialConductorUI tutorialConductorUI;
    [Tooltip("Tile_jugar del menu del conductor: se oculta hasta que complete su tutorial")]
    public GameObject tileJugarConductor;

    [Header("Tiburones del tutorial del conductor")]
    [Tooltip("Paso 3: metros desde los que el tiburon va por el bote (grande para que choque si o si)")]
    public float radioEmbestidaTutorial = 45f;
    [Tooltip("Paso 3: segundos que persigue al bote antes de rendirse")]
    public float duracionEmbestidaTutorial = 30f;

    // Paso del tutorial del conductor: 0 = no hay, 1..4 = pasos con prueba, 5 = slide final.
    // Nombre con "c": Netcode ordena las variables por nombre, asi llega ANTES que isGameStartedNet.
    private NetworkVariable<byte> conductorTutorialPasoNet = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private byte pasoTutorialConductorOffline = 0;

    private byte PasoTutorialConductor => IsSpawned ? conductorTutorialPasoNet.Value : pasoTutorialConductorOffline;

    // ---------------- ESTADO DEL CONDUCTOR GUARDADO EN LA PC (para cuando se reconecta) ----------------
    // Si la tablet cierra la app pierde todo lo local: al volver, toma esto de la PC.
    // Completo su tutorial (desbloquea Jugar)
    private NetworkVariable<bool> conductorTutorialCompletadoNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    // Paso (1..4) en el que se desconecto en pleno tutorial: al volver sigue desde ahi. 0 = ninguno
    private NetworkVariable<byte> conductorTutorialPendienteNet = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    // Estaba viendo su game over (hasta que toca Reiniciar o Volver al menu)
    private NetworkVariable<bool> conductorEnGameOverNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Tablet: el tutorial se cierra como completado (lo usa CerrarTutorialConductorUI)
    private bool finConductorCompletado = false;

    private bool TutorialConductorAbierto => tutorialConductorUI != null && tutorialConductorUI.gameObject.activeSelf;
    public bool EnTutorialConductor => PasoTutorialConductor != 0;

    // ---------------- estado del servidor ----------------
    private ulong clienteTutorialConductor = ulong.MaxValue;
    private int pasoPreparadoConductor = 0;
    private bool probandoConductor = false;
    private readonly List<GameObject> objetosTutorialConductor = new List<GameObject>();
    private readonly List<GameObject> minasObligatoriasConductor = new List<GameObject>();

    private GameObject prefabMinaCache, prefabPezCache, prefabTiburonCache;

    // La PC, o el modo sin red (sin NetworkManager). Una tablet DESCONECTADA no cuenta: no debe
    // mover el bote ni poner minas por su cuenta.
    private bool SoyServidorTutorial => IsSpawned ? IsServer : NetworkManager.Singleton == null;

    // =====================================================================
    // RED
    // =====================================================================
    private void SuscribirTutorialConductorRed()
    {
        conductorTutorialPasoNet.OnValueChanged += OnPasoTutorialConductorCambio;
        conductorTutorialCompletadoNet.OnValueChanged += OnTutorialConductorCompletadoCambio;
        if (!IsServer) AplicarZonaTutorialConductorLocal(conductorTutorialPasoNet.Value);
        else if (conductorTutorialPasoNet.Value != pasoTutorialConductorOffline) conductorTutorialPasoNet.Value = pasoTutorialConductorOffline;
    }

    private void DesuscribirTutorialConductorRed()
    {
        conductorTutorialPasoNet.OnValueChanged -= OnPasoTutorialConductorCambio;
        conductorTutorialCompletadoNet.OnValueChanged -= OnTutorialConductorCompletadoCambio;
    }

    private void OnTutorialConductorCompletadoCambio(bool previo, bool nuevo)
    {
        if (!EsConductor() || !nuevo) return;
        tutorialCompletado = true;
        if (panelMenuConductor != null && panelMenuConductor.activeInHierarchy) ActualizarBotonJugarConductor();
    }

    /// <summary>
    /// Tablet, al conectarse o RECONECTARSE: vuelve a la pantalla en la que estaba segun la PC
    /// (partida, su tutorial en el paso en que iba, su game over o el menu).
    /// </summary>
    private void RestaurarPantallaConductorLocal()
    {
        if (conductorTutorialCompletadoNet.Value) tutorialCompletado = true;
        // Lo completo mientras estaba sin red: se le avisa a la PC
        else if (tutorialCompletado) ConductorTutorialCompletadoServerRpc();

        if (isGameStartedNet.Value)
        {
            OcultarMenuYEmpezarLocal();
            return;
        }

        // Solo se cayo la red (la app siguio abierta): el panel sigue donde estaba, se le vuelve
        // a pedir a la PC que prepare ese paso (lo habia terminado al desconectarse)
        if (TutorialConductorAbierto)
        {
            tutorialConductorUI.ReenviarPaso();
            return;
        }

        // Cerro la app en pleno tutorial: sigue desde el paso en que iba
        byte pendiente = conductorTutorialPendienteNet.Value;
        if (pendiente >= 1 && pendiente <= 4 && !tutorialCompletado)
        {
            Debug.Log($"[TUTORIAL CONDUCTOR] Reconectado: sigue en el paso {pendiente}.");
            AbrirTutorialConductorLocal(pendiente);
            return;
        }

        if (conductorEnGameOverNet.Value)
        {
            MostrarGameOverLocal();
            return;
        }

        if (panelMenuConductor != null && panelMenuConductor.activeInHierarchy) ActualizarBotonJugarConductor();
    }

    /// <summary>Servidor: el conductor ya no esta en su game over (toco Reiniciar o Volver, o empezo la partida).</summary>
    private void ConductorSalioDelGameOverServidor()
    {
        if (IsSpawned && IsServer && conductorEnGameOverNet.Value) conductorEnGameOverNet.Value = false;
    }

    /// <summary>Servidor: la partida termino, el conductor queda en su game over.</summary>
    private void ConductorEntroAlGameOverServidor()
    {
        if (IsSpawned && IsServer && !conductorEnGameOverNet.Value) conductorEnGameOverNet.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConductorTutorialCompletadoServerRpc()
    {
        MarcarTutorialConductorCompletadoServidor();
    }

    /// <summary>Servidor: tutorial del conductor hecho (tecla T, o lo completo sin red). Ya no hay nada que retomar.</summary>
    private void MarcarTutorialConductorCompletadoServidor()
    {
        if (IsSpawned && IsServer && !conductorTutorialCompletadoNet.Value) conductorTutorialCompletadoNet.Value = true;
        if (IsSpawned && IsServer && conductorTutorialPendienteNet.Value != 0) conductorTutorialPendienteNet.Value = 0;
    }

    private void OnPasoTutorialConductorCambio(byte previo, byte nuevo)
    {
        if (IsServer) return; // la PC ya lo aplico
        AplicarZonaTutorialConductorLocal(nuevo);
    }

    /// <summary>Todos los dispositivos: enciende la zona y los objetos del paso.</summary>
    private void AplicarZonaTutorialConductorLocal(byte paso)
    {
        if (zonaTutorialConductor != null) zonaTutorialConductor.MostrarPaso(paso);
    }

    private void SetPasoTutorialConductor(byte paso)
    {
        pasoTutorialConductorOffline = paso;
        if (IsSpawned && IsServer && conductorTutorialPasoNet.Value != paso) conductorTutorialPasoNet.Value = paso;
        AplicarZonaTutorialConductorLocal(paso);
    }

    // =====================================================================
    // LO QUE PIDE LA TABLET
    // =====================================================================
    /// <summary>Tablet: paso 1..5. probando = false mientras se ve el slide, true durante la prueba.</summary>
    public void PedirPasoTutorialConductor(int paso, bool probando)
    {
        if (IsSpawned && !IsServer) PasoTutorialConductorServerRpc((byte)paso, probando);
        else PasoTutorialConductorServidor(paso, probando);
    }

    /// <summary>Tablet: termino (completado) o cancelo su tutorial.</summary>
    public void PedirFinTutorialConductor(bool completado)
    {
        if (IsSpawned && !IsServer) FinTutorialConductorServerRpc(completado);
        else FinTutorialConductorServidor(completado, false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PasoTutorialConductorServerRpc(byte paso, bool probando, ServerRpcParams rpcParams = default)
    {
        clienteTutorialConductor = rpcParams.Receive.SenderClientId;
        PasoTutorialConductorServidor(paso, probando);
    }

    [ServerRpc(RequireOwnership = false)]
    private void FinTutorialConductorServerRpc(bool completado)
    {
        FinTutorialConductorServidor(completado, false);
    }

    // =====================================================================
    // SERVIDOR
    // =====================================================================
    private void PasoTutorialConductorServidor(int paso, bool probando)
    {
        if (!SoyServidorTutorial) return;
        if (IsSpawned && isGameStartedNet.Value) return; // durante la partida no hay tutorial
        paso = Mathf.Clamp(paso, 1, 5);

        bool empezando = !EnTutorialConductor;
        SetPasoTutorialConductor((byte)paso);
        if (IsSpawned && conductorTutorialPendienteNet.Value != 0) conductorTutorialPendienteNet.Value = 0; // ya lo retomo
        if (empezando) EntrarTutorialConductorServidor();

        bool nuevoPaso = empezando || paso != pasoPreparadoConductor;
        pasoPreparadoConductor = paso;
        probandoConductor = probando;

        BoatController bc = boteObjeto != null ? boteObjeto.GetComponent<BoatController>() : null;

        if (nuevoPaso)
        {
            LimpiarObjetosTutorialConductor();
            if (bc != null)
            {
                bc.CancelarInmovilizacion();
                bc.CancelarTambaleo();
            }
            if (paso <= 4) ColocarBoteEnInicioPaso(paso); // el 5 es solo el slide final
            PrepararObjetosPaso(paso);
            Debug.Log($"[TUTORIAL CONDUCTOR] Paso {paso} preparado.");
        }

        bool mover = probando && paso >= 1 && paso <= 4;
        if (bc != null)
        {
            bc.SetZonaPermitida(mover && zonaTutorialConductor != null ? zonaTutorialConductor.AreaDe(paso) : null);
            bc.SetMovimientoHabilitado(mover);
        }
    }

    private void EntrarTutorialConductorServidor()
    {
        Debug.Log("[TUTORIAL CONDUCTOR] Empieza.");

        // El pescador sale del bote (si esta en su tutorial ya esta en su plataforma)
        if (!enTutorialPescador)
        {
            CambiarPadreJugador(true);
            ColocarPescadorEnPoseMenu();
            FijarVistaMenuPescador();
        }
    }

    /// <summary>
    /// completado: lo termino (se guarda en la PC para cuando se reconecte).
    /// porDesconexion: la tablet se desconecto en pleno tutorial: se guarda el paso para retomarlo.
    /// </summary>
    private void FinTutorialConductorServidor(bool completado, bool porDesconexion)
    {
        if (!SoyServidorTutorial) return;

        // El slide final (5) ya es tutorial completado aunque se haya desconectado ahi
        if (porDesconexion && pasoPreparadoConductor >= 5) completado = true;

        if (IsSpawned && IsServer)
        {
            if (completado && !conductorTutorialCompletadoNet.Value) conductorTutorialCompletadoNet.Value = true;

            byte pendiente = (byte)(porDesconexion && !completado && EnTutorialConductor
                ? Mathf.Clamp(pasoPreparadoConductor, 1, 4) : 0);
            if (conductorTutorialPendienteNet.Value != pendiente) conductorTutorialPendienteNet.Value = pendiente;
        }

        if (!EnTutorialConductor) return;

        Debug.Log($"[TUTORIAL CONDUCTOR] Termina (completado={completado}, desconexion={porDesconexion}).");

        LimpiarObjetosTutorialConductor();
        SetPasoTutorialConductor(0);
        pasoPreparadoConductor = 0;
        probandoConductor = false;
        clienteTutorialConductor = ulong.MaxValue;

        GuardarPosicionesIniciales();
        BoatController bc = boteObjeto != null ? boteObjeto.GetComponent<BoatController>() : null;
        if (bc != null)
        {
            bc.SetZonaPermitida(null);
            bc.SetMovimientoHabilitado(false);
            bc.SnapTo(botePosInicial, boteRotInicial);
        }

        // De vuelta al bote, salvo que el pescador siga en su propio tutorial
        if (!enTutorialPescador)
        {
            CambiarPadreJugador(false);
            ResetearPosicionesOriginales();
            FijarVistaMenuPescador();
        }
    }

    private void ColocarBoteEnInicioPaso(int paso)
    {
        if (boteObjeto == null || zonaTutorialConductor == null) return;
        Transform inicio = zonaTutorialConductor.InicioDe(paso);
        if (inicio == null) return;

        GuardarPosicionesIniciales();
        Vector3 pos = new Vector3(inicio.position.x, botePosInicial.y, inicio.position.z);
        Quaternion rot = Quaternion.Euler(0f, inicio.eulerAngles.y, 0f);

        BoatController bc = boteObjeto.GetComponent<BoatController>();
        if (bc != null) bc.SnapTo(pos, rot);
        else boteObjeto.transform.SetPositionAndRotation(pos, rot);
    }

    /// <summary>
    /// Pescador fuera del bote, quieto donde estaria en el menu (encima del punto de inicio del bote).
    /// Se usa en todos los dispositivos (ResetearPosicionesOriginales); en red manda la PC.
    /// </summary>
    private void ColocarPescadorEnPoseMenu()
    {
        GarantizarJugador();
        if (playerController == null) return;
        GuardarPosicionesIniciales();

        CharacterController cc = playerController.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Vector3 escala = boteObjeto != null ? boteObjeto.transform.lossyScale : Vector3.one;
        Vector3 pos = botePosInicial + boteRotInicial * Vector3.Scale(escala, posicionInicialPescador);
        playerController.transform.SetPositionAndRotation(pos, boteRotInicial);

        Physics.SyncTransforms();
        TeleportRedJugador();
    }

    private void PrepararObjetosPaso(int paso)
    {
        ZonaTutorialConductor z = zonaTutorialConductor;
        if (z == null)
        {
            Debug.LogWarning("[TUTORIAL CONDUCTOR] Falta zonaTutorialConductor: usa Fishing Rush > Crear tutorial del conductor.");
            return;
        }

        switch (paso)
        {
            case 2:
                if (z.minasObligatoriasPaso2 != null)
                {
                    foreach (Transform p in z.minasObligatoriasPaso2)
                    {
                        GameObject m = SpawnMinaTutorial(p, false);
                        if (m != null) minasObligatoriasConductor.Add(m);
                    }
                }
                // Todas las minas derivan como en la partida (la fila puede dejar huecos a ratos)
                SpawnVarias(z.minasPaso2, p => SpawnMinaTutorial(p, false));
                break;

            case 3:
                SpawnVarias(z.tiburonesPaso3, p => SpawnTiburonTutorial(p, true));
                break;

            case 4:
                SpawnVarias(z.circulosPaso4, SpawnPezTutorial);
                SpawnVarias(z.minasPaso4, p => SpawnMinaTutorial(p, false)); // en la zona abierta derivan como en la partida
                SpawnVarias(z.tiburonesPaso4, p => SpawnTiburonTutorial(p, false));
                break;
        }
    }

    private static void SpawnVarias(Transform[] puntos, System.Func<Transform, GameObject> spawn)
    {
        if (puntos == null) return;
        foreach (Transform p in puntos) spawn(p);
    }

    /// <summary>quieta = true: la mina no deriva. Hoy todas las del tutorial derivan como en la partida.</summary>
    private GameObject SpawnMinaTutorial(Transform punto, bool quieta)
    {
        if (punto == null) return null;
        if (prefabMinaCache == null)
        {
            MinaSpawner ms = FindObjectOfType<MinaSpawner>(true);
            if (ms != null) prefabMinaCache = ms.minaPrefab;
        }

        return SpawnTutorial(prefabMinaCache, punto.position, Quaternion.Euler(0f, punto.eulerAngles.y, 0f), go =>
        {
            MinaExplosiva mina = go.GetComponent<MinaExplosiva>();
            if (mina != null) mina.SetQuieta(quieta);
        }, null);
    }

    private GameObject SpawnTiburonTutorial(Transform punto, bool chocarSiOSi)
    {
        if (punto == null) return null;
        if (prefabTiburonCache == null)
        {
            FishSpawner fs = FindObjectOfType<FishSpawner>(true);
            if (fs != null) prefabTiburonCache = fs.sharkPrefab;
        }

        return SpawnTutorial(prefabTiburonCache, punto.position, Quaternion.identity, go =>
        {
            FishCube fc = go.GetComponent<FishCube>();
            if (fc != null) fc.maxLifeTime = 100000f; // no desaparece por tiempo durante el tutorial
        }, go =>
        {
            FishCube fc = go.GetComponent<FishCube>();
            if (fc == null) return;
            if (chocarSiOSi) fc.ConfigurarEmbestidaTutorial(radioEmbestidaTutorial, duracionEmbestidaTutorial);
            else fc.PermitirEmbestida = true;
        });
    }

    private GameObject SpawnPezTutorial(Transform punto)
    {
        if (punto == null) return null;
        if (prefabPezCache == null)
        {
            FishSpawner fs = FindObjectOfType<FishSpawner>(true);
            if (fs != null) prefabPezCache = fs.fishPrefab;
        }

        return SpawnTutorial(prefabPezCache, punto.position, Quaternion.identity, go =>
        {
            FishCube fc = go.GetComponent<FishCube>();
            if (fc != null) fc.maxLifeTime = 100000f;
        }, null);
    }

    private GameObject SpawnTutorial(GameObject prefab, Vector3 pos, Quaternion rot,
                                     System.Action<GameObject> antesDeSpawn, System.Action<GameObject> despuesDeSpawn)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[TUTORIAL CONDUCTOR] Falta un prefab (mina en MinaSpawner, pez o tiburon en FishSpawner).");
            return null;
        }

        GameObject go = Instantiate(prefab, pos, rot);
        antesDeSpawn?.Invoke(go);

        NetworkObject no = go.GetComponent<NetworkObject>();
        if (no != null && IsSpawned && IsServer) no.Spawn(true);

        despuesDeSpawn?.Invoke(go);
        objetosTutorialConductor.Add(go);
        return go;
    }

    private void LimpiarObjetosTutorialConductor()
    {
        foreach (GameObject go in objetosTutorialConductor) DestruirObjetoTutorial(go);
        objetosTutorialConductor.Clear();
        minasObligatoriasConductor.Clear();
    }

    private static void DestruirObjetoTutorial(GameObject go)
    {
        if (go == null) return;
        NetworkObject no = go.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn(true);
        else Destroy(go);
    }

    /// <summary>Servidor, cada frame: al explotar una mina de la fila obligatoria se quitan las demas
    /// (si no, el bote las iria chocando una tras otra).</summary>
    private void ActualizarTutorialConductorServidor()
    {
        if (!SoyServidorTutorial || minasObligatoriasConductor.Count == 0) return;

        bool algunaExploto = false;
        foreach (GameObject m in minasObligatoriasConductor)
        {
            if (m == null) { algunaExploto = true; break; }
        }
        if (!algunaExploto) return;

        foreach (GameObject m in minasObligatoriasConductor) DestruirObjetoTutorial(m);
        minasObligatoriasConductor.Clear();
    }

    // =====================================================================
    // TABLET (local)
    // =====================================================================
    /// <summary>Tablet: abre su tutorial (paso 1, o el paso en que iba si se reconecto).</summary>
    private void AbrirTutorialConductorLocal(int paso = 1)
    {
        if (EsperandoConexion()) return;

        if (tutorialConductorUI == null)
        {
            Debug.LogWarning("[TUTORIAL CONDUCTOR] Falta tutorialConductorUI: usa Fishing Rush > Crear tutorial del conductor.");
            return;
        }

        OcultarPanelTablero();
        AjustarVolumenMusica(volumenJuego);

        if (canvasGameOverConductor != null) canvasGameOverConductor.SetActive(false);
        if (canvasMenuConductor != null) canvasMenuConductor.SetActive(true);
        if (panelMenuConductor != null) panelMenuConductor.SetActive(false);
        if (panelEsperaConductor != null) panelEsperaConductor.SetActive(false);

        ConfigurarCamarasPorRol();
        SetBoteVisible(true);
        // El pescador queda flotando donde estaria en el menu: en la tablet no se dibuja
        // (salvo que este en su plataforma haciendo su propio tutorial)
        SetRenderersPescador(enTutorialPescador);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        tutorialConductorUI.PasoAlAbrir = paso;
        tutorialConductorUI.gameObject.SetActive(true); // su OnEnable arranca en ese paso
    }

    /// <summary>Tablet: muestra u oculta los botones del bote (durante las pruebas).</summary>
    public void SetControlesTutorialConductor(bool activos)
    {
        SetControlesBoteActivos(activos);
        if (!activos) UIBoteInput.InputTactil = Vector2.zero;
    }

    /// <summary>Tablet: lo llama TutorialConductorUI al terminar (completado) o al cancelar.</summary>
    public void TerminarTutorialConductorLocal(bool completado)
    {
        if (completado) MarcarTutorialCompletadoLocal();
        finConductorCompletado = completado;
        MostrarMenuConductorLocal(); // cierra el panel y avisa a la PC (completado o no)
    }

    /// <summary>Tablet: cierra el panel del tutorial si estaba abierto y avisa a la PC.</summary>
    private void CerrarTutorialConductorUI()
    {
        if (tutorialConductorUI == null || !tutorialConductorUI.gameObject.activeSelf) return;

        bool completado = finConductorCompletado;
        finConductorCompletado = false;

        tutorialConductorUI.gameObject.SetActive(false);
        SetControlesTutorialConductor(false);
        PedirFinTutorialConductor(completado);
    }

    /// <summary>Tablet: el boton Jugar solo aparece con el tutorial del conductor completado.</summary>
    private void ActualizarBotonJugarConductor()
    {
        if (tileJugarConductor == null && panelMenuConductor != null)
        {
            Transform t = panelMenuConductor.transform.Find("TilesGrid/Tile_jugar");
            if (t != null) tileJugarConductor = t.gameObject;
        }

        if (tileJugarConductor != null && tileJugarConductor.activeSelf != tutorialCompletado)
            tileJugarConductor.SetActive(tutorialCompletado);
    }

    private void SetRenderersPescador(bool visible)
    {
        GarantizarJugador();
        if (playerController == null) return;
        foreach (Renderer r in playerController.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }
}
