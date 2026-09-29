using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementController : MonoBehaviour
{

    [SerializeField]
    PlayerStats stats;

    Vector2 moveInput;

    [SerializeField]
    float moveSpeed = 5f;

    //[SerializeField]
    CharacterController controller;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();

        //Debug.Log(stats.Health);
    }

    // Update is called once per frame
    void Update()
    {
        controller.Move(moveSpeed * Time.deltaTime * new Vector3(moveInput.x, 0, moveInput.y));
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        stats.Health += 10;
    }
}
