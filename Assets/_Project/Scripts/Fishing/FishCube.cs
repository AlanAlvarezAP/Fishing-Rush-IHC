using UnityEngine;
using Unity.Netcode;

public class FishCube : NetworkBehaviour
{
    private bool isHooked = false;
    private Transform targetHook;
    private Transform playerTransform;

    private float lifeTimer = 0f;
    public float maxLifeTime = 60f;

    void Update()
    {
        if (IsServer && !isHooked)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= maxLifeTime)
            {
                DespawnFish();
                return;
            }
        }

        if (isHooked && targetHook != null && playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer < 2.0f)
            {
                if (IsServer)
                {
                    if (FishingCounterManager.Instance != null)
                    {
                        FishingCounterManager.Instance.AddFish();
                    }
                    DespawnFish();
                }
            }
        }
    }

    private void DespawnFish()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void HookMe(Transform hook, Transform player)
    {
        if (isHooked) return;

        isHooked = true;
        targetHook = hook;
        playerTransform = player;

        FishAlive.FishMotion motion = GetComponentInChildren<FishAlive.FishMotion>();
        if (motion != null)
        {
            motion.SetAutoMotion(false);
            motion.transform.localPosition = Vector3.zero;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        transform.SetParent(hook);
        transform.localPosition = Vector3.zero;
    }
}