using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class ThirdPController : NetworkBehaviour
{
    public CharacterController controller;
    public new Transform camera;
    public float speed = 6.0f;
    public float runSpeed = 12.0f;

    private float currentSpeed = 0.0f;

    public float turnSmoothingTime = 0.1f;
    float turnSmoothingVelocity;

    bool isGrounded;
    public float jumpHeight = 1.0f;
    public float jumpAdjustment = -2.0f;
    public float gravity = -9.81f;
    public float gravityFactor = 2.0f;

    public Vector3 playerVelocity;
    public bool bhopEnabled = false;

    public Animator animator;
    private CharacterInputController cinput;
    public float speedChangeRate = 0.1f;
    private float currentVelY = 0f;
    private float targetVelY = 0f;

    [Header("Estados de Pesca")]
    public bool alreadyCast = false;
    public bool isFishing = false;
    public bool isReeling = false;
    public bool isCastingInProcess = false;

    public bool isHookOccupied = false;
    private FishCube.FishingGesture _currentRequestedGesture = FishCube.FishingGesture.None;
    public FishCube.FishingGesture currentRequestedGesture
    {
        get => _currentRequestedGesture;
        set
        {
            _currentRequestedGesture = value;
            if (IsOwner && FishingHUDManager.Instance != null)
            {
                if (_currentRequestedGesture == FishCube.FishingGesture.None)
                {
                    FishingHUDManager.Instance.HideGesturePrompt();
                }
                else
                {
                    FishingHUDManager.Instance.ShowGesturePrompt(_currentRequestedGesture.ToString(), 0, 0);
                }
            }
        }
    }

    private NetworkVariable<Vector3> netHookPos = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );
    private NetworkVariable<Quaternion> netHookRot = new NetworkVariable<Quaternion>(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    //private NetworkVariable<Quaternion> netCameraRot = new NetworkVariable<Quaternion>(
    //    Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    //);

    // Solo el SERVIDOR escribe el yaw (lo recibe del celular VR por RPC). El resto solo lee.
    private NetworkVariable<float> netPlayerYaw = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // El dueño (PC) avisa si el anzuelo esta colgando de la caña (aun sin lanzar).
    // Asi el celular lo pega a SU propia punta de caña al instante, sin esperar la red.
    private NetworkVariable<bool> netHookHanging = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    // Estado "estoy pescando" replicado: los clientes lo usan para animar al modelo,
    // mostrar la caña, la linea y el anzuelo (no dependen de un RPC puntual).
    private NetworkVariable<bool> netIsFishing = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    [Header("Debug")]
    [SerializeField] private bool debugHookCliente = false;

    [Header("Referencias de Pesca")]
    public GameObject fishingRod;
    [SerializeField] private GameObject lineObject;
    [SerializeField] private GameObject hookObject;
    [SerializeField] private Transform rodTip;
    [SerializeField] private Collider hookCollider;

    [Header("Ajustes de Animacion y Cuelgue")]
    [SerializeField] private float castDelay = 1.75f;
    [SerializeField] private float reelSpeed = 15f;
    [SerializeField] private float hangDistance = 0.4f;
    [SerializeField] private float hookFollowSpeed = 12f;

    [Header("Ajustes de Agua")]
    public float waterSurfaceY = -2.0f;

    [Header("Ajustes de Recogida")]
    public float liftThreshold = 0.9f;
    public float waterRippleAmount = 0.1f;

    [Header("Otros")]
    private VerletLine verletScript;
    private float currentMaxLineLength = 0.5f;
    private Rigidbody hookRb;

    private Vector3 reelStartPos;
    private float reelTotalDistance;
    private float reelProgress;

    private int wiiCastState = 0;
    private float peakSwingForce = 0f;
    private float phase1Timer = 0f;
    [SerializeField] private float phase1MaxTime = 3.0f;

    [Header("Configuracion de Ventana de Movimiento (Wii Lanzamiento)")]
    public int windowSize = 25;
    private Queue<float> movementWindow = new Queue<float>();

    [Header("Ventana de Gestos (Lucha / Minijuego)")]
    private Queue<Vector2> gestureWindow = new Queue<Vector2>();
    public int gestureWindowSize = 40;

    [Header("Umbrales de Fuerza (Latigazo Dinamico)")]
    public float umbralMinimo = 0.6f;
    public float umbralAlto = 1.8f;

    [Header("Fuerza Aplicada al Anzuelo")]
    public float fuerzaBaja = 5f;
    public float fuerzaAlta = 35f;

    private bool wasWiiB = false;
    private bool wasWiiA = false;
    private bool wasWiiPlus = false;

    private float debugAccelMag = 0f;
    private float debugAccelY = 0f;
    private float debugCalculatedForce = 12f;
    #pragma warning disable CS0414
    private string debugState = "Iniciando...";
    #pragma warning restore CS0414

    [Header("Efectos de Sonido (SFX)")]
    public AudioSource sfxSource;
    public AudioClip sfxLanzamiento;
    public AudioClip sfxAciertoPatron;
    public AudioClip sfxFalloPatron;

    public AudioClip sfxHazPatron;
    public AudioClip sfxVictoriaMinijuego;
    public AudioClip sfxDerrotaMinijuego;

    private float lastSentYaw = -999f;
    private float sendTimer = 0f;
    private bool hasRemoteYaw = false;

    private NetworkVariable<double> finBloqueoTiburonNet = new NetworkVariable<double>(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private float finBloqueoTiburonOffline = 0f;

    public float SegundosBloqueoTiburon
    {
        get
        {
            if (IsSpawned) return Mathf.Max(0f, (float)(finBloqueoTiburonNet.Value - NetworkManager.ServerTime.Time));
            return Mathf.Max(0f, finBloqueoTiburonOffline - Time.time);
        }
    }

    public bool BloqueadoPorTiburon => SegundosBloqueoTiburon > 0f;

    public void BloquearPorTiburon(float segundos)
    {
        if (IsSpawned)
        {
            if (!IsServer) return;
            finBloqueoTiburonNet.Value = NetworkManager.ServerTime.Time + segundos;
        }
        else
        {
            finBloqueoTiburonOffline = Time.time + segundos;
        }
        Debug.Log($"[TIBURON] Cana bloqueada {segundos:0.0} s.");
    }

    public void ReducirBloqueoTiburon(float segundos)
    {
        if (IsSpawned)
        {
            if (!IsServer) return;
            finBloqueoTiburonNet.Value = finBloqueoTiburonNet.Value - segundos;
        }
        else
        {
            finBloqueoTiburonOffline -= segundos;
        }
    }

    public void CancelarBloqueoTiburon()
    {
        finBloqueoTiburonOffline = 0f;
        if (IsSpawned && IsServer && finBloqueoTiburonNet.Value != 0d) finBloqueoTiburonNet.Value = 0d;
    }

    public bool CanFishBite()
    {
        return isFishing && alreadyCast && !isReeling && !isCastingInProcess && !isHookOccupied;
    }

    public bool CanKeepApproaching()
    {
        return isFishing && alreadyCast && !isReeling && !isCastingInProcess;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (camera == null && Camera.main != null)
        {
            camera = Camera.main.transform;
        }

        cinput = GetComponent<CharacterInputController>();

        if (animator != null) animator.SetBool("startFishing", false);
        if (fishingRod != null) fishingRod.SetActive(false);

        if (lineObject != null)
        {
            verletScript = lineObject.GetComponent<VerletLine>();
            lineObject.SetActive(false);
        }

        if (hookObject != null)
        {
            hookRb = hookObject.GetComponent<Rigidbody>();
            if (hookCollider == null) hookCollider = hookObject.GetComponent<Collider>();
            if (hookCollider != null) hookCollider.isTrigger = false;

            if (hookRb != null)
            {
                hookRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            SetHookKinematic(true);
            hookObject.SetActive(false);
        }
    }

    void Update()
    {
        //Debug.Log($"[UPDATE RUNNING] scriptEnabled={enabled} | isFishing={isFishing}");
        //if (!IsOwner) return;
        var nm = NetworkManager.Singleton;
        bool offline = nm == null || !nm.IsListening;
        bool tieneControl = offline || IsOwner || IsServer;

        if (!tieneControl) return;

        if (!isFishing) debugState = "No pescando";
        else if (isCastingInProcess) debugState = "Cargando tiro...";
        else if (isReeling) debugState = "Recogiendo...";
        else if (alreadyCast) debugState = "En el agua";
        else if (wiiCastState == 1) debugState = "Cana Arriba (Lanza)";
        else debugState = "Levanta a 90 para preparar";

        bool wiiCastTrigger = false;
        bool wiiReelTrigger = false;
        bool wiiEquipTrigger = false;
        bool wiiJumpDown = false;
        bool wiiJumpHeld = false;
        float wiiHorizontal = 0f;
        float wiiVertical = 0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            float[] accel = wiimote.Accel.GetCalibratedAccelData();

            if (accel != null && accel.Length >= 3)
            {
                float accelZ = accel[2];
                float rawMag = new Vector3(accel[0], accel[1], accel[2]).magnitude;
                float dynamicAccelMag = Mathf.Max(0f, rawMag - 1.0f);

                debugAccelMag = dynamicAccelMag;
                debugAccelY = accel[1];

                gestureWindow.Enqueue(new Vector2(accel[0], accel[1]));
                if (gestureWindow.Count > gestureWindowSize)
                {
                    gestureWindow.Dequeue();
                }

                movementWindow.Enqueue(dynamicAccelMag);
                if (movementWindow.Count > windowSize)
                {
                    movementWindow.Dequeue();
                }

                if (!isFishing || alreadyCast || isCastingInProcess)
                {
                    if (wiiCastState != 0)
                    {
                        ResetWiiCastState();
                    }
                }
                else
                {
                    switch (wiiCastState)
                    {
                        case 0: // En reposo / Esperando que levantes la caña
                            if (Mathf.Abs(accelZ) > 0.88f && Mathf.Abs(accel[1]) < 0.35f && rawMag > 0.8f && rawMag < 1.2f)
                            {
                                wiiCastState = 1;
                                peakSwingForce = 0f;
                                phase1Timer = 0f;
                                movementWindow.Clear();
                            }
                            break;

                        case 1: // Caña levantada (Ventana de tiempo activa)
                            phase1Timer += Time.deltaTime;
                            float timeLeft = Mathf.Max(0f, phase1MaxTime - phase1Timer);

                            // Muestra/actualiza el tiempo restante en el HUD
                            if (IsOwner && FishingHUDManager.Instance != null)
                            {
                                FishingHUDManager.Instance.ShowCastWindowTimer(timeLeft);
                            }

                            // CASO A: Se agotó el tiempo (1.5 segundos)
                            if (phase1Timer > phase1MaxTime)
                            {
                                ResetWiiCastState(); // Se oculta el timer y vuelve a 0
                            }
                            else if (movementWindow.Count >= 5)
                            {
                                float maxAccel = 0f;
                                foreach (float val in movementWindow)
                                {
                                    if (val > maxAccel) maxAccel = val;
                                }

                                peakSwingForce = maxAccel;

                                // CASO B: Lanzaste con suficiente fuerza
                                if (maxAccel > umbralMinimo && dynamicAccelMag < maxAccel * 0.8f)
                                {
                                    wiiCastTrigger = true;
                                    float t = Mathf.InverseLerp(umbralMinimo, umbralAlto, maxAccel);
                                    debugCalculatedForce = Mathf.Lerp(fuerzaBaja, fuerzaAlta, t);

                                    ResetWiiCastState(); // Lanzamiento exitoso -> Se oculta el timer
                                    movementWindow.Clear();
                                }
                                // CASO C: Cancelaste o bajaste la caña despacio
                                else if (Mathf.Abs(accelZ) < 0.4f && Mathf.Abs(accel[1]) > 0.7f && rawMag < 1.2f)
                                {
                                    ResetWiiCastState(); // Cancelado -> Se oculta el timer
                                }
                            }
                            break;
                    }
                }
            }

            bool currentWiiB = wiimote.Button.b;
            if (currentWiiB && !wasWiiB) wiiEquipTrigger = true;
            wasWiiB = currentWiiB;

            bool currentWiiA = wiimote.Button.a;
            if (currentWiiA && !wasWiiA) wiiReelTrigger = true;
            wasWiiA = currentWiiA;

            bool currentWiiPlus = wiimote.Button.plus;
            if (currentWiiPlus && !wasWiiPlus) wiiJumpDown = true;
            wiiJumpHeld = currentWiiPlus;
            wasWiiPlus = currentWiiPlus;

            if (wiimote.Button.d_up) wiiVertical = 1f;
            if (wiimote.Button.d_down) wiiVertical = -1f;
            if (wiimote.Button.d_left) wiiHorizontal = -1f;
            if (wiimote.Button.d_right) wiiHorizontal = 1f;
        }
#endif

        if (controller.isGrounded && playerVelocity.y < 0)
        {
            if (animator != null) animator.SetBool("isGrounded", true);
            playerVelocity.y = 0f;
            playerVelocity.x = 0f;
            playerVelocity.z = 0f;
        }

        // --- EQUIPAR / DESEQUIPAR ---
        if ((Input.GetKeyDown(KeyCode.E) || wiiEquipTrigger) && !BloqueadoPorTiburon)
        {
            if (!isFishing)
            {
                if (animator != null) animator.SetBool("startFishing", true);
                isFishing = true;
                currentVelY = 0f;
                //targetVelY = 0f;
                if (animator != null) animator.SetFloat("velY", 0f);

                Vector3 initPos = rodTip != null ? rodTip.position + (Vector3.down * hangDistance) : transform.position;
                Quaternion initRot = rodTip != null ? rodTip.rotation : transform.rotation;

                if (hookObject != null && rodTip != null)
                {
                    if (hookRb != null && !hookRb.isKinematic)
                    {
                        hookRb.velocity = Vector3.zero;
                        hookRb.angularVelocity = Vector3.zero;
                    }

                    SetHookKinematic(true);
                    hookObject.transform.position = initPos;
                    hookObject.transform.rotation = rodTip.rotation;

                    if (hookRb != null)
                    {
                        hookRb.position = initPos;
                    }

                    var netTransform = hookObject.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                    if (netTransform != null)
                    {
                        if (netTransform.IsSpawned && (netTransform.IsServer || netTransform.IsOwner))
                        {
                            netTransform.Teleport(initPos, rodTip.rotation, hookObject.transform.localScale);
                        }
                    }

                    Physics.SyncTransforms();

                    if (hookCollider != null) hookCollider.isTrigger = false;
                }

                if (fishingRod != null) fishingRod.SetActive(true);
                if (lineObject != null) lineObject.SetActive(true);
                if (hookObject != null) hookObject.SetActive(true);
                if (verletScript != null) verletScript.ResetRope();

                SetFishingVisualsClientRpc(true, initPos, initRot);
            }
            else if (isFishing && !alreadyCast && !isReeling && !isCastingInProcess)
            {
                if (animator != null) animator.SetBool("startFishing", false);
                isFishing = false;
                isHookOccupied = false;
                currentRequestedGesture = FishCube.FishingGesture.None;
                if (fishingRod != null) fishingRod.SetActive(false);
                if (lineObject != null) lineObject.SetActive(false);
                if (hookObject != null) hookObject.SetActive(false);

                if (FishingHUDManager.Instance != null) FishingHUDManager.Instance.HideAll();
                SetFishingVisualsClientRpc(false, Vector3.zero, Quaternion.identity);
            }
        }

        // --- LANZAMIENTO ---
        if ((Input.GetMouseButtonDown(0) || wiiCastTrigger) && isFishing && !alreadyCast && !isReeling && !isCastingInProcess && !BloqueadoPorTiburon)
        {
            if (Input.GetMouseButtonDown(0)) debugCalculatedForce = 25f;
            animator.SetTrigger("cast");
            SyncAnimTrigger(0);
            ReproducirSonidoLanzamiento();
            StartCoroutine(ExecuteCastWithDelay(debugCalculatedForce));
        }

        // --- RECOGIDA ---
        bool inputReel = Input.GetKeyDown(KeyCode.Q) || wiiReelTrigger;
        if (inputReel && isFishing && alreadyCast && !isReeling && !isCastingInProcess && !BloqueadoPorTiburon)
        {
            StartReeling();
        }

        if (isReeling && animator != null)
        {
            if (reelTotalDistance > 0.1f)
            {
                reelProgress += (reelSpeed / reelTotalDistance) * Time.deltaTime;
            }
            else
            {
                reelProgress = 1f;
            }

            float clampedProgress = Mathf.Clamp01(reelProgress);

            if (hookObject != null && rodTip != null)
            {
                Vector3 targetEndPos = rodTip.position + (Vector3.down * hangDistance);
                float currentX = Mathf.Lerp(reelStartPos.x, targetEndPos.x, clampedProgress);
                float currentZ = Mathf.Lerp(reelStartPos.z, targetEndPos.z, clampedProgress);
                float currentY;

                if (clampedProgress < liftThreshold)
                {
                    float ripple = Mathf.Sin(Time.time * 20f) * waterRippleAmount;
                    currentY = waterSurfaceY + ripple;
                }
                else
                {
                    float liftNormalized = (clampedProgress - liftThreshold) / (1f - liftThreshold);
                    float smoothLift = Mathf.SmoothStep(0f, 1f, liftNormalized);
                    currentY = Mathf.Lerp(waterSurfaceY, targetEndPos.y, smoothLift);
                }

                Vector3 finalPos = new Vector3(currentX, currentY, currentZ);

                if (hookRb != null) hookRb.position = finalPos;
                else hookObject.transform.position = finalPos;
            }

            if (hookObject != null && rodTip != null)
            {
                currentMaxLineLength = Vector3.Distance(hookObject.transform.position, rodTip.position);
                if (verletScript != null) verletScript.SetTargetLength(currentMaxLineLength);
            }

            if (clampedProgress >= 1.0f)
            {
                FinishReeling();
            }
        }

        // --- MOVIMIENTO ---
        /*
        if (controller.isGrounded)
        {
            if (bhopEnabled)
            {
                if (Input.GetButton("Jump") || wiiJumpHeld) Jump();
            }
            else
            {
                if (Input.GetButtonDown("Jump") || wiiJumpDown) Jump();
            }
        }
        */

        if (!isFishing)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (wiiHorizontal != 0f) horizontal = wiiHorizontal;
            if (wiiVertical != 0f) vertical = wiiVertical;

            Vector3 inputDir = new Vector3(horizontal, 0f, vertical);

            //Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;
            //targetVelY = Mathf.Clamp01(inputDir.magnitude);

            if (inputDir.sqrMagnitude >= 0.01f)
            {
                /*
                Transform camTransform = camera != null ? camera : (Camera.main != null ? Camera.main.transform : transform);

                Vector3 camForward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;

                Vector3 moveDirection = (camForward * vertical + camRight * horizontal).normalized;
                */
                Transform targetModel = (animator != null && animator.transform != transform) ? animator.transform : transform;
                Vector3 moveDirection = (targetModel.forward * vertical + targetModel.right * horizontal).normalized;

                currentSpeed = speed;

                bool controllerListo = controller != null && controller.enabled;
                if (controllerListo)
                {
                    controller.Move(moveDirection * currentSpeed * Time.deltaTime);
                }
            }
        }

        // --- ACTUALIZACIÓN DE ANIMADOR Y GRAVEDAD ---
        currentVelY = Mathf.Lerp(currentVelY, targetVelY, speedChangeRate);
        if (animator != null)
        {
            animator.SetFloat("velY", currentVelY);
        }

        if (controller != null && controller.enabled)
        {
            if (controller.isGrounded && playerVelocity.y < 0)
            {
                if (animator != null) animator.SetBool("isGrounded", true);
                playerVelocity.y = -2f;
            }
            else
            {
                playerVelocity.y += gravityFactor * gravity * Time.deltaTime;
            }

            controller.Move(playerVelocity * Time.deltaTime);
        }
    }

    private IEnumerator ExecuteCastWithDelay(float force)
    {
        isCastingInProcess = true;
        yield return new WaitForSeconds(castDelay);

        isCastingInProcess = false;
        alreadyCast = true;
        isHookOccupied = false;
        currentRequestedGesture = FishCube.FishingGesture.None;

        if (IsOwner && FishingHUDManager.Instance != null)
        {
            FishingHUDManager.Instance.ShowCastPower(force);
        }

        if (hookObject != null && rodTip != null)
        {
            SetHookKinematic(false);

            if (hookRb != null)
            {
                hookRb.velocity = Vector3.zero;
                hookRb.angularVelocity = Vector3.zero;

                // Direccion = frente del MODELO, solo eje Y (el modelo ya sigue la vista del celular)
                Transform modelo = (animator != null && animator.transform != transform) ? animator.transform : transform;
                Vector3 aimDir = Vector3.ProjectOnPlane(modelo.forward, Vector3.up).normalized;

                Vector3 throwDirection = aimDir * force + Vector3.up * (force * 0.35f);

                //Vector3 throwDirection = transform.forward * force + Vector3.up * (force * 0.35f);
                hookRb.AddForce(throwDirection, ForceMode.Impulse);
                currentMaxLineLength = Mathf.Clamp(force * 1.1f, 4f, 50f);
            }
        }
    }

    private void StartReeling()
    {
        animator.SetTrigger("reel");
        SyncAnimTrigger(1);
        isReeling = true;
        isHookOccupied = false;
        currentRequestedGesture = FishCube.FishingGesture.None;

        if (hookObject != null && rodTip != null)
        {
            reelStartPos = hookObject.transform.position;
            reelTotalDistance = Vector3.Distance(reelStartPos, rodTip.position);
            reelProgress = 0f;
        }

        SetHookKinematic(true);
        if (hookCollider != null) hookCollider.isTrigger = true;
    }

    private void FinishReeling()
    {
        alreadyCast = false;
        isReeling = false;
        isHookOccupied = false;
        currentRequestedGesture = FishCube.FishingGesture.None;

        if (hookObject != null && rodTip != null)
        {
            Vector3 finalPos = rodTip.position + (Vector3.down * hangDistance);
            SetHookKinematic(true);
            if (hookRb != null) hookRb.position = finalPos;
            else hookObject.transform.position = finalPos;
        }

        if (hookCollider != null) hookCollider.isTrigger = false;
        if (verletScript != null) verletScript.ResetRope();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (!isFishing) return;

        if ((!alreadyCast || isCastingInProcess) && !isReeling)
        {
            if (hookObject != null && rodTip != null)
            {
                SetHookKinematic(true);

                /*
                Vector3 targetPos = rodTip.position + (Vector3.down * hangDistance);
                Vector3 smoothedPos = Vector3.Lerp(hookObject.transform.position, targetPos, Time.fixedDeltaTime * hookFollowSpeed);

                if (hookRb != null)
                {
                    hookRb.MovePosition(smoothedPos);
                    hookRb.MoveRotation(Quaternion.Slerp(hookObject.transform.rotation, rodTip.rotation, Time.fixedDeltaTime * (hookFollowSpeed * 0.6f)));
                }
                else
                {
                    hookObject.transform.position = smoothedPos;
                }
                */

                if (verletScript != null)
                {
                    verletScript.SetTargetLength(hangDistance);
                }
            }
        }
        else if (alreadyCast && !isReeling && hookObject != null && rodTip != null)
        {
            if (hookRb != null && !hookRb.isKinematic)
            {
                if (hookObject.transform.position.y <= waterSurfaceY)
                {
                    Vector3 pos = hookRb.position;
                    pos.y = waterSurfaceY;
                    hookRb.position = pos;

                    Vector3 vel = hookRb.velocity;
                    if (vel.y < 0) vel.y = 0;
                    vel.x = Mathf.Lerp(vel.x, 0, Time.fixedDeltaTime * 2f);
                    vel.z = Mathf.Lerp(vel.z, 0, Time.fixedDeltaTime * 2f);
                    hookRb.velocity = vel;
                }
            }

            Vector3 direction = hookObject.transform.position - rodTip.position;
            float currentDist = direction.magnitude;

            if (currentDist > currentMaxLineLength)
            {
                Vector3 constrainedPos = rodTip.position + direction.normalized * currentMaxLineLength;
                if (hookRb != null) hookRb.MovePosition(constrainedPos);
                else hookObject.transform.position = constrainedPos;

                if (hookRb != null && !hookRb.isKinematic)
                {
                    Vector3 vel = hookRb.velocity;
                    Vector3 outwardVel = Vector3.Project(vel, direction.normalized);
                    if (Vector3.Dot(outwardVel, direction) > 0)
                    {
                        hookRb.velocity -= outwardVel;
                    }
                }
            }

            if (verletScript != null)
            {
                verletScript.SetTargetLength(Mathf.Max(currentDist, 0.5f));
            }
        }
    }

    private void SetHookKinematic(bool kinematic)
    {
        if (hookObject == null) return;
        if (hookRb == null) hookRb = hookObject.GetComponent<Rigidbody>();

        if (hookRb != null)
        {
            hookRb.isKinematic = kinematic;
            if (!kinematic)
            {
                hookRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                hookRb.velocity = Vector3.zero;
                hookRb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void Jump()
    {
        if (controller.isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * jumpAdjustment * gravity);

            if (animator != null)
            {
                animator.SetBool("isGrounded", false);
            }
        }
    }

    public void TriggerRumble(float duration)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            StartCoroutine(RumbleRoutine(duration));
        }
#endif
    }

    private IEnumerator RumbleRoutine(float duration)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];
            wiimote.RumbleOn = true;
            wiimote.SendStatusInfoRequest();

            yield return new WaitForSeconds(duration);

            wiimote.RumbleOn = false;
            wiimote.SendStatusInfoRequest();
        }
#else
        yield return null;
#endif
    }


    public void ClearGestureWindow()
    {
        gestureWindow.Clear();
    }

    public bool ValidateSpecificGesture(FishCube.FishingGesture expectedGesture)
    {
        if (gestureWindow.Count < 15) return false;

        float minX = 999f, maxX = -999f, minY = 999f, maxY = -999f;
        int zeroCrossesX = 0, zeroCrossesY = 0;
        Vector2 prev = Vector2.zero;
        bool first = true;

        foreach (Vector2 val in gestureWindow)
        {
            if (val.x < minX) minX = val.x;
            if (val.x > maxX) maxX = val.x;
            if (val.y < minY) minY = val.y;
            if (val.y > maxY) maxY = val.y;

            if (!first)
            {
                if (Mathf.Sign(val.x) != Mathf.Sign(prev.x)) zeroCrossesX++;
                if (Mathf.Sign(val.y) != Mathf.Sign(prev.y)) zeroCrossesY++;
            }
            prev = val;
            first = false;
        }

        float varX = maxX - minX;
        float varY = maxY - minY;

        if (zeroCrossesX > 8 || zeroCrossesY > 8)
        {
            return false;
        }

        switch (expectedGesture)
        {
            case FishCube.FishingGesture.Horizontal:
                return varX > 0.65f && varX > varY * 1.4f;

            case FishCube.FishingGesture.Vertical:
                return varY > 0.65f && varY > varX * 1.4f;

            case FishCube.FishingGesture.Circle:
                bool isBalanced = Mathf.Abs(varX - varY) < 0.6f;
                return varX > 0.55f && varY > 0.55f && isBalanced && zeroCrossesX <= 4 && zeroCrossesY <= 4;

            case FishCube.FishingGesture.Cross:
                return varX > 0.7f && varY > 0.7f && (zeroCrossesX >= 2 || zeroCrossesY >= 2);
        }

        return false;
    }

    private void OnDisable()
    {
        currentVelY = 0f;
        targetVelY = 0f;
        if (animator != null)
        {
            animator.SetFloat("velY", 0f);
        }

        if (isFishing)
        {
            if (fishingRod != null) fishingRod.SetActive(false);
            if (lineObject != null) lineObject.SetActive(false);
            if (hookObject != null) hookObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = true;

        if (isFishing)
        {
            if (fishingRod != null) fishingRod.SetActive(true);
            if (lineObject != null) lineObject.SetActive(true);
            if (hookObject != null) hookObject.SetActive(true);
        }
    }

    private void SyncAnimTrigger(int id)
    {
        if (IsSpawned && IsServer) PlayAnimTriggerClientRpc(id);
    }

    [ClientRpc]
    private void PlayAnimTriggerClientRpc(int id)
    {
        if (IsOwner || animator == null) return;
        animator.SetTrigger(id == 0 ? "cast" : "reel");
    }

    [ClientRpc]
    private void SetFishingVisualsClientRpc(bool active, Vector3 hookPos, Quaternion hookRot)
    {
        if (IsOwner) return;

        if (hookObject != null)
        {
            SetHookKinematic(true);
            hookObject.transform.SetPositionAndRotation(hookPos, hookRot);
            hookObject.SetActive(active);
        }

        if (fishingRod != null) fishingRod.SetActive(active);
        if (lineObject != null) lineObject.SetActive(active);
        if (verletScript != null && active) verletScript.ResetRope();
    }

    void LateUpdate()
    {
        if (!IsSpawned) return;

        Transform rotTarget = (animator != null && animator.transform != transform) ? animator.transform : transform;
        Transform camTransform = camera != null ? camera : (Camera.main != null ? Camera.main.transform : null);

#if UNITY_EDITOR || UNITY_STANDALONE
        // Pruebas en PC (rol VR sin celular): Z/X giran la camara y el modelo la sigue
        if (camTransform != null)
        {
            float rotSpeed = 90f;
            if (Input.GetKey(KeyCode.Z)) 
            {
                camTransform.Rotate(Vector3.up, -rotSpeed * Time.deltaTime, Space.World);
            }
            if (Input.GetKey(KeyCode.X)) 
            {
                camTransform.Rotate(Vector3.up, rotSpeed * Time.deltaTime, Space.World);
            }
        }
#endif

        bool esVR = ConfigJugador.RolActual == TipoJugador.Jugador1_PescadorVR;

        // ---------------------------------------------------------------
        // ESTADO DE PESCA
        //   PC (dueño)       : publica isFishing
        //   Celular/Tablet   : refleja animacion, caña, linea y anzuelo desde la variable
        // ---------------------------------------------------------------
        if (IsOwner)
        {
            if (netIsFishing.Value != isFishing) netIsFishing.Value = isFishing;
        }
        else
        {
            bool pescando = netIsFishing.Value;

            if (animator != null && animator.GetBool("startFishing") != pescando)
                animator.SetBool("startFishing", pescando);

            if (fishingRod != null && fishingRod.activeSelf != pescando)
                fishingRod.SetActive(pescando);

            if (lineObject != null && lineObject.activeSelf != pescando)
            {
                lineObject.SetActive(pescando);
                if (pescando && verletScript != null) verletScript.ResetRope();
            }

            if (hookObject != null && hookObject.activeSelf != pescando)
            {
                if (pescando)
                {
                    SetHookKinematic(true);
                    if (rodTip != null)
                        hookObject.transform.SetPositionAndRotation(rodTip.position + Vector3.down * hangDistance, rodTip.rotation);
                }
                hookObject.SetActive(pescando);
            }
        }

        // ---------------------------------------------------------------
        // YAW: unica fuente de verdad = la vista del celular VR (solo eje Y)
        //   Celular VR (cliente) : aplica su propia vista al instante y la manda al servidor
        //   PC (servidor)        : recibe el yaw del celular (RPC) y lo aplica al modelo
        //   Tablet (cliente)     : solo refleja netPlayerYaw
        // IMPORTANTE: el jugador pertenece al SERVIDOR, asi que en el celular IsOwner es false.
        // ---------------------------------------------------------------
        float targetYaw = 0f;
        bool haveYaw = false;
        bool aplicarInstantaneo = false;

        if (!IsServer)
        {
            if (esVR && camTransform != null)
            {
                float currentYaw = camTransform.eulerAngles.y;
                targetYaw = currentYaw;
                haveYaw = true;
                aplicarInstantaneo = true; // en VR el modelo/caña deben seguir la vista sin retraso

                sendTimer += Time.deltaTime;
                if (sendTimer >= 0.05f &&
                    (lastSentYaw < -900f || Mathf.Abs(Mathf.DeltaAngle(lastSentYaw, currentYaw)) > 0.1f))
                {
                    lastSentYaw = currentYaw;
                    sendTimer = 0f;
                    TransmitirYawServerRpc(currentYaw);
                }
            }
            else
            {
                targetYaw = netPlayerYaw.Value; // tablet: solo visualiza
                haveYaw = true;
            }
        }
        else
        {
            if (hasRemoteYaw)
            {
                targetYaw = netPlayerYaw.Value; // viene del celular
                haveYaw = true;
            }
            else if (esVR && camTransform != null)
            {
                // PC en rol VR sin celular conectado (pruebas)
                targetYaw = camTransform.eulerAngles.y;
                netPlayerYaw.Value = targetYaw;
                haveYaw = true;
            }
        }

        if (haveYaw)
        {
            if (aplicarInstantaneo)
            {
                rotTarget.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
            else
            {
                float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
                float y = Mathf.LerpAngle(rotTarget.eulerAngles.y, targetYaw, t);
                rotTarget.rotation = Quaternion.Euler(0f, y, 0f);
            }
        }

        // ---------------------------------------------------------------
        // ANZUELO
        // ---------------------------------------------------------------
        bool colgando = isFishing && (!alreadyCast || isCastingInProcess) && !isReeling
                        && hookObject != null && hookObject.activeSelf && rodTip != null;

        if (IsOwner || IsServer)
        {
            // El servidor o dueño actualiza el anzuelo en la punta de la caña en cada frame
            if (colgando)
            {
                Vector3 hangPos = rodTip.position + Vector3.down * hangDistance;
                hookObject.transform.SetPositionAndRotation(hangPos, rodTip.rotation);
                if (hookRb != null)
                {
                    hookRb.position = hangPos;
                    hookRb.rotation = rodTip.rotation;
                }
            }

            if (netHookHanging.Value != colgando) netHookHanging.Value = colgando;

            if (hookObject != null && hookObject.activeSelf)
            {
                netHookPos.Value = hookObject.transform.position;
                netHookRot.Value = hookObject.transform.rotation;
            }
        }
        else if (hookObject != null && hookObject.activeSelf)
        {
            // El cliente pega el anzuelo a su propio rodTip sin depender del lag de red
            if (netHookHanging.Value && rodTip != null)
            {
                Vector3 hangPos = rodTip.position + Vector3.down * hangDistance;
                hookObject.transform.SetPositionAndRotation(hangPos, rodTip.rotation);
                if (hookRb != null)
                {
                    hookRb.position = hangPos;
                    hookRb.rotation = rodTip.rotation;
                }
            }
            else
            {
                float t = 1f - Mathf.Exp(-20f * Time.deltaTime);
                Vector3 p = Vector3.Lerp(hookObject.transform.position, netHookPos.Value, t);
                Quaternion r = Quaternion.Slerp(hookObject.transform.rotation, netHookRot.Value, t);
                hookObject.transform.SetPositionAndRotation(p, r);
            }
        }
    }

    private void ResetWiiCastState()
    {
        wiiCastState = 0;
        phase1Timer = 0f;

        if (IsOwner && FishingHUDManager.Instance != null)
        {
            FishingHUDManager.Instance.HideCastWindowTimer();
        }
    }

    public void SetRequestedGesture(FishCube.FishingGesture gesture, int hits, int attempts)
    {
        _currentRequestedGesture = gesture;
        if (IsOwner && FishingHUDManager.Instance != null)
        {
            if (_currentRequestedGesture == FishCube.FishingGesture.None)
            {
                FishingHUDManager.Instance.HideGesturePrompt();
            }
            else
            {
                FishingHUDManager.Instance.ShowGesturePrompt(_currentRequestedGesture.ToString(), hits, attempts);
            }
        }
    }

    // ---------------------------------------------------------------
    // SFX: la logica de pesca/minijuego solo corre en la PC (servidor).
    // Cada sonido se reproduce local y se reenvia a los celulares por ClientRpc.
    // ---------------------------------------------------------------
    private enum SfxId : byte
    {
        Lanzamiento = 0,
        AciertoPatron = 1,
        FalloPatron = 2,
        HazPatron = 3,
        VictoriaMinijuego = 4,
        DerrotaMinijuego = 5
    }

    private void ReproducirSfx(SfxId id)
    {
        ReproducirSfxLocal(id);
        if (IsSpawned && IsServer) ReproducirSfxClientRpc((byte)id);
    }

    [ClientRpc]
    private void ReproducirSfxClientRpc(byte id)
    {
        if (IsServer) return; // la PC ya lo reprodujo
        ReproducirSfxLocal((SfxId)id);
    }

    private void ReproducirSfxLocal(SfxId id)
    {
        if (sfxSource == null) return;

        AudioClip clip = null;
        switch (id)
        {
            case SfxId.Lanzamiento: clip = sfxLanzamiento; break;
            case SfxId.AciertoPatron: clip = sfxAciertoPatron; break;
            case SfxId.FalloPatron: clip = sfxFalloPatron; break;
            case SfxId.HazPatron: clip = sfxHazPatron; break;
            case SfxId.VictoriaMinijuego: clip = sfxVictoriaMinijuego; break;
            case SfxId.DerrotaMinijuego: clip = sfxDerrotaMinijuego; break;
        }

        if (clip != null) sfxSource.PlayOneShot(clip, 1.0f);
    }

    public void ReproducirSonidoLanzamiento()
    {
        ReproducirSfx(SfxId.Lanzamiento);
    }

    public void ReproducirResultadoPatron(bool exito)
    {
        ReproducirSfx(exito ? SfxId.AciertoPatron : SfxId.FalloPatron);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransmitirYawServerRpc(float yaw)
    {
        netPlayerYaw.Value = yaw;
        hasRemoteYaw = true;
    }


    public void ReproducirInicioPatron()
    {
        ReproducirSfx(SfxId.HazPatron);
    }

    public void ReproducirResultadoMinijuego(bool victoria)
    {
        // 1. Audio (local + celulares)
        ReproducirSfx(victoria ? SfxId.VictoriaMinijuego : SfxId.DerrotaMinijuego);

        // 2. Anuncio en pantalla (AnunciosUI ya lo reenvia a los celulares)
        if (AnunciosUI.Instance != null)
        {
            if (victoria) AnunciosUI.Instance.MostrarVictoria();
            else AnunciosUI.Instance.MostrarDerrota();
        }
    }
}