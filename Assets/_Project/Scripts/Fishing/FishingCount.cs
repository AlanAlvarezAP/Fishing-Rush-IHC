using UnityEngine;
using TMPro;

public class FishingCounterManager : MonoBehaviour
{
    public static FishingCounterManager Instance;

    [Header("Componentes UI")]
    [SerializeField] private TextMeshProUGUI fishCounterText;

    [Header("Configuración")]
    [SerializeField] private string prefix = "Peces atrapados: ";

    private int fishCount = 0;

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
    }

    void Start()
    {
        ResetCounter();
    }

    public void AddFish()
    {
        fishCount++;
        UpdateCounterDisplay();
        Debug.Log("[PESCA] Pez capturado. Total: " + fishCount);
    }

    public void ResetCounter()
    {
        fishCount = 0;
        UpdateCounterDisplay();
    }

    private void UpdateCounterDisplay()
    {
        if (fishCounterText != null)
        {
            fishCounterText.text = prefix + fishCount;
        }
    }

    public int GetFishCount()
    {
        return fishCount;
    }
}