using UnityEngine;
using Unity.Netcode;

public class FishSpawner : NetworkBehaviour
{
    public GameObject fishPrefab;

    [Header("Limites del Lago")]
    public Transform lakeCenter;
    public Vector2 lakeSize = new Vector2(180f, 180f);
    public float waterSurfaceY = -3.75f;

    [Header("Control de Poblacion")]
    public int maxFishCount = 20;
    public float spawnInterval = 5f;
    private float timer = 0f;

    // TIBURONES: aparecen por su cuenta, independientes de los peces (antes solo podian salir
    // "en lugar de" un pez nuevo, y con el lago lleno de peces no salia ninguno).
    // Los nombres de los campos son nuevos a proposito: asi toman estos valores por defecto y no
    // los que habia guardados en la escena con la version anterior.
    [Header("Tiburones (independientes de los peces)")]
    [Tooltip("Prefab del tiburon (lo crea el menu Fishing Rush > Crear tiburon)")]
    public GameObject sharkPrefab;
    [Tooltip("Tiburones que aparecen apenas empieza la partida")]
    public int tiburonesAlInicio = 1;
    [Tooltip("Maximo de tiburones vivos a la vez")]
    public int maxTiburonesVivos = 3;
    [Tooltip("Probabilidad de que aparezca otro tiburon en cada intento (0.5 = 50%)")]
    [Range(0f, 1f)] public float probabilidadAparicionTiburon = 0.5f;
    [Tooltip("Cada cuantos segundos se intenta (con la probabilidad de arriba) que aparezca otro tiburon")]
    public float segundosEntreIntentosTiburon = 15f;
    [Tooltip("Un tiburon nunca aparece a menos de estos metros del bote")]
    public float distanciaMinimaTiburonAlBote = 30f;

    private MenuMismaEscena menuCache;
    private Transform boteCache;
    private bool tiburonesIniciados = false;
    private float timerTiburon = 0f;

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        // --- Peces normales (los tiburones ya no cuentan para este maximo) ---
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            if (ContarPeces(false) < maxFishCount) SpawnFishInLake();
        }

        ActualizarTiburones();
    }

    void SpawnFishInLake()
    {
        if (fishPrefab == null) return;
        Spawnear(fishPrefab, PosicionAleatoriaEnLago());
    }

    // ---------------- TIBURONES ----------------
    private void ActualizarTiburones()
    {
        if (sharkPrefab == null) return;

        if (menuCache == null) menuCache = FindObjectOfType<MenuMismaEscena>();
        bool enPartida = menuCache == null || menuCache.PartidaEnCurso;

        // Fuera de la partida (menu, tutorial, game over) no aparecen tiburones
        if (!enPartida)
        {
            tiburonesIniciados = false;
            return;
        }

        // Al empezar la partida: los tiburones iniciales
        if (!tiburonesIniciados)
        {
            tiburonesIniciados = true;
            timerTiburon = 0f;

            int faltan = Mathf.Min(tiburonesAlInicio, maxTiburonesVivos) - ContarPeces(true);
            for (int i = 0; i < faltan; i++) SpawnTiburon();

            Debug.Log($"[TIBURON] Inicio de partida: {ContarPeces(true)} tiburon(es) en el lago.");
            return;
        }

        // Durante la partida: cada cierto tiempo, una probabilidad de que aparezca otro
        timerTiburon += Time.deltaTime;
        if (timerTiburon < segundosEntreIntentosTiburon) return;
        timerTiburon = 0f;

        if (ContarPeces(true) >= maxTiburonesVivos) return;
        if (Random.value >= probabilidadAparicionTiburon) return;

        SpawnTiburon();
    }

    private void SpawnTiburon()
    {
        if (!BuscarPosicionTiburon(out Vector3 posicion)) return;
        Spawnear(sharkPrefab, posicion);
        Debug.Log("[TIBURON] Aparecio un tiburon.");
    }

    private bool BuscarPosicionTiburon(out Vector3 posicion)
    {
        if (boteCache == null)
        {
            BoatController bote = FindObjectOfType<BoatController>();
            if (bote != null) boteCache = bote.transform;
        }

        for (int intento = 0; intento < 20; intento++)
        {
            posicion = PosicionAleatoriaEnLago();
            if (boteCache == null) return true;

            Vector3 d = boteCache.position - posicion;
            d.y = 0f;
            if (d.magnitude >= distanciaMinimaTiburonAlBote) return true;
        }

        posicion = Vector3.zero;
        return false;
    }

    // ---------------- HELPERS ----------------
    /// <summary>Cuenta tiburones (true) o peces normales (false) que siguen vivos en red.</summary>
    private int ContarPeces(bool tiburones)
    {
        int n = 0;
        foreach (FishCube pez in FindObjectsOfType<FishCube>())
        {
            if (pez.EsTiburon != tiburones) continue;
            // Los que se estan quitando este frame (despawn) ya no cuentan
            if (pez.NetworkObject != null && !pez.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) continue;
            n++;
        }
        return n;
    }

    private Vector3 PosicionAleatoriaEnLago()
    {
        Vector3 center = lakeCenter != null ? lakeCenter.position : transform.position;

        float randomX = Random.Range(center.x - lakeSize.x / 2f, center.x + lakeSize.x / 2f);
        float randomZ = Random.Range(center.z - lakeSize.y / 2f, center.z + lakeSize.y / 2f);

        return new Vector3(randomX, waterSurfaceY, randomZ);
    }

    private void Spawnear(GameObject prefab, Vector3 posicion)
    {
        GameObject nuevo = Instantiate(prefab, posicion, Quaternion.identity);

        NetworkObject netObj = nuevo.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            netObj.Spawn();
        }
    }

    public void ClearAllFish()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        GameObject[] existingFish = GameObject.FindGameObjectsWithTag("Fish");
        foreach (GameObject fish in existingFish)
        {
            if (fish != null)
            {
                NetworkObject netObj = fish.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                else
                {
                    Destroy(fish);
                }
            }
        }
    }

    public void ReiniciarSpawning()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        ClearAllFish();
        timer = spawnInterval;

        // Vuelve a poner los tiburones iniciales en cuanto empiece la partida
        tiburonesIniciados = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = lakeCenter != null ? lakeCenter.position : transform.position;
        Vector3 boxSize = new Vector3(lakeSize.x, 0.5f, lakeSize.y);
        Vector3 boxCenter = new Vector3(center.x, waterSurfaceY, center.z);
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}