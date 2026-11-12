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

    [Header("Tiburones (NUEVO)")]
    [Tooltip("Prefab del tiburon (lo crea el menu Fishing Rush > Crear tiburon)")]
    public GameObject sharkPrefab;
    [Tooltip("Probabilidad de que lo que aparece sea un tiburon en vez de un pez (0.25 = 1 de cada 4)")]
    [Range(0f, 1f)] public float probabilidadTiburon = 0.25f;
    [Tooltip("Maximo de tiburones vivos a la vez")]
    public int maxTiburones = 2;
    [Tooltip("Un tiburon nunca aparece a menos de estos metros del bote")]
    public float distanciaMinimaTiburonAlBote = 30f;

    private MenuMismaEscena menuCache;
    private Transform boteCache;

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;
        
        int currentFishCount = GameObject.FindGameObjectsWithTag("Fish").Length;
        
        if (currentFishCount < maxFishCount)
        {
            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                timer = 0f;
                SpawnFishInLake();
            }
        }
    }

    void SpawnFishInLake()
    {
        if (fishPrefab == null) return;

        Vector3 center = lakeCenter != null ? lakeCenter.position : transform.position;

        float randomX = Random.Range(center.x - lakeSize.x / 2f, center.x + lakeSize.x / 2f);
        float randomZ = Random.Range(center.z - lakeSize.y / 2f, center.z + lakeSize.y / 2f);

        Vector3 spawnPosition = new Vector3(randomX, waterSurfaceY, randomZ);

        // NUEVO (tiburon): a veces, en vez de un pez, aparece un tiburon
        GameObject prefab = DebeSalirTiburon(spawnPosition) ? sharkPrefab : fishPrefab;

        GameObject newFish = Instantiate(prefab, spawnPosition, Quaternion.identity);

        NetworkObject netObj = newFish.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
    }

    // NUEVO (tiburon): solo durante la partida (no en el menu ni en el tutorial), con un maximo
    // de tiburones vivos y nunca pegado al bote.
    private bool DebeSalirTiburon(Vector3 posicion)
    {
        if (sharkPrefab == null) return false;
        if (Random.value >= probabilidadTiburon) return false;

        if (menuCache == null) menuCache = FindObjectOfType<MenuMismaEscena>();
        if (menuCache != null && !menuCache.PartidaEnCurso) return false;

        int tiburones = 0;
        foreach (FishCube pez in FindObjectsOfType<FishCube>())
        {
            if (pez.EsTiburon) tiburones++;
        }
        if (tiburones >= maxTiburones) return false;

        if (boteCache == null)
        {
            BoatController bote = FindObjectOfType<BoatController>();
            if (bote != null) boteCache = bote.transform;
        }
        if (boteCache != null)
        {
            Vector3 d = boteCache.position - posicion;
            d.y = 0f;
            if (d.magnitude < distanciaMinimaTiburonAlBote) return false;
        }

        return true;
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