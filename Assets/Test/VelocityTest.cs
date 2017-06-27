using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VelocityTest : MonoBehaviour {
    Rigidbody _rigidbody;
    Vector3 savedVelocity = Vector3.zero;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate() {
        savedVelocity = _rigidbody.velocity;
    }

	void OnCollisionEnter(Collision collision) {
        Debug.Log("Saved Velocity: " + savedVelocity);
        Debug.Log("Velocity: " + _rigidbody.velocity);
    }
}
