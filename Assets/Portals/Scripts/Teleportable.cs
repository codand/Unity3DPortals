using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
namespace Portals {
    public class Teleportable : MonoBehaviour {
        #region Variables
        private static List<Type> _validCloneBehaviours = new List<Type>(){
            typeof(Teleportable),
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };

        private enum CameraType {
            None,
            FirstPerson,
            ThirdPerson,
        }

        [SerializeField]
        private CameraType _cameraType;

        [SerializeField]
        private Camera _camera;

        [SerializeField]
        private bool _spawnCloneOnAwake = true;

        private const float _clippingOffset = 0.01f;

        private bool _isClone;

        // Shader that will be applied when passing through the portal
        private Shader _clippedShader;
        private int _clippedShaderPlaneHash;
        private Material _clippedMaterial;

        // Stores this object's original shaders so that they can be restored
        private Dictionary<Renderer, Shader> _rendererToShader;
        private Dictionary<Portal, PortalContext> _contextByPortal = new Dictionary<Portal, PortalContext>();

        private ObjectPool<Teleportable> _clonePool;


        private Collider[] _allColliders;

        private Rigidbody _rigidbody;
        private RigidbodyInfo _rigidbodyInfo;

        #endregion


        #region Events
        public delegate void PortalEvent(Teleportable sender, Portal portal);
        public event PortalEvent onTeleport;
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
                _clonePool = new ObjectPool<Teleportable>(_spawnCloneOnAwake ? 1 : 0, CreateClone);

                SaveShaders(this.gameObject);
            }
        }
        #endregion

        #region Updates
        void LateUpdate() {
            if (!_isClone) {
                if (!ShouldUseFixedUpdate()) {
                    TeleportCheck();
                }

                foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Lock clone to master
                    clone.transform.position = portal.TeleportPoint(transform.position);
                    clone.transform.rotation = portal.TeleportRotation(transform.rotation);

                    clone.CopyAnimations(this);
                }

                _rigidbodyInfo.cameraPosition = _camera.transform.position;
            }
        }

        void FixedUpdate() {
            if (!_isClone) {
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

                if (ShouldUseFixedUpdate()) {
                    TeleportCheck();
                }
            }
        }
        #endregion

        #region Triggers
        private Portal TeleportCheck() {
            Portal toTeleport = null;
            foreach (Portal portal in _contextByPortal.Keys) {
                if (ShouldTeleport(portal)) {
                    toTeleport = portal;
                    break;
                }
            }
            if (toTeleport) {
                OnTeleport(toTeleport);
            }
            return toTeleport;
        }

        private bool ShouldTeleport(Portal portal) {
            //TODO: Support CharacterController?
            Vector3 positionLastStep = _camera && _cameraType == CameraType.FirstPerson ? _rigidbodyInfo.cameraPosition : _rigidbodyInfo.position;
            Vector3 positionThisStep = _camera && _cameraType == CameraType.FirstPerson ? _camera.transform.position : _rigidbody.position;
            //Vector3 diff = positionThisStep - positionLastStep;
            //Debug.Log(positionLastStep + "=>" + positionThisStep);
            return !portal.Plane.GetSide(positionLastStep) && portal.Plane.GetSide(positionThisStep);
        }

        private void OnCompositeTriggerEnter(CompositeTrigger t) {
            if (_isClone) {
                return;
            }
            
            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }
            if (!trigger.portal.ExitPortal || !trigger.portal.ExitPortal.isActiveAndEnabled || _contextByPortal.ContainsKey(trigger.portal)) {
                return;
            }

            OnTrigger(trigger.portal);
        }

        private void OnCompositeTriggerStay(CompositeTrigger t) {
            // In the case that this portal's exit portal is not active, but becomes active, need to check for enter again
            OnCompositeTriggerEnter(t);
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
            OnExit(trigger.portal);
        }

        private void OnTrigger(Portal portal) {
            PortalContext context = new PortalContext();
            _contextByPortal[portal] = context;

            Teleportable clone = SpawnClone();
            clone.gameObject.SetActive(true);
            clone.SaveRigidbodyInfo();

            ReplaceShaders(this.gameObject, portal);
            ReplaceShaders(clone.gameObject, portal.ExitPortal);

            IgnoreCollisions(portal.IgnoredColliders, true);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

            portal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;
            portal.onExitPortalChanged += OnExitPortalChanged;
            portal.ExitPortal.onIgnoredCollidersChanged += clone.OnIgnoredCollidersChanged;

            context.clone = clone;
        }

        private void OnTeleport(Portal portal) {
            PortalContext context = _contextByPortal[portal];
            Teleportable clone = context.clone;

            // Always replace our shader because we only support 1 clipping plane at the moment
            ReplaceShaders(this.gameObject, portal.ExitPortal);

            bool alreadyInExitPortal = _contextByPortal.ContainsKey(portal.ExitPortal);
            if (alreadyInExitPortal) {
                // Update our frame data early as to zero out the velocity diffs.
                // Effectively this prevents the clones from applying any forces to the master
                clone.SaveRigidbodyInfo();
                _contextByPortal[portal.ExitPortal].clone.SaveRigidbodyInfo();
            } else {
                clone.SaveRigidbodyInfo();

                IgnoreCollisions(portal.IgnoredColliders, false);
                IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

                clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);
                clone.IgnoreCollisions(portal.IgnoredColliders, true);

                portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
                portal.onExitPortalChanged -= OnExitPortalChanged;
                portal.ExitPortal.onIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

                portal.ExitPortal.onIgnoredCollidersChanged += OnIgnoredCollidersChanged;
                portal.ExitPortal.onExitPortalChanged += OnExitPortalChanged;
                portal.onIgnoredCollidersChanged += clone.OnIgnoredCollidersChanged;

                ReplaceShaders(clone.gameObject, portal);
                
                // Swap clones if we're not already standing in the exit portal
                // This only applies to portals very close together.
                _contextByPortal.Remove(portal);
                _contextByPortal.Add(portal.ExitPortal, context);
            }

            if (_rigidbody) {
                // Interpolate velocity change through the portal
                Vector3 frameMovement = _rigidbody.position - _rigidbodyInfo.position;
                float distanceFromPortalLastFrame;
                if (portal.Plane.Raycast(new Ray(_rigidbodyInfo.position, frameMovement), out distanceFromPortalLastFrame)) {
                    float ratioLastFrame = distanceFromPortalLastFrame / frameMovement.magnitude;
                    float ratioThisFrame = 1 - ratioLastFrame;

                    Vector3 acceleration = Physics.gravity * Time.deltaTime; // Unfortunately we can't known about forces other than gravity because they aren't exposed by Rigidbody API

                    Vector3 velocity = _rigidbody.velocity;
                    velocity -= acceleration; // Rewind acceleration
                    velocity += acceleration * ratioLastFrame; // Replay acceleration until we hit the portal
                    velocity = portal.TeleportVector(velocity); // Teleport velocity
                    velocity += acceleration * ratioThisFrame; // Replay acceleration after we passed the portal
                    _rigidbody.velocity = velocity;

                } else {
                    _rigidbody.velocity = portal.TeleportVector(_rigidbody.velocity);
                }

                //float scaleDelta = portal.PortalScaleAverage();
                //
                //_rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;

                _rigidbody.position = portal.TeleportPoint(_rigidbody.position);
                _rigidbody.transform.rotation = portal.TeleportRotation(_rigidbody.rotation);

                transform.position = _rigidbody.position;
                transform.rotation = _rigidbody.rotation;
            }
            //portal.ApplyWorldToPortalTransform(transform, transform);

            gameObject.SendMessage("OnPortalTeleport", portal, SendMessageOptions.DontRequireReceiver);
            if (onTeleport != null) {
                onTeleport(this, portal);
            }
        }

        private void OnExit(Portal portal) {
            PortalContext context = _contextByPortal[portal];
            _contextByPortal.Remove(portal);

            Teleportable clone = context.clone;

            IgnoreCollisions(portal.IgnoredColliders, false);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);

            portal.onIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
            portal.onExitPortalChanged -= OnExitPortalChanged;
            portal.ExitPortal.onIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

            DespawnClone(clone);
            RestoreShaders();
        }

        #endregion

        #region Callbacks
        private void OnIgnoredCollidersChanged(Portal portal, Collider[] oldColliders, Collider[] newColliders) {
            IgnoreCollisions(oldColliders, false);
            IgnoreCollisions(newColliders, true);
        }

        private void OnExitPortalChanged(Portal portal, Portal oldExitPortal, Portal newExitPortal) {
            PortalContext context;
            if (_contextByPortal.TryGetValue(portal, out context)) {
                Teleportable clone = context.clone;
                clone.IgnoreCollisions(oldExitPortal.IgnoredColliders, false);
                clone.IgnoreCollisions(newExitPortal.IgnoredColliders, true);
            }
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

            return clone;
        }

        private static void DisableInvalidComponentsRecursively(GameObject obj) {
            Behaviour[] allBehaviours = obj.GetComponents<Behaviour>();
            foreach (Behaviour behaviour in allBehaviours) {
                if (!_validCloneBehaviours.Contains(behaviour.GetType())) {
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
                _rendererToShader[renderer] = renderer.sharedMaterial.shader;
            }

            foreach (Transform child in obj.transform) {
                SaveShaders(child.gameObject);
            }
        }

        void ReplaceShaders(GameObject obj, Portal portal) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                Vector4 clippingPlane = portal.VectorPlane;
                clippingPlane.w -= _clippingOffset;
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

        void CopyAnimations(Teleportable from) {
            Animator src = from.GetComponent<Animator>();
            if (!src) {
                return;
            }
            Animator dst = this.GetComponent<Animator>();
            if (!dst) {
                return;
            }

            for (int i = 0; i < src.layerCount; i++) {
                AnimatorStateInfo srcInfo = src.GetCurrentAnimatorStateInfo(i);
                AnimatorStateInfo srcInfoNext = src.GetNextAnimatorStateInfo(i);
                AnimatorTransitionInfo srcTransitionInfo = src.GetAnimatorTransitionInfo(i);

                dst.Play(srcInfo.fullPathHash, i, srcInfo.normalizedTime);
            }

            for (int i = 0; i < src.parameterCount; i++) {
                AnimatorControllerParameter parameter = src.parameters[i];
                if (src.IsParameterControlledByCurve(parameter.nameHash)) {
                    continue;
                }
                switch (parameter.type) {
                    case AnimatorControllerParameterType.Float:
                        dst.SetFloat(parameter.name, src.GetFloat(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Int:
                        dst.SetInteger(parameter.name, src.GetInteger(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        dst.SetBool(parameter.name, src.GetBool(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        // TODO: figure out how to set triggers
                        //dst.SetTrigger(parameter.nameHash, parameter.);
                        break;
                    default:
                        break;
                }
            }

            dst.speed = src.speed;
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

        private bool ShouldUseFixedUpdate() {
            return _cameraType == CameraType.None || _camera == null;
        }

        Portal[] SweepTest() {
            Vector3 direction = _rigidbody.velocity;
            float distance = _rigidbody.velocity.magnitude * Time.fixedDeltaTime;
            if (direction == Vector3.zero || distance == 0) {
                direction = Physics.gravity;
                distance = Physics.gravity.magnitude * Time.fixedDeltaTime;
            }
            List<Portal> portalsHit = new List<Portal>();
            RaycastHit[] hits = _rigidbody.SweepTestAll(direction, distance, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++) {
                RaycastHit hit = hits[i];
                if (hit.collider.gameObject.layer == PortalPhysics.PortalLayer) {
                    portalsHit.Add(hit.collider.GetComponent<Portal>());
                }
            }
            return portalsHit.ToArray();
        }
        #endregion

        private struct RigidbodyInfo {
            public Vector3 cameraPosition;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        private class PortalContext {
            public Teleportable clone;
        }
    }
}
