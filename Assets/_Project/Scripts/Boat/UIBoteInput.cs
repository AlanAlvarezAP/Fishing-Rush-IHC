using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIBoteInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static Vector2 InputTactil = Vector2.zero;

    [Header("Dirección de este botón")]
    public Vector2 direccionBoton; // Ej: Adelante (0, 1), Atrás (0, -1), Izquierda (-1, 0), Derecha (1, 0)

    [Header("Letra del boton")]
    public Color colorLetra = new Color(0.1f, 0.1f, 0.1f, 1f);
    public float tamanoLetraMax = 72f;

    private void Start()
    {
        try
        {
            AjustarCanvas();
            AjustarLetra();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PADS] No se pudo ajustar el boton: " + e.Message);
        }
    }

    private void AjustarCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        CanvasScaler scaler = canvas.rootCanvas.GetComponent<CanvasScaler>();
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) return;

        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    }

    private void AjustarLetra()
    {
        TMP_Text letra = GetComponentInChildren<TMP_Text>(true);
        if (letra == null) return;

        letra.gameObject.SetActive(true);
        letra.enabled = true;

        RectTransform rt = letra.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = Vector3.zero;

        letra.enableAutoSizing = true;
        letra.fontSizeMin = 24f;
        letra.fontSizeMax = tamanoLetraMax;
        letra.fontStyle = FontStyles.Bold;
        letra.alignment = TextAlignmentOptions.Center;
        letra.enableWordWrapping = false;
        letra.color = colorLetra;
        letra.raycastTarget = false;
        letra.transform.SetAsLastSibling();

        if (string.IsNullOrWhiteSpace(letra.text))
        {
            if (direccionBoton.y > 0.5f) letra.text = "A";
            else if (direccionBoton.y < -0.5f) letra.text = "R";
            else if (direccionBoton.x < -0.5f) letra.text = "<";
            else if (direccionBoton.x > 0.5f) letra.text = ">";
        }
    }

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