using UnityEngine;

public static class CamaraActivaUtil
{
    public static bool EnVR => UnityEngine.XR.XRSettings.enabled;

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
