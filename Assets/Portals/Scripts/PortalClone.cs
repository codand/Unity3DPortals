using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class PortalClone : MonoBehaviour {
    public Transform target;
    public Portal portal;

    void Awake() {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody) {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }
    }

    void Start() {
        portal.ApplyWorldToPortalTransform(this.transform, target);
    }

	void LateUpdate () {
        portal.ApplyWorldToPortalTransform(this.transform, target);
	}
}
