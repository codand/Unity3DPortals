using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    [RequireComponent(typeof(Rigidbody))]
    public class GravityManipulator : MonoBehaviour {
        public Vector3 upVector = Vector3.up;
        public bool useGravity = true;

        private Rigidbody _rigidbody;

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
        }

        private void FixedUpdate() {
            _rigidbody.useGravity = false;
            if (this.useGravity && !_rigidbody.isKinematic) {
                // Gravity is scaled with the size of the object
                float scaleFactor = Portals.Helpers.VectorInternalAverage(this.transform.localScale);
                Vector3 gravityForce = -1 * Physics.gravity.magnitude * this.upVector;
                _rigidbody.AddForce(gravityForce * scaleFactor, ForceMode.Acceleration);
            }
        }

        private void OnPortalExit(Portal portal) {
            Vector3 newUp = portal.WorldToPortalQuaternion() * upVector;
            upVector = newUp.normalized;
        }
    }
}