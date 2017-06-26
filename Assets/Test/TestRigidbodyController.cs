using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestRigidbodyController : MonoBehaviour {
    [SerializeField]
    float _mouseSensitivity = 3.0f;

    public Transform head;
    public float force;

    Rigidbody _rigidbody;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void Update() {
        float rotationX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        if (rotationX != 0) {
            Rotate(rotationX);
        }

        float rotationY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        if (rotationY != 0) {
            RotateHead(rotationY);
        }
    }

    private void FixedUpdate() {
        Vector3 moveDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) {
            moveDir += Camera.main.transform.forward;
        }
        if (Input.GetKey(KeyCode.A)) {
            moveDir -= Camera.main.transform.right;
        }
        if (Input.GetKey(KeyCode.S)) {
            moveDir -= Camera.main.transform.forward;
        }
        if (Input.GetKey(KeyCode.D)) {
            moveDir += Camera.main.transform.right;
        }

        _rigidbody.AddForce(moveDir * force);
    }

    public void Rotate(float angle) {
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        //this.transform.localRotation = this.transform.localRotation * rotation;
        _rigidbody.MoveRotation(_rigidbody.rotation * rotation);
    }

    public void RotateHead(float angle) {
        head.localRotation *= Quaternion.Euler(-angle, 0, 0);
    }
}
