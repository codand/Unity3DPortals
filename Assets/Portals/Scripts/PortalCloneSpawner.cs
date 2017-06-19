using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;

namespace Portals {
    public class PortalCloneSpawner : MonoBehaviour {
        [SerializeField]
        private Portal _portal;

        private static List<Type> _validBehaviours = new List<Type>(){
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };

        // Dictionary mapping objects to their clones on the other side of a portal
        private Dictionary<GameObject, GameObject> _objectToClone = new Dictionary<GameObject, GameObject>();
        private Dictionary<GameObject, GameObject> _objectToDepthMask = new Dictionary<GameObject, GameObject>();

        private Material _depthMaskMaterial;

        private void Awake() {
            _portal.onPortalTeleport += OnPortalTeleport;
        }

        private void Start() {
            _depthMaskMaterial = new Material(Shader.Find("Portal/DepthMaskClipped"));
            _depthMaskMaterial.SetVector("_ClippingPlane", _portal.VectorPlane);
            _depthMaskMaterial.renderQueue = 2010; // Geometry + 10
        }

        private void OnTriggerEnter(Collider collider) {
            SpawnClone(collider.gameObject);
            SpawnDepthMask(collider.gameObject);
        }

        private void OnTriggerExit(Collider collider) {
            DespawnClone(collider.gameObject);
        }

        private void OnPortalTeleport(GameObject obj) {
            _portal.ExitPortal.GetComponentInChildren<PortalCloneSpawner>().SpawnClone(obj);
            DespawnClone(obj);
        }

        private GameObject SpawnClone(GameObject obj) {
            GameObject clone;
            _objectToClone.TryGetValue(obj, out clone);
            if (clone) {
                clone.SetActive(true);
            } else {
                RenderQueueThing(obj);

                clone = CloneObject(obj);
                PortalClone script = clone.AddComponent<PortalClone>();
                script.target = obj.transform;
                script.portal = _portal;
                script.isDepthMasker = false;

                _objectToClone[obj] = clone;
            }
            return clone;
        }

        private void DespawnClone(GameObject obj, bool destroy = false) {
            GameObject clone;
            _objectToClone.TryGetValue(obj, out clone);
            if (clone) {
                if (destroy) {
                    GameObject.Destroy(clone);
                } else {
                    clone.SetActive(false);
                }
            }
        }

        private GameObject SpawnDepthMask(GameObject obj) {
            GameObject clone;
            _objectToDepthMask.TryGetValue(obj, out clone);
            if (clone) {
                clone.SetActive(true);
            } else {
                clone = CloneObject(obj);
                PortalClone script = clone.AddComponent<PortalClone>();
                script.target = obj.transform;
                script.portal = _portal;
                script.isDepthMasker = true;

                SwapMaterial(clone);
                _objectToDepthMask[obj] = clone;
            }
            return clone;
        }

        private void DespawnDepthMask(GameObject obj, bool destroy = false) {
            GameObject clone;
            _objectToDepthMask.TryGetValue(obj, out clone);
            if (clone) {
                if (destroy) {
                    GameObject.Destroy(clone);
                } else {
                    clone.SetActive(false);
                }
            }
        }

        private static GameObject CloneObject(GameObject obj) {
            GameObject clone = Instantiate(obj);
            DisableInvalidComponentsRecursively(clone);
            return clone;
        }

        private static void DisableInvalidComponentsRecursively(GameObject obj) {
            Behaviour[] allBehaviours = obj.GetComponents<Behaviour>();
            foreach (Behaviour behaviour in allBehaviours) {
                if (!_validBehaviours.Contains(behaviour.GetType())) {
                    behaviour.enabled = false;
                }
            }
            foreach (Transform child in obj.transform) {
                DisableInvalidComponentsRecursively(child.gameObject);
            }
        }

        private void SwapMaterial(GameObject obj) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) { 
                renderer.sharedMaterial = _depthMaskMaterial;
            }
            foreach(Transform child in obj.transform) {
                SwapMaterial(child.gameObject);
            }
        }

        private void RenderQueueThing(GameObject obj) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                renderer.sharedMaterial.renderQueue += 20;
            }
            foreach (Transform child in obj.transform) {
                RenderQueueThing(child.gameObject);
            }
        }
    }
}
