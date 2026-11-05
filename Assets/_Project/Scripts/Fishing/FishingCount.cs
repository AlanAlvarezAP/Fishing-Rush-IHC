using UnityEngine;
using TMPro;
using Unity.Netcode;

public class FishingCounterManager : NetworkBehaviour
{
    public static FishingCounterManager Instance;

    [Header("Componentes UI")]
    [SerializeField] private TextMeshProUGUI fishCountText;

    [Header("Configuracion")]
    [SerializeField] private string prefix = "Peces atrapados: ";

    private NetworkVariable<int> fishCountNet = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    //private int fishCount = 0;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        fishCountNet.OnValueChanged += OnFishCountChanged;
        ActualizarTextoUI(fishCountNet.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        fishCountNet.OnValueChanged -= OnFishCountChanged;
    }

    //void Start()
    //{
    //    ResetCounter();
    //}

    private void OnFishCountChanged(int previousValue, int newValue)
    {
        ActualizarTextoUI(newValue);
    }

    //public void AddFish()
    //{
    //    fishCount++;
    //    UpdateCounterDisplay();
    //    Debug.Log("[PESCA] Pez capturado. Total: " + fishCount);
    //}

    public void AddFish(int amount = 1)
    {
        if (IsServer || IsHost)
        {
            fishCountNet.Value += amount;
        }
        else
        {
            AddFishServerRpc(amount);
        }
        Debug.Log("[PESCA] Pez capturado. Total: " + fishCountNet);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddFishServerRpc(int amount)
    {
        fishCountNet.Value += amount;
    }

    public void ResetCounter()
    {
        if (IsServer || IsHost)
        {
            fishCountNet.Value = 0;
        }
        else if (IsSpawned)
        {
            ResetCounterServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetCounterServerRpc()
    {
        fishCountNet.Value = 0;
    }

    //private void UpdateCounterDisplay()
    //{
    //    if (fishCounterText != null)
    //    {
    //        fishCounterText.text = prefix + fishCount;
    //    }
    //}

    public int GetFishCount()
    {
        return IsSpawned ? fishCountNet.Value : 0;
    }

    private void ActualizarTextoUI(int count)
    {
        if (fishCountText != null)
        {
            fishCountText.text = $"Peces: {count}";
        }
    }
}