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

        if (isHooked && targetHook != null && playerTransform != null)
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

    public void HookMe(Transform hook, Transform player)
    {
        if (isHooked) return;

        isHooked = true;
        targetHook = hook;
        playerTransform = player;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        transform.SetParent(hook);
        transform.localPosition = Vector3.zero;
    }
}