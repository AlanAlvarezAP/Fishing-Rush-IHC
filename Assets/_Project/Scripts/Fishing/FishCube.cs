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

    [Header("Tiburon: mordida (bloquea la cana)")]
    [SerializeField] private bool esTiburon = false;
    [Tooltip("Segundos que queda bloqueada la cana cuando el tiburon muerde el anzuelo")]
    [SerializeField] private float segundosBloqueo = 8f;
    [Tooltip("Segundos que le quita al bloqueo cada patron bien hecho")]
    [SerializeField] private float segundosPorPatron = 1f;

    [Header("Tiburon: choque con el bote (tambaleo)")]
    [Tooltip("Segundos que se tambalea el bote")]
    [SerializeField] private float segundosTambaleo = 3f;
    [Tooltip("Si el bote esta a menos de estos metros, el tiburon va por el")]
    [SerializeField] private float radioEmbestida = 25f;
    [Tooltip("Distancia al casco a la que cuenta como choque")]
    [SerializeField] private float radioChoque = 1.3f;
    [Tooltip("Velocidad del tiburon al embestir (el bote va a 8)")]
    [SerializeField] private float velocidadEmbestida = 5f;
    [Tooltip("Grados por segundo que gira al embestir")]
    [SerializeField] private float giroEmbestida = 120f;
    [Tooltip("Si en estos segundos no alcanza al bote, se rinde")]
    [SerializeField] private float duracionMaxEmbestida = 7f;
    [Tooltip("Segundos de descanso entre una embestida/choque y el siguiente")]
    [SerializeField] private float esperaEntreEmbestidas = 12f;

    public bool EsTiburon => esTiburon;

    private TiburonVisual tiburonVisual;
    private bool embistiendo = false;
    private bool huyendo = false;
    private float finEmbestida = 0f;
    private float finHuida = 0f;
    private float proximaEmbestida = 0f;
    private float proximoChoque = 0f;
    private Vector3 dirHuida = Vector3.forward;
    private static BoatController boteCache;

    private NetworkVariable<Vector3> posTiburonNet = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<float> yawTiburonNet = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private bool posTiburonRecibida = false;

    private bool EsAutoridad => !IsSpawned || IsServer;

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

        if (esTiburon) proximaEmbestida = Time.time + 5f;

        // Posicion inicial para los celulares (peces normales y tiburones)
        if (IsServer && fishMotion != null)
        {
            posTiburonNet.Value = fishMotion.transform.position;
            yawTiburonNet.Value = fishMotion.transform.eulerAngles.y;
        }
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
                    if (!IsServer)
                    {
                        fishMotion.SetAutoMotion(false);
                        //fishMotion.enabled = false;
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

        if (esTiburon && tiburonVisual != null)
        {
            tiburonVisual.SetAtacando(state == FishNetState.Fighting);
        }
    }

    private void Awake()
    {
        fishMotion = GetComponentInChildren<FishAlive.FishMotion>();
        tiburonVisual = GetComponentInChildren<TiburonVisual>(true);
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

                if (esTiburon && isFighting) cachedPlayerController.CancelarBloqueoTiburon();
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

            if (esTiburon && ActualizarEmbestida()) return;

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
                        if (esTiburon)
                            StartCoroutine(TiburonMordidaRoutine(targetHook, playerController));
                        else
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
        LateUpdateSincronizar();

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
            playerController.ReproducirInicioPatron();
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

            if (AnunciosUI.Instance != null)
            {
                AnunciosUI.Instance.MostrarGestoRequerido(expectedGesture, 1.5f);
            }

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

                MostrarMinigameUI(MinigameUIAccion.Direccion, (int)expectedGesture);
                MostrarMinigameUI(MinigameUIAccion.Patron, (int)expectedGesture, i + 1, totalTurns);
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

                    if (gestureDone && BoteTambaleando())
                    {
                        gestureDone = false;
                        if (playerController != null) playerController.ClearGestureWindow();
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
                MostrarMinigameUI(MinigameUIAccion.Feedback, 1);

                if (playerController != null) playerController.ReproducirResultadoPatron(true);
            }
            else
            {
                if (showDebug) Debug.Log($"--> FALLO: El tiempo se agotó.");
                MostrarMinigameUI(MinigameUIAccion.Feedback, 0);

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

        if (playerController != null)
        {
            playerController.ReproducirResultadoMinijuego(isVictory);
        }

        MostrarMinigameUI(MinigameUIAccion.Final, isVictory ? 1 : 0);

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

    private static BoatController ObtenerBote()
    {
        if (boteCache == null) boteCache = FindObjectOfType<BoatController>();
        return boteCache;
    }

    private static bool BoteTambaleando()
    {
        BoatController bote = ObtenerBote();
        return bote != null && bote.EstaTambaleando;
    }

    private IEnumerator TiburonMordidaRoutine(Transform hook, ThirdPController playerController)
    {
        if (IsServer)
        {
            currentNetState.Value = FishNetState.Fighting;
        }
        isApproaching = false;
        isFighting = true;
        embistiendo = false;
        huyendo = false;
        targetHook = hook;
        cachedPlayerController = playerController;
        if (playerController != null) playerController.isHookOccupied = true;

        if (!EsAutoridad) yield break;

        if (playerController == null)
        {
            isFighting = false;
            EscapeAndDespawn();
            yield break;
        }

        playerTransform = playerController.transform;

        if (fishMotion != null) fishMotion.enabled = false;
        if (!IsSpawned && tiburonVisual != null) tiburonVisual.SetAtacando(true);

        playerController.BloquearPorTiburon(segundosBloqueo);
        playerController.TriggerRumble(0.6f);
        playerController.ReproducirResultadoPatron(false);

        if (showDebug) Debug.LogWarning("[TIBURON] Mordio el anzuelo. Cana bloqueada.");

        yield return new WaitForSeconds(1.0f);

        int aciertos = 0;
        int intentos = 0;

        while (playerController != null && playerController.BloqueadoPorTiburon)
        {
            FishingGesture gesto = (FishingGesture)Random.Range(1, 5);

            if (AnunciosUI.Instance != null) AnunciosUI.Instance.MostrarGestoRequerido(gesto, 1.5f);
            playerController.SetRequestedGesture(gesto, aciertos, intentos);
            playerController.TriggerRumble(0.2f);
            MostrarMinigameUI(MinigameUIAccion.Direccion, (int)gesto);
            playerController.ClearGestureWindow();

            bool acerto = false;
            float t = 0f;

            while (t < turnDuration && !acerto && playerController != null && playerController.BloqueadoPorTiburon)
            {
                t += Time.deltaTime;

                bool hecho = Input.GetKeyDown(KeyCode.R) || playerController.ValidateSpecificGesture(gesto);
                if (hecho && BoteTambaleando())
                {
                    hecho = false;
                    playerController.ClearGestureWindow();
                }
                if (hecho) acerto = true;

                yield return null;
            }

            if (playerController == null) break;

            intentos++;
            if (acerto)
            {
                aciertos++;
                playerController.ReducirBloqueoTiburon(segundosPorPatron);
                playerController.ReproducirResultadoPatron(true);
                MostrarMinigameUI(MinigameUIAccion.Feedback, 1);
                playerController.ClearGestureWindow();
                if (showDebug) Debug.Log($"[TIBURON] Patron correcto: -{segundosPorPatron:0.0} s.");
            }
            else if (playerController.BloqueadoPorTiburon)
            {
                playerController.ReproducirResultadoPatron(false);
                MostrarMinigameUI(MinigameUIAccion.Feedback, 0);
            }

            playerController.SetRequestedGesture(gesto, aciertos, intentos);

            yield return new WaitForSeconds(0.3f);
        }

        isFighting = false;

        if (playerController != null)
        {
            playerController.CancelarBloqueoTiburon();
            playerController.isHookOccupied = false;
            playerController.currentRequestedGesture = FishingGesture.None;
        }

        if (showDebug) Debug.Log($"[TIBURON] Solto el anzuelo ({aciertos} patrones correctos).");

        EscapeAndDespawn();
    }

    private bool ActualizarEmbestida()
    {
        if (!EsAutoridad || fishMotion == null) return false;

        BoatController bote = ObtenerBote();
        if (bote == null || !bote.MovimientoHabilitado)
        {
            if (embistiendo || huyendo) VolverANadar();
            return false;
        }

        Transform t = fishMotion.transform;
        float ahora = Time.time;

        if (huyendo)
        {
            MoverTiburon(t, t.position + dirHuida * 10f);
            if (ahora >= finHuida) VolverANadar();
            return true;
        }

        if (isApproaching) return false;

        float dist = bote.DistanciaHorizontalAlCasco(t.position);

        if (dist <= radioChoque && ahora >= proximoChoque)
        {
            ChocarConBote(bote, t);
            return true;
        }

        if (!embistiendo)
        {
            if (ahora < proximaEmbestida || dist > radioEmbestida) return false;

            embistiendo = true;
            finEmbestida = ahora + duracionMaxEmbestida;
            fishMotion.enabled = false;
            Debug.Log($"[TIBURON] Embiste al bote (estaba a {dist:0} m).");
        }

        MoverTiburon(t, bote.transform.position);

        if (ahora >= finEmbestida)
        {
            proximaEmbestida = ahora + esperaEntreEmbestidas;
            VolverANadar();
        }
        return true;
    }

    private void ChocarConBote(BoatController bote, Transform t)
    {
        float ahora = Time.time;
        proximoChoque = ahora + esperaEntreEmbestidas;
        proximaEmbestida = ahora + esperaEntreEmbestidas;

        bote.Tambalear(segundosTambaleo);
        MostrarAtaqueTiburon();

        foreach (ThirdPController pc in FindObjectsOfType<ThirdPController>())
        {
            pc.TriggerRumble(0.6f);
        }

        embistiendo = false;
        huyendo = true;
        finHuida = ahora + 3f;
        dirHuida = t.position - bote.transform.position;
        dirHuida.y = 0f;
        dirHuida = dirHuida.sqrMagnitude > 0.01f ? dirHuida.normalized : -t.forward;
        fishMotion.enabled = false;
    }

    private void MoverTiburon(Transform t, Vector3 destino)
    {
        Vector3 dir = destino - t.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion objetivo = Quaternion.LookRotation(dir.normalized, Vector3.up);
            t.rotation = Quaternion.RotateTowards(t.rotation, objetivo, giroEmbestida * Time.deltaTime);
        }

        Vector3 adelante = t.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude > 0.0001f)
        {
            t.position += adelante.normalized * velocidadEmbestida * Time.deltaTime;
        }
    }

    private void VolverANadar()
    {
        embistiendo = false;
        huyendo = false;

        if (fishMotion != null)
        {
            fishMotion.enabled = true;
            fishMotion.target = gameObject;
            fishMotion.SetReachMode(FishAlive.ReachMode.Wander);
            fishMotion.PingTarget();
        }
    }

    private void MostrarAtaqueTiburon()
    {
        if (tiburonVisual != null) tiburonVisual.AtacarUnaVez();
        if (IsSpawned && IsServer) AtaqueTiburonClientRpc();
    }

    [ClientRpc]
    private void AtaqueTiburonClientRpc()
    {
        if (IsServer) return; // la PC ya lo mostro localmente
        if (tiburonVisual != null) tiburonVisual.AtacarUnaVez();
    }

    /// <summary>
    /// Posicion del pez/tiburon: la PC (autoridad) la publica y los celulares la siguen.
    /// Se sincroniza SIEMPRE (no solo en la lucha): si no, cada dispositivo simula su propio nado
    /// (FishMotion) y en el VR un pez podia estar en otro lugar que en la PC hasta que mordia.
    /// En los celulares FishMotion se apaga; su Animator sigue animando el nado.
    /// (Las variables se llaman posTiburonNet / yawTiburonNet pero las usan todos los peces.)
    /// </summary>
    private void LateUpdateSincronizar()
    {
        if (fishMotion == null || isHooked) return;

        Transform t = fishMotion.transform;

        if (EsAutoridad)
        {
            if (esTiburon && !isFighting && !isApproaching)
            {
                Vector3 p = t.position;
                p.y = transform.position.y;
                t.position = p;
            }

            if (IsSpawned)
            {
                // Umbral de 2 cm / 0.5 grados para no mandar datos si el pez esta quieto
                if ((posTiburonNet.Value - t.position).sqrMagnitude > 0.0004f) posTiburonNet.Value = t.position;

                float yaw = t.eulerAngles.y;
                if (Mathf.Abs(Mathf.DeltaAngle(yawTiburonNet.Value, yaw)) > 0.5f) yawTiburonNet.Value = yaw;
            }
            return;
        }

        if (fishMotion.enabled) fishMotion.enabled = false;

        Vector3 destino = posTiburonNet.Value;
        if (destino == Vector3.zero) return;

        Quaternion rotDestino = Quaternion.Euler(0f, yawTiburonNet.Value, 0f);

        if (!posTiburonRecibida || (t.position - destino).sqrMagnitude > 400f)
        {
            posTiburonRecibida = true;
            t.SetPositionAndRotation(destino, rotDestino);
        }
        else
        {
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, destino, k), Quaternion.Slerp(t.rotation, rotDestino, k));
        }
    }

    // ---------------- UI DEL MINIJUEGO: PC (servidor) -> celulares ----------------
    // El minijuego solo corre en la PC; la UI del pez se pinta local y se reenvia por el
    // propio pez (es un NetworkObject spawneado, asi que su ClientRpc llega a los celulares).
    private enum MinigameUIAccion : byte
    {
        Direccion = 0,  // a = gesto (direccion de la flecha)
        Patron = 1,     // a = gesto, b = ronda actual, c = rondas totales
        Feedback = 2,   // a = 1 acierto / 0 fallo
        Final = 3       // a = 1 atrapado / 0 escapo
    }

    private void MostrarMinigameUI(MinigameUIAccion accion, int a, int b = 0, int c = 0)
    {
        AplicarMinigameUILocal(accion, a, b, c);
        if (IsSpawned && IsServer) MinigameUIClientRpc((byte)accion, a, b, c);
    }

    [ClientRpc]
    private void MinigameUIClientRpc(byte accion, int a, int b, int c)
    {
        if (IsServer) return; // la PC ya lo mostro localmente
        AplicarMinigameUILocal((MinigameUIAccion)accion, a, b, c);
    }

    private void AplicarMinigameUILocal(MinigameUIAccion accion, int a, int b, int c)
    {
        FishMinigameUI ui = GetMinigameUI();
        if (ui == null) return;

        try
        {
            switch (accion)
            {
                case MinigameUIAccion.Direccion:
                    ui.ShowDirection(a);
                    break;
                case MinigameUIAccion.Patron:
                    ui.UpdatePatternText((FishingGesture)a, b, c);
                    break;
                case MinigameUIAccion.Feedback:
                    ui.ShowFeedback(a == 1);
                    break;
                case MinigameUIAccion.Final:
                    ui.ShowFinalResult(a == 1);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MINIJUEGO][UI] Error aplicando " + accion + ": " + e.Message);
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
        if (esTiburon) return;

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