using System.Collections.Generic;
using UnityEngine;
using WiimoteApi; 

public class ThirdPController : MonoBehaviour
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
    public bool alreadyCast = false;
    public bool isFishing = false;
    public bool isReeling = false;

    // fishing objects
    public GameObject fishingRod;
    [SerializeField] private GameObject lineObject; 
    [SerializeField] private GameObject hookObject; 
    [SerializeField] private Transform rodTip;
    [SerializeField] private Collider hookCollider;

    // Variables para el mando
    private bool isWiiPreparing = false;
    private float peakSwingForce = 0f; 
    private bool wasWiiB = false;
    private bool wasWiiA = false;
    private bool wasWiiPlus = false;
    
    // Variables de DEBUG para imprimir en pantalla
    private float debugAccelMag = 0f;
    private float debugAccelY = 0f;
    private float debugCalculatedForce = 12f; 
    private string debugState = "Iniciando...";

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked; 
        cinput = GetComponent<CharacterInputController>();
        animator.SetBool("startFishing", false);
        fishingRod.SetActive(false);

        if (lineObject != null) lineObject.SetActive(false);
        if (hookObject != null) hookObject.SetActive(false);
    }

    void Update()
    {
        // Actualizar el estado para la pantalla de Debug
        if (!isFishing) debugState = "No pescando";
        else if (alreadyCast) debugState = "En el agua";
        else if (isWiiPreparing) debugState = "¡Latigazo!";
        else debugState = "Listo (Sube caña)";

        // --- LECTURA DEL MANDO WII ---
        bool wiiCastTrigger = false;
        bool wiiReelTrigger = false;
        bool wiiEquipTrigger = false;
        bool wiiJumpDown = false;
        bool wiiJumpHeld = false;
        float wiiHorizontal = 0f;
        float wiiVertical = 0f;

        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            float[] accel = wiimote.Accel.GetCalibratedAccelData();
            if (accel != null && accel.Length >= 3)
            {
                float accelMag = new Vector3(accel[0], accel[1], accel[2]).magnitude;
                float accelY = accel[1];
                
                debugAccelMag = accelMag;
                debugAccelY = accelY;

                // 1. Detectar si levanta la caña (Y se inclina hacia arriba)
                if (accelY > 0.4f && !alreadyCast && isFishing && !isWiiPreparing) 
                {
                    isWiiPreparing = true;
                    peakSwingForce = 0f; 
                }

                // 2. Lógica de lanzar
                if (isWiiPreparing) 
                {
                    // Mientras baja el brazo, capturamos SIEMPRE la fuerza más alta
                    if (accelMag > peakSwingForce)
                    {
                        peakSwingForce = accelMag;
                    }

                    // Lanzamos cuando detectamos el "frenazo" al final del movimiento
                    // Bajé el límite a 1.5 para que detecte incluso tiros suaves
                    if (peakSwingForce > 1.5f && accelMag < 1.3f)
                    {
                        wiiCastTrigger = true;
                        
                        // --- AQUÍ ESTÁ LA MAGIA DE LA DISTANCIA ---
                        // 1.5 es un tiro súper suave, 4.5 es un latigazo rompe-telescopios
                        float normalizedForce = Mathf.InverseLerp(1.5f, 4.5f, peakSwingForce);
                        
                        // Hacemos que la curva sea exponencial (un tiro un poco más fuerte se nota MUCHÍSIMO más)
                        normalizedForce = Mathf.Pow(normalizedForce, 1.5f);
                        
                        // RANGO EXTREMO: De 3 (cae a tus pies) a 45 (vuela por los aires)
                        debugCalculatedForce = Mathf.Lerp(3f, 45f, normalizedForce);
                        
                        isWiiPreparing = false; 
                    }
                    else if (peakSwingForce < 1.5f && accelY < 0.1f) 
                    {
                        isWiiPreparing = false; // Cancelado por bajarlo muy despacito
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
        // -----------------------------

        if (controller.isGrounded && playerVelocity.y < 0)
        {
            animator.SetBool("isGrounded", true);
            playerVelocity.y = 0f;
            playerVelocity.x = 0f;
            playerVelocity.z = 0f;
        }
        
        if (Input.GetKeyDown(KeyCode.E) || wiiEquipTrigger) 
        {
            if (!isFishing)
            {
                animator.SetBool("startFishing", true);
                isFishing = true;

                currentVelY = 0f;
                targetVelY = 0f;
                animator.SetFloat("velY", 0f);

                if (hookObject != null && rodTip != null)
                {
                    hookObject.transform.position = rodTip.position;
                }

                fishingRod.SetActive(true);
                if (lineObject != null) lineObject.SetActive(true);
                if (hookObject != null) hookObject.SetActive(true);
            }
            else if (isFishing && !alreadyCast && !isReeling)
            {
                animator.SetBool("startFishing", false);
                isFishing = false;
                fishingRod.SetActive(false);
                if (lineObject != null) lineObject.SetActive(false);
                if (hookObject != null) hookObject.SetActive(false);
            }
        }

        // Lógica de LANZAR
        if ((Input.GetMouseButtonDown(0) || wiiCastTrigger) && isFishing && !alreadyCast && !isReeling) 
        {
            if (Input.GetMouseButtonDown(0)) 
            {
                debugCalculatedForce = 15f; // Fuerza promedio para ratón
            }

            animator.SetTrigger("cast");
            alreadyCast = true;

            if (hookObject != null)
            {
                Rigidbody rb = hookObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.velocity = Vector3.zero; 

                    // --- MÁS ARCO DE VUELO ---
                    // Ahora la fuerza vertical (hacia arriba) es el 40% de la fuerza total, antes era 33%.
                    Vector3 throwDirection = transform.forward * debugCalculatedForce + Vector3.up * (debugCalculatedForce * 0.40f);
                    rb.AddForce(throwDirection, ForceMode.Impulse);
                }
            }
        }

        if ((Input.GetKeyDown(KeyCode.Q) || wiiReelTrigger) && isFishing && alreadyCast && !isReeling) 
        {
            animator.SetTrigger("reel");
            isReeling = true;
            if (hookCollider != null)
            {
                hookCollider.isTrigger = true;
            }
        }

        if (isReeling)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName("Reel In"))
            {
                alreadyCast = false;
                isReeling = false;
                if (hookCollider != null)
                {
                    hookCollider.isTrigger = false;
                }
            }
        }

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

    void Jump()
    {
        playerVelocity.y += Mathf.Sqrt(jumpHeight * jumpAdjustment * gravityFactor * gravity);
    }

    // --- INTERFAZ DE DEPURACIÓN EN PANTALLA ---
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 12; 
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        float boxWidth = 200f;
        float boxHeight = 130f;
        float posX = 10f;
        float posY = Screen.height - boxHeight - 10f;

        GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), "");
        GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), ""); 

        GUILayout.BeginArea(new Rect(posX + 10, posY + 10, boxWidth - 20, boxHeight - 20));
        GUILayout.Label("- DEBUG WII -", style);
        GUILayout.Label("Estado: " + debugState, style);
        GUILayout.Label("Fuerza Act: " + debugAccelMag.ToString("F2"), style);
        GUILayout.Label("Fuerza MAX: " + peakSwingForce.ToString("F2"), style);
        GUILayout.Label("Inclinación: " + debugAccelY.ToString("F2"), style);
        
        style.normal.textColor = Color.green;
        GUILayout.Label("Potencia: " + debugCalculatedForce.ToString("F1"), style);
        
        GUILayout.EndArea();
    }
}