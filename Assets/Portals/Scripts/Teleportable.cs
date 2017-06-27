using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class Teleportable : MonoBehaviour {
        #region Variables
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
        
        private HashSet<PortalTrigger> _occupiedTriggers;
        private HashSet<Portal> _occupiedPortals;

        private Rigidbody _rigidbody;

        private Teleportable _master;

        private Collider[] _allColliders;
        #endregion

        #region Initialization
        void Awake() {
            // Awake is called on all clones
            _rigidbody = GetComponent<Rigidbody>();
            _allColliders = GetComponentsInChildren<Collider>(true);

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
                _occupiedTriggers = new HashSet<PortalTrigger>();
                _occupiedPortals = new HashSet<Portal>();

                SaveShaders(this.gameObject);
            }
        }
        #endregion

        #region Updates
        void LateUpdate() {
            if (!_isClone) {
                LockClones(true);
            }
        }

        void FixedUpdate() {
            //Debug.Log("Fixed update: " + _rigidbody.velocity);
            if (!_isClone) {
                LockClones(false);
                //SweepTest();
            }
        }

        //void SweepTest() {
        //    List<Collider> portalsHit = new List<Collider>();
        //    RaycastHit[] hits = _rigidbody.SweepTestAll( _rigidbody.velocity, _rigidbody.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Collide);
        //    for (int i = 0; i < hits.Length; i++) {
        //        RaycastHit hit = hits[i];
        //        if (hit.collider.gameObject.layer == PortalPhysics.PortalLayer) {
        //            portalsHit.Add(hit.collider);
        //        }
        //    }

        //    for (int i = 0; i < portalsHit.Count; i++) {
        //        Collider portalCollider = portalsHit[i];
        //        OnTriggerEnter(portalCollider);
        //    }
        //}

        //IEnumerator LateFixedUpdateRoutine() {
        //    while (true) {
        //        yield return new WaitForFixedUpdate();
        //        LateFixedUpdate();
        //    }
        //}

        //void LateFixedUpdate() {
        //    if (_isClone) {
        //        _master._rigidbody.velocity += CalculateImpulseTransfer(this._frameImpulse.translational, _master._frameImpulse.translational);
        //        _master._rigidbody.angularVelocity += CalculateImpulseTransfer(this._frameImpulse.rotational, _master._frameImpulse.rotational);
        //    }
        //}
        #endregion

        #region Triggers
        private void OnCompositeTriggerEnter(CompositeTrigger t) {
            if (_isClone) {
                return;
            }

            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }
            
            if (!trigger.portal.ExitPortal || _occupiedPortals.Contains(trigger.portal)) {
                return;
            }
            _occupiedPortals.Add(trigger.portal);
            OnPortalTriggerEnter(trigger);
        }

        private void OnCompositeTriggerStay(CompositeTrigger t) {
            if (_isClone) {
                return;
            }
            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }

            if (!trigger.portal.ExitPortal || !_occupiedPortals.Contains(trigger.portal)) {
                return;
            }

            Portal portal = trigger.portal;
            Vector3 position = _camera ? _camera.transform.position : transform.position;
            bool throughPortal = portal.Plane.GetSide(position);
            if (throughPortal) {
                _occupiedPortals.Remove(portal);
                _occupiedPortals.Add(portal.ExitPortal);

                Teleport(trigger.portal);
            }
        }

        private void OnCompositeTriggerExit(CompositeTrigger t) {
            if (_isClone) {
                return;
            }
            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }
            if (!trigger.portal.ExitPortal || !_occupiedPortals.Contains(trigger.portal)) {
                return;
            }

            _occupiedPortals.Remove(trigger.portal);
            OnPortalTriggerExit(trigger);
        }

        private void OnPortalTriggerEnter(PortalTrigger trigger) {
            //Debug.Log("Trigger Enter " + trigger.portal.name + " " + trigger.name + " " + this.name);
            Portal portal = trigger.portal;
            switch (trigger.function) {
                case PortalTrigger.TriggerFunction.Teleport:
                    IgnoreCollisions(trigger.portal, true);
                    trigger.portal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;
                    break;
                case PortalTrigger.TriggerFunction.SpawnClone:
                    Teleportable clone = GetClone(portal);
                    if (!clone) {
                        clone = SpawnClone(portal);
                        LockClones(true);
                    }
                    ReplaceShaders(this.gameObject, portal);
                    ReplaceShaders(clone.gameObject, portal.ExitPortal);
                    clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);
                    break;
            }
        }

        private void Teleport(Portal portal) {
            //Debug.Log("Trigger Teleport " + portal.name + " " + this.name);
            IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);
            if (_camera) {
                portal.ExitPortal.RegisterCamera(_camera);
                portal.UnregisterCamera(_camera);
            }

            portal.ApplyWorldToPortalTransform(transform, transform);
            if (_rigidbody != null) {
                // TODO: Evaluate whether or not using Rigidbody.position is important
                // Currently it messes up the _cameraInside stuff because it happens at the next physics step
                //Vector3 newPosition = PortalMatrix().MultiplyPoint3x4(rigidbody.position);
                //Quaternion newRotation = PortalRotation() * rigidbody.rotation;
                //rigidbody.position = newPosition;
                //rigidbody.transform.rotation = newRotation;

                float scaleDelta = portal.PortalScaleAverage();
                _rigidbody.velocity = portal.PortalRotation() * _rigidbody.velocity * scaleDelta;
                _rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;
            }

            //Teleportable clone = GetClone(portal);
            //ReplaceShaders(this.gameObject, portal.ExitPortal);
            //ReplaceShaders(clone.gameObject, portal);

            //_portalToClone.Remove(portal);
            //_portalToClone.Add(portal.ExitPortal, clone);

            //clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);
            //clone.IgnoreCollisions(portal.IgnoredColliders, true);
        }

        private void OnPortalTriggerExit(PortalTrigger trigger){
            //TODO: Fuck. Multiple trigger exits don't combo well
            //Debug.Log("Trigger Exit " + trigger.portal.name + " " + trigger.name + " " + this.name);
            Portal portal = trigger.portal;
            switch (trigger.function) {
                case PortalTrigger.TriggerFunction.Teleport:
                    IgnoreCollisions(portal.IgnoredColliders, false);
                    portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
                    if (_camera) {
                        portal.UnregisterCamera(_camera);
                    }
                    break;
                case PortalTrigger.TriggerFunction.SpawnClone:
                    Teleportable clone = GetClone(portal);
                    if (clone) {
                        // Exiting because walking out of trigger
                        DespawnClone(portal);
                        RestoreShaders();
                    } else {
                        // Exiting because of teleport
                        clone = GetClone(portal.ExitPortal);
                    }


                    break;
            }
        }


        //// FSM:
        //// Inside->Outisde
        //// Inside->Inside
        
        //[Flags]
        //private enum TriggerStatus {
        //    Entered = 1,
        //    Stayed = 2,
        //    Teleported = 4,
        //    Exited = 8,
        //}

        //private struct PortalContext {
        //    public Portal portal;
        //    public Teleportable clone;
        //    public TriggerStatus status;
        //}

        //private void BeginPortalContext(Portal portal) {
        //    PortalContext context;
        //    context.portal = portal;
        //    context.clone = GetClone(portal);
        //    context.status = TriggerStatus.Entered;
        //}

        //private IEnumerator TeleportRoutine(PortalContext context) {
        //    while (context.status == TriggerStatus.Stayed) {

        //    }
        //}

        #endregion

        #region Collisions
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
        #endregion

        #region Callbacks
        void OnPortalEnter(Portal portal) {
            IgnoreCollisions(portal, true);
            portal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;

            if (_camera) {
                portal.RegisterCamera(_camera);
            }
            Debug.Log("Enter " + portal.name);

            //Teleportable clone = GetClone(portal);
            //if (!clone) {
            //    clone = SpawnClone(portal);
            //    LockClones(true);
            //}
            //ReplaceShaders(this.gameObject, portal);
            //ReplaceShaders(clone.gameObject, portal.ExitPortal);
        }

        void OnPortalTeleport(Portal portal) {
            IgnoreCollisions(portal, false);
            portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;

            IgnoreCollisions(portal.ExitPortal, true);
            portal.ExitPortal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;

            if (_camera) {
                portal.UnregisterCamera(_camera);
                portal.ExitPortal.RegisterCamera(_camera);
            }

            Debug.Log("Teleport " + portal.name);
            //Teleportable clone = GetClone(portal);
            //ReplaceShaders(this.gameObject, portal.ExitPortal);
            //ReplaceShaders(clone.gameObject, portal);

            //_portalToClone.Remove(portal);
            //_portalToClone.Add(portal.ExitPortal, clone);
        }

        void OnPortalExit(Portal portal) {
            IgnoreCollisions(portal, false);
            portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;

            if (_camera) {
                portal.UnregisterCamera(_camera);
            }

            Debug.Log("Exit " + portal.name);

            //DespawnClone(portal);
            //RestoreShaders();
        }

        private void OnIgnoredCollidersChanged(Portal portal, Collider[] oldColliders) {
            IgnoreCollisions(oldColliders, false);
            IgnoreCollisions(portal.IgnoredColliders, true);
        }

        private void IgnoreCollisions(Portal portal, bool ignore) {
            IgnoreCollisions(portal.IgnoredColliders, ignore);

            //Collider portalCollider = portal._triggerCollider;
            //for (int i = 0; i < _allColliders.Length; i++) {
            //    Collider collider = _allColliders[i];
            //    Physics.IgnoreCollision(collider, portalCollider, ignore);
            //}
        }

        private void IgnoreCollisions(Collider[] ignoredColliders, bool ignore) {
            for (int i = 0; i < ignoredColliders.Length; i++) {
                Collider other = ignoredColliders[i];
                for (int j = 0; j < _allColliders.Length; j++) {
                    Collider collider = _allColliders[j];
                    Physics.IgnoreCollision(collider, other, ignore);
                }
            }
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

        #endregion

    }
}
