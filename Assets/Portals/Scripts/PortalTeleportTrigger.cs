using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class PortalTeleportTrigger : MonoBehaviour {
        [SerializeField] Portal _portal;

        void OnTriggerStay(Collider collider) {
            if (!_portal.ExitPortal) {
                return;
            }

            Vector3 normal = transform.forward;
            float d = -1 * Vector3.Dot(normal, transform.position);
            bool throughPortal = new Plane(normal, d).GetSide(collider.transform.position);
            if (throughPortal) {
                _portal.OnPortalExit(collider);
            }
        }
    }
}
