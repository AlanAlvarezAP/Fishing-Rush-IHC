using UnityEngine;
using UnityEngine.EventSystems;

public class UIBoteInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static Vector2 InputTactil = Vector2.zero;

    [Header("Dirección de este botón")]
    public Vector2 direccionBoton; // Ej: Adelante (0, 1), Atrás (0, -1), Izquierda (-1, 0), Derecha (1, 0)

    public void OnPointerDown(PointerEventData eventData)
    {
        InputTactil += direccionBoton;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputTactil -= direccionBoton;
    }

    private void OnDisable()
    {
        InputTactil = Vector2.zero;
    }
}