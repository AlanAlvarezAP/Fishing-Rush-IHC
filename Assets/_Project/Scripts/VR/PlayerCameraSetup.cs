using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerCameraSetup : NetworkBehaviour {
    [SerializeField] private Camera vrCamera;
    [SerializeField] private AudioListener vrAudioListener;

    public override void OnNetworkSpawn() {
        
        if (!IsOwner)
        {
            // Este Player no es el mío (es el avatar de otro jugador
            // conectado). No debo ver ni escuchar a través de su cámara.
            if (vrCamera != null) vrCamera.enabled = false;
            if (vrAudioListener != null) vrAudioListener.enabled = false;
            return;
        }
 
        // Soy el dueño de este Player: activo su cámara y audio listener.
        if (vrCamera != null) vrCamera.enabled = true;
        if (vrAudioListener != null) vrAudioListener.enabled = true;
 
        // Apago la Main Camera de la escena (la que no pertenece a
        // ningún Player) para no quedarme viendo por esa en vez de
        // por la del prefab.
        if (Camera.main != null && Camera.main != vrCamera)
        {
            Camera.main.gameObject.SetActive(false);
        }
    }
}