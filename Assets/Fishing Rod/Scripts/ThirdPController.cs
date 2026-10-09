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
    public FishCube.FishingGesture currentRequestedGesture = FishCube.FishingGesture.None;

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
    [SerializeField] private float phase1MaxTime = 1.5f;

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
    public float fuerzaBaja = 12f;
    public float fuerzaAlta = 35f;

    private bool wasWiiB = false;
    private bool wasWiiA = false;
    private bool wasWiiPlus = false;

    private float debugAccelMag = 0f;
    private float debugAccelY = 0f;
    private float debugCalculatedForce = 12f;
    private string debugState = "Iniciando...";

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
        Cursor.lockState = CursorLockMode.Locked;
        cinput = GetComponent<CharacterInputController>();
        animator.SetBool("startFishing", false);
        fishingRod.SetActive(false);

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

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

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
                    wiiCastState = 0;
                    phase1Timer = 0f;
                }
                else
                {
                    switch (wiiCastState)
                    {
                        case 0:
                            if (Mathf.Abs(accelZ) > 0.88f && Mathf.Abs(accel[1]) < 0.35f && rawMag > 0.8f && rawMag < 1.2f)
                            {
                                wiiCastState = 1;
                                peakSwingForce = 0f;
                                phase1Timer = 0f;
                                movementWindow.Clear();
                            }
                            break;

                        case 1:
                            phase1Timer += Time.deltaTime;

                            if (phase1Timer > phase1MaxTime)
                            {
                                wiiCastState = 0;
                                phase1Timer = 0f;
                            }
                            else if (movementWindow.Count >= 5)
                            {
                                float maxAccel = 0f;
                                foreach (float val in movementWindow)
                                {
                                    if (val > maxAccel) maxAccel = val;
                                }

                                peakSwingForce = maxAccel;

                                if (maxAccel > umbralMinimo && dynamicAccelMag < maxAccel * 0.8f)
                                {
                                    wiiCastTrigger = true;
                                    float t = Mathf.InverseLerp(umbralMinimo, umbralAlto, maxAccel);
                                    debugCalculatedForce = Mathf.Lerp(fuerzaBaja, fuerzaAlta, t);

                                    wiiCastState = 0;
                                    phase1Timer = 0f;
                                    movementWindow.Clear();
                                }
                                else if (Mathf.Abs(accelZ) < 0.4f && Mathf.Abs(accel[1]) > 0.7f && rawMag < 1.2f)
                                {
                                    wiiCastState = 0;
                                    phase1Timer = 0f;
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
        if (Input.GetKeyDown(KeyCode.E) || wiiEquipTrigger)
        {
            if (!isFishing)
            {
                if (animator != null) animator.SetBool("startFishing", true);
                isFishing = true;
                currentVelY = 0f;
                targetVelY = 0f;
                if (animator != null) animator.SetFloat("velY", 0f);

                if (hookObject != null && rodTip != null)
                {
                    Vector3 initPos = rodTip.position + (Vector3.down * hangDistance);
                    SetHookKinematic(true);
                    if (hookRb != null) hookRb.position = initPos;
                    else hookObject.transform.position = initPos;

                    hookObject.transform.rotation = rodTip.rotation;
                    if (hookCollider != null) hookCollider.isTrigger = false;
                }

                if (fishingRod != null) fishingRod.SetActive(true);
                if (lineObject != null) lineObject.SetActive(true);
                if (hookObject != null) hookObject.SetActive(true);
                if (verletScript != null) verletScript.ResetRope();
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
            }
        }

        // --- LANZAMIENTO ---
        if ((Input.GetMouseButtonDown(0) || wiiCastTrigger) && isFishing && !alreadyCast && !isReeling && !isCastingInProcess)
        {
            if (Input.GetMouseButtonDown(0)) debugCalculatedForce = 25f;
            animator.SetTrigger("cast");
            StartCoroutine(ExecuteCastWithDelay(debugCalculatedForce));
        }

        // --- RECOGIDA ---
        bool inputReel = Input.GetKeyDown(KeyCode.Q) || wiiReelTrigger;
        if (inputReel && isFishing && alreadyCast && !isReeling && !isCastingInProcess)
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
        if (!isFishing)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (wiiHorizontal != 0f) horizontal = wiiHorizontal;
            if (wiiVertical != 0f) vertical = wiiVertical;

            targetVelY = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);

            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;
            if (direction.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camera.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothingVelocity, turnSmoothingTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

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
                    currentSpeed = speed;
                }
                controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            }
        }

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

        currentVelY = Mathf.Lerp(currentVelY, targetVelY, speedChangeRate);
        animator.SetFloat("velY", currentVelY);

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

    private IEnumerator ExecuteCastWithDelay(float force)
    {
        isCastingInProcess = true;
        yield return new WaitForSeconds(castDelay);

        isCastingInProcess = false;
        alreadyCast = true;
        isHookOccupied = false;
        currentRequestedGesture = FishCube.FishingGesture.None;

        if (hookObject != null && rodTip != null)
        {
            SetHookKinematic(false);
            if (hookRb != null)
            {
                hookRb.velocity = Vector3.zero;
                hookRb.angularVelocity = Vector3.zero;
                Vector3 throwDirection = transform.forward * force + Vector3.up * (force * 0.35f);
                hookRb.AddForce(throwDirection, ForceMode.Impulse);
                currentMaxLineLength = Mathf.Clamp(force * 1.1f, 4f, 50f);
            }
        }
    }

    private void StartReeling()
    {
        animator.SetTrigger("reel");
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
        if (!isFishing) return;

        if ((!alreadyCast || isCastingInProcess) && !isReeling)
        {
            if (hookObject != null && rodTip != null)
            {
                SetHookKinematic(true);

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

    // --- SISTEMA DE VIBRACIÓN DEL MANDO DE WII ---
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

    void OnGUI()
    {
        if (!IsOwner) return;

#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return;
#else
        GUIStyle style = new GUIStyle();
        style.fontSize = 12;
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        GUIStyle styleLock = new GUIStyle(style);
        styleLock.normal.textColor = isHookOccupied ? Color.red : Color.green;

        GUIStyle styleGesture = new GUIStyle(style);
        styleGesture.normal.textColor = Color.cyan;

        float boxWidth = 280f;
        float boxHeight = 185f;
        float posX = 10f;
        float posY = Screen.height - boxHeight - 10f;

        GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), "");

        GUILayout.BeginArea(new Rect(posX + 10, posY + 10, boxWidth - 20, boxHeight - 20));
        GUILayout.Label("- DEBUG WII FISHING -", style);
        GUILayout.Label("Estado: " + debugState, style);
        GUILayout.Label("Anzuelo Ocupado: " + (isHookOccupied ? "SÍ (BLOQUEADO)" : "NO (LIBRE)"), styleLock);
        GUILayout.Label("Gesto Solicitado: " + currentRequestedGesture.ToString(), styleGesture);

        if (wiiCastState == 1)
        {
            float timeLeft = Mathf.Max(0f, phase1MaxTime - phase1Timer);
            GUIStyle timerStyle = new GUIStyle(style);
            timerStyle.normal.textColor = Color.cyan;
            GUILayout.Label("Ventana de tiro: " + timeLeft.ToString("F2") + "s", timerStyle);
        }

        GUILayout.Label("Acel. Dinámica: " + debugAccelMag.ToString("F2"), style);
        GUILayout.Label("Pico MAX Dinámico: " + peakSwingForce.ToString("F2"), style);

        style.normal.textColor = Color.green;
        GUILayout.Label("Potencia Tiro Final: " + debugCalculatedForce.ToString("F1"), style);

        GUILayout.EndArea();
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
}