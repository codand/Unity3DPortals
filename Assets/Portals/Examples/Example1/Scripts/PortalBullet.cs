using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals.Examples {
    [RequireComponent(typeof(Rigidbody))]
    public class PortalBullet : MonoBehaviour {
        Rigidbody _rigidbody;

        void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }

        void Start() {
            _rigidbody.useGravity = false;
        }

        void OnCollisionEnter() {
            Destroy(this.gameObject);
        }
    }
}
