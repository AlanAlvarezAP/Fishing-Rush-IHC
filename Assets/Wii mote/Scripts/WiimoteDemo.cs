using UnityEngine;
using UnityEngine.UI;
using WiimoteApi;

public class WiimoteDemo : MonoBehaviour
{
    public static bool IsReadyToPlay = false;

    public enum CalibrationState
    {
        NotConnected,
        WarmingUp,
        ReadyToCalibrate,
        Playing
    }

    [Header("UI - Canvas Moderno")]
    public GameObject calibrationOverlay;
    public Text statusText;
    public Text instructionText;
    public Button uiCalibrateButton;

    [Header("Mapeo del Modelo 3D del Mando")]
    public WiimoteModel model;

    [Header("Infrarrojos (Opcional)")]
    public RectTransform[] ir_dots;
    public RectTransform[] ir_bb;
    public RectTransform ir_pointer;

    private Quaternion initial_rotation;
    private Quaternion calibration_rotation;

    private Wiimote wiimote;
    private CalibrationState currentState = CalibrationState.NotConnected;

    private bool wasWMPActive = false;
    private int wmpWarmupFrames = 0;
    private bool wasHomePressed = false;
    private bool wasAPressed = false;

    private bool isCalibrated = false;
    private bool isWarmingUp = false;

    void Start()
    {
        IsReadyToPlay = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (model != null && model.rot != null)
        {
            initial_rotation = model.rot.localRotation;
            calibration_rotation = initial_rotation;
        }

        if (uiCalibrateButton != null)
        {
            uiCalibrateButton.onClick.AddListener(ConfirmInitialCalibration);
        }

        UpdateUIState(CalibrationState.NotConnected);
    }

    void Update()
    {
        if (!WiimoteManager.HasWiimote())
        {
            WiimoteManager.FindWiimotes();

            if (currentState != CalibrationState.NotConnected)
            {
                currentState = CalibrationState.NotConnected;
                IsReadyToPlay = false;
                isCalibrated = false;
                UpdateUIState(CalibrationState.NotConnected);
            }
            return;
        }

        wiimote = WiimoteManager.Wiimotes[0];

        if (wiimote.current_ext != ExtensionController.MOTIONPLUS)
        {
            wiimote.ActivateWiiMotionPlus();
            return;
        }

        if (wiimote.current_ext == ExtensionController.MOTIONPLUS)
        {
            if (!wasWMPActive)
            {
                wasWMPActive = true;
                wmpWarmupFrames = 40;
                isWarmingUp = true;
                isCalibrated = false;

                wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_EXT16);
                if (model != null && model.rot != null)
                {
                    model.rot.localRotation = initial_rotation;
                }

                currentState = CalibrationState.WarmingUp;
                UpdateUIState(CalibrationState.WarmingUp);
            }
        }
        else
        {
            wasWMPActive = false;
            isCalibrated = false;
            isWarmingUp = false;
        }

        int ret;
        do
        {
            ret = wiimote.ReadWiimoteData();

            if (ret > 0 && wiimote.current_ext == ExtensionController.MOTIONPLUS)
            {
                if (wmpWarmupFrames > 0)
                {
                    wmpWarmupFrames--;
                    if (wmpWarmupFrames == 0)
                    {
                        isWarmingUp = false;
                        currentState = CalibrationState.ReadyToCalibrate;
                        UpdateUIState(CalibrationState.ReadyToCalibrate);
                    }
                    continue;
                }

                if (isCalibrated && model != null && model.rot != null)
                {
                    float threshold = 10.0f;
                    float pitch = Mathf.Abs(wiimote.MotionPlus.PitchSpeed) > threshold ? wiimote.MotionPlus.PitchSpeed : 0f;
                    float yaw = Mathf.Abs(wiimote.MotionPlus.YawSpeed) > threshold ? wiimote.MotionPlus.YawSpeed : 0f;
                    float roll = Mathf.Abs(wiimote.MotionPlus.RollSpeed) > threshold ? wiimote.MotionPlus.RollSpeed : 0f;

                    Vector3 offset = new Vector3(-pitch, yaw, roll) / 95f;
                    if (offset != Vector3.zero)
                    {
                        model.rot.Rotate(offset, Space.Self);
                    }
                }
            }
        } while (ret > 0);

        bool currentHome = wiimote.Button.home;
        bool currentA = wiimote.Button.a;
        bool currentSpace = Input.GetKeyDown(KeyCode.Space);

        if ((currentHome && !wasHomePressed) || (currentA && !wasAPressed) || currentSpace)
        {
            if (currentState == CalibrationState.ReadyToCalibrate || currentState == CalibrationState.WarmingUp || !isCalibrated)
            {
                ConfirmInitialCalibration();
            }
        }

        wasHomePressed = currentHome;
        wasAPressed = currentA;

        UpdateModelButtons();
        UpdateIRPointers();
    }

    public void ConfirmInitialCalibration()
    {
        if (wiimote != null)
        {
            wiimote.SendPlayerLED(true, false, false, false);

            if (wiimote.current_ext == ExtensionController.MOTIONPLUS)
            {
                wiimote.MotionPlus.SetZeroValues();
            }
        }

        if (model != null && model.rot != null)
        {
            model.rot.localRotation = initial_rotation;
            calibration_rotation = model.rot.localRotation;
        }

        isCalibrated = true;
        IsReadyToPlay = true;

        currentState = CalibrationState.Playing;
        UpdateUIState(CalibrationState.Playing);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("================================\nCALIBRACIÓN COMPLETADA: ¡MANDO LISTO!\n================================");
    }

    private void UpdateUIState(CalibrationState state)
    {
        switch (state)
        {
            case CalibrationState.NotConnected:
                if (calibrationOverlay != null) calibrationOverlay.SetActive(true);
                if (statusText != null) statusText.text = "Buscando Wii Remote...";
                if (instructionText != null) instructionText.text = "Asegúrate de que el mando esté emparejado.";
                break;
            case CalibrationState.WarmingUp:
                if (calibrationOverlay != null) calibrationOverlay.SetActive(true);
                if (statusText != null) statusText.text = "Detectando MotionPlus...";
                if (instructionText != null) instructionText.text = "Mantén el mando quieto mientras se estabiliza.";
                break;
            case CalibrationState.ReadyToCalibrate:
                if (calibrationOverlay != null) calibrationOverlay.SetActive(true);
                if (statusText != null) statusText.text = "¡Mando Listo!";
                if (instructionText != null) instructionText.text = "Apunta a la pantalla y presiona Botón A o Espacio.";
                break;
            case CalibrationState.Playing:
                if (calibrationOverlay != null) calibrationOverlay.SetActive(false);
                break;
        }
    }

    private void UpdateModelButtons()
    {
        if (model == null || wiimote == null) return;

        if (model.a != null) model.a.enabled = wiimote.Button.a;
        if (model.b != null) model.b.enabled = wiimote.Button.b;
        if (model.one != null) model.one.enabled = wiimote.Button.one;
        if (model.two != null) model.two.enabled = wiimote.Button.two;
        if (model.d_up != null) model.d_up.enabled = wiimote.Button.d_up;
        if (model.d_down != null) model.d_down.enabled = wiimote.Button.d_down;
        if (model.d_left != null) model.d_left.enabled = wiimote.Button.d_left;
        if (model.d_right != null) model.d_right.enabled = wiimote.Button.d_right;
        if (model.plus != null) model.plus.enabled = wiimote.Button.plus;
        if (model.minus != null) model.minus.enabled = wiimote.Button.minus;
        if (model.home != null) model.home.enabled = wiimote.Button.home;
    }

    private void UpdateIRPointers()
    {
        if (wiimote == null || ir_dots == null || ir_dots.Length < 4) return;

        float[,] ir = wiimote.Ir.GetProbableSensorBarIR();
        for (int i = 0; i < 2; i++)
        {
            float x = (float)ir[i, 0] / 1023f;
            float y = (float)ir[i, 1] / 767f;

            if (x == -1 || y == -1)
            {
                if (ir_dots[i] != null)
                {
                    ir_dots[i].anchorMin = Vector2.zero;
                    ir_dots[i].anchorMax = Vector2.zero;
                }
            }
            else
            {
                if (ir_dots[i] != null)
                {
                    ir_dots[i].anchorMin = new Vector2(x, y);
                    ir_dots[i].anchorMax = new Vector2(x, y);
                }
            }

            if (ir[i, 2] != -1 && ir_bb != null && ir_bb.Length > i && ir_bb[i] != null)
            {
                int index = (int)ir[i, 2];
                float xmin = (float)wiimote.Ir.ir[index, 3] / 127f;
                float ymin = (float)wiimote.Ir.ir[index, 4] / 127f;
                float xmax = (float)wiimote.Ir.ir[index, 5] / 127f;
                float ymax = (float)wiimote.Ir.ir[index, 6] / 127f;

                ir_bb[i].anchorMin = new Vector2(xmin, ymin);
                ir_bb[i].anchorMax = new Vector2(xmax, ymax);
            }
        }

        if (ir_pointer != null)
        {
            float[] pointer = wiimote.Ir.GetPointingPosition();
            ir_pointer.anchorMin = new Vector2(pointer[0], pointer[1]);
            ir_pointer.anchorMax = new Vector2(pointer[0], pointer[1]);
        }
    }

    [System.Serializable]
    public class WiimoteModel
    {
        public Transform rot;
        public Renderer a;
        public Renderer b;
        public Renderer one;
        public Renderer two;
        public Renderer d_up;
        public Renderer d_down;
        public Renderer d_left;
        public Renderer d_right;
        public Renderer plus;
        public Renderer minus;
        public Renderer home;
    }

    void OnApplicationQuit()
    {
        if (wiimote != null)
        {
            WiimoteManager.Cleanup(wiimote);
            wiimote = null;
        }
    }
}