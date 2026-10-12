using UnityEngine;
using TMPro;
using System.Collections;

public class FishingHUDManager : MonoBehaviour
{
    public static FishingHUDManager Instance;

    [Header("Panel de Potencia de Lanzamiento")]
    [SerializeField] private GameObject powerPanel;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private float powerDisplayDuration = 2.0f;

    [Header("Panel de Gestos Requeridos")]
    [SerializeField] private GameObject gesturePanel;
    [SerializeField] private TextMeshProUGUI gestureText;

    private Coroutine powerCoroutine;

    [Header("Panel Ventana de Tiro")]
    [SerializeField] private GameObject castWindowPanel;
    [SerializeField] private TextMeshProUGUI castWindowText;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        HideAll();
    }

    /// <summary>
    /// Envía únicamente el valor flotante de la fuerza al componente de texto.
    /// </summary>
    public void ShowCastPower(float force)
    {
        if (powerPanel == null || powerText == null) return;

        if (powerCoroutine != null) StopCoroutine(powerCoroutine);

        powerText.text = force.ToString("F1");
        powerPanel.SetActive(true);

        powerCoroutine = StartCoroutine(HidePowerPanelRoutine());
    }

    private IEnumerator HidePowerPanelRoutine()
    {
        yield return new WaitForSeconds(powerDisplayDuration);
        if (powerPanel != null) powerPanel.SetActive(false);
    }

    /// <summary>
    /// Envía únicamente el nombre del gesto en texto plano al componente.
    /// </summary>
    public void ShowGesturePrompt(string gestureName, int hits, int attempts)
    {
        if (gesturePanel == null || gestureText == null) return;

        if (string.IsNullOrEmpty(gestureName) || gestureName == "None")
        {
            HideGesturePrompt();
            return;
        }

        // Muestra por ejemplo: "HORIZONTAL 1/2"
        gestureText.text = $"{gestureName.ToUpper()} {hits}/{attempts}";
        gesturePanel.SetActive(true);
    }

    public void HideGesturePrompt()
    {
        if (gesturePanel != null) gesturePanel.SetActive(false);
    }


    public void ShowCastWindowTimer(float timeLeft)
    {
        if (castWindowPanel == null || castWindowText == null) return;

        castWindowText.text = timeLeft.ToString("F1");
        if (!castWindowPanel.activeSelf) castWindowPanel.SetActive(true);
    }

    public void HideCastWindowTimer()
    {
        if (castWindowPanel != null && castWindowPanel.activeSelf)
        {
            castWindowPanel.SetActive(false);
        }
    }


    // --- ACTUALIZAR HideAll() ---
    public void HideAll()
    {
        if (powerPanel != null) powerPanel.SetActive(false);
        if (gesturePanel != null) gesturePanel.SetActive(false);
        if (castWindowPanel != null) castWindowPanel.SetActive(false);
    }


}