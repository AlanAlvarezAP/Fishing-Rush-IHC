using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    public GameObject fishPrefab;       // El cubo prefab
    public Transform playerTransform;   // Arrastra aquí a tu personaje (Player)
    public float spawnInterval = 3f;    // Cada cuántos segundos aparece
    public float minDistance = 8f;      // Distancia mínima para que no salgan en la base
    public float maxDistance = 18f;     // Distancia máxima hacia adelante

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnFishStrictlyForward();
        }
    }

    void SpawnFishStrictlyForward()
    {
        if (fishPrefab == null)
        {
            Debug.LogWarning("¡Falta asignar el Prefab del pez en el Spawner!");
            return;
        }

        // Si no asignaste al jugador en el Inspector, usamos este objeto donde está el script
        Transform originTransform = playerTransform != null ? playerTransform : transform;

        // Tomamos la dirección hacia donde mira la CÁMARA (para saber exactamente el centro de tu pantalla)
        // pero si prefieres que sea hacia donde mira el cuerpo del jugador, cambia Camera.main por originTransform
        Transform lookDirectionSource = Camera.main != null ? Camera.main.transform : originTransform;

        // Un cono MUY estrecho al frente para que salgan solo donde miras en la pantalla (-15 a +15 grados)
        float forwardAngle = Random.Range(-15f, 15f);

        // Distancia aleatoria hacia adelante (fuera de la base)
        float randomDistance = Random.Range(minDistance, maxDistance);

        // Calculamos la dirección frontal basada en la vista de la cámara/jugador
        Vector3 spawnDirection = Quaternion.Euler(0, forwardAngle, 0) * lookDirectionSource.forward;
        spawnDirection.y = 0; // Aplanamos para que no floten al cielo si miras arriba/abajo
        spawnDirection.Normalize();

        // **LA CLAVE:** Partimos desde la posición DEL JUGADOR (no de la cámara), 
        // y avanzamos en línea recta hacia adelante según la distancia que configuraste.
        Vector3 spawnPosition = originTransform.position + (spawnDirection * randomDistance);

        // Forzamos la altura a la altura de los pies/suelo del jugador
        spawnPosition.y = originTransform.position.y;

        // Instanciamos el cubo
        Instantiate(fishPrefab, spawnPosition, Quaternion.identity);
    }
}