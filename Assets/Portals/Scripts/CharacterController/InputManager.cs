using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class InputManager : MonoBehaviour {
    [SerializeField] float _mouseSensitivity = 3.0f;

    RigidbodyCharacterController _playerController;

    void Awake() {
        _playerController = GetComponent<RigidbodyCharacterController>();
    }


    void Update() {
#if UNITY_IOS || UNITY_ANDROID
        foreach (Touch touch in Input.touches) {
            switch (touch.phase) {
                case TouchPhase.Began:
                    break;
                case TouchPhase.Canceled:
                    break;
                case TouchPhase.Ended:
                    break;
                case TouchPhase.Moved:
                    float rotationX = touch.deltaPosition.x * Config.mouseSensitivity;
                    _playerController.Rotate(rotationX);

                    float rotationY = touch.deltaPosition.y * Config.mouseSensitivity;
                    _playerController.RotateHead(rotationY);
                    break;
                case TouchPhase.Stationary:
                    break;
            }
        }
#else
        // In game actions
        float rotationX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        _playerController.Rotate(rotationX);

        float rotationY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        _playerController.RotateHead(rotationY);

        if (Input.GetKeyDown(KeyCode.Space)) {
            _playerController.Jump();
        }

        if (Input.GetKeyDown(KeyCode.Q)) {
            _playerController.ToggleNoClip();
        }

        if (Input.GetMouseButtonDown(0)) {
            //GetComponent<PowerKinesis>().Fire(Camera.main);
        }
#endif
    }

    void HandleMovement() {
        Vector3 moveDir = Vector3.zero;
        bool moved = false;
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

        if (moved) {
            _playerController.Move(moveDir);
        }
    }

    void FixedUpdate() {
        HandleMovement();
    }
}
