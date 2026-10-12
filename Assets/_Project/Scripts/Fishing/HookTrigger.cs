using UnityEngine;
using Unity.Netcode;

public class HookTrigger : MonoBehaviour
{
    private ThirdPController playerController;

    void Start()
    {
        BuscarJugador();
    }

    private void BuscarJugador()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<ThirdPController>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        FishCube fish = other.GetComponent<FishCube>();
        if (fish != null)
        {
            BuscarJugador();
            if (playerController != null)
            {
                fish.HookMe(transform, playerController.transform);
            }
        }
    }
}