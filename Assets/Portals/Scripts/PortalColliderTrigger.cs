using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class PortalColliderTrigger : MonoBehaviour {
        [SerializeField] private Portal _portal;
        [SerializeField] private GameObject _portalColliders;

        private Dictionary<Teleportable, int> _triggerCounts = new Dictionary<Teleportable, int>();
        private List<Collider> _ignoredColliders = new List<Collider>();

        public void ResetIgnoredCollisions() {
            for(int i = 0; i < _ignoredColliders.Count; i++) {
                Collider collider = _ignoredColliders[i];
                if (collider) {
                    IgnoreCollisions(collider, false);
                }
            }
        }

        void EnablePortalColliders() {
            _portalColliders.SetActive(true);
        }

        void DisablePortalColliders() {
            _portalColliders.SetActive(false);
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

        int IncrementTriggerCount(Teleportable teleportable) {
            int triggerCount = 0;
            if (!_triggerCounts.TryGetValue(teleportable, out triggerCount)) {
                triggerCount = 1;
            } else {
                triggerCount += 1;
            }
            _triggerCounts[teleportable] = triggerCount;
            return triggerCount;
        }

        int DecrementTriggerCount(Teleportable teleportable) {
            int triggerCount = 0;
            if (!_triggerCounts.TryGetValue(teleportable, out triggerCount)) {
                throw new System.Exception("Attempted to decrement trigger count below zero. This should never happen");
            } else {
                triggerCount -= 1;
                if (triggerCount == 0) {
                    _triggerCounts.Remove(teleportable);
                } else {
                    _triggerCounts[teleportable] = triggerCount;
                }
            }
            return triggerCount;
        }

        void OnTriggerEnter(Collider collider) {
            Teleportable teleportable = collider.GetComponent<Teleportable>();
            if (!teleportable) {
                return;
            }
            int triggerCount = IncrementTriggerCount(teleportable);
            if (triggerCount > 1) {
                return;
            }

            if (!_portal.ExitPortal) {
                return;
            }

            IgnoreCollisions(collider, true);
            EnablePortalColliders();
        }

        void OnTriggerExit(Collider collider) {
            Teleportable teleportable = collider.GetComponent<Teleportable>();
            if (!teleportable) {
                return;
            }
            int triggerCount = DecrementTriggerCount(teleportable);
            if (triggerCount > 0) {
                return;
            }

            if (!_portal.ExitPortal) {
                return;
            }

            IgnoreCollisions(collider, false);
            DisablePortalColliders();
        }
    }
}
