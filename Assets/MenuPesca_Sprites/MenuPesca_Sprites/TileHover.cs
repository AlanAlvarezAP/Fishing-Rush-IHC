using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Borde / Marcador Visual")]
    public GameObject outlineObject; // Arrastra aquí el objeto 'BordeLuminoso' (outline_btn)

    [Header("Configuración de Escalado")]
    public float hoverScale = 1.05f;  // Se agranda ligeramente al pasar el ratón
    public float pressScale = 0.95f;  // Se encoge al presionar
    public float animSpeed = 15f;     // Velocidad de transición

    private Vector3 targetScale;
    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
        targetScale = initialScale;

        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    void Update()
    {
        // Transición suave de tamaño
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animSpeed);
    }

    // Al pasar el ratón sobre la tarjeta
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = initialScale * hoverScale;
        if (outlineObject != null)
            outlineObject.SetActive(true);
    }

    // Al quitar el ratón de la tarjeta
    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = initialScale;
        if (outlineObject != null)
            outlineObject.SetActive(false);
    }

    // Al presionar el clic o botón
    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = initialScale * pressScale;
    }

    // Al soltar el clic
    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = initialScale * hoverScale;
    }
}