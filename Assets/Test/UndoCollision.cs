using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UndoCollision : MonoBehaviour {
    Rigidbody _rigidbody;
    private Vector3 velocity;
    private Vector3 angularVelocity;
    private Vector3 position;
    private Quaternion rotation;

    public Collider foo;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate() {
        velocity = _rigidbody.velocity;
        angularVelocity = _rigidbody.angularVelocity;
        position = _rigidbody.position;
        rotation = _rigidbody.rotation;
    }

	void OnTriggerEnter(Collider collider) {
        if (collider.gameObject.layer != LayerMask.NameToLayer("Portal")) {
            return;
        }

        if (foo) {
            Physics.IgnoreCollision(GetComponent<Collider>(), foo);
        }

        _rigidbody.position = position + velocity * Time.fixedDeltaTime + (1 / 2) * Physics.gravity * Time.fixedDeltaTime * Time.fixedDeltaTime;
        _rigidbody.rotation = rotation * Quaternion.Euler(angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime);

        _rigidbody.velocity = velocity + Physics.gravity * Time.fixedDeltaTime;
        _rigidbody.angularVelocity = angularVelocity;

        transform.position = _rigidbody.position;
        transform.rotation = _rigidbody.rotation;

        Debug.Log("trigger enter");
    }
}

// a = a0
// v = a0t + v0
// x = (1/2)a0t^2 + v0t + x0

// a = a0
// w = a0t + w0
// theta = 
