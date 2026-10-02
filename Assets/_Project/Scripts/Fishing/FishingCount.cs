using UnityEngine;

public class FishingCounterManager : MonoBehaviour
{
    public static FishingCounterManager Instance;
    private int fishCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddFish()
    {
        fishCount++;
        Debug.Log("¡Pez capturado! Total: " + fishCount);
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.normal.textColor = Color.cyan;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.UpperRight;

        float boxWidth = 250f;
        float boxHeight = 50f;
        float posX = Screen.width - boxWidth - 30f;
        float posY = 30f;

        GUI.Label(new Rect(posX, posY, boxWidth, boxHeight), "Peces atrapados: " + fishCount, style);
    }
}