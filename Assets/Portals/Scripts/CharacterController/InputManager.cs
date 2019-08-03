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
#if UNITY_IOS || UNITY_ANDROID
        //foreach (Touch touch in Input.touches) {
        //    switch (touch.phase) {
        //        case TouchPhase.Began:
        //            break;
        //        case TouchPhase.Canceled:
        //            break;
        //        case TouchPhase.Ended:
        //            break;
        //        case TouchPhase.Moved:
        //            float rotationX = touch.deltaPosition.x * _mouseSensitivity;
        //            _playerController.Rotate(rotationX);

        //            float rotationY = touch.deltaPosition.y * _mouseSensitivity;
        //            _playerController.RotateHead(rotationY);
        //            break;
        //        case TouchPhase.Stationary:
        //            break;
        //    }
        //}
        float rotationX = UnityStandardAssets.CrossPlatformInput.CrossPlatformInputManager.GetAxis("RightHorizontal") * _mouseSensitivity;
        if (rotationX != 0) {
            _playerController.Rotate(rotationX);
        }

        float rotationY = UnityStandardAssets.CrossPlatformInput.CrossPlatformInputManager.GetAxis("RightVertical") * _mouseSensitivity;
        if (rotationY != 0) {
            _playerController.RotateHead(rotationY);
        }
#else
        if (Input.GetKeyDown(KeyCode.BackQuote)) {
            _movementEnabled = !_movementEnabled;
            if (_movementEnabled) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            } else {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (!_movementEnabled) {
            return;
        }

        float rotationX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        if (rotationX != 0) {
            _playerController.Rotate(rotationX);
        }

        float rotationY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        if (rotationY != 0) {
            _playerController.RotateHead(rotationY);
        }

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

#if UNITY_ANDROID || UNITY_IOS
        MobileMovement();
#else
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
#endif
    }

    void MobileMovement() {
        float vertical = UnityStandardAssets.CrossPlatformInput.CrossPlatformInputManager.GetAxis("LeftVertical");
        float horizontal = UnityStandardAssets.CrossPlatformInput.CrossPlatformInputManager.GetAxis("LeftHorizontal");

        Vector3 movement = Camera.main.transform.forward * vertical + Camera.main.transform.right * horizontal;
        _playerController.Move(movement);
    }

    void FixedUpdate() {
        HandleMovement();
    }
}
