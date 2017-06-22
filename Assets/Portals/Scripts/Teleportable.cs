using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class Teleportable : MonoBehaviour {
        private static List<Type> _validBehaviours = new List<Type>(){
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };

        [SerializeField]
        private bool _hasFirstPersonCamera;

        [SerializeField]
        private Camera _camera;

        private bool _isClone;

        // Shader that will be applied when passing through the portal
        private Shader _clippedShader;
        private int _clippedShaderPlaneHash;
        private Material _clippedMaterial;

        // Stores this object's original shaders so that they can be restored
        private Dictionary<Renderer, Shader> _rendererToShader;

        // Stores one doppleganger for each portal that we're currently in.
        private Dictionary<Portal, Teleportable> _portalToClone;

        private ObjectPool<Teleportable> _clonePool;

        private GameObject _clone;

        private HashSet<Portal> _occupiedPortals;
        public HashSet<Portal> _receivedOnTriggerEnterFrom;

        private Rigidbody _rigidbody;

        private Teleportable _mainBody;

        public Camera Camera {
            get { return _camera; }
        }

        public bool IsClone {
            get { return _isClone; }
        }

        public bool IsInsidePortal(Portal portal) {
            return _occupiedPortals.Contains(portal);
        }

        void Awake() {
            // Awake is called on all clones
            _rigidbody = GetComponent<Rigidbody>();
        }

        void Start() {
            // Start is not called on clones
            _mainBody = this;

            _isClone = false;

            _clippedShader = Shader.Find("Portals/StandardClipped");
            _clippedShaderPlaneHash = Shader.PropertyToID("_ClippingPlane");
            

            _rendererToShader = new Dictionary<Renderer, Shader>();
            _portalToClone = new Dictionary<Portal, Teleportable>();
            _clonePool =  new ObjectPool<Teleportable>(1, CreateClone);
            _occupiedPortals = new HashSet<Portal>();
            _receivedOnTriggerEnterFrom = new HashSet<Portal>();

            SaveShaders(this.gameObject);
        }

        void OnCollisionEnter(Collision collision) {
            if (_rigidbody && IsClone) {
                _mainBody._rigidbody.AddForce(collision.impulse);
            }
        }

        void FixedUpdate() {
            _receivedOnTriggerEnterFrom.Clear();
        }

        void LateUpdate() {
            foreach(KeyValuePair<Portal, Teleportable> kvp in _portalToClone) {
                Portal portal = kvp.Key;
                Teleportable clone = kvp.Value;

                //Rigidbody rigidbody = clone.GetComponent<Rigidbody>();
                //if (rigidbody) {
                //    Vector3 newPosition = portal.PortalMatrix().MultiplyPoint3x4(this._rigidbody.position);
                //    Quaternion newRotation = portal.PortalRotation() * this._rigidbody.rotation;
                //    rigidbody.MovePosition(newPosition);
                //    rigidbody.transform.rotation = newRotation;
                //} else {
                    portal.ApplyWorldToPortalTransform(clone.transform, this.transform);
                //}
            }
        }

        void OnPortalEnter(Portal portal) {
            _occupiedPortals.Add(portal);

            Debug.Log("Enter " + portal.name);
            Teleportable clone = GetClone(portal);
            if (!clone) {
                clone = SpawnClone(portal);
            }
            ReplaceShaders(this.gameObject, portal);
            ReplaceShaders(clone.gameObject, portal.ExitPortal);
        }

        void OnPortalTeleport(Portal portal) {
            _occupiedPortals.Remove(portal);
            _occupiedPortals.Add(portal.ExitPortal);

            Debug.Log("Teleport " + portal.name);
            Teleportable clone = GetClone(portal);
            ReplaceShaders(this.gameObject, portal.ExitPortal);
            ReplaceShaders(clone.gameObject, portal);

            _portalToClone.Remove(portal);
            _portalToClone.Add(portal.ExitPortal, clone);
        }

        void OnPortalExit(Portal portal) {
            _occupiedPortals.Remove(portal);
            Debug.Log("Exit " + portal.name);

            DespawnClone(portal);
            RestoreShaders();
        }

        private Teleportable GetClone(Portal portal) {
            Teleportable clone;
            _portalToClone.TryGetValue(portal, out clone);
            return clone;
        }

        private Teleportable SpawnClone(Portal portal) {
            Teleportable clone = GetClone(portal);
            if (clone) {
                clone.gameObject.SetActive(true);
            } else {
                clone = _clonePool.Take();
                clone.gameObject.SetActive(true);

                _portalToClone[portal] = clone;
            }
            return clone;
        }

        private void DespawnClone(Portal portal, bool destroy = false) {
            Teleportable clone = GetClone(portal);
            if (clone) {
                if (destroy) {
                    Destroy(clone.gameObject);
                } else {
                    clone.gameObject.SetActive(false);

                    _portalToClone.Remove(portal);
                    _clonePool.Give(clone);
                }
            }
        }

        private Teleportable CreateClone() {
            Teleportable clone = Instantiate(this);
            DisableInvalidComponentsRecursively(clone.gameObject);
            clone.gameObject.SetActive(false);
            clone._isClone = true;
            clone._mainBody = this;

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

        void SaveShaders(GameObject obj) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                _rendererToShader[renderer] = renderer.material.shader;
            }

            foreach (Transform child in obj.transform) {
                SaveShaders(child.gameObject);
            }
        }

        void ReplaceShaders(GameObject obj, Portal portal) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                Vector4 clippingPlane = portal.VectorPlane;
                clippingPlane.w -= portal.ClippingOffset;
                renderer.material.shader = _clippedShader;
                renderer.material.SetVector(_clippedShaderPlaneHash, clippingPlane);
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
