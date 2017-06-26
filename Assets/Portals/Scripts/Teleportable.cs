using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class Teleportable : MonoBehaviour {
        private static List<Type> _validBehaviours = new List<Type>(){
            typeof(Teleportable),
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

        private HashSet<Portal> _occupiedPortals;
        public HashSet<Portal> _receivedOnTriggerEnterFrom;

        private Rigidbody _rigidbody;

        private Teleportable _master;

        public Camera Camera {
            get { return _camera; }
        }

        public bool IsClone {
            get { return _isClone; }
        }

        public bool IsInsidePortal(Portal portal) {
            return _occupiedPortals.Contains(portal);
        }

        public Portal myPortal;

        private struct Impulse {
            public Vector3 translational;
            public Vector3 rotational;
        }

        private Impulse _frameImpulse;

        void Awake() {
            // Awake is called on all clones
            _rigidbody = GetComponent<Rigidbody>();

            if (_isClone) {
                //StartCoroutine(LateFixedUpdateRoutine());
            }
        }

        void Start() {
            if (!_isClone) {
                _clippedShader = Shader.Find("Portals/StandardClipped");
                _clippedShaderPlaneHash = Shader.PropertyToID("_ClippingPlane");


                _rendererToShader = new Dictionary<Renderer, Shader>();
                _portalToClone = new Dictionary<Portal, Teleportable>();
                _clonePool = new ObjectPool<Teleportable>(1, CreateClone);
                _occupiedPortals = new HashSet<Portal>();
                _receivedOnTriggerEnterFrom = new HashSet<Portal>();

                SaveShaders(this.gameObject);
            }
        }

        //IEnumerator LateFixedUpdateRoutine() {
        //    while (true) {
        //        yield return new WaitForFixedUpdate();
        //        LateFixedUpdate();
        //    }
        //}

        void LateUpdate() {
            if (!_isClone) {
                //LockClones(true);
            }
        }

        void FixedUpdate() {
            _frameImpulse.rotational = Vector3.zero;
            _frameImpulse.translational = Vector3.zero;

            if (!_isClone) {
                LockClones(false);
                _receivedOnTriggerEnterFrom.Clear();
            }
        }

        //void LateFixedUpdate() {
        //    if (_isClone) {
        //        _master._rigidbody.velocity += CalculateImpulseTransfer(this._frameImpulse.translational, _master._frameImpulse.translational);
        //        _master._rigidbody.angularVelocity += CalculateImpulseTransfer(this._frameImpulse.rotational, _master._frameImpulse.rotational);
        //    }
        //}


        //void OnCollisionEnter(Collision collision) {
        //    HandleCollision(collision);
        //}

        //void OnCollisionStay(Collision collision) {
        //    HandleCollision(collision);
        //}

        //void HandleCollision(Collision collision) {
        //    Vector3 positionSum = Vector3.zero;
        //    Vector3 normalSum = Vector3.zero;
        //    foreach (ContactPoint contact in collision.contacts) {
        //        positionSum += contact.point;
        //        normalSum += contact.normal;
        //    }
        //    Vector3 averageContactPoint = positionSum / collision.contacts.Length;
        //    Vector3 averageNormal = normalSum / collision.contacts.Length;

        //    Vector3 impulse = collision.impulse;
        //    // Impulse isn't always pointing in the right direction. We have to correct it manually
        //    if (Vector3.Dot(impulse, averageNormal) < 0) {
        //        impulse *= -1;
        //    }
        //    Vector3 rotationImpulse = Vector3.Cross(averageContactPoint - _rigidbody.worldCenterOfMass, impulse);
        //    _frameImpulse.translational += impulse;
        //    _frameImpulse.rotational += rotationImpulse;
        //}

        //Vector3 CalculateImpulseTransfer(Vector3 imp1, Vector3 imp2) {
        //    Vector3 impParallel = Vector3.Project(imp1, imp2);
        //    Vector3 impPerpendicular = imp1 - impParallel;
        //    Vector3 impTransfer = impPerpendicular;
        //    float magnitude = Vector3.Dot(imp1, imp2.normalized);
        //    if (magnitude < 0) {
        //        impTransfer += impParallel;
        //    } else if (magnitude > imp2.magnitude) {
        //        impTransfer += (impParallel - imp2);
        //    }

        //    return impTransfer;
        //}

        void LockClones(bool instant=false) {
            foreach (KeyValuePair<Portal, Teleportable> kvp in _portalToClone) {
                Portal portal = kvp.Key;
                Teleportable clone = kvp.Value;

                if (clone.isActiveAndEnabled) {

                    if (instant) {
                        Vector3 newPosition = portal.PortalMatrix().MultiplyPoint3x4(this.transform.position);
                        Quaternion newRotation = portal.PortalRotation() * this.transform.rotation;
                        clone.transform.position = newPosition;
                        clone.transform.rotation = newRotation;
                        clone._rigidbody.velocity = _rigidbody.velocity;
                        clone._rigidbody.angularVelocity = _rigidbody.angularVelocity;
                    } else {
                        Vector3 newPosition = portal.PortalMatrix().MultiplyPoint3x4(this._rigidbody.position);
                        Quaternion newRotation = portal.PortalRotation() * this._rigidbody.rotation;
                        clone._rigidbody.MovePosition(newPosition);
                        clone._rigidbody.MoveRotation(newRotation);
                        clone._rigidbody.velocity = _rigidbody.velocity;
                        clone._rigidbody.angularVelocity = _rigidbody.angularVelocity;
                    }
                }
            }
        }



        void OnPortalEnter(Portal portal) {
            _occupiedPortals.Add(portal);

            Debug.Log("Enter " + portal.name);
            Teleportable clone = GetClone(portal);
            if (!clone) {
                clone = SpawnClone(portal);
                LockClones(true);
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
            clone._master = this;

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
