using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Borde / Marcador Visual")]
    public GameObject outlineObject; // Objeto del borde (ej. 'BordeLuminoso')

    [Header("Configuración de Escalado")]
    [Range(1.0f, 1.3f)]
    public float hoverScale = 1.10f;  // 1.10 = 10% más grande (Escala clara y visible)
    public float pressScale = 0.95f;  // Se contrae levemente al presionar
    public float animSpeed = 15f;

    [Header("Configuración de Color")]
    public bool useColorTint = true;
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 1f, 0.8f, 1f); // Tono amarillo suave/brillante
    public Color pressColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private Vector3 targetScale;
    private Vector3 initialScale;
    private Image tileImage;
    private Color targetColor;

    void Start()
    {
        initialScale = transform.localScale;
        targetScale = initialScale;

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
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);

        // Transición suave de color
        if (useColorTint && tileImage != null)
        {
            tileImage.color = Color.Lerp(tileImage.color, targetColor, Time.unscaledDeltaTime * animSpeed);
        }
    }

    // --- ACTIVAR HOVER (Ratón o Wiimote/Teclado) ---
    public void ActivarHover()
    {
        targetScale = initialScale * hoverScale;
        targetColor = hoverColor;

        if (outlineObject != null)
            outlineObject.SetActive(true);
    }

    // --- DESACTIVAR HOVER ---
    public void DesactivarHover()
    {
        targetScale = initialScale;
        targetColor = normalColor;

        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    // Eventos de Mouse
    public void OnPointerEnter(PointerEventData eventData) => ActivarHover();
    public void OnPointerExit(PointerEventData eventData) => DesactivarHover();

    // Eventos de Wiimote / Teclado (Cuando el script MenuMismaEscena ejecuta .Select())
    public void OnSelect(BaseEventData eventData) => ActivarHover();
    public void OnDeselect(BaseEventData eventData) => DesactivarHover();

    // Clics
    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = initialScale * pressScale;
        targetColor = pressColor;
    }

    public void OnPointerUp(PointerEventData eventData) => ActivarHover();
}