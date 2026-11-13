using UnityEngine;
using Unity.Netcode;
using TMPro;

public class MinaExplosiva : NetworkBehaviour
{
    [Header("Efecto sobre el bote")]
    [SerializeField] private float segundosInmovil = 5f;

    [Header("Movimiento (deriva)")]
    [Tooltip("Metros que se aleja de su punto de origen (0 = quieta)")]
    [SerializeField] private float radioDeriva = 5f;
    [Tooltip("Segundos que tarda en dar una vuelta completa")]
    [SerializeField] private float segundosPorVuelta = 24f;
    [Tooltip("Metros que sube y baja con el oleaje")]
    [SerializeField] private float alturaOleaje = 0.12f;

    private Vector3 origen;
    private bool origenListo = false;
    private float fase = 0f;
    private float sentido = 1f;

    [Header("Explosion (opcionales)")]
    [Tooltip("Prefab de particulas. Si se deja vacio se usa una explosion simple hecha por codigo.")]
    [SerializeField] private GameObject efectoExplosion;
    [SerializeField] private AudioClip sonidoExplosion;
    [Range(0f, 1f)]
    [SerializeField] private float volumenExplosion = 1f;

    [Header("Simbolo de aviso sobre la mina (como el icono de los peces)")]
    [Tooltip("Imagen del simbolo. Si se deja vacio se dibuja un '!' rojo.")]
    [SerializeField] private Sprite iconoAviso;
    [Tooltip("Altura del simbolo sobre la mina (metros)")]
    [SerializeField] private float alturaIcono = 2.2f;
    [Tooltip("Alto del simbolo (metros)")]
    [SerializeField] private float tamanoIcono = 1.5f;
    [Tooltip("El simbolo solo aparece si el bote esta a menos de estos metros (0 = siempre visible)")]
    [SerializeField] private float distanciaAviso = 30f;
    [SerializeField] private Color colorAviso = new Color(1f, 0.15f, 0.1f);

    private Transform icono;
    private Camera camaraIcono;
    private static Transform boteCache;

    private NetworkVariable<bool> explotoNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private const float SEGUNDOS_ANTES_DE_DESPAWN = 0.3f;

    private bool yaExploto = false;

    private static bool SinRed =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        explotoNet.OnValueChanged += OnExplotoCambio;

        origen = transform.position;
        fase = (NetworkObjectId % 360UL) * Mathf.Deg2Rad;
        sentido = (NetworkObjectId % 2UL == 0UL) ? 1f : -1f;
        origenListo = true;

        if (explotoNet.Value) OcultarModelo();
    }

    public override void OnNetworkDespawn()
    {
        explotoNet.OnValueChanged -= OnExplotoCambio;
        base.OnNetworkDespawn();
    }

    private void Start()
    {
        CrearIcono();

        if (!origenListo)
        {
            origen = transform.position;
            fase = Random.Range(0f, Mathf.PI * 2f);
            origenListo = true;
        }
    }

    private void Update()
    {
        if (!origenListo || yaExploto || radioDeriva <= 0f || segundosPorVuelta <= 0.1f) return;
        if (IsSpawned && explotoNet.Value) return;

        double tiempo = IsSpawned ? NetworkManager.ServerTime.Time : (double)Time.time;
        double vueltas = tiempo / segundosPorVuelta;
        float angulo = fase + sentido * (float)((vueltas - System.Math.Floor(vueltas)) * 2.0 * System.Math.PI);

        transform.position = origen + new Vector3(
            Mathf.Cos(angulo) * radioDeriva,
            Mathf.Sin(angulo * 3f + fase) * alturaOleaje,
            Mathf.Sin(angulo) * radioDeriva * 0.6f);
    }

    private void CrearIcono()
    {
        GameObject go = new GameObject("IconoAviso");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * alturaIcono;

        if (iconoAviso != null)
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = iconoAviso;

            float altoSprite = iconoAviso.bounds.size.y;
            if (altoSprite > 0.0001f) go.transform.localScale = Vector3.one * (tamanoIcono / altoSprite);
        }
        else
        {
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "!";
            tmp.fontSize = tamanoIcono * 10f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = colorAviso;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 4f);
        }

        icono = go.transform;
        icono.localPosition = Vector3.up * alturaIcono;

        go.SetActive(false);
    }

    private void LateUpdate()
    {
        if (icono == null) return;

        if (yaExploto || (IsSpawned && explotoNet.Value))
        {
            Destroy(icono.gameObject);
            icono = null;
            return;
        }

        bool visible = true;
        if (distanciaAviso > 0f)
        {
            if (boteCache == null)
            {
                BoatController bc = FindObjectOfType<BoatController>();
                if (bc != null) boteCache = bc.transform;
            }

            if (boteCache != null)
            {
                Vector3 d = boteCache.position - transform.position;
                d.y = 0f;
                visible = d.sqrMagnitude <= distanciaAviso * distanciaAviso;
            }
        }

        if (icono.gameObject.activeSelf != visible) icono.gameObject.SetActive(visible);
        if (!visible) return;

        icono.localPosition = Vector3.up * (alturaIcono + 0.15f * Mathf.Sin(Time.time * 3f));

        if (camaraIcono == null || !camaraIcono.isActiveAndEnabled) camaraIcono = BuscarCamara();
        if (camaraIcono != null) icono.rotation = camaraIcono.transform.rotation;
    }

    private static Camera BuscarCamara()
    {
        Camera principal = Camera.main;
        if (principal != null && principal.targetTexture == null) return principal;

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.targetTexture == null) return cam;
        }
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (yaExploto) return;

        if (!SinRed && !(IsSpawned && IsServer)) return;

        if (other.isTrigger) return;
        BoatController bote = other.GetComponentInParent<BoatController>();
        if (bote == null) return;

        yaExploto = true;
        bote.Inmovilizar(segundosInmovil);

        if (SinRed)
        {
            MostrarExplosionLocal();
            Destroy(gameObject);
            return;
        }

        explotoNet.Value = true;
        Invoke(nameof(DespawnServidor), SEGUNDOS_ANTES_DE_DESPAWN);
    }

    private void DespawnServidor()
    {
        if (IsSpawned && IsServer) NetworkObject.Despawn(true);
    }

    private void OnExplotoCambio(bool anterior, bool nuevo)
    {
        if (!nuevo) return;
        MostrarExplosionLocal();
        OcultarModelo();
    }

    private void MostrarExplosionLocal()
    {
        Vector3 pos = transform.position;

        if (efectoExplosion != null)
        {
            GameObject fx = Instantiate(efectoExplosion, pos, Quaternion.identity);
            Destroy(fx, 5f);
        }
        else
        {
            ExplosionSimple.Crear(pos, MaterialParaExplosion());
        }

        ReproducirSonido();
    }

    private Material MaterialParaExplosion()
    {
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.transform == icono) continue;
            if (r.sharedMaterial != null) return r.sharedMaterial;
        }
        return null;
    }

    private void ReproducirSonido()
    {
        if (sonidoExplosion == null) return;

        GameObject go = new GameObject("SonidoExplosionMina");
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.clip = sonidoExplosion;
        fuente.volume = volumenExplosion;
        fuente.spatialBlend = 0f;
        fuente.playOnAwake = false;
        fuente.Play();
        Destroy(go, sonidoExplosion.length + 0.2f);
    }

    private void OcultarModelo()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }
    }
}
