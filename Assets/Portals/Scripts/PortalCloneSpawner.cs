using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class PortalCloneSpawner : MonoBehaviour {
        [SerializeField] private Portal _portal;
        private Dictionary<GameObject, GameObject> _objectToClone = new Dictionary<GameObject, GameObject>();

        void OnTriggerEnter(Collider collider) {
            PortalClone isClone = collider.GetComponent<PortalClone>();
            if (isClone) {
                return;
            }
            if (collider.tag != "Player" ) {
                GameObject clone = null;
                _objectToClone.TryGetValue(collider.gameObject, out clone);
                if (!clone) {
                    clone = Instantiate(collider.gameObject);

                    // Add clone script
                    PortalClone cloneScript = clone.AddComponent<PortalClone>();
                    cloneScript.target = collider.transform;
                    cloneScript.portal = _portal;

                    //_portal.ApplyWorldToPortalTransform(clone.transform, collider.gameObject.transform);
                    _objectToClone[collider.gameObject] = clone;
                }
            }

            //UnityEditor.EditorApplication.isPaused = true;
        }

        void OnTriggerExit(Collider collider) {
            PortalClone isClone = collider.GetComponent<PortalClone>();
            if (isClone) {
                return;
            }
            if (collider.tag != "Player") {
                GameObject clone = null;
                _objectToClone.TryGetValue(collider.gameObject, out clone);
                if (clone) {
                    Destroy(clone);
                    _objectToClone.Remove(collider.gameObject);
                }
            }
        }
    }
}