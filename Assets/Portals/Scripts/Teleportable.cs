// -----------------------------------------------------------------------------------------------------------
// <summary>
// Attach this script to any GameObject with a Rigidbody or CharacterController to enable Portal teleportation.
// </summary>
// -----------------------------------------------------------------------------------------------------------
namespace Portals {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;

    public class Teleportable : MonoBehaviour {
        #region Constants
        private const float VisualClippingOffset = 0.01f;
        #endregion

        #region Members
        private static List<Type> m_ValidCloneBehaviours = new List<Type>() {
            typeof(Teleportable),
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };
        
        [SerializeField] private CameraType _cameraType;
        [SerializeField] private Camera _camera;
        [SerializeField] private Shader _replacementShader;
        [SerializeField] private bool _spawnCloneOnAwake = true;

        private bool _isClone;

        // Shader that will be applied when passing through the portal
        private int _clippingPlaneShaderHash;

        // Stores this object's original shaders so that they can be restored
        private Dictionary<Renderer, Shader> _shaderByRenderer;
        private Dictionary<Portal, PortalContext> _contextByPortal;
        private HashSet<Portal> _portalTriggersSeen;

        private ObjectPool<Teleportable> _cloneObjectPool;
        
        private Collider[] _allColliders;

        private Rigidbody _rigidbody;
        private RigidbodyInfo _rigidbodyLastTick;
        private Vector3 _cameraPositionLastFrame;

        #endregion

        #region Events
        public delegate void PortalEvent(Teleportable sender, Portal portal);

        public event PortalEvent OnTeleport;
        #endregion

        #region Enums
        private enum CameraType {
            None,
            FirstPerson,
            ThirdPerson,
        }

        [Flags]
        private enum TriggerStatus {
            None = 0,
            Enter = 1,
            Stay = 2,
            Exit = 4,
        }
        #endregion

        #region Initialization
        private void Awake() {
            // Awake is called on all clones
            _rigidbody = GetComponent<Rigidbody>();
            _allColliders = GetComponentsInChildren<Collider>(true);

            StartCoroutine(LateFixedUpdateRoutine());
        }

        private IEnumerator LateFixedUpdateRoutine() {
            while (Application.isPlaying) {
                yield return new WaitForFixedUpdate();
                LateFixedUpdate();
            }
        }

        private void Start() {
            if (_isClone) {
                if (_rigidbody) {
                    _rigidbody.useGravity = false;
                }
            } else {
                _clippingPlaneShaderHash = Shader.PropertyToID("_ClippingPlane");
                _contextByPortal = new Dictionary<Portal, PortalContext>();
                _portalTriggersSeen = new HashSet<Portal>();

                _shaderByRenderer = new Dictionary<Renderer, Shader>();
                _cloneObjectPool = new ObjectPool<Teleportable>(
                    _spawnCloneOnAwake ? 1 : 0,
                    CreateClone);

                SaveShaders(this.gameObject);
            }
        }

        private void OnDestroy() {
            if (!_isClone) {
                for (int i = 0; i < _cloneObjectPool.Count; i++) {
                    Teleportable clone = _cloneObjectPool.Take();
                    if (clone) {
                        Destroy(clone.gameObject);
                    }
                }
            }
        }
        #endregion

        #region Updates
        private void LateUpdate() {
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

                if (_camera) {
                    _cameraPositionLastFrame = _camera.transform.position;
                }
            }
        }

        Dictionary<Portal, int> m_PortalFrameCounter = new Dictionary<Portal, int>();

        private void OnTriggerStay(Collider other) {
            if (_isClone) {
                return;
            }

            Portal portal = other.GetComponent<Portal>();
            if (!portal) {
                return;
            }
            m_PortalFrameCounter[portal] = 2;
            if (portal.IsOpen && !_contextByPortal.ContainsKey(portal)) {

                Debug.Log("Trigger");
                EnterPortal(portal);
            }
        }

        private void SweepTest() {
            if (_isClone) {
                return;
            }

            //m_PortalTriggersSeen = new HashSet<Portal>();
            RaycastHit[] hits = _rigidbody.SweepTestAll(_rigidbody.velocity, _rigidbody.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Collide);
            //RaycastHit[] hits = Physics.SphereCastAll(transform.position, 1f, transform.forward, 1.0f);
            foreach (var hit in hits) {
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Portal")) {
                    Portal portal = hit.collider.GetComponent<Portal>();

                    m_PortalFrameCounter[portal] = 2;
                    if (portal.IsOpen && !_contextByPortal.ContainsKey(portal)) {
                        Debug.Log("Enter " + portal);
                        EnterPortal(portal);
                    }
                }
            }

            HashSet<Portal> toRemove = new HashSet<Portal>();
            foreach(var kvp in m_PortalFrameCounter.ToList()) {
                int count = kvp.Value - 1;
                m_PortalFrameCounter[kvp.Key] = count;
                if (count == -1) {
                    toRemove.Add(kvp.Key);
                    Debug.Log("Adding " + kvp.Key);
                }
            }
            
            foreach (var portal in toRemove) {
                Debug.Log("Exit " + portal);
                if (_contextByPortal.ContainsKey(portal)) {
                    ExitPortal(portal);
                }
                m_PortalFrameCounter.Remove(portal);
            }
        }

        private void Update() {

            //SweepTest();
        }

        private void FixedUpdate() {
            if (!_isClone) {
                //if (m_Rigidbody.IsSleeping()) {
                //    m_Rigidbody.WakeUp();
                //}
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

        private void LateFixedUpdate() {
            if (!_isClone) {
                SweepTest();
                foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Apply velocity restrictions to master
                    Vector3 slaveDeltaVelocity = clone._rigidbody.velocity - clone._rigidbodyLastTick.velocity;
                    Vector3 masterDeltaVelocity = _rigidbody.velocity - _rigidbodyLastTick.velocity;

                    Vector3 slaveDeltaPosition = clone._rigidbody.position - clone._rigidbodyLastTick.position;
                    Vector3 masterDeltaPosition = _rigidbody.position - _rigidbodyLastTick.position;

                    Vector3 slaveDeltaAngularVelocity = clone._rigidbody.angularVelocity - clone._rigidbodyLastTick.angularVelocity;
                    Vector3 masterDeltaAngularVelocity = _rigidbody.angularVelocity - _rigidbodyLastTick.angularVelocity;

                    //// Quaternion slaveDeltaRotation = clone.m_Rigidbody.rotation * Quaternion.Inverse(clone.m_RigidbodyLastTick.rotation);
                    //// Quaternion masterDeltaRotation = m_Rigidbody.rotation * Quaternion.Inverse(m_RigidbodyLastTick.rotation);

                    Vector3 velocityTransfer = CalculateImpulseTransfer(portal.InverseTeleportVector(slaveDeltaVelocity), masterDeltaVelocity);
                    Vector3 positionTransfer = CalculateImpulseTransfer(portal.InverseTeleportVector(slaveDeltaPosition), masterDeltaPosition);
                    Vector3 angularVelocityTransfer = CalculateImpulseTransfer(portal.InverseTeleportVector(slaveDeltaAngularVelocity), masterDeltaAngularVelocity);
                    //// Quaternion rotationTransfer = portal.ExitPortal.TeleportRotation(slaveDeltaRotation) * Quaternion.Inverse(masterDeltaRotation);

                    _rigidbody.velocity += velocityTransfer;
                    _rigidbody.position += positionTransfer;
                    _rigidbody.angularVelocity += angularVelocityTransfer;
                    //// _rigidbody.rotation *= rotationTransfer;

                }

                if (ShouldUseFixedUpdate()) {
                    TeleportCheck();
                }
            }
        }
        #endregion

        #region Triggers

        //private void OnCompositeTriggerEnter(CompositeTrigger t) {
        //    if (m_IsClone) {
        //        return;
        //    }

        //    PortalTrigger trigger = t as PortalTrigger;
        //    if (!trigger) {
        //        return;
        //    }
        //}

        //private void OnCompositeTriggerStay(CompositeTrigger t) {
        //    if (m_IsClone) {
        //        return;
        //    }

        //    PortalTrigger trigger = t as PortalTrigger;
        //    if (!trigger) {
        //        return;

        //    }

        //    m_PortalTriggersSeen.Add(trigger.portal);
        //    if (trigger.portal.IsOpen && !m_ContextByPortal.ContainsKey(trigger.portal)) {
        //        TriggerPortal(trigger.portal);
        //    }
        //}

        //private void OnCompositeTriggerExit(CompositeTrigger t) {
        //    if (m_IsClone) {
        //        return;
        //    }

        //    PortalTrigger trigger = t as PortalTrigger;
        //    if (!trigger) {
        //        return;
        //    }

        //    m_PortalTriggersSeen.Add(trigger.portal);
        //    if (m_ContextByPortal.ContainsKey(trigger.portal)) {
        //        ExitPortal(trigger.portal);
        //    }
        //}

        #endregion

        #region Teleportation
        private void EnterPortal(Portal portal) {
            Teleportable clone = SpawnClone();
            clone.gameObject.SetActive(true);
            clone.SaveRigidbodyInfo();

            ReplaceShaders(this.gameObject, portal);
            ReplaceShaders(clone.gameObject, portal.ExitPortal);

            IgnoreCollisions(portal.IgnoredColliders, true);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

            portal.OnIgnoredCollidersChanged += OnIgnoredCollidersChanged;
            portal.OnExitPortalChanged += OnExitPortalChanged;
            portal.ExitPortal.OnIgnoredCollidersChanged += clone.OnIgnoredCollidersChanged;

            PortalContext context = new PortalContext() { clone = clone };
            _contextByPortal[portal] = context;
        }

        private void Teleport(Portal portal) {
            PortalContext context = _contextByPortal[portal];
            Teleportable clone = context.clone;
            clone.SaveRigidbodyInfo();

            // Always replace our shader because we only support 1 clipping plane at the moment
            ReplaceShaders(this.gameObject, portal.ExitPortal);

            bool alreadyInExitPortal = _contextByPortal.ContainsKey(portal.ExitPortal);
            if (alreadyInExitPortal) {
                // Update our frame data early as to zero out the velocity diffs.
                // Effectively this prevents the clones from applying any forces to the master
                _contextByPortal[portal.ExitPortal].clone.SaveRigidbodyInfo();
            } else {
                IgnoreCollisions(portal.IgnoredColliders, false);
                clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);

                portal.OnIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
                portal.OnExitPortalChanged -= OnExitPortalChanged;
                portal.ExitPortal.OnIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

                if (portal.ExitPortal.ExitPortal) {
                    // In the case that the exit portal does not point back to the portal itself,
                    // 
                    portal.ExitPortal.OnIgnoredCollidersChanged += OnIgnoredCollidersChanged;
                    portal.ExitPortal.OnExitPortalChanged += OnExitPortalChanged;
                    portal.ExitPortal.ExitPortal.OnIgnoredCollidersChanged += clone.OnIgnoredCollidersChanged;

                    IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);
                    clone.IgnoreCollisions(portal.ExitPortal.ExitPortal.IgnoredColliders, true);
                    ReplaceShaders(clone.gameObject, portal.ExitPortal.ExitPortal);

                    // Swap clones if we're not already standing in the exit portal
                    // This only applies to portals very close together.
                    _contextByPortal.Remove(portal);
                    _contextByPortal.Add(portal.ExitPortal, context);
                } else {
                    _contextByPortal.Remove(portal);
                    DespawnClone(clone);
                    RestoreShaders();
                }
            }

            if (_rigidbody && !_rigidbody.isKinematic) {
                // Interpolate velocity change through the portal
                Vector3 frameMovement = _rigidbody.position - _rigidbodyLastTick.position;
                float distanceFromPortalLastFrame;
                if (portal.Plane.Raycast(new Ray(_rigidbodyLastTick.position, frameMovement), out distanceFromPortalLastFrame)) {
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

                //// float scaleDelta = portal.PortalScaleAverage();
                ////
                //// _rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;

                //Debug.Log(m_Rigidbody.position + " " + portal.TeleportPoint(m_Rigidbody.position));
                //m_Rigidbody.position = portal.TeleportPoint(m_Rigidbody.position);
                //m_Rigidbody.transform.rotation = portal.TeleportRotation(m_Rigidbody.rotation);

                // TODO: Determine whether or not I need to be using rigidbody.position
                transform.position = portal.TeleportPoint(_rigidbody.position);
                transform.rotation = portal.TeleportRotation(_rigidbody.rotation);

                //m_Rigidbody.position = Vector3.zero;
                //transform.position = Vector3.zero;
            } else {
                transform.position = portal.TeleportPoint(transform.position);
                transform.rotation = portal.TeleportRotation(transform.rotation);
            }

            //StartCoroutine(HighSpeedExitTriggerCheck(portal.ExitPortal));

            gameObject.SendMessage("OnPortalTeleport", portal, SendMessageOptions.DontRequireReceiver);
            if (OnTeleport != null) {
                OnTeleport(this, portal);
            }
        }

        private void ExitPortal(Portal portal) {
            PortalContext context = _contextByPortal[portal];

            // Do an extra teleport check in case...
            //  1. We're using Update to check for teleports instead of FixedUpdate (we have a first person camera)
            //  2. We exit the trigger before the Update loop has a chance to see that the camera crossed the portal plane
            if (ShouldTeleport(portal, context)) {
                Teleport(portal);
                return;
            }

            _contextByPortal.Remove(portal);

            Teleportable clone = context.clone;

            IgnoreCollisions(portal.IgnoredColliders, false);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);

            portal.OnIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
            portal.OnExitPortalChanged -= OnExitPortalChanged;
            portal.ExitPortal.OnIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

            DespawnClone(clone);
            RestoreShaders();
        }

        private IEnumerator HighSpeedExitTriggerCheck(Portal portal) {
            // In the case that our velocity is high enough that teleporting puts the object outside
            // of the exit portal's trigger, we have to manually call exit
            yield return new WaitForFixedUpdate();
            if (!_portalTriggersSeen.Contains(portal)) {
                ExitPortal(portal);
            }
        }

        private Portal TeleportCheck() {
            Portal toTeleport = null;
            foreach (KeyValuePair<Portal, PortalContext> kvp in _contextByPortal) {
                Portal portal = kvp.Key;
                PortalContext context = kvp.Value;

                if (ShouldTeleport(portal, context)) {
                    toTeleport = portal;
                    break;
                }
            }

            if (toTeleport) {
                Teleport(toTeleport);
            }

            return toTeleport;
        }

        private bool ShouldTeleport(Portal portal, PortalContext context) {
            Vector3 positionThisStep = _camera && _cameraType == CameraType.FirstPerson ? _camera.transform.position : _rigidbody.position;
            bool inFrontLastFrame = context.isInFrontOfPortal;
            bool inFrontThisFrame = portal.Plane.GetSide(positionThisStep);
            context.isInFrontOfPortal = inFrontThisFrame;

            return !inFrontLastFrame && inFrontThisFrame;
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
        #endregion

        #region Private Methods
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
            return _cloneObjectPool.Take();
        }

        private void DespawnClone(Teleportable clone, bool destroy = false) {
            clone.gameObject.SetActive(false);
            _cloneObjectPool.Give(clone);
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
                if (!m_ValidCloneBehaviours.Contains(behaviour.GetType())) {
                    behaviour.enabled = false;
                }
            }

            foreach (Transform child in obj.transform) {
                DisableInvalidComponentsRecursively(child.gameObject);
            }
        }

        private void SaveShaders(GameObject obj) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                _shaderByRenderer[renderer] = renderer.sharedMaterial.shader;
            }

            foreach (Transform child in obj.transform) {
                SaveShaders(child.gameObject);
            }
        }

        private void ReplaceShaders(GameObject obj, Portal portal) {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer) {
                Vector4 clippingPlane = portal.VectorPlane;
                clippingPlane.w -= VisualClippingOffset;
                renderer.material.shader = _replacementShader;
                renderer.material.SetVector(_clippingPlaneShaderHash, clippingPlane);
            }

            foreach (Transform child in obj.transform) {
                ReplaceShaders(child.gameObject, portal);
            }
        }

        private void RestoreShaders() {
            foreach (KeyValuePair<Renderer, Shader> kvp in _shaderByRenderer) {
                Renderer renderer = kvp.Key;
                Shader shader = kvp.Value;

                renderer.material.shader = shader;
            }
        }

        private void CopyAnimations(Teleportable from) {
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
                //// AnimatorStateInfo srcInfoNext = src.GetNextAnimatorStateInfo(i);
                //// AnimatorTransitionInfo srcTransitionInfo = src.GetAnimatorTransitionInfo(i);

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
                        // dst.SetTrigger(parameter.nameHash, parameter.);
                        break;
                    default:
                        break;
                }
            }

            dst.speed = src.speed;
        }

        private static Vector3 CalculateImpulseTransfer(Vector3 imp1, Vector3 imp2) {
            Vector3 impParallel = Vector3.Project(imp1, imp2);
            Vector3 impPerpendicular = imp1 - impParallel;
            Vector3 impTransfer = impPerpendicular;
            float magnitude = Vector3.Dot(imp1, imp2.normalized);
            if (magnitude < 0) {
                impTransfer += impParallel;
            } else if (magnitude > imp2.magnitude) {
                impTransfer += impParallel - imp2;
            }

            return impTransfer;
        }

        private void SaveRigidbodyInfo() {
            if (_rigidbody) {
                _rigidbodyLastTick.position = _rigidbody.position;
                _rigidbodyLastTick.rotation = _rigidbody.rotation;
                _rigidbodyLastTick.velocity = _rigidbody.velocity;
                _rigidbodyLastTick.angularVelocity = _rigidbody.angularVelocity;
            }
        }

        private bool ShouldUseFixedUpdate() {
            return _cameraType == CameraType.None || _camera == null;
        }

        #endregion

        #region Structs and Classes
        private struct RigidbodyInfo {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        private class PortalContext {
            public Teleportable clone;
            public TriggerStatus triggerStatus;
            public bool isInFrontOfPortal;
        }
        #endregion
    }
}
