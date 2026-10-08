using UnityEngine;
using Unity.Netcode;

public class FishCube : NetworkBehaviour
{
    private bool isHooked = false;
    private Transform targetHook;
    private Transform playerTransform;

    private float lifeTimer = 0f;
    public float maxLifeTime = 90f;

    [Header("UI del Pez")]
    [SerializeField] private FishTimerUI timerUI;

    void Update()
    {
        if (!isHooked)
        {
            lifeTimer += Time.deltaTime;

            if (timerUI != null)
            {
                timerUI.UpdateTimer(lifeTimer, maxLifeTime);
            }

            if (IsServer && lifeTimer >= maxLifeTime)
            {
                DespawnFish();
                return;
            }
        }
        else
        {
            if (timerUI != null && timerUI.gameObject.activeSelf)
            {
                timerUI.gameObject.SetActive(false);
            }
        }

        if (targetHook != null)
        {
            transform.position = targetHook.position;
            transform.rotation = Quaternion.identity;

            if (playerTransform != null)
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
            rb.detectCollisions = false;
        }
    }
}
