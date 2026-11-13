using UnityEngine;

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

        float crecimiento = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(0.5f, diametroFinal, crecimiento);

        PonerColor(Color.Lerp(colorInicio, colorFin, t));

        if (t >= 1f) Destroy(gameObject);
    }

    private void PonerColor(Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
