using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class InputManager : MonoBehaviour {
    [SerializeField] float _mouseSensitivity = 3.0f;

    // TODO: Remove;
    [SerializeField] bool _autowalk = false;


    RigidbodyCharacterController _playerController;
    private bool _movementEnabled;

    void Awake() {
        _playerController = GetComponent<RigidbodyCharacterController>();
        _movementEnabled = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update() {
        //if (Input.GetKeyDown(KeyCode.BackQuote)) {
        //    _movementEnabled = !_movementEnabled;
        //    if (_movementEnabled) {
        //        Cursor.lockState = CursorLockMode.Locked;
        //        Cursor.visible = false;
        //    } else {
        //        Cursor.lockState = CursorLockMode.None;
        //        Cursor.visible = true;
        //    }
        //}

        if (!_movementEnabled) {
            return;
        }

        float xRotation = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float yRotation = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        _playerController.Rotate(xRotation, yRotation);

        if (Input.GetKeyDown(KeyCode.Space)) {
            _playerController.Jump();
        }

        if (Input.GetKeyDown(KeyCode.Q)) {
            _playerController.ToggleNoClip();
        }
    }

    void HandleMovement() {
        Vector3 moveDir = Vector3.zero;
        bool moved = false;
        if (_movementEnabled) {
            if (Input.GetKey(KeyCode.W)) {
                moveDir += Camera.main.transform.forward;
                moved = true;
            }
            if (Input.GetKey(KeyCode.A)) {
                moveDir -= Camera.main.transform.right;
                moved = true;
            }
            if (Input.GetKey(KeyCode.S)) {
                moveDir -= Camera.main.transform.forward;
                moved = true;
            }
            if (Input.GetKey(KeyCode.D)) {
                moveDir += Camera.main.transform.right;
                moved = true;
            }

        }
        if (_autowalk) {
            moveDir += Camera.main.transform.forward;
            moved = true;
        }

        if (moved) {
            _playerController.Move(moveDir);
        }
    }

    void FixedUpdate() {
        HandleMovement();
    }
}
