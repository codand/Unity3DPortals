using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    [RequireComponent(typeof(SphereCollider))]
    public class PortalCameraTrigger : MonoBehaviour {
        [SerializeField]
        private Camera _camera;
        [SerializeField]
        private Teleportable _teleportable;

        private SphereCollider _collider;

        void Awake() {
            _collider = GetComponent<SphereCollider>();
        }

    }
}
