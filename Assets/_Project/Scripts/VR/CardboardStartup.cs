using Google.XR.Cardboard;
using UnityEngine;

public class CardboardStartup : MonoBehaviour
{
    private bool readyToRun = false;

    private void Start()
    {
#if !UNITY_ANDROID
        // Si estamos ejecutando en la PC (Servidor/Editor), desactivamos este script
        enabled = false;
        return;
#else
        // En Android (Cliente VR), configuramos el brillo y mantenemos la pantalla encendida
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.brightness = 1.0f;

        if (!Api.HasDeviceParams())
        {
            Api.ScanDeviceParams();
        }

        readyToRun = true;
#endif
    }

    public void Update()
    {
        if (!readyToRun) return;

#if !UNITY_EDITOR
        if (UnityEngine.XR.Management.XRGeneralSettings.Instance == null ||
            UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager == null ||
            !UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.isInitializationComplete)
        {
            return;
        }
#endif

        if (Api.IsGearButtonPressed)
        {
            Api.ScanDeviceParams();
        }

        if (Api.IsCloseButtonPressed)
        {
            Application.Quit();
        }

        if (Api.IsTriggerHeldPressed)
        {
            Api.Recenter(); // Recentra la orientación de la vista VR
        }

        if (Api.HasNewDeviceParams())
        {
            Api.ReloadDeviceParams();
        }

#if !UNITY_EDITOR
        Api.UpdateScreenParams();
#endif
    }
}