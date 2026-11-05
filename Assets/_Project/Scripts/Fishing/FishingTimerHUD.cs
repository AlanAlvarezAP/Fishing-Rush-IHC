using UnityEngine;
using TMPro;

/// <summary>
/// Tiempo de la partida.
/// - Con red: la PC fija la hora de fin (MenuMismaEscena.finPartidaNet, en tiempo del servidor) y
///   cada dispositivo muestra el tiempo restante calculado con esa hora. Asi un celular que se
///   reconecta a mitad de partida ve exactamente el mismo tiempo que la PC.
/// - Solo la PC (o el modo sin red) decide que se acabo el tiempo y muestra el game over a todos;
///   los celulares solo llegan a 00:00 y esperan el game over de la PC.
/// </summary>
public class FishingTimerHUD : MonoBehaviour
{
    [Header("Componentes UI")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Referencias de Menu")]
    [SerializeField] private MenuMismaEscena menuManager;

    [Header("Configuracion de Tiempo")]
    [SerializeField] private float totalGameTime = 180f; // 3 minutos
    private float currentTime;
    private bool isTimerRunning = false;

    /// <summary>Duracion de la partida en segundos (la usa la PC para fijar la hora de fin).</summary>
    public float DuracionPartida => totalGameTime;

    void Awake()
    {
        // Forzamos la limpieza del tiempo al cargar el script o reiniciar la escena
        currentTime = totalGameTime;
        isTimerRunning = false;
        if (menuManager == null) menuManager = FindObjectOfType<MenuMismaEscena>();
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

        if (menuManager != null && menuManager.TiempoSincronizado)
        {
            // Tiempo de la PC (igual en todos los dispositivos)
            currentTime = menuManager.TiempoRestanteRed();
        }
        else
        {
            // Sin red: cuenta local
            currentTime -= Time.deltaTime;
            if (currentTime < 0) currentTime = 0;
        }

        UpdateTimerDisplay(currentTime);

        if (currentTime <= 0f)
        {
            currentTime = 0;
            isTimerRunning = false;
            OnTimeUp();
        }
    }

    public void StartTimer()
    {
        currentTime = totalGameTime; // con red, en el siguiente Update se toma el tiempo de la PC
        isTimerRunning = true;
        Debug.Log("[TEMPORIZADOR] La partida ha comenzado");
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
            timerText.text = "TIEMPO AGOTADO";
            timerText.color = Color.red;
        }

        // Solo la PC (o sin red) termina la partida; a los celulares les llega el game over de la PC
        if (menuManager != null && menuManager.PuedeTerminarPartida)
        {
            menuManager.MostrarGameOver();
        }

        Debug.Log("[TEMPORIZADOR] Se termino el tiempo.");
    }
}
