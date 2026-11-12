using UnityEngine;

/// <summary>
/// NUEVO. Utilidad para saber con que camara se esta viendo el juego en ESTE dispositivo.
/// Hace falta porque depende del dispositivo:
///   - PC (pescador): por defecto se ve en TERCERA persona con "Main Camera"; la camara de
///     primera persona (FirstPersonCamera) esta apagada salvo que se pulse C.
///   - Celular VR: FirstPersonCamera.
///   - Tablet / conductor: BoatCamera.
/// </summary>
public static class CamaraActivaUtil
{
    /// <summary>true si el dispositivo esta dibujando en modo VR (visor).</summary>
    public static bool EnVR => UnityEngine.XR.XRSettings.enabled;

    /// <summary>
    /// Devuelve 'preferida' si esta encendida; si no, la camara encendida que dibuja en pantalla
    /// con mayor profundidad (se ignoran las que dibujan a una textura, como la del Wiimote).
    /// </summary>
    public static Camera Buscar(Camera preferida = null)
    {
        if (preferida != null && preferida.isActiveAndEnabled) return preferida;

        Camera mejor = null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            if (c.targetTexture != null) continue;
            if (c.cameraType != CameraType.Game) continue;

            if (mejor == null || c.depth > mejor.depth) mejor = c;
        }
        return mejor;
    }
}
