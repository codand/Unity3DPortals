using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    [RequireComponent(typeof(Rigidbody))]
    public class GravityManipulator : MonoBehaviour {
        public Vector3 upVector = Vector3.up;
        public bool useGravity = true;
        public bool invert = false;
        private Rigidbody _rigidbody;

        private void Start() {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
        }

        private void FixedUpdate() {
            _rigidbody.useGravity = false;
            if (this.useGravity && !_rigidbody.isKinematic) {
                // Gravity is scaled with the size of the object
                float scaleFactor = Portals.MathUtil.VectorInternalAverage(this.transform.localScale);
                Vector3 gravityForce = -1 * Physics.gravity.magnitude * this.upVector;
                _rigidbody.AddForce(gravityForce * scaleFactor, ForceMode.Acceleration);
            }
        }

        private void OnPortalTeleport(Portal portal) {
            if (portal.ModifyGravity) {
                Vector3 newUp = portal.TeleportVector(upVector);
                upVector = newUp.normalized;
                if (invert) {
                    upVector *= -1;
                }
            }
        }
    }
}
