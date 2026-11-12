using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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

    // NUEVO (minas): hora del servidor en la que termina la inmovilizacion (0 = no esta inmovilizado).
    // Al ser NetworkVariable, el celular conductor tambien sabe cuanto falta (para su aviso).
    private NetworkVariable<double> finInmovilNet = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // NUEVO (minas): lo mismo pero sin red (pruebas offline), en Time.time
    private float finInmovilOffline = 0f;

    /// <summary>NUEVO (minas): segundos que faltan para que el bote pueda moverse otra vez (0 = libre).</summary>
    public float SegundosInmovilRestantes
    {
        get
        {
            if (IsSpawned) return Mathf.Max(0f, (float)(finInmovilNet.Value - NetworkManager.ServerTime.Time));
            return Mathf.Max(0f, finInmovilOffline - Time.time);
        }
    }

    /// <summary>NUEVO (minas): true mientras el bote esta inmovilizado por una mina.</summary>
    public bool EstaInmovilizado => SegundosInmovilRestantes > 0f;

    /// <summary>NUEVO (minas): deja el bote quieto durante 'segundos'. Solo lo decide la PC (servidor).</summary>
    public void Inmovilizar(float segundos)
    {
        // OJO: no se borra inputRemoto. El celular solo manda su input cuando cambia; si el
        // conductor sigue con el boton apretado, el bote arranca solo al terminar la mina.
        if (IsSpawned)
        {
            if (!IsServer) return;
            finInmovilNet.Value = NetworkManager.ServerTime.Time + segundos;
        }
        else
        {
            finInmovilOffline = Time.time + segundos;
        }

        Debug.Log($"[MINA] Bote inmovilizado {segundos:0.0} s.");
    }

    /// <summary>NUEVO (minas): quita la inmovilizacion (al empezar o reiniciar la partida).</summary>
    public void CancelarInmovilizacion()
    {
        finInmovilOffline = 0f;
        if (IsSpawned && IsServer) finInmovilNet.Value = 0d;
    }

    // ------------------------------------------------------------------
    // NUEVO (tiburon): tambaleo. Mismo patron que la mina: la PC guarda la hora de fin en una
    // NetworkVariable y cada dispositivo calcula su efecto por su cuenta.
    // ------------------------------------------------------------------
    [Header("Tambaleo (choque de tiburon)")]
    [Tooltip("Grados que se inclina la camara del conductor al inicio del tambaleo")]
    [SerializeField] private float amplitudTambaleo = 6f;
    [Tooltip("Grados que se inclina la vista del pescador (PC y VR). Menos que la del conductor: en VR marea")]
    [SerializeField] private float amplitudTambaleoPescador = 3f;
    [Tooltip("Velocidad del balanceo (10 = unos 1.6 balanceos por segundo)")]
    [SerializeField] private float frecuenciaTambaleo = 10f;

    // ------------------------------------------------------------------
    // NUEVO: direccion por inclinacion de la tablet (como un volante). Los pads siguen sirviendo:
    // adelante/atras siempre van por pad, e izquierda/derecha por pad tienen prioridad sobre la inclinacion.
    // ------------------------------------------------------------------
    [Header("Direccion por inclinacion (tablet del conductor)")]
    [SerializeField] private bool usarInclinacion = true;
    [Tooltip("Inclinacion que se ignora (0.12 = unos 7 grados), para que el bote no gire solo")]
    [SerializeField] private float zonaMuertaInclinacion = 0.12f;
    [Tooltip("Inclinacion con la que gira al maximo (0.5 = unos 30 grados)")]
    [SerializeField] private float inclinacionMaxima = 0.5f;
    [Tooltip("Marcalo si el bote gira al reves de como inclinas la tablet")]
    [SerializeField] private bool invertirInclinacion = false;

    private float proximoEnvioInput = 0f;
    // Puede haber mas de un jugador (ThirdPController) en la escena: se inclinan TODOS sus
    // "CameraOffset", asi no importa cual es el que esta mirando este dispositivo.
    private Transform[] camarasPescador = new Transform[0];
    private Quaternion[] rotBasePescador = new Quaternion[0];
    private float proximaBusquedaPescador = 0f;
    private bool tambaleoPescadorAplicado = false;

    private NetworkVariable<double> finTambaleoNet = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private float finTambaleoOffline = 0f;

    private Transform camaraTambaleo;
    private bool camaraBuscada = false;
    private Collider[] collidersCasco;

    /// <summary>NUEVO (tiburon): true durante la partida (el bote puede moverse).</summary>
    public bool MovimientoHabilitado => movimientoHabilitado;

    /// <summary>NUEVO (tiburon): segundos de tambaleo que quedan (0 = estable).</summary>
    public float SegundosTambaleoRestantes
    {
        get
        {
            if (IsSpawned) return Mathf.Max(0f, (float)(finTambaleoNet.Value - NetworkManager.ServerTime.Time));
            return Mathf.Max(0f, finTambaleoOffline - Time.time);
        }
    }

    /// <summary>NUEVO (tiburon): true mientras el bote se tambalea (los patrones del pescador no cuentan).</summary>
    public bool EstaTambaleando => SegundosTambaleoRestantes > 0f;

    /// <summary>NUEVO (tiburon): hace tambalear el bote 'segundos'. Solo lo decide la PC (servidor).</summary>
    public void Tambalear(float segundos)
    {
        if (IsSpawned)
        {
            if (!IsServer) return;
            finTambaleoNet.Value = NetworkManager.ServerTime.Time + segundos;
        }
        else
        {
            finTambaleoOffline = Time.time + segundos;
        }

        Debug.Log($"[TIBURON] Choque con el bote: tambaleo {segundos:0.0} s.");
    }

    /// <summary>NUEVO (tiburon): quita el tambaleo (al empezar o reiniciar la partida).</summary>
    public void CancelarTambaleo()
    {
        finTambaleoOffline = 0f;
        if (IsSpawned && IsServer) finTambaleoNet.Value = 0d;
    }

    /// <summary>
    /// NUEVO (tiburon): distancia en el plano del agua (se ignora la altura) entre un punto y el
    /// casco del bote. 0 = el punto esta debajo o dentro del casco.
    /// </summary>
    public float DistanciaHorizontalAlCasco(Vector3 punto)
    {
        if (collidersCasco == null)
        {
            // Los colliders solidos del bote, sin contar los del pescador que va encima
            var lista = new System.Collections.Generic.List<Collider>();
            foreach (BoxCollider c in GetComponentsInChildren<BoxCollider>())
            {
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<ThirdPController>() != null) continue;
                lista.Add(c);
            }
            collidersCasco = lista.ToArray();
        }

        float mejor = float.MaxValue;
        foreach (Collider c in collidersCasco)
        {
            if (c == null || !c.enabled) continue;

            Vector3 p = punto;
            p.y = c.bounds.center.y;
            Vector3 cercano = c.ClosestPoint(p);
            cercano.y = p.y;

            float d = Vector3.Distance(cercano, p);
            if (d < mejor) mejor = d;
        }

        if (mejor == float.MaxValue)
        {
            Vector3 a = punto; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            mejor = Vector3.Distance(a, b);
        }
        return mejor;
    }

    // ------------------------------------------------------------------
    // NUEVO (tiburon): balanceo visual. No se toca el bote ni sus colliders (el pescador va encima).
    //
    // Pantalla normal (PC y tablet): la camara se inclina JUSTO ANTES de dibujarse y se devuelve a
    // su sitio justo despues. Asi funciona con cualquier camara, la mueva quien la mueva: la del
    // conductor (BoatCamera) y la del pescador en la PC, que es "Main Camera" en tercera persona
    // y la controla Cinemachine (por eso inclinarla en LateUpdate no servia: Cinemachine la pisaba).
    //
    // Celular VR: la rotacion de la camara la pone el visor, asi que se inclina su padre
    // ("CameraOffset") en LateUpdate.
    // ------------------------------------------------------------------
    private readonly Dictionary<Camera, Quaternion> rotacionesGuardadas = new Dictionary<Camera, Quaternion>();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += AntesDeDibujarCamara;
        RenderPipelineManager.endCameraRendering += DespuesDeDibujarCamara;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDeDibujarCamara;
        RenderPipelineManager.endCameraRendering -= DespuesDeDibujarCamara;
    }

    // Grados de inclinacion en este instante (0 si no hay tambaleo)
    private float GiroTambaleo(float amplitud)
    {
        float restante = SegundosTambaleoRestantes;
        if (restante <= 0f) return 0f;

        // Empieza fuerte y se va calmando en el ultimo segundo y medio
        float fuerza = Mathf.Clamp01(restante / 1.5f);
        return amplitud * fuerza * Mathf.Sin(Time.time * frecuenciaTambaleo);
    }

    private void AntesDeDibujarCamara(ScriptableRenderContext contexto, Camera cam)
    {
        if (cam == null || CamaraActivaUtil.EnVR) return;
        if (cam.cameraType != CameraType.Game || cam.targetTexture != null) return; // ni reflejos ni la del Wiimote

        if (!camaraBuscada)
        {
            camaraBuscada = true;
            camaraTambaleo = transform.Find("BoatCamera"); // la del conductor
        }

        bool esDelConductor = camaraTambaleo != null && cam.transform == camaraTambaleo;
        float giro = GiroTambaleo(esDelConductor ? amplitudTambaleo : amplitudTambaleoPescador);
        if (Mathf.Abs(giro) < 0.001f) return;

        Transform t = cam.transform;
        rotacionesGuardadas[cam] = t.rotation;
        t.rotation = t.rotation * Quaternion.Euler(0f, 0f, giro);
    }

    private void DespuesDeDibujarCamara(ScriptableRenderContext contexto, Camera cam)
    {
        if (cam == null) return;

        if (rotacionesGuardadas.TryGetValue(cam, out Quaternion original))
        {
            cam.transform.rotation = original;
            rotacionesGuardadas.Remove(cam);
        }
    }

    // Solo para el celular VR
    private void LateUpdate()
    {
        bool enVR = CamaraActivaUtil.EnVR;
        float giro = enVR ? GiroTambaleo(amplitudTambaleoPescador) : 0f;
        bool activo = Mathf.Abs(giro) > 0.001f;

        // La lista se refresca cada segundo, pero nunca en mitad de un tambaleo (para no guardar
        // como "rotacion normal" una rotacion ya inclinada)
        if (enVR && !activo && !tambaleoPescadorAplicado && Time.unscaledTime >= proximaBusquedaPescador)
        {
            proximaBusquedaPescador = Time.unscaledTime + 1f;

            var lista = new List<Transform>();
            foreach (ThirdPController pc in FindObjectsOfType<ThirdPController>())
            {
                Transform offset = pc.transform.Find("CameraOffset");
                if (offset != null) lista.Add(offset);
            }

            camarasPescador = lista.ToArray();
            rotBasePescador = new Quaternion[camarasPescador.Length];
            for (int i = 0; i < camarasPescador.Length; i++) rotBasePescador[i] = camarasPescador[i].localRotation;
        }

        if (activo)
        {
            Quaternion giroPescador = Quaternion.Euler(0f, 0f, giro);
            for (int i = 0; i < camarasPescador.Length; i++)
            {
                if (camarasPescador[i] != null) camarasPescador[i].localRotation = rotBasePescador[i] * giroPescador;
            }
            tambaleoPescadorAplicado = true;
        }
        else if (tambaleoPescadorAplicado)
        {
            for (int i = 0; i < camarasPescador.Length; i++)
            {
                if (camarasPescador[i] != null) camarasPescador[i].localRotation = rotBasePescador[i];
            }
            tambaleoPescadorAplicado = false;
        }
    }

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
            // NUEVO: como mucho ~16 envios por segundo (la inclinacion cambia en cada frame)
            if ((inputTablet - ultimoInputEnviado).sqrMagnitude > 0.0001f && Time.unscaledTime >= proximoEnvioInput)
            {
                proximoEnvioInput = Time.unscaledTime + 0.06f;
                ultimoInputEnviado = inputTablet;
                EnviarInputBoteServerRpc(inputTablet);
            }
        }

        if (IsServer || offline)
        {
            if (movimientoHabilitado && !EstaInmovilizado) // NUEVO (minas): quieto mientras dure la mina
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
        // Pads: adelante/atras (y) e izquierda/derecha (x)
        Vector2 pads = Vector2.zero;
        if (UIBoteInput.InputTactil.sqrMagnitude > 0.01f)
        {
            pads = UIBoteInput.InputTactil.normalized;
        }

        // NUEVO: si no se esta tocando izquierda/derecha, la direccion sale de inclinar la tablet
        float giro = pads.x;
        if (Mathf.Abs(pads.x) < 0.1f) giro = LeerInclinacion();

        return new Vector2(giro, pads.y);
    }

    // Devuelve de -1 (todo a la izquierda) a 1 (todo a la derecha), en pasos de 0.05.
    // Usa el acelerometro: al inclinar la tablet como un volante, la gravedad se reparte hacia
    // un lado de la pantalla. Solo en la tablet del conductor; en la PC siempre da 0.
    private float LeerInclinacion()
    {
        if (!usarInclinacion) return 0f;
        if (ConfigJugador.RolActual != TipoJugador.Jugador2_ConductorTablet) return 0f;
        if (!SystemInfo.supportsAccelerometer) return 0f;

        float x = Input.acceleration.x;
        if (invertirInclinacion) x = -x;

        float magnitud = Mathf.Abs(x);
        if (magnitud <= zonaMuertaInclinacion) return 0f;

        float rango = Mathf.Max(0.05f, inclinacionMaxima - zonaMuertaInclinacion);
        float valor = Mathf.Clamp01((magnitud - zonaMuertaInclinacion) / rango) * Mathf.Sign(x);

        return Mathf.Round(valor * 20f) / 20f;
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
        CancelarInmovilizacion(); // NUEVO (minas): cada partida empieza con el bote libre
        CancelarTambaleo();       // NUEVO (tiburon)
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
