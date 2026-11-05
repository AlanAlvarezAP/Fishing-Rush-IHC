using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Camaras")]
    public GameObject firstPersonCamera;
    public GameObject thirdPersonCamera;

    [Header("Referencias Visuales del Personaje")]
    public SkinnedMeshRenderer[] characterMeshRenderers;

    [Header("Configuracion de Entrada")]
    public KeyCode toggleKey = KeyCode.C;
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
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCamera();
        }
    }

    public void ToggleCamera()
    {
        isFirstPerson = !isFirstPerson;
        UpdateCameraState();
    }

    public void SetCameraMode(bool firstPerson)
    {
        isFirstPerson = firstPerson;
        UpdateCameraState();
    }

    private void UpdateCameraState()
    {
        if (firstPersonCamera != null && thirdPersonCamera != null)
        {
            AudioListener fpListener = firstPersonCamera.GetComponentInChildren<AudioListener>(true);
            AudioListener tpListener = thirdPersonCamera.GetComponentInChildren<AudioListener>(true);

            if (isFirstPerson)
            {
                if (tpListener != null) tpListener.enabled = false;
                thirdPersonCamera.SetActive(false);

                firstPersonCamera.SetActive(true);
                if (fpListener != null) fpListener.enabled = true;
            }
            else
            {
                if (fpListener != null) fpListener.enabled = false;
                firstPersonCamera.SetActive(false);

                thirdPersonCamera.SetActive(true);
                if (tpListener != null) tpListener.enabled = true;
            }

            SetMeshVisibility(!isFirstPerson);
        }
    }

    public void SetMeshVisibility(bool visible)
    {
        if (characterMeshRenderers != null)
        {
            foreach (var mesh in characterMeshRenderers)
            {
                if (mesh != null)
                {
                    mesh.enabled = visible;
                }
            }
        }
    }
}