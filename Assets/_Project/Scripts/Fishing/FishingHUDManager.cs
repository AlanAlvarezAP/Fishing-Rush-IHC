using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode;

public class FishingHUDManager : NetworkBehaviour
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

    /*
    void Start()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }
    }
    */
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
        HideAllLocal();
    }

    public void EnsureCamera()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            if (canvas.worldCamera == null || canvas.worldCamera != Camera.main)
            {
                canvas.worldCamera = Camera.main;
            }
        }
        /*
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }
        */
    }

    // --- POTENCIA DE LANZAMIENTO ---
    public void ShowCastPower(float force)
    {
        if (IsServer || IsHost)
        {
            ShowCastPowerClientRpc(force);
        }
        else
        {
            ShowCastPowerLocal(force);
        }
    }

    [ClientRpc]
    private void ShowCastPowerClientRpc(float force)
    {
        ShowCastPowerLocal(force);
    }

    private void ShowCastPowerLocal(float force)
    {
        EnsureCamera();
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

    // --- GESTOS REQUERIDOS ---
    public void ShowGesturePrompt(string gestureName, int hits, int attempts)
    {
        if (IsServer || IsHost)
        {
            ShowGesturePromptClientRpc(gestureName, hits, attempts);
        }
        else
        {
            ShowGesturePromptLocal(gestureName, hits, attempts);
        }
    }

    [ClientRpc]
    private void ShowGesturePromptClientRpc(string gestureName, int hits, int attempts)
    {
        ShowGesturePromptLocal(gestureName, hits, attempts);
    }

    private void ShowGesturePromptLocal(string gestureName, int hits, int attempts)
    {
        EnsureCamera();
        if (gesturePanel == null || gestureText == null) return;

        if (string.IsNullOrEmpty(gestureName) || gestureName == "None")
        {
            HideGesturePromptLocal();
            return;
        }

        gestureText.text = $"{gestureName.ToUpper()} {hits}/{attempts}";
        gesturePanel.SetActive(true);
    }

    public void HideGesturePrompt()
    {
        if (IsServer || IsHost)
        {
            HideGesturePromptClientRpc();
        }
        else
        {
            HideGesturePromptLocal();
        }
    }

    [ClientRpc]
    private void HideGesturePromptClientRpc()
    {
        HideGesturePromptLocal();
    }

    private void HideGesturePromptLocal()
    {
        if (gesturePanel != null) gesturePanel.SetActive(false);
    }

    // --- VENTANA DE TIRO ---
    public void ShowCastWindowTimer(float timeLeft)
    {
        if (IsServer || IsHost)
        {
            ShowCastWindowTimerClientRpc(timeLeft);
        }
        else
        {
            ShowCastWindowTimerLocal(timeLeft);
        }
    }

    [ClientRpc]
    private void ShowCastWindowTimerClientRpc(float timeLeft)
    {
        ShowCastWindowTimerLocal(timeLeft);
    }

    private void ShowCastWindowTimerLocal(float timeLeft)
    {
        EnsureCamera();
        if (castWindowPanel == null || castWindowText == null) return;

        castWindowText.text = timeLeft.ToString("F1");
        if (!castWindowPanel.activeSelf) castWindowPanel.SetActive(true);
    }

    public void HideCastWindowTimer()
    {
        if (IsServer || IsHost)
        {
            HideCastWindowTimerClientRpc();
        }
        else
        {
            HideCastWindowTimerLocal();
        }
    }

    [ClientRpc]
    private void HideCastWindowTimerClientRpc()
    {
        HideCastWindowTimerLocal();
    }

    private void HideCastWindowTimerLocal()
    {
        if (castWindowPanel != null && castWindowPanel.activeSelf)
        {
            castWindowPanel.SetActive(false);
        }
    }

    // --- OCULTAR TODO ---
    public void HideAll()
    {
        if (IsServer || IsHost)
        {
            HideAllClientRpc();
        }
        else
        {
            HideAllLocal();
        }
    }

    [ClientRpc]
    private void HideAllClientRpc()
    {
        HideAllLocal();
    }

    private void HideAllLocal()
    {
        if (powerPanel != null) powerPanel.SetActive(false);
        if (gesturePanel != null) gesturePanel.SetActive(false);
        if (castWindowPanel != null) castWindowPanel.SetActive(false);
    }
}