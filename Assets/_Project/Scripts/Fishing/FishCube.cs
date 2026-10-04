using UnityEngine;

public class FishCube : MonoBehaviour
{
    private bool isHooked = false;
    private Transform targetHook;
    private Transform playerTransform;

    private float lifeTimer = 0f;
    private float maxLifeTime = 15f; // Desaparece a los 15s si no se pesca

    void Update()
    {
        if (!isHooked)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= maxLifeTime)
            {
                Destroy(gameObject);
                return;
            }
        }
        // Si ya está enganchado, hacemos que siga al anzuelo manualmente cada frame
        else if (targetHook != null)
        {
            // Mantiene la posición exactamente en el anzuelo sin heredar rotaciones locas
            transform.position = targetHook.position;
            transform.rotation = Quaternion.identity; // Opcional: fija la rotación recta

            // Comprobamos la distancia al jugador para sumar el punto
            if (playerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

                if (distanceToPlayer < 2.0f)
                {
                    if (FishingCounterManager.Instance != null)
                    {
                        FishingCounterManager.Instance.AddFish();
                    }
                    Destroy(gameObject);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isHooked) return;

        if (collision.gameObject.CompareTag("Hook") || collision.gameObject.name.Contains("Hook"))
        {
            Transform hookTransform = collision.transform;

            Transform playerTr = null;
            ThirdPController playerController = FindObjectOfType<ThirdPController>();
            if (playerController != null)
            {
                playerTr = playerController.transform;
            }

            HookMe(hookTransform, playerTr);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHooked) return;

        if (other.CompareTag("Hook") || other.name.Contains("Hook"))
        {
            Transform hookTransform = other.transform;

            Transform playerTr = null;
            ThirdPController playerController = FindObjectOfType<ThirdPController>();
            if (playerController != null)
            {
                playerTr = playerController.transform;
            }

            HookMe(hookTransform, playerTr);
        }
    }

    public void HookMe(Transform hook, Transform player)
    {
        if (isHooked) return;

        isHooked = true;
        targetHook = hook;
        playerTransform = player;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Desactivamos la gravedad y las físicas del pez para que no pelee con el anzuelo
            rb.isKinematic = true;
            rb.detectCollisions = false; // Evita colisiones fantasma mientras está atrapado
        }

        // ELIMINADO: Ya no usamos SetParent para evitar que herede giros erráticos
    }
}