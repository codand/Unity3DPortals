using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class Teleportable : MonoBehaviour {
        [SerializeField]
        private bool _hasFirstPersonCamera;

        [SerializeField]
        private Camera _camera;

        public Camera Camera {
            get { return _camera; }
        }

        private static List<Type> _validBehaviours = new List<Type>(){
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };

        // Shader that will be applied when passing through the portal
        private Shader _clippedShader;
        private int _clippedShaderPlaneHash;

        // Stores this object's original shaders so that they can be restored
        private Dictionary<Renderer, Shader> _rendererToShader = new Dictionary<Renderer, Shader>();
        
        // Stores one doppleganger for each portal that we're currently in.
        private Dictionary<Portal, GameObject> _portalToClone = new Dictionary<Portal, GameObject>();

        private GameObject _clone;

        void Start() {
            _clippedShader = Shader.Find("Portals/StandardClipped");
            _clippedShaderPlaneHash = Shader.PropertyToID("_ClippingPlane");
        }

        void LateUpdate() {
            foreach(KeyValuePair<Portal, GameObject> kvp in _portalToClone) {
                Portal portal = kvp.Key;
                GameObject clone = kvp.Value;

                portal.ApplyWorldToPortalTransform(clone.transform, this.transform);
            }
        }

        //void OnPortalEnter(Portal portal) {
        //    GameObject clone = SpawnClone(portal);
        //    ReplaceShaders(this.gameObject, portal);
        //    ReplaceShaders(clone, portal.ExitPortal);
        //}

        //void OnPortalExit(Portal portal) {
        //    DespawnClone(portal);
        //    RestoreShaders();
        //}

        private GameObject SpawnClone(Portal portal) {
            GameObject clone;
            _portalToClone.TryGetValue(portal, out clone);
            if (clone) {
                clone.SetActive(true);
            } else {
                clone = CloneObject(this.gameObject);
                _portalToClone[portal] = clone;
            }
            return clone;
        }

        private void DespawnClone(Portal portal, bool destroy = false) {
            GameObject clone;
            _portalToClone.TryGetValue(portal, out clone);
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

            Teleportable teleportable = clone.GetComponent<Teleportable>();
            if (teleportable) {
                // Should always be true
                Destroy(teleportable);
            }
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

        void ReplaceShaders(GameObject obj, Portal portal) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                _rendererToShader[renderer] = renderer.material.shader;
                renderer.material.shader = _clippedShader;
                renderer.material.SetVector(_clippedShaderPlaneHash, portal.VectorPlane);
            }

            foreach (Transform child in obj.transform) {
                ReplaceShaders(child.gameObject, portal);
            }
        }

        void RestoreShaders() {
            foreach (KeyValuePair<Renderer, Shader> kvp in _rendererToShader) {
                Renderer renderer = kvp.Key;
                Shader shader = kvp.Value;

                renderer.material.shader = shader;
            }
        }
    }
}
