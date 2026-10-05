using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WiimoteApi;
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
    float _inputForward = 0f;
    float _inputTurn = 0f;
    public float speedChangeRate = 0.1f;
    private float currentVelY = 0f;
    private float targetVelY = 0f;

    [Header("Estados de Pesca")]
    public bool alreadyCast = false;
    public bool isFishing = false;
    public bool isReeling = false;
    public bool isCastingInProcess = false;

    [Header("Referencias de Pesca")]
    public GameObject fishingRod;
    [SerializeField] private GameObject lineObject;
    [SerializeField] private GameObject hookObject;
    [SerializeField] private Transform rodTip;
    [SerializeField] private Collider hookCollider;

    [Header("Ajustes de Animación y Cuelgue")]
    [SerializeField] private float castDelay = 1.75f;
    [SerializeField] private float reelSpeed = 15f;
    [SerializeField] private float reelArcHeight = 2.5f;
    [SerializeField] private float hangDistance = 0.4f;
    [SerializeField] private float hookFollowSpeed = 12f;

    private VerletLine verletScript;
    private float currentMaxLineLength = 0.5f;
    private Rigidbody hookRb;

    // Variables internas para el recojo parabólico
    private Vector3 reelStartPos;
    private float reelTotalDistance;
    private float reelProgress;

    // --- VARIABLES MANDO WII & MÁQUINA DE ESTADOS DE LANZAMIENTO ---
    // 0 = Reposo, 1 = Caña Levantada (Evaluando latigazo con Ventana)
    private int wiiCastState = 0;
    private float peakSwingForce = 0f;
    private float phase1Timer = 0f;
    [SerializeField] private float phase1MaxTime = 1.5f; // Tiempo límite para lanzar tras preparar

    [Header("Configuración de Ventana de Movimiento (Wii)")]
    public int windowSize = 25; // Tamaño de la ventana para evaluar el movimiento
    private Queue<float> movementWindow = new Queue<float>();

    [Header("Umbrales de Fuerza (Latigazo Dinámico)")]
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

    void Start()
    {
        controller = GetComponent<CharacterController>();
	/*
        if (animator != null) animator.SetBool("startFishing", false);
        if (fishingRod != null) fishingRod.SetActive(false);

        if (lineObject != null) lineObject.SetActive(false);
        if (hookObject != null) hookObject.SetActive(false);

        if (camera == null && Camera.main != null)
            camera = Camera.main.transform;
	*/

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
	    /*
        if (lineObject != null) lineObject.SetActive(false);
        if (hookObject != null) hookObject.SetActive(false);
        */
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
        else if (wiiCastState == 1) debugState = "¡Caña Arriba! (Lanza)";
        else debugState = "Levanta a 90° para preparar";

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

            // Vaciar el buffer del Wiimote en cada frame para tener 0 latencia y datos frescos
            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            float[] accel = wiimote.Accel.GetCalibratedAccelData();

            if (accel != null && accel.Length >= 3)
            {
                float accelZ = accel[2]; // Z es el eje longitudinal

                // Magnitud pura incluyendo gravedad para chequear posturas estáticas de preparación
                float rawMag = new Vector3(accel[0], accel[1], accel[2]).magnitude;

                // Aceleración dinámica real (restando 1G de gravedad estática) para evitar falsos positivos en movimientos lentos
                float dynamicAccelMag = Mathf.Max(0f, rawMag - 1.0f);

                debugAccelMag = dynamicAccelMag;
                debugAccelY = accel[1];

                // 1. MANTENER LA VENTANA ACTUALIZADA SIEMPRE CON ACELERACIÓN DINÁMICA
                movementWindow.Enqueue(dynamicAccelMag);
                if (movementWindow.Count > windowSize)
                {
                    movementWindow.Dequeue();
                }

                // Solo procesamos el lanzamiento si estamos pescando y listos
                if (!isFishing || alreadyCast || isCastingInProcess)
                {
                    wiiCastState = 0;
                    phase1Timer = 0f;
                }
                else
                {
                    // --- MÁQUINA DE ESTADOS CON VENTANA DE MOVIMIENTO ---
                    switch (wiiCastState)
                    {
                        case 0: // FASE 0: ESPERANDO QUE LEVANTE LA CAÑA
                            if (Mathf.Abs(accelZ) > 0.88f && Mathf.Abs(accel[1]) < 0.35f && rawMag > 0.8f && rawMag < 1.2f)
                            {
                                wiiCastState = 1;
                                peakSwingForce = 0f;
                                phase1Timer = 0f;
                                movementWindow.Clear(); // Limpiamos para grabar el latigazo desde cero
                            }
                            break;

                        case 1: // FASE 1: EVALUANDO LATIGAZO A TRAVÉS DE LA VENTANA
                            phase1Timer += Time.deltaTime;

                            // Si se acaba el tiempo, cancelar
                            if (phase1Timer > phase1MaxTime)
                            {
                                wiiCastState = 0;
                                phase1Timer = 0f;
                            }
                            // Evaluamos desde los primeros frames (ej. 5) para evitar puntos ciegos
                            else if (movementWindow.Count >= 5)
                            {
                                // Buscar el pico máximo de aceleración dinámica en nuestra ventana actual
                                float maxAccel = 0f;
                                foreach (float val in movementWindow)
                                {
                                    if (val > maxAccel) maxAccel = val;
                                }

                                peakSwingForce = maxAccel; // Lo guardamos para mostrar en el UI

                                // Condición de soltado: El pico superó el umbral dinámico mínimo Y la aceleración empezó a caer
                                if (maxAccel > umbralMinimo && dynamicAccelMag < maxAccel * 0.8f)
                                {
                                    wiiCastTrigger = true;

                                    // Variación real continua: Mapea la aceleración de forma fluida entre umbrales
                                    float t = Mathf.InverseLerp(umbralMinimo, umbralAlto, maxAccel);
                                    debugCalculatedForce = Mathf.Lerp(fuerzaBaja, fuerzaAlta, t);

                                    wiiCastState = 0;
                                    phase1Timer = 0f;
                                    movementWindow.Clear(); // Evita que un mismo movimiento dispare 2 veces
                                }
                                // Cancelación si el usuario vuelve a bajar la caña muy suavemente
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

            // Lectura de Botones
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
                if (fishingRod != null) fishingRod.SetActive(false);
                if (lineObject != null) lineObject.SetActive(false);
                if (hookObject != null) hookObject.SetActive(false);
            }
        }

        // --- LANZAMIENTO ---
        if ((Input.GetMouseButtonDown(0) || wiiCastTrigger) && isFishing && !alreadyCast && !isReeling && !isCastingInProcess)
        {
            if (Input.GetMouseButtonDown(0)) debugCalculatedForce = 10f;
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
                Vector3 currentLinearPos = Vector3.Lerp(reelStartPos, targetEndPos, clampedProgress);
                float arc = Mathf.Sin(clampedProgress * Mathf.PI) * reelArcHeight;
                Vector3 finalPos = currentLinearPos + Vector3.up * arc;

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
            else if (controller.isGrounded)
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

            playerVelocity.y += gravityFactor * gravity * Time.deltaTime;
            controller.Move(playerVelocity * Time.deltaTime);
        }
    }

    private IEnumerator ExecuteCastWithDelay(float force)
    {
        isCastingInProcess = true;
        yield return new WaitForSeconds(castDelay);

        isCastingInProcess = false;
        alreadyCast = true;

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

        float boxWidth = 260f;
        float boxHeight = 150f;
        float posX = 10f;
        float posY = Screen.height - boxHeight - 10f;

        GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), "");

        GUILayout.BeginArea(new Rect(posX + 10, posY + 10, boxWidth - 20, boxHeight - 20));
        GUILayout.Label("- DEBUG WII FISHING -", style);
        GUILayout.Label("Estado: " + debugState, style);

        // Muestra el contador en regresiva cuando estás en la Fase 1
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
}
