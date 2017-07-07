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

        [SerializeField] private CameraType m_CameraType;
        [SerializeField] private Camera m_Camera;
        [SerializeField] private bool m_SpawnCloneOnAwake = true;

        private bool m_IsClone;

        // Shader that will be applied when passing through the portal
        private Shader m_ClippedShader;
        private int m_ClippedShaderPlaneHash;
        private Material m_ClippedMaterial;

        // Stores this object's original shaders so that they can be restored
        private Dictionary<Renderer, Shader> m_ShaderByRenderer;
        private Dictionary<Portal, PortalContext> m_ContextByPortal;

        private ObjectPool<Teleportable> m_CloneObjectPool;
        
        private Collider[] m_AllColliders;

        private Rigidbody m_Rigidbody;
        private RigidbodyInfo m_RigidbodyLastTick;
        private Vector3 m_CameraPositionLastFrame;

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
        #endregion

        #region Initialization
        private void Awake() {
            // Awake is called on all clones
            m_Rigidbody = GetComponent<Rigidbody>();
            m_AllColliders = GetComponentsInChildren<Collider>(true);

            StartCoroutine(LateFixedUpdateRoutine());
        }

        private IEnumerator LateFixedUpdateRoutine() {
            while (Application.isPlaying) {
                yield return new WaitForFixedUpdate();
                LateFixedUpdate();
            }
        }

        private void Start() {
            if (m_IsClone) {
                if (m_Rigidbody) {
                    m_Rigidbody.useGravity = false;
                }
            } else {
                m_ClippedShader = Shader.Find("Portals/StandardClipped");
                m_ClippedShaderPlaneHash = Shader.PropertyToID("_ClippingPlane");
                m_ContextByPortal = new Dictionary<Portal, PortalContext>();

                m_ShaderByRenderer = new Dictionary<Renderer, Shader>();
                m_CloneObjectPool = new ObjectPool<Teleportable>(m_SpawnCloneOnAwake ? 1 : 0, CreateClone);

                SaveShaders(this.gameObject);
            }
        }
        #endregion

        #region Updates
        private void LateUpdate() {
            if (!m_IsClone) {
                if (!ShouldUseFixedUpdate()) {
                    TeleportCheck();
                }

                foreach (KeyValuePair<Portal, PortalContext> kvp in m_ContextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Lock clone to master
                    clone.transform.position = portal.TeleportPoint(transform.position);
                    clone.transform.rotation = portal.TeleportRotation(transform.rotation);

                    clone.CopyAnimations(this);
                }

                m_CameraPositionLastFrame = m_Camera.transform.position;
            }
        }

        private void FixedUpdate() {
            if (!m_IsClone) {
                foreach (KeyValuePair<Portal, PortalContext> kvp in m_ContextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Lock clone to master
                    clone.m_Rigidbody.position = portal.TeleportPoint(m_Rigidbody.position);
                    clone.m_Rigidbody.rotation = portal.TeleportRotation(m_Rigidbody.rotation);
                    clone.m_Rigidbody.velocity = portal.TeleportVector(m_Rigidbody.velocity);
                    clone.m_Rigidbody.angularVelocity = portal.TeleportVector(m_Rigidbody.angularVelocity);

                    // Save clone's modified state
                    clone.SaveRigidbodyInfo();
                }
                
                // Save master unmodified state
                SaveRigidbodyInfo();
            }
        }

        private void LateFixedUpdate() {
            if (!m_IsClone) {
                foreach (KeyValuePair<Portal, PortalContext> kvp in m_ContextByPortal) {
                    Portal portal = kvp.Key;
                    PortalContext context = kvp.Value;
                    Teleportable clone = context.clone;

                    // Apply velocity restrictions to master
                    Vector3 slaveDeltaVelocity = clone.m_Rigidbody.velocity - clone.m_RigidbodyLastTick.velocity;
                    Vector3 masterDeltaVelocity = m_Rigidbody.velocity - m_RigidbodyLastTick.velocity;

                    Vector3 slaveDeltaPosition = clone.m_Rigidbody.position - clone.m_RigidbodyLastTick.position;
                    Vector3 masterDeltaPosition = m_Rigidbody.position - m_RigidbodyLastTick.position;

                    Vector3 slaveDeltaAngularVelocity = clone.m_Rigidbody.angularVelocity - clone.m_RigidbodyLastTick.angularVelocity;
                    Vector3 masterDeltaAngularVelocity = m_Rigidbody.angularVelocity - m_RigidbodyLastTick.angularVelocity;

                    //// Quaternion slaveDeltaRotation = clone.m_Rigidbody.rotation * Quaternion.Inverse(clone.m_RigidbodyLastTick.rotation);
                    //// Quaternion masterDeltaRotation = m_Rigidbody.rotation * Quaternion.Inverse(m_RigidbodyLastTick.rotation);

                    Vector3 velocityTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaVelocity), masterDeltaVelocity);
                    Vector3 positionTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaPosition), masterDeltaPosition);
                    Vector3 angularVelocityTransfer = CalculateImpulseTransfer(portal.ExitPortal.TeleportVector(slaveDeltaAngularVelocity), masterDeltaAngularVelocity);
                    //// Quaternion rotationTransfer = portal.ExitPortal.TeleportRotation(slaveDeltaRotation) * Quaternion.Inverse(masterDeltaRotation);

                    m_Rigidbody.velocity += velocityTransfer;
                    m_Rigidbody.position += positionTransfer;
                    m_Rigidbody.angularVelocity += angularVelocityTransfer;
                    //// _rigidbody.rotation *= rotationTransfer;
                }

                if (ShouldUseFixedUpdate()) {
                    TeleportCheck();
                }
            }
        }
        #endregion

        #region Triggers

        private void OnCompositeTriggerEnter(CompositeTrigger t) {
            if (m_IsClone) {
                return;
            }
            
            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }

            if (!trigger.portal.ExitPortal || !trigger.portal.ExitPortal.isActiveAndEnabled || m_ContextByPortal.ContainsKey(trigger.portal)) {
                return;
            }

            TriggerPortal(trigger.portal);
        }

        private void OnCompositeTriggerStay(CompositeTrigger t) {
            // In the case that this portal's exit portal is not active, but becomes active, need to check for enter again
            OnCompositeTriggerEnter(t);
        }

        private void OnCompositeTriggerExit(CompositeTrigger t) {
            if (m_IsClone) {
                return;
            }

            PortalTrigger trigger = t as PortalTrigger;
            if (!trigger) {
                return;
            }

            if (!trigger.portal.ExitPortal || !m_ContextByPortal.ContainsKey(trigger.portal)) {
                return;
            }

            ExitPortal(trigger.portal);
        }
        #endregion

        #region Teleportation
        private void TriggerPortal(Portal portal) {
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
            m_ContextByPortal[portal] = context;
        }

        private void Teleport(Portal portal) {
            PortalContext context = m_ContextByPortal[portal];
            Teleportable clone = context.clone;

            // Always replace our shader because we only support 1 clipping plane at the moment
            ReplaceShaders(this.gameObject, portal.ExitPortal);

            bool alreadyInExitPortal = m_ContextByPortal.ContainsKey(portal.ExitPortal);
            if (alreadyInExitPortal) {
                // Update our frame data early as to zero out the velocity diffs.
                // Effectively this prevents the clones from applying any forces to the master
                clone.SaveRigidbodyInfo();
                m_ContextByPortal[portal.ExitPortal].clone.SaveRigidbodyInfo();
            } else {
                clone.SaveRigidbodyInfo();

                IgnoreCollisions(portal.IgnoredColliders, false);
                IgnoreCollisions(portal.ExitPortal.IgnoredColliders, true);

                clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);
                clone.IgnoreCollisions(portal.IgnoredColliders, true);

                portal.OnIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
                portal.OnExitPortalChanged -= OnExitPortalChanged;
                portal.ExitPortal.OnIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

                portal.ExitPortal.OnIgnoredCollidersChanged += OnIgnoredCollidersChanged;
                portal.ExitPortal.OnExitPortalChanged += OnExitPortalChanged;
                portal.OnIgnoredCollidersChanged += clone.OnIgnoredCollidersChanged;

                ReplaceShaders(clone.gameObject, portal);
                
                // Swap clones if we're not already standing in the exit portal
                // This only applies to portals very close together.
                m_ContextByPortal.Remove(portal);
                m_ContextByPortal.Add(portal.ExitPortal, context);
            }

            if (m_Rigidbody) {
                // Interpolate velocity change through the portal
                Vector3 frameMovement = m_Rigidbody.position - m_RigidbodyLastTick.position;
                float distanceFromPortalLastFrame;
                if (portal.Plane.Raycast(new Ray(m_RigidbodyLastTick.position, frameMovement), out distanceFromPortalLastFrame)) {
                    float ratioLastFrame = distanceFromPortalLastFrame / frameMovement.magnitude;
                    float ratioThisFrame = 1 - ratioLastFrame;

                    Vector3 acceleration = Physics.gravity * Time.deltaTime; // Unfortunately we can't known about forces other than gravity because they aren't exposed by Rigidbody API

                    Vector3 velocity = m_Rigidbody.velocity;
                    velocity -= acceleration; // Rewind acceleration
                    velocity += acceleration * ratioLastFrame; // Replay acceleration until we hit the portal
                    velocity = portal.TeleportVector(velocity); // Teleport velocity
                    velocity += acceleration * ratioThisFrame; // Replay acceleration after we passed the portal
                    m_Rigidbody.velocity = velocity;
                } else {
                    m_Rigidbody.velocity = portal.TeleportVector(m_Rigidbody.velocity);
                }

                //// float scaleDelta = portal.PortalScaleAverage();
                ////
                //// _rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;

                m_Rigidbody.position = portal.TeleportPoint(m_Rigidbody.position);
                m_Rigidbody.transform.rotation = portal.TeleportRotation(m_Rigidbody.rotation);

                transform.position = m_Rigidbody.position;
                transform.rotation = m_Rigidbody.rotation;
            }
            //// portal.ApplyWorldToPortalTransform(transform, transform);

            gameObject.SendMessage("OnPortalTeleport", portal, SendMessageOptions.DontRequireReceiver);
            if (OnTeleport != null) {
                OnTeleport(this, portal);
            }
        }

        private void ExitPortal(Portal portal) {
            PortalContext context = m_ContextByPortal[portal];
            m_ContextByPortal.Remove(portal);

            Teleportable clone = context.clone;

            IgnoreCollisions(portal.IgnoredColliders, false);
            clone.IgnoreCollisions(portal.ExitPortal.IgnoredColliders, false);

            portal.OnIgnoredCollidersChanged -= OnIgnoredCollidersChanged;
            portal.OnExitPortalChanged -= OnExitPortalChanged;
            portal.ExitPortal.OnIgnoredCollidersChanged -= clone.OnIgnoredCollidersChanged;

            DespawnClone(clone);
            RestoreShaders();
        }

        private Portal TeleportCheck() {
            Portal toTeleport = null;
            foreach (Portal portal in m_ContextByPortal.Keys) {
                if (ShouldTeleport(portal)) {
                    toTeleport = portal;
                    break;
                }
            }

            if (toTeleport) {
                Teleport(toTeleport);
            }

            return toTeleport;
        }

        private bool ShouldTeleport(Portal portal) {
            // TODO: Support CharacterController?
            Vector3 positionLastStep = m_Camera && m_CameraType == CameraType.FirstPerson ? m_CameraPositionLastFrame : m_RigidbodyLastTick.position;
            Vector3 positionThisStep = m_Camera && m_CameraType == CameraType.FirstPerson ? m_Camera.transform.position : m_Rigidbody.position;
            return !portal.Plane.GetSide(positionLastStep) && portal.Plane.GetSide(positionThisStep);
        }

        #endregion

        #region Callbacks
        private void OnIgnoredCollidersChanged(Portal portal, Collider[] oldColliders, Collider[] newColliders) {
            IgnoreCollisions(oldColliders, false);
            IgnoreCollisions(newColliders, true);
        }

        private void OnExitPortalChanged(Portal portal, Portal oldExitPortal, Portal newExitPortal) {
            PortalContext context;
            if (m_ContextByPortal.TryGetValue(portal, out context)) {
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
                for (int j = 0; j < m_AllColliders.Length; j++) {
                    Collider collider = m_AllColliders[j];
                    Physics.IgnoreCollision(collider, other, ignore);
                }
            }
        }

        private Teleportable SpawnClone() {
            return m_CloneObjectPool.Take();
        }

        private void DespawnClone(Teleportable clone, bool destroy = false) {
            clone.gameObject.SetActive(false);
            m_CloneObjectPool.Give(clone);
        }

        private Teleportable CreateClone() {
            Teleportable clone = Instantiate(this);
            DisableInvalidComponentsRecursively(clone.gameObject);
            clone.gameObject.SetActive(false);
            clone.m_IsClone = true;

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
                m_ShaderByRenderer[renderer] = renderer.sharedMaterial.shader;
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
                renderer.material.shader = m_ClippedShader;
                renderer.material.SetVector(m_ClippedShaderPlaneHash, clippingPlane);
            }

            foreach (Transform child in obj.transform) {
                ReplaceShaders(child.gameObject, portal);
            }
        }

        private void RestoreShaders() {
            foreach (KeyValuePair<Renderer, Shader> kvp in m_ShaderByRenderer) {
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
            m_RigidbodyLastTick.position = m_Rigidbody.position;
            m_RigidbodyLastTick.rotation = m_Rigidbody.rotation;
            m_RigidbodyLastTick.velocity = m_Rigidbody.velocity;
            m_RigidbodyLastTick.angularVelocity = m_Rigidbody.angularVelocity;
        }

        private bool ShouldUseFixedUpdate() {
            return m_CameraType == CameraType.None || m_Camera == null;
        }

        ////private Portal[] SweepTest() {
        ////    Vector3 direction = m_Rigidbody.velocity;
        ////    float distance = m_Rigidbody.velocity.magnitude * Time.fixedDeltaTime;
        ////    if (direction == Vector3.zero || distance == 0) {
        ////        direction = Physics.gravity;
        ////        distance = Physics.gravity.magnitude * Time.fixedDeltaTime;
        ////    }

        ////    List<Portal> portalsHit = new List<Portal>();
        ////    RaycastHit[] hits = m_Rigidbody.SweepTestAll(direction, distance, QueryTriggerInteraction.Collide);
        ////    for (int i = 0; i < hits.Length; i++) {
        ////        RaycastHit hit = hits[i];
        ////        if (hit.collider.gameObject.layer == PortalPhysics.PortalLayer) {
        ////            portalsHit.Add(hit.collider.GetComponent<Portal>());
        ////        }
        ////    }

        ////    return portalsHit.ToArray();
        ////}
        #endregion

        #region Structs and Classes
        private struct RigidbodyInfo {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        private struct PortalContext {
            public Teleportable clone;
        }
        #endregion
    }
}
