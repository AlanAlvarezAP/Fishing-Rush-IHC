using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cámaras")]
    public Camera firstPersonCamera;
    public Camera thirdPersonCamera;

    [Header("Configuración de Entrada")]
    public KeyCode toggleKey = KeyCode.C; // Tecla temporal para alternar
    public bool startInFirstPerson = false;

    [HideInInspector]
    public bool isFirstPerson;

    void Start()
    {
        isFirstPerson = startInFirstPerson;
        UpdateCameraState();
    }

    void Update()
    {
        // Cambiar de cámara mediante teclado
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCamera();
        }
    }

    /// <summary>
    /// Invoca el cambio entre 1ra y 3ra persona.
    /// Puede ser llamado desde código o desde un botón de UI.
    /// </summary>
    public void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        UpdateCameraState();
    }

    private void UpdateCameraState()
    {
        if (firstPersonCamera != null && thirdPersonCamera != null)
        {
            firstPersonCamera.gameObject.SetActive(isFirstPerson);
            thirdPersonCamera.gameObject.SetActive(!isFirstPerson);

            // Asegurar que solo la cámara activa escuche el audio
            AudioListener fpListener = firstPersonCamera.GetComponent<AudioListener>();
            AudioListener tpListener = thirdPersonCamera.GetComponent<AudioListener>();

            if (fpListener != null) fpListener.enabled = isFirstPerson;
            if (tpListener != null) tpListener.enabled = !isFirstPerson;
        }
    }
}