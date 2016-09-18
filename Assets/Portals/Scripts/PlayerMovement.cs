using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour {
    [SerializeField] Camera _camera;
    [SerializeField] float _movespeed = 5.0f;
    [SerializeField] float _mouseSensitivity = 1.0f;
    [SerializeField] float _keySpeedMultiplier = 10.0f;
    [SerializeField] KeyCode _fastKey = KeyCode.LeftShift;
    [SerializeField] KeyCode _slowKey = KeyCode.F;
    [SerializeField] bool _canFly = false;
    //[SerializeField] KeyCode _flyKey = KeyCode.Q;

    Rigidbody _rigidbody;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
    }

	void FixedUpdate () {
        // Translate
        Vector3 moveDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) {
            moveDir += _camera.transform.forward;
        }
        if (Input.GetKey(KeyCode.A)) {
            moveDir -= _camera.transform.right;
        }
        if (Input.GetKey(KeyCode.S)) {
            moveDir -= _camera.transform.forward;
        }
        if (Input.GetKey(KeyCode.D)) {
            moveDir += _camera.transform.right;
        }

        if (!_canFly) {
            moveDir.y = 0;
            moveDir = moveDir.normalized;
        }
        Vector3 deltaMove = moveDir * _movespeed * transform.localScale.magnitude * Time.deltaTime;
        if (Input.GetKey(_fastKey)) {
            deltaMove *= _keySpeedMultiplier;
        }
        if (Input.GetKey(_slowKey)) {
            deltaMove /= _keySpeedMultiplier;
        }

        _rigidbody.MovePosition(_rigidbody.position + deltaMove);

        // Rotate
        float rotationX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);
        _rigidbody.MoveRotation(_rigidbody.rotation * xQuaternion);

        float rotationY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, -Vector3.right);
        _camera.transform.localRotation = _camera.transform.localRotation * yQuaternion;
    }
}
