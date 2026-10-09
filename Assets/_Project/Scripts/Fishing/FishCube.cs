using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class FishCube : NetworkBehaviour
{
    private bool isHooked = false;
    private bool isApproaching = false;
    private bool isFighting = false;

    private Transform targetHook;
    private Transform playerTransform;

    private float lifeTimer = 0f;
    public float maxLifeTime = 90f;

    [Header("UI del Pez")]
    [SerializeField] private FishTimerUI timerUI;
    [SerializeField] private float uiHeightOffset = 3.5f;

    [Header("Configuracion de Mordida y Lucha")]
    [SerializeField] private float biteDistance = 2.0f;
    [SerializeField] private float turnDuration = 2.0f;
    [SerializeField] private int totalTurns = 5;
    [SerializeField] private float evasiveDistance = 25.0f;
    [SerializeField] private FishAlive.SwimConfig swimAgileConfig;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private FishAlive.FishMotion fishMotion;
    private GameObject evasiveTargetObject;

    private void Awake()
    {
        fishMotion = GetComponentInChildren<FishAlive.FishMotion>();
    }

    private void OnDestroy()
    {
        if (evasiveTargetObject != null)
        {
            Destroy(evasiveTargetObject);
        }
    }

    void Update()
    {
        if (!isHooked && !isFighting)
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

            if (isApproaching && targetHook != null && fishMotion != null)
            {
                float distToHook = Vector3.Distance(fishMotion.transform.position, targetHook.position);

                if (Time.frameCount % 15 == 0)
                {
                    Debug.Log($"[APROXIMANDO] Distancia actual al anzuelo: {distToHook:F2}m | Requerida: {biteDistance:F2}m");
                }

                if (distToHook <= biteDistance)
                {
                    Debug.Log("Pez mordio el anzuelo! Iniciando EvasiveFightRoutine...");
                    ThirdPController playerController = FindObjectOfType<ThirdPController>();
                    Transform playerTr = playerController != null ? playerController.transform : null;

                    StartCoroutine(EvasiveFightRoutine(targetHook, playerTr));
                }
            }
        }
        else if (isFighting)
        {
            if (targetHook != null && fishMotion != null)
            {
                targetHook.position = fishMotion.transform.position;
            }
        }
        else if (isHooked)
        {
            if (timerUI != null && timerUI.gameObject.activeSelf)
            {
                timerUI.gameObject.SetActive(false);
            }

            if (targetHook != null)
            {
                Vector3 currentFishPos = transform.position;

                if (fishMotion != null)
                {
                    fishMotion.transform.position = targetHook.position;
                    fishMotion.transform.rotation = Quaternion.identity;
                    currentFishPos = fishMotion.transform.position;
                }
                else
                {
                    transform.position = targetHook.position;
                    transform.rotation = Quaternion.identity;
                }

                if (playerTransform != null)
                {
                    float distanceToPlayer = Vector3.Distance(currentFishPos, playerTransform.position);

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
    }

    private void LateUpdate()
    {
        if (timerUI != null && timerUI.gameObject.activeSelf && fishMotion != null)
        {
            Vector3 fishPos = fishMotion.transform.position;

            float fixedY = transform.position.y + uiHeightOffset;

            timerUI.transform.position = new Vector3(fishPos.x, fixedY, fishPos.z);

            if (Camera.main != null)
            {
                timerUI.transform.rotation = Quaternion.LookRotation(timerUI.transform.position - Camera.main.transform.position);
            }
        }
    }

    private IEnumerator EvasiveFightRoutine(Transform hook, Transform player)
    {
        isApproaching = false;
        isFighting = true;
        targetHook = hook;
        playerTransform = player;

        if (evasiveTargetObject == null)
        {
            evasiveTargetObject = new GameObject($"EvasiveTarget_{gameObject.name}");
        }

        float fixedWaterY = transform.position.y;

        if (fishMotion != null)
        {
            Vector3 currentEuler = fishMotion.transform.eulerAngles;
            fishMotion.transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);

            if (swimAgileConfig != null)
            {
                fishMotion.SetConfig(swimAgileConfig);
            }
            fishMotion.SetReachMode(FishAlive.ReachMode.Position);
        }

        float currentSide = Random.value > 0.5f ? 1f : -1f; 

        for (int i = 0; i < totalTurns; i++)
        {
            if (playerTransform != null && fishMotion != null)
            {
                float angleOffset = Random.Range(70f, 90f) * currentSide;
                Vector3 targetPos = CalculateEvasivePosition(angleOffset, evasiveDistance, fixedWaterY);
                evasiveTargetObject.transform.position = targetPos;

                fishMotion.target = evasiveTargetObject;
                fishMotion.PingTarget();

                fishMotion.StartTurnTowardsTarget(targetPos, abortExisting: true, turnVelocityMultiplier: 1.0f);

                if (showDebug)
                {
                    string lado = currentSide > 0 ? "DERECHA" : "IZQUIERDA";
                    Debug.Log($"[LUCHA] Tirón {i + 1}/{totalTurns} hacia la {lado} | Posición Objetivo: {targetPos}");
                }
            }

            yield return new WaitForSeconds(turnDuration);

            currentSide *= -1f;
        }

        if (showDebug) Debug.Log("[LUCHA] Fin de la lucha. Pez agotado, enganchando.");

        isFighting = false;

        if (evasiveTargetObject != null)
        {
            Destroy(evasiveTargetObject);
        }

        HookMe(hook, player);
    }

    private Vector3 CalculateEvasivePosition(float angleOffsetDegrees, float distance, float fixedY)
    {
        Vector3 fishCurrentPos = fishMotion != null ? fishMotion.transform.position : transform.position;
        fishCurrentPos.y = fixedY;

        if (playerTransform == null) return fishCurrentPos;

        Vector3 playerToFish = fishCurrentPos - playerTransform.position;
        playerToFish.y = 0f;

        if (playerToFish == Vector3.zero) 
        {
            playerToFish = playerTransform.forward;
            playerToFish.y = 0f;
        }

        Quaternion rotation = Quaternion.Euler(0f, angleOffsetDegrees, 0f);
        Vector3 evasiveDir = rotation * playerToFish.normalized;

        Vector3 targetPos = playerTransform.position + (evasiveDir * distance);
        targetPos.y = fixedY;

        return targetPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHooked || isApproaching || isFighting) return;

        if (other.CompareTag("Hook") || other.name.Contains("Hook"))
        {
            targetHook = other.transform;
            isApproaching = true;

            if (fishMotion != null)
            {
                fishMotion.target = other.gameObject;
                fishMotion.SetReachMode(FishAlive.ReachMode.Position);
                fishMotion.PingTarget();
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
        isApproaching = false;
        isFighting = false;
        targetHook = hook;
        playerTransform = player;

        if (fishMotion != null)
        {
            fishMotion.SetAutoMotion(false);

            transform.position = fishMotion.transform.position;
            fishMotion.transform.localPosition = Vector3.zero;
        }

        if (targetHook != null)
        {
            targetHook.position = transform.position;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }
}
