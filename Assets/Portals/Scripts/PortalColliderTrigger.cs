using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class PortalColliderTrigger : MonoBehaviour {
        [SerializeField] private Portal _portal;

        private int _triggerCount = 0;
        private List<Collider> _ignoredColliders = new List<Collider>();

        public void ResetIgnoredCollisions() {
            for(int i = 0; i < _ignoredColliders.Count; i++) {
                Collider collider = _ignoredColliders[i];
                if (collider) {
                    IgnoreCollisions(collider, false);
                }
            }
        }

        void IgnoreCollisions(Collider collider, bool ignore) {
            if (_portal.AttachedCollider) {
                if (ignore) {
                    _ignoredColliders.Add(collider);
                } else {
                    _ignoredColliders.Remove(collider);
                }
                Physics.IgnoreCollision(collider, _portal.AttachedCollider, ignore);
            }
        }

        void OnTriggerEnter(Collider collider) {
            _triggerCount += 1;
            if (_triggerCount > 1) {
                return;
            }

            if (!_portal.ExitPortal) {
                return;
            }

            IgnoreCollisions(collider, true);
        }

        void OnTriggerExit(Collider collider) {
            _triggerCount -= 1;
            if (_triggerCount > 0) {
                return;
            }

            if (!_portal.ExitPortal) {
                return;
            }

            IgnoreCollisions(collider, false);
        }


        //void OnTriggerStay(Collider collider) {
        //    if (!_portal.ExitPortal) {
        //        return;
        //    }

        //    Vector3 normal = transform.forward;
        //    float d = -1 * Vector3.Dot(normal, transform.position);
        //    bool throughPortal = new Plane(normal, d).GetSide(collider.transform.position);
        //    if (throughPortal) {
        //        _portal.OnPortalTeleport(collider);
        //    }
        //}
    }
}
