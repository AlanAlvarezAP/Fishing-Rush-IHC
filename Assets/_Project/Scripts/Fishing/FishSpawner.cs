using UnityEngine;
using Unity.Netcode;

public class FishSpawner : NetworkBehaviour
{
    public GameObject fishPrefab;

    [Header("Limites del Lago")]
    public Transform lakeCenter;
    public Vector2 lakeSize = new Vector2(180f, 180f);
    public float waterSurfaceY = -2f;

    [Header("Control de Poblacion")]
    public int maxFishCount = 30;
    public float spawnInterval = 3f;
    private float timer = 0f;

    void Update()
    {
        if (!IsServer) return;
        
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

        GameObject newFish = Instantiate(fishPrefab, spawnPosition, Quaternion.identity);

        NetworkObject netObj = newFish.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
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