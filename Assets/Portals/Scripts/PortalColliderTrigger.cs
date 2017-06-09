using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class PortalColliderTrigger : MonoBehaviour {
        [SerializeField] private Portal _portal;

        void IgnoreCollisions(Collider collider, bool ignore) {
            if (_portal.AttachedCollider) {
                Physics.IgnoreCollision(collider, _portal.AttachedCollider, ignore);
            }
            if (_portal.ExitPortal && _portal.ExitPortal.AttachedCollider) {
                Physics.IgnoreCollision(collider, _portal.ExitPortal.AttachedCollider, ignore);
            }
        }

        void OnTriggerEnter(Collider collider) {
            if (!_portal.ExitPortal) {
                return;
            }

            IgnoreCollisions(collider, true);
        }

        void OnTriggerExit(Collider collider) {
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
        //        _portal.OnPortalExit(collider);
        //    }
        //}
    }
}
