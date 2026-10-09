using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class BoatController : NetworkBehaviour
{
    [Header("Ajustes de Movimiento")]
    [SerializeField] private float moveSpeed = 8.0f;
    [SerializeField] private float turnSpeed = 40.0f;

    private bool isBeingDriven = false;

    public void SetDrivingState(bool driving)
    {
        isBeingDriven = driving;
    }

    void Update()
    {
        if (!isBeingDriven || !IsServer) return;

        float vertical = Input.GetAxis("Vertical");
        float horizontal = Input.GetAxis("Horizontal");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            // Lectura de la cruceta direccional (D-Pad) del Wiimote
            if (wiimote.Button.d_up) vertical = 1f;
            if (wiimote.Button.d_down) vertical = -1f;
            if (wiimote.Button.d_left) horizontal = -1f;
            if (wiimote.Button.d_right) horizontal = 1f;
        }
#endif

        if (Mathf.Abs(vertical) > 0.1f)
        {
            Vector3 moveDirection = transform.forward * vertical * moveSpeed * Time.deltaTime;
            transform.position += moveDirection;
        }

        if (Mathf.Abs(horizontal) > 0.1f)
        {
            float rotationAmount = horizontal * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationAmount);
        }
    }
}