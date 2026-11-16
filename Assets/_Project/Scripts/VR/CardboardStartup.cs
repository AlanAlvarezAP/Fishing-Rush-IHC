using System.Collections;
using Google.XR.Cardboard;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Arranque del modo VR (Cardboard), solo en el celular del PESCADOR.
///
/// El XR ya NO se inicia solo al abrir la app (Project Settings > XR Plug-in Management > Android >
/// "Initialize XR on Startup" desmarcado): se inicia aqui y solo si la build es del pescador.
/// Antes se iniciaba siempre y el celular conductor lo apagaba al arrancar (DeinitializeLoader);
/// apagar el XR en caliente en Android rompia el dibujo de los textos del conductor (botones y consola
/// sin letras). Asi el conductor nunca enciende el XR.
///
/// Si "Initialize XR on Startup" sigue marcado, se mantiene el comportamiento anterior como respaldo.
/// </summary>
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
        // En Android mantenemos la pantalla encendida y con brillo maximo
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.brightness = 1.0f;

        if (ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet)
        {
            // Conductor: sin VR. Solo si el XR se inicio solo (ajuste antiguo) se apaga como respaldo.
            if (XRIniciado()) ApagarXR();
            enabled = false;
            return;
        }

        StartCoroutine(IniciarVR());
#endif
    }

    private static XRManagerSettings Manager =>
        XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;

    private static bool XRIniciado() => Manager != null && Manager.isInitializationComplete;

    private IEnumerator IniciarVR()
    {
        if (Manager == null)
        {
            Debug.LogError("[VR] No hay configuracion de XR (XR Plug-in Management).");
            yield break;
        }

        if (!XRIniciado())
        {
            Debug.Log("[VR] Iniciando Cardboard...");
            yield return Manager.InitializeLoader();

            if (Manager.activeLoader == null)
            {
                Debug.LogError("[VR] No se pudo iniciar el XR (Cardboard).");
                yield break;
            }

            Manager.StartSubsystems();
        }

        if (!Api.HasDeviceParams())
        {
            Api.ScanDeviceParams();
        }

        readyToRun = true;
        Debug.Log("[VR] Cardboard listo.");
    }

    private void ApagarXR()
    {
        if (Manager == null || !XRIniciado()) return;

        Manager.StopSubsystems();
        Manager.DeinitializeLoader();
    }

    private void OnApplicationQuit()
    {
        // Solo el pescador enciende el XR: se apaga ordenadamente al salir
        if (readyToRun) ApagarXR();
    }

    public void Update()
    {
        if (!readyToRun) return;

#if !UNITY_EDITOR
        if (!XRIniciado()) return;
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
