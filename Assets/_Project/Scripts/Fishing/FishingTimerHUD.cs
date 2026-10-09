using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FishingTimerHUD : MonoBehaviour
{
    [Header("Componentes UI")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Referencias de Menú")]
    [SerializeField] private MenuMismaEscena menuManager;

    [Header("Configuración de Tiempo")]
    [SerializeField] private float totalGameTime = 180f; // 3 minutos
    private float currentTime;
    private bool isTimerRunning = false;

    void Awake()
    {
        // Forzamos la limpieza del tiempo al cargar el script o reiniciar la escena
        currentTime = totalGameTime;
        isTimerRunning = false;
    }

    void Start()
    {
        currentTime = totalGameTime;
        isTimerRunning = false;
        UpdateTimerDisplay(currentTime);
    }

    void Update()
    {
        if (!isTimerRunning) return;

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            if (currentTime < 0) currentTime = 0;

            UpdateTimerDisplay(currentTime);
        }
        else
        {
            currentTime = 0;
            isTimerRunning = false;
            OnTimeUp();
        }
    }

    public void StartTimer()
    {
        currentTime = totalGameTime; // Reinicia el tiempo exacto al presionar Jugar
        isTimerRunning = true;
        Debug.Log("[TEMPORIZADOR] ¡Los 3 minutos han comenzado!");
    }

    private void UpdateTimerDisplay(float timeToDisplay)
    {
        if (timerText == null) return;

        float minutes = Mathf.FloorToInt(timeToDisplay / 60f);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (timeToDisplay <= 30f)
        {
            timerText.color = Color.red;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void OnTimeUp()
    {
        if (timerText != null)
        {
            timerText.text = "¡TIEMPO AGOTADO!";
            timerText.color = Color.red;
        }

        if (menuManager != null)
        {
            menuManager.MostrarGameOver();
        }

        Debug.Log("[TEMPORIZADOR] Los 3 minutos han terminado.");
    }
}