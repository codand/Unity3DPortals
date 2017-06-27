using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetVelocity : MonoBehaviour {
    public bool enable = false;
    public Vector3 velocity;

    Rigidbody _rigidbody;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
    }
	
	// Update is called once per frame
	void FixedUpdate () {
		if (enable) {
            _rigidbody.velocity = velocity;
        } else {
            velocity = _rigidbody.velocity;
        }
	}
}
