using TMPro;
using UnityEngine;

public static class FuenteSegura
{
    private static TMP_FontAsset fuente;

    private static bool UsaShaderMovil(TMP_FontAsset f)
    {
        return f != null && f.material != null && f.material.shader != null && f.material.shader.name.Contains("Mobile");
    }

    public static TMP_FontAsset Obtener()
    {
        if (fuente != null) return fuente;

        const string necesarios = "AZaz09%+#<>-";
        int mejorPuntos = 0;

        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null || f.material == null || f.material.shader == null) continue;
            if (UsaShaderMovil(f)) continue;

            bool completa = true;
            foreach (char c in necesarios)
            {
                if (!f.HasCharacter(c))
                {
                    completa = false;
                    break;
                }
            }
            if (!completa) continue;

            int puntos = 1;
            if (f.name.Contains("Lato-Bold")) puntos = 3;
            else if (f.name.Contains("Lato")) puntos = 2;

            if (puntos > mejorPuntos)
            {
                mejorPuntos = puntos;
                fuente = f;
            }
        }

        return fuente;
    }

    public static void Aplicar(TMP_Text texto)
    {
        if (texto == null) return;

        TMP_FontAsset f = Obtener();
        if (f != null && texto.font != f) texto.font = f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ReemplazarEnConductorMovil()
    {
        if (!Application.isMobilePlatform || !CamaraActivaUtil.EsConductor) return;

        TMP_FontAsset f = Obtener();
        if (f == null)
        {
            Debug.LogWarning("[FUENTE] No se encontro una fuente de reemplazo.");
            return;
        }

        int cambiados = 0;
        foreach (TMP_Text texto in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (texto == null || !(texto is TextMeshProUGUI)) continue;
            if (!texto.gameObject.scene.IsValid()) continue;
            if (!UsaShaderMovil(texto.font)) continue;

            texto.font = f;
            cambiados++;
        }

        Debug.Log($"[FUENTE] {cambiados} textos pasados a {f.name}.");
    }
}
