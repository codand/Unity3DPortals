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

        private RigidbodyInfo _rigidbodyInfo;
        #endregion

        #region Initialization
        void Awake() {
            // Awake is called on all clones
            _rigidbody = GetComponent<Rigidbody>();
            _allColliders = GetComponentsInChildren<Collider>(true);

            StartCoroutine(LateFixedUpdateRoutine());
        }

        IEnumerator LateFixedUpdateRoutine() {
            while (Application.isPlaying) {
                yield return new WaitForFixedUpdate();
                LateFixedUpdate();
            }
        }

        void Start() {
            if (_isClone) {
                if (_rigidbody) {
                    _rigidbody.useGravity = false;
                }
            } else {
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
                foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Lock clone to master
                    clone.transform.position = portal.TeleportPoint(transform.position);
                    clone.transform.rotation = portal.TeleportRotation(transform.rotation);
                }
            }
        }

        void FixedUpdate() {
            if (!_isClone) {

                SweepTest();
                foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Lock clone to master
                    clone._rigidbody.position = portal.TeleportPoint(_rigidbody.position);
                    clone._rigidbody.rotation = portal.TeleportRotation(_rigidbody.rotation);
                    clone._rigidbody.velocity = portal.TeleportVector(_rigidbody.velocity);
                    clone._rigidbody.angularVelocity = portal.TeleportVector(_rigidbody.angularVelocity);

                    // Save clone's modified state
                    clone.SaveRigidbodyInfo();
                }

                // Save master unmodified state
                SaveRigidbodyInfo();
            }
        }

        void LateFixedUpdate() {
            if (!_isClone) {
                foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Apply velocity restrictions to master
                    Vector3 slaveDeltaVelocity = clone._rigidbody.velocity - clone._rigidbodyInfo.velocity;
                    Vector3 masterDeltaVelocity = _rigidbody.velocity - _rigidbodyInfo.velocity;

                    Vector3 slaveDeltaPosition = clone._rigidbody.position - clone._rigidbodyInfo.position;
                    Vector3 masterDeltaPosition = _rigidbody.position - _rigidbodyInfo.position;

                    Vector3 slaveDeltaAngularVelocity = clone._rigidbody.angularVelocity - clone._rigidbodyInfo.angularVelocity;
                    Vector3 masterDeltaAngularVelocity = _rigidbody.angularVelocity - _rigidbodyInfo.angularVelocity;

                    Quaternion slaveDeltaRotation = clone._rigidbody.rotation * Quaternion.Inverse(clone._rigidbodyInfo.rotation);
                    Quaternion masterDeltaRotation = _rigidbody.rotation * Quaternion.Inverse(_rigidbodyInfo.rotation);

                    Vector3 velocityTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaVelocity), masterDeltaVelocity);
                    Vector3 positionTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaPosition), masterDeltaPosition);
                    Vector3 angularVelocityTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaAngularVelocity), masterDeltaAngularVelocity);
                    //Quaternion rotationTransfer = portal.ExitPortal.TeleportRotation(slaveDeltaRotation) * Quaternion.Inverse(masterDeltaRotation);

                    _rigidbody.velocity += velocityTransfer;
                    _rigidbody.position += positionTransfer;
                    _rigidbody.angularVelocity += angularVelocityTransfer;
                    //_rigidbody.rotation *= rotationTransfer;
                }
            }
        }

        void SweepTest() {
            List<Collider> portalsHit = new List<Collider>();
            RaycastHit[] hits = _rigidbody.SweepTestAll(_rigidbody.velocity, _rigidbody.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++) {
                RaycastHit hit = hits[i];
                if (hit.collider.gameObject.layer == PortalPhysics.PortalLayer) {
                    portalsHit.Add(hit.collider);
                }
            }

            //for (int i = 0; i < portalsHit.Count; i++) {
            //    Collider portalCollider = portalsHit[i];
            //    OnTriggerEnter(portalCollider);
            //}
        }
        #endregion

        [Flags]
        private enum TriggerFlags {
            None = 0,
            Enter = 1,
            Stay = 2,
            Exit = 4
        }

        class PortalContext {
            public Teleportable clone;
            public TriggerFlags triggerFlags;
        }

        Dictionary<Portal, PortalContext> _contextByPortal = new Dictionary<Portal, PortalContext>();

        #region Triggers
        private void OnCompositeTriggerEnter(CompositeTrigger t) {
            if (_isClone) {
                return;
            }

            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }

            if (!trigger.portal.ExitPortal || _contextByPortal.ContainsKey(trigger.portal)) {
                return;
            }
            
            PortalContext context = new PortalContext();
            _contextByPortal[trigger.portal] = context;

            OnTrigger(trigger.portal, context);
        }

        private void OnCompositeTriggerStay(CompositeTrigger t) {
            if (_isClone) {
                return;
            }
            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }

            if (!trigger.portal.ExitPortal || !_contextByPortal.ContainsKey(trigger.portal)) {
                return;
            }

            Portal portal = trigger.portal;
            Vector3 position = _camera ? _camera.transform.position : transform.position;
            bool throughPortal = portal.Plane.GetSide(position);
            if (throughPortal) {
                PortalContext context = _contextByPortal[portal];
                OnTeleport(trigger.portal, context);

                _contextByPortal.Remove(portal);
                _contextByPortal.Add(portal.ExitPortal, context);
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
            if (!trigger.portal.ExitPortal || !_contextByPortal.ContainsKey(trigger.portal)) {
                return;
            }

            PortalContext context = _contextByPortal[trigger.portal];
            _contextByPortal.Remove(trigger.portal);
            OnExit(trigger.portal, context);
        }

        private void OnTrigger(Portal portal, PortalContext context) {
            Teleportable clone = SpawnClone();
            clone.gameObject.SetActive(true);
            clone.SaveRigidbodyInfo();

            ReplaceShaders(this.gameObject, portal);
            ReplaceShaders(clone.gameObject, portal.ExitPortal);

            IgnoreCollisions(portal.IgnoredColliders, true);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

            portal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;

            context.clone = clone;
        }

        private void OnTeleport(Portal portal, PortalContext context) {
            IgnoreCollisions(portal.IgnoredColliders, false);
            IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

            context.clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);
            context.clone.IgnoreCollisions(portal.IgnoredColliders, true);

            context.clone.SaveRigidbodyInfo();

            portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
            portal.ExitPortal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;

            portal.ApplyWorldToPortalTransform(transform, transform);
            if (_rigidbody != null) {
                // TODO: Evaluate whether or not using Rigidbody.position is important
                // Currently it messes up the _cameraInside stuff because it happens at the next physics step
                //Vector3 newPosition = PortalMatrix().MultiplyPoint3x4(rigidbody.position);
                //Quaternion newRotation = PortalRotation() * rigidbody.rotation;
                //rigidbody.position = newPosition;
                //rigidbody.transform.rotation = newRotation;

                float scaleDelta = portal.PortalScaleAverage();
                _rigidbody.velocity = portal.TeleportVector(_rigidbody.velocity);
                _rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;
            }

            ReplaceShaders(this.gameObject, portal.ExitPortal);
            ReplaceShaders(context.clone.gameObject, portal);

            gameObject.SendMessage("OnPortalTeleport", portal, SendMessageOptions.DontRequireReceiver);
        }

        private void OnExit(Portal portal, PortalContext context) {
            IgnoreCollisions(portal.IgnoredColliders, false);
            context.clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);

            portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;

            DespawnClone(context.clone);
            RestoreShaders();
        }

        #endregion

        #region Callbacks
        private void OnIgnoredCollidersChanged(Portal portal, Collider[] oldColliders) {
            IgnoreCollisions(oldColliders, false);
            IgnoreCollisions(portal.IgnoredColliders, true);
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

        private Teleportable SpawnClone() {
            return _clonePool.Take();
        }

        private void DespawnClone(Teleportable clone, bool destroy = false) {
            clone.gameObject.SetActive(false);
            _clonePool.Give(clone);
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

        Vector3 CalculateImpulseTransfer(Vector3 imp1, Vector3 imp2) {
            Vector3 impParallel = Vector3.Project(imp1, imp2);
            Vector3 impPerpendicular = imp1 - impParallel;
            Vector3 impTransfer = impPerpendicular;
            float magnitude = Vector3.Dot(imp1, imp2.normalized);
            if (magnitude < 0) {
                impTransfer += impParallel;
            } else if (magnitude > imp2.magnitude) {
                impTransfer += (impParallel - imp2);
            }

            return impTransfer;
        }

        private void SaveRigidbodyInfo() {
            _rigidbodyInfo.position = _rigidbody.position;
            _rigidbodyInfo.rotation = _rigidbody.rotation;
            _rigidbodyInfo.velocity = _rigidbody.velocity;
            _rigidbodyInfo.angularVelocity = _rigidbody.angularVelocity;
        }

        #endregion

        private struct RigidbodyInfo {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public Vector3 translationalImpulse;
            public Vector3 angularImpulse;
        }
    }
}
