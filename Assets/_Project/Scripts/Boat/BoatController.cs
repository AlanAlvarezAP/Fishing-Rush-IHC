using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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