using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class FishCube : NetworkBehaviour
{
    public enum FishingGesture { None, Horizontal, Vertical, Circle, Cross }

    private bool isHooked = false;
    private bool isApproaching = false;
    private bool isFighting = false;

    private Transform targetHook;
    private Transform playerTransform;
    private ThirdPController cachedPlayerController;

    private float lifeTimer = 0f;
    public float maxLifeTime = 90f;

    [Header("UI del Pez")]
    [SerializeField] private FishTimerUI timerUI;
    [SerializeField] private FishMinigameUI minigameUI;
    [SerializeField] private float uiHeightOffset = 3.5f;

    [Header("Configuracion de Mordida y Lucha")]
    [SerializeField] private float detectionRadius = 12.0f;
    [SerializeField] private float biteDistance = 2.25f;
    [SerializeField] private float turnDuration = 3.0f;
    [SerializeField] private float evasiveDistance = 30.0f;
    [SerializeField] private FishAlive.SwimConfig swimAgileConfig;

    [Header("Mecanica de Gestos")]
    [SerializeField] private int totalTurns = 4;
    [SerializeField] private int requiredSuccesses = 3;
    private int successfulFollows = 0;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private FishAlive.FishMotion fishMotion;
    private GameObject evasiveTargetObject;

    public enum FishNetState : byte
    {
        Swimming,
        Fighting,
        Hooked
    }

    private NetworkVariable<FishNetState> currentNetState = new NetworkVariable<FishNetState>(
        FishNetState.Swimming, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        currentNetState.OnValueChanged += OnFishStateChanged;

        ApplyNetStateLocally(currentNetState.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentNetState.OnValueChanged -= OnFishStateChanged;
    }

    private void OnFishStateChanged(FishNetState oldState, FishNetState newState)
    {
        ApplyNetStateLocally(newState);
    }

    private void ApplyNetStateLocally(FishNetState state)
    {
        switch (state)
        {
            case FishNetState.Swimming:
                isFighting = false;
                isHooked = false;
                if (fishMotion != null)
                {
                    fishMotion.enabled = true;
                    fishMotion.SetAutoMotion(true);
                }
                break;

            case FishNetState.Fighting:
                isFighting = true;
                isHooked = false;
                if (timerUI != null) timerUI.gameObject.SetActive(false);

                if (fishMotion != null)
                {
                    // En el cliente desactivamos la IA autónoma local para que no se desvíe del Host,
                    // pero mantenemos el componente enabled para que reproduzca animaciones
                    if (!IsServer)
                    {
                        fishMotion.SetAutoMotion(false);
                    }
                }
                break;

            case FishNetState.Hooked:
                isFighting = false;
                isHooked = true;
                if (timerUI != null) timerUI.gameObject.SetActive(false);

                if (fishMotion != null)
                {
                    fishMotion.SetAutoMotion(false);
                    fishMotion.SetSpeed(0f);
                    fishMotion.enabled = false; // Se apaga la animación al ser capturado
                    fishMotion.transform.localPosition = Vector3.zero;
                    fishMotion.transform.localRotation = Quaternion.identity;
                }

                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }
                break;
        }
    }

    private void Awake()
    {
        fishMotion = GetComponentInChildren<FishAlive.FishMotion>();
    }

    private FishMinigameUI GetMinigameUI()
    {
        if (minigameUI != null) return minigameUI;

        minigameUI = GetComponentInChildren<FishMinigameUI>(true);

        return minigameUI;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (isApproaching || isFighting || isHooked)
        {
            if (cachedPlayerController != null)
            {
                cachedPlayerController.isHookOccupied = false;
                cachedPlayerController.currentRequestedGesture = FishingGesture.None;
            }
        }

        if (evasiveTargetObject != null)
        {
            Destroy(evasiveTargetObject);
        }
    }

    private ThirdPController GetControllerFromHook(Transform hook)
    {
        if (cachedPlayerController != null) return cachedPlayerController;
        if (hook == null) return null;

        cachedPlayerController = hook.GetComponentInParent<ThirdPController>();
        if (cachedPlayerController == null)
        {
            cachedPlayerController = FindObjectOfType<ThirdPController>();
        }
        return cachedPlayerController;
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

            if (!isApproaching)
            {
                CheckForNearbyHook();
            }
            else if (isApproaching)
            {
                ThirdPController playerController = GetControllerFromHook(targetHook);

                if (playerController == null || !playerController.CanKeepApproaching())
                {
                    AbortApproach(playerController);
                }
                else if (targetHook != null && fishMotion != null)
                {
                    float distToHook = Vector3.Distance(fishMotion.transform.position, targetHook.position);

                    if (distToHook <= biteDistance)
                    {
                        StartCoroutine(EvasiveFightRoutine(targetHook, playerController.transform, playerController));
                    }
                }
            }
        }
        else if (isFighting)
        {
            if (targetHook == null)
            {
                GameObject hookObj = GameObject.FindWithTag("Hook");
                if (hookObj != null) targetHook = hookObj.transform;
            }

            if (targetHook != null && fishMotion != null)
            {
                if (IsServer)
                {
                    if (fishMotion != null)
                    {
                        targetHook.position = fishMotion.transform.position;
                    }
                }
                else
                {
                    transform.position = targetHook.position;
                    if (fishMotion != null)
                    {
                        fishMotion.transform.position = targetHook.position;
                    }
                }
            }
        }
        else if (isHooked)
        {
            if (timerUI != null && timerUI.gameObject.activeSelf)
            {
                timerUI.gameObject.SetActive(false);
            }

            if (targetHook == null)
            {
                GameObject hookObj = GameObject.FindWithTag("Hook");
                if (hookObj != null) targetHook = hookObj.transform;
            }

            if (targetHook != null)
            {
                transform.position = targetHook.position;
                transform.rotation = targetHook.rotation;

                if (fishMotion != null)
                {
                    fishMotion.transform.position = targetHook.position;
                    fishMotion.transform.rotation = targetHook.rotation;
                }

                if (playerTransform == null)
                {
                    ThirdPController pc = FindObjectOfType<ThirdPController>();
                    if (pc != null) playerTransform = pc.transform;
                }

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

                            ThirdPController playerController = GetControllerFromHook(targetHook);
                            if (playerController != null)
                            {
                                playerController.isHookOccupied = false;
                                playerController.currentRequestedGesture = FishingGesture.None;
                            }

                            DespawnFish();
                        }
                    }
                }
            }
        }
    }

    private void CheckForNearbyHook()
    {
        ThirdPController playerController = FindObjectOfType<ThirdPController>();
        if (playerController != null && playerController.CanFishBite())
        {
            GameObject hookObj = GameObject.FindWithTag("Hook");
            if (hookObj == null) hookObj = playerController.fishingRod;

            if (hookObj != null)
            {
                Vector3 currentPos = fishMotion != null ? fishMotion.transform.position : transform.position;
                float dist = Vector3.Distance(currentPos, hookObj.transform.position);

                if (dist <= detectionRadius)
                {
                    playerController.isHookOccupied = true;
                    targetHook = hookObj.transform;
                    isApproaching = true;

                    if (fishMotion != null)
                    {
                        fishMotion.target = hookObj;
                        fishMotion.SetReachMode(FishAlive.ReachMode.Position);
                        fishMotion.PingTarget();
                    }
                }
            }
        }
    }

    private void AbortApproach(ThirdPController pc)
    {
        isApproaching = false;
        targetHook = null;

        if (pc != null)
        {
            pc.isHookOccupied = false;
            pc.currentRequestedGesture = FishingGesture.None;
        }

        if (fishMotion != null)
        {
            fishMotion.SetReachMode(FishAlive.ReachMode.Wander);
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

    private IEnumerator EvasiveFightRoutine(Transform hook, Transform player, ThirdPController playerController)
    {
        if (IsServer)
        {
            currentNetState.Value = FishNetState.Fighting;
        }
        isApproaching = false;
        isFighting = true;
        targetHook = hook;
        playerTransform = player;
        successfulFollows = 0;

        if (playerController != null)
        {
            playerController.isHookOccupied = true;
        }

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

        FishMinigameUI ui = GetMinigameUI();

        for (int i = 0; i < totalTurns; i++)
        {
            if (playerController == null || !playerController.isFishing)
            {
                if (playerController != null)
                {
                    playerController.isHookOccupied = false;
                    playerController.currentRequestedGesture = FishingGesture.None;
                }
                EscapeAndDespawn();
                yield break;
            }

            FishingGesture expectedGesture = (FishingGesture)Random.Range(1, 5);

            if (playerController != null)
            {
                playerController.SetRequestedGesture(expectedGesture, successfulFollows, i);
                playerController.TriggerRumble(0.3f);
            }

            if (playerTransform != null && fishMotion != null)
            {
                float angleOffset = Random.Range(-90f, 90f);
                Vector3 targetPos = CalculateEvasivePosition(angleOffset, evasiveDistance, fixedWaterY);
                evasiveTargetObject.transform.position = targetPos;

                fishMotion.target = evasiveTargetObject;
                fishMotion.PingTarget();
                fishMotion.StartTurnTowardsTarget(targetPos, abortExisting: true, turnVelocityMultiplier: 1.0f);

                if (showDebug) Debug.LogWarning($"¡Capturar el pez!\nPatrón: {expectedGesture}");

                if (ui != null)
                {
                    ui.ShowDirection((int)expectedGesture);
                    ui.UpdatePatternText(expectedGesture, i + 1, totalTurns);
                }
            }

            bool matchedThisTurn = false;
            float turnTimer = 0f;

            if (playerController != null) playerController.ClearGestureWindow();

            while (turnTimer < turnDuration)
            {
                turnTimer += Time.deltaTime;

                if (!matchedThisTurn)
                {
                    //bool gestureDone = false;
                    bool gestureDone = Input.GetKeyDown(KeyCode.R);

                    if (!gestureDone)
                    {
                        if (playerController != null)
                            gestureDone = playerController.ValidateSpecificGesture(expectedGesture);
                        else
                            gestureDone = CheckKeyboardFallback(expectedGesture);
                    }

                    if (gestureDone)
                    {
                        Debug.Log("[DEBUG PESCA] Gesto/R completado con exito!");
                        matchedThisTurn = true;
                        if (playerController != null) playerController.ClearGestureWindow();
                    }
                }

                yield return null;
            }

            if (matchedThisTurn)
            {
                successfulFollows++;
                if (showDebug) Debug.Log($"--> ¡ÉXITO! Gesto correcto.");
                if (ui != null) ui.ShowFeedback(true);

                if (playerController != null) playerController.ReproducirResultadoPatron(true);
            }
            else
            {
                if (showDebug) Debug.Log($"--> FALLO: El tiempo se agotó.");
                if (ui != null) ui.ShowFeedback(false);

                if (playerController != null) playerController.ReproducirResultadoPatron(false);
            }

            if (playerController != null)
            {
                playerController.SetRequestedGesture(expectedGesture, successfulFollows, i + 1);
            }

            yield return new WaitForSeconds(0.4f);
        }

        isFighting = false;

        if (playerController != null)
        {
            playerController.currentRequestedGesture = FishingGesture.None;
        }

        if (evasiveTargetObject != null)
        {
            Destroy(evasiveTargetObject);
        }

        bool isVictory = successfulFollows >= requiredSuccesses;

        if (ui != null)
        {
            ui.ShowFinalResult(isVictory);
        }

        if (isVictory)
        {
            if (showDebug) Debug.Log($"[VICTORIA] {successfulFollows}/{totalTurns} aciertos. Pez capturado.");
            HookMe(hook, player);
        }
        else
        {
            if (showDebug) Debug.Log($"[DERROTA] Solo {successfulFollows}/{totalTurns} aciertos. El pez escapó.");

            if (playerController != null)
            {
                playerController.isHookOccupied = false;
                playerController.currentRequestedGesture = FishingGesture.None;
            }

            EscapeAndDespawn();
        }
    }

    private bool CheckKeyboardFallback(FishingGesture expected)
    {
        switch (expected)
        {
            case FishingGesture.Horizontal:
                return Input.GetAxisRaw("Horizontal") != 0;
            case FishingGesture.Vertical:
                return Input.GetAxisRaw("Vertical") != 0;
            case FishingGesture.Circle:
                return Input.GetKeyDown(KeyCode.O);
            case FishingGesture.Cross:
                return Input.GetKeyDown(KeyCode.X);
        }
        return false;
    }

    private void EscapeAndDespawn()
    {
        if (IsServer)
        {
            currentNetState.Value = FishNetState.Swimming;
        }

        if (fishMotion != null)
        {
            fishMotion.SetReachMode(FishAlive.ReachMode.Wander);
        }

        DespawnFish();

        isHooked = false;
        isApproaching = false;
        isFighting = false;

        DespawnFish();
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
        targetHook = hook;
        playerTransform = player;

        if (IsServer)
        {
            currentNetState.Value = FishNetState.Hooked;
        }
        /*
        if (isHooked) return;

        ApplyHookedStateLocally(hook, player);

        if (IsServer || IsHost)
        {
            HookMeClientRpc();
        }
        */
    }

    private void ApplyHookedStateLocally(Transform hook, Transform player)
    {
        isHooked = true;
        isApproaching = false;
        isFighting = false;
        targetHook = hook;
        playerTransform = player;

        if (fishMotion != null)
        {
            fishMotion.SetAutoMotion(false);
            fishMotion.transform.localPosition = Vector3.zero;
            fishMotion.transform.localRotation = Quaternion.identity;
        }

        if (targetHook != null)
        {
            transform.position = targetHook.position;
            transform.rotation = targetHook.rotation;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    [ClientRpc]
    private void HookMeClientRpc()
    {
        if (IsServer) return;

        GameObject hookObj = GameObject.FindWithTag("Hook");
        Transform hTransform = hookObj != null ? hookObj.transform : null;

        ThirdPController pc = FindObjectOfType<ThirdPController>();
        Transform pTransform = pc != null ? pc.transform : null;

        ApplyHookedStateLocally(hTransform, pTransform);
    }
}