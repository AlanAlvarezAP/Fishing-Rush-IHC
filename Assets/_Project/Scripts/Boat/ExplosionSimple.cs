using UnityEngine;

/// <summary>
/// NUEVO (minas). Explosion low-poly hecha por codigo (sin particulas ni assets):
/// una esfera que crece rapido y pasa de amarillo a rojo oscuro, y luego desaparece.
/// Uso: ExplosionSimple.Crear(posicion, materialBase);
///
/// OJO: 'materialBase' es obligatorio en la practica. El material por defecto de las primitivas
/// creadas por codigo usa un shader que en las builds con URP no existe, y la esfera sale ROSA.
/// Por eso se copia un material que ya esta en la build (el de la propia mina) y solo se le
/// cambia el color.
/// </summary>
public class ExplosionSimple : MonoBehaviour
{
    private float duracion = 0.6f;
    private float diametroFinal = 7f;
    private float tiempo = 0f;
    private Material material;
    private Material materialBase;

    private static readonly Color colorInicio = new Color(1f, 0.9f, 0.3f);
    private static readonly Color colorFin = new Color(0.5f, 0.05f, 0f);

    public static void Crear(Vector3 posicion, Material materialBase = null, float diametroFinal = 7f, float duracion = 0.6f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ExplosionMina";
        go.transform.position = posicion;
        go.transform.localScale = Vector3.one * 0.5f;

        // Es solo visual: sin collider para no empujar ni disparar triggers
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }

        ExplosionSimple explosion = go.AddComponent<ExplosionSimple>();
        explosion.materialBase = materialBase;
        explosion.diametroFinal = diametroFinal;
        explosion.duracion = Mathf.Max(0.05f, duracion);
    }

    private void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            // Copia propia del material (se destruye al final). Con materialBase el shader es
            // uno que seguro esta en la build; sin el, queda el de la primitiva (rosa en builds URP).
            if (materialBase != null)
            {
                material = new Material(materialBase);
                r.sharedMaterial = material;
            }
            else
            {
                material = r.material;
            }
            PonerColor(colorInicio);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        float t = Mathf.Clamp01(tiempo / duracion);

        // Crece rapido al inicio y frena al final
        float crecimiento = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(0.5f, diametroFinal, crecimiento);

        PonerColor(Color.Lerp(colorInicio, colorFin, t));

        if (t >= 1f) Destroy(gameObject);
    }

    private void PonerColor(Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);         // Built-in
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
