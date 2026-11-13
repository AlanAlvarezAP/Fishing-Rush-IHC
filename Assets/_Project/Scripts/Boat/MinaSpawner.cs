using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MinaSpawner : MonoBehaviour
{
    public GameObject minaPrefab;

    [Header("Limites del Lago (los mismos del FishSpawner)")]
    public Transform lakeCenter;
    public Vector2 lakeSize = new Vector2(180f, 180f);
    [Tooltip("Altura (Y) a la que flotan las minas. El agua esta en Y = -2.")]
    public float alturaMina = -2f;

    [Header("Control de Poblacion")]
    public int maxMinas = 12;
    [Tooltip("Segundos hasta reponer una mina que ya exploto (0 = no se reponen)")]
    public float segundosReposicion = 15f;

    [Header("Distancias minimas (metros)")]
    public float distanciaMinimaAlBote = 20f;
    public float distanciaMinimaEntreMinas = 15f;
    [Tooltip("Profundidad minima del agua para poner una mina (evita la orilla y la tierra)")]
    public float profundidadMinima = 1.5f;

    private readonly List<GameObject> minas = new List<GameObject>();
    private bool activo = false;
    private float timer = 0f;
    private Transform bote;

    private static bool SinRed =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

    private static bool PuedeSpawnear => SinRed || NetworkManager.Singleton.IsServer;

    private void Update()
    {
        if (!activo || !PuedeSpawnear) return;

        minas.RemoveAll(m => m == null);

        if (segundosReposicion <= 0f || minas.Count >= maxMinas) return;

        timer += Time.deltaTime;
        if (timer >= segundosReposicion)
        {
            timer = 0f;
            SpawnMina();
        }
    }

    public void Reiniciar()
    {
        if (!PuedeSpawnear) return;

        ClearAll();
        activo = true;
        timer = 0f;

        for (int i = 0; i < maxMinas; i++) SpawnMina();

        Debug.Log($"[MINA] {minas.Count} minas colocadas en el lago.");
    }

    public void ClearAll()
    {
        activo = false;
        if (!PuedeSpawnear) return;

        foreach (GameObject mina in minas)
        {
            if (mina == null) continue;

            NetworkObject netObj = mina.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            else Destroy(mina);
        }
        minas.Clear();
    }

    private void SpawnMina()
    {
        if (minaPrefab == null) return;

        if (!BuscarPosicionLibre(out Vector3 pos)) return;

        GameObject mina = Instantiate(minaPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        if (!SinRed)
        {
            NetworkObject netObj = mina.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        minas.Add(mina);
    }

    private bool BuscarPosicionLibre(out Vector3 pos)
    {
        if (bote == null)
        {
            BoatController bc = FindObjectOfType<BoatController>();
            if (bc != null) bote = bc.transform;
        }

        Vector3 center = lakeCenter != null ? lakeCenter.position : transform.position;

        for (int intento = 0; intento < 30; intento++)
        {
            float x = Random.Range(center.x - lakeSize.x / 2f, center.x + lakeSize.x / 2f);
            float z = Random.Range(center.z - lakeSize.y / 2f, center.z + lakeSize.y / 2f);
            pos = new Vector3(x, alturaMina, z);

            if (bote != null && DistanciaPlana(pos, bote.position) < distanciaMinimaAlBote) continue;
            if (HayMinaCerca(pos)) continue;
            if (!HayAguaSuficiente(pos)) continue;

            return true;
        }

        pos = Vector3.zero;
        return false;
    }

    private bool HayMinaCerca(Vector3 pos)
    {
        foreach (GameObject mina in minas)
        {
            if (mina == null) continue;
            if (DistanciaPlana(pos, mina.transform.position) < distanciaMinimaEntreMinas) return true;
        }
        return false;
    }

    private bool HayAguaSuficiente(Vector3 pos)
    {
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) return true;

        float alturaFondo = terreno.transform.position.y + terreno.SampleHeight(pos);
        return alturaFondo < alturaMina - profundidadMinima;
    }

    private static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = lakeCenter != null ? lakeCenter.position : transform.position;
        Gizmos.DrawWireCube(new Vector3(center.x, alturaMina, center.z), new Vector3(lakeSize.x, 0.5f, lakeSize.y));
    }
}
