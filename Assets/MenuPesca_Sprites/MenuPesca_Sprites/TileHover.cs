using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Borde / Marcador Visual")]
    public GameObject outlineObject; // Arrastra aquí el objeto 'BordeLuminoso'

    [Header("Configuración de Escalado")]
    public float hoverScale = 50.0f;  // Ligeramente más grande para que se note más
    public float pressScale = 0.92f;
    public float animSpeed = 15f;

    [Header("Configuración de Color (Feedback Visual)")]
    public bool useColorTint = true;
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(10.5f, 10.5f, 10.5f, 1f); // Un poco más brillante
    public Color pressColor = new Color(0.85f, 0.85f, 0.85f, 1f);  // Un poco más oscuro al presionar

    private Vector3 targetScale;
    private Vector3 initialScale;
    private Image tileImage;
    private Color targetColor;

    void Start()
    {
        initialScale = transform.localScale;
        targetScale = initialScale;

        // Intentar obtener el componente Image del propio objeto o buscarlo
        tileImage = GetComponent<Image>();
        if (tileImage != null)
        {
            normalColor = tileImage.color;
            targetColor = normalColor;
        }

        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    void Update()
    {
        // Transición suave de tamaño
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animSpeed);

        // Transición suave de color
        if (useColorTint && tileImage != null)
        {
            tileImage.color = Color.Lerp(tileImage.color, targetColor, Time.deltaTime * animSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = initialScale * hoverScale;
        targetColor = hoverColor;

        if (outlineObject != null)
            outlineObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = initialScale;
        targetColor = normalColor;

        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = initialScale * pressScale;
        targetColor = pressColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = initialScale * hoverScale;
        targetColor = hoverColor;
    }
}