using UnityEngine;

public static class CamaraActivaUtil
{
    private static Camera cache;
    private static float proximaBusqueda;

    public static bool EsConductor => ConfigJugador.RolActual == TipoJugador.Jugador2_ConductorTablet;

#if UNITY_ANDROID && !UNITY_EDITOR
    public static bool EnVR => !EsConductor;
#else
    public static bool EnVR => UnityEngine.XR.XRSettings.enabled && !EsConductor;
#endif

    public static Camera Buscar(Camera preferida = null)
    {
        bool conductor = EsConductor;

        if (!conductor && preferida != null && preferida.isActiveAndEnabled) return preferida;

        if (cache != null && cache.isActiveAndEnabled && Time.unscaledTime < proximaBusqueda) return cache;
        proximaBusqueda = Time.unscaledTime + 0.5f;

        Camera mejor = null;
        int mejorPuntos = int.MinValue;

        foreach (Camera c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            if (c.targetTexture != null) continue;
            if (c.cameraType != CameraType.Game) continue;

            int puntos = Mathf.RoundToInt(c.depth);
            if (c.name == "BoatCamera") puntos += conductor ? 1000 : -1000;
            if (c.GetComponentInParent<ThirdPController>() != null) puntos += conductor ? -100 : 100;
            if (c.CompareTag("MainCamera")) puntos += 10;

            if (puntos > mejorPuntos)
            {
                mejorPuntos = puntos;
                mejor = c;
            }
        }

        cache = mejor;
        return mejor;
    }

    public static void ColocarFrente(Canvas canvas, Camera cam, Vector2 tamano, float distancia, float ancho)
    {
        if (canvas == null || cam == null || tamano.x <= 0f) return;

        if (canvas.renderMode != RenderMode.WorldSpace) canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.worldCamera != cam) canvas.worldCamera = cam;

        RectTransform rt = (RectTransform)canvas.transform;
        float escala = ancho / tamano.x;

        rt.sizeDelta = tamano;
        rt.localScale = new Vector3(escala, escala, escala);

        Transform t = cam.transform;
        rt.SetPositionAndRotation(t.position + t.forward * distancia, t.rotation);
    }
}
