using UnityEngine;

public class HookTrigger : MonoBehaviour
{
    private ThirdPController playerController;

    void Start()
    {
        playerController = FindObjectOfType<ThirdPController>();
    }

    void OnTriggerEnter(Collider other)
    {
        FishCube fish = other.GetComponent<FishCube>();
        if (fish != null && playerController != null)
        {
            fish.HookMe(transform, playerController.transform);
        }
    }
}