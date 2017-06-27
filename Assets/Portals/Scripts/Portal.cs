using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    //[ExecuteInEditMode]
    public class Portal : MonoBehaviour {
        [SerializeField] private Portal _exitPortal;
        [SerializeField] private Texture _defaultTexture;
        [SerializeField] private Texture _transparencyMask;
        [SerializeField] private int _maxRecursiveDepth = 2;
        [SerializeField] private bool _fakeInfiniteRecursion = true;
        [SerializeField] private bool _useCullingMatrix = true;
        [SerializeField] private bool _useProjectionMatrix = true;
        // TODO: Make this only appear when useProjectionMatrix is enabled
        [SerializeField] private float _clippingOffset = 0.25f;
        [SerializeField] private bool _copyGI = false;
        [SerializeField] private List<Collider> _ignoredColliders;

        private HashSet<Camera> _camerasInside = new HashSet<Camera>();

        // TODO: Remove these
        public delegate void StaticPortalEvent(Portal portal, GameObject obj);
        public static event StaticPortalEvent onPortalEnterGlobal;
        public static event StaticPortalEvent onPortalExitGlobal;
        public static event StaticPortalEvent onPortalTeleportGlobal;

        public delegate void PortalTriggerEvent(Portal portal, GameObject obj);
        public event PortalTriggerEvent onPortalEnter;
        public event PortalTriggerEvent onPortalExit;
        public event PortalTriggerEvent onPortalTeleport;

        public delegate void PortalIgnoredCollidersChangedEvent(Portal portal, Collider[] colliders);
        public event PortalIgnoredCollidersChangedEvent onIgnoredCollidersChanged;


        public Portal ExitPortal {
            get { return _exitPortal; }
            set {
                //if (value) {
                //    _portalMaterial.EnableKeyword("DONT_SAMPLE");
                //} else {
                //    _portalMaterial.DisableKeyword("DONT_SAMPLE");
                //}
                _exitPortal = value;
            }
        }

        public Texture DefaultTexture {
            get {
                return _defaultTexture;
            }
            set {
                _defaultTexture = value;
                //_portalMaterial.SetTexture("_DefaultTexture", _defaultTexture);
            }
        }

        public Texture TransparencyMask {
            get {
                return _transparencyMask;
            }
            set {
                _transparencyMask = value;
                //_portalMaterial.SetTexture("_TransparencyMask", _transparencyMask);
            }
        }

        public int MaxRecursiveDepth {
            get { return _maxRecursiveDepth; }
            set { _maxRecursiveDepth = value; }
        }

        public bool FakeInfiniteRecursion {
            get { return _fakeInfiniteRecursion; }
            set { _fakeInfiniteRecursion = value; }
        }

        public bool UseCullingMatrix {
            get { return _useCullingMatrix; }
            set { _useCullingMatrix = value; }
        }

        public bool UseProjectionMatrix {
            get { return _useProjectionMatrix; }
            set { _useProjectionMatrix = value; }
        }

        public float ClippingOffset {
            get { return _clippingOffset; }
            set { _clippingOffset = value; }
        }

        public bool CopyGI {
            get { return _copyGI; }
            set { _copyGI = value; }
        }

        public Collider[] IgnoredColliders {
            get { return _ignoredColliders.ToArray(); }
            set {
                Collider[] oldColliders = _ignoredColliders.ToArray();
                _ignoredColliders = new List<Collider>(value);

                if (onIgnoredCollidersChanged != null) {
                    onIgnoredCollidersChanged(this, oldColliders);
                }
            }
        }

        public PortalTrigger[] PortalTriggers {
            get {
                return GetComponentsInChildren<PortalTrigger>();
            }
        }

        public Plane Plane {
            get {
                return new Plane(transform.forward, transform.position);
            }
        }

        public Vector4 VectorPlane {
            get {
                Plane plane = this.Plane;
                Vector3 normal = plane.normal;
                return new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }
        }

        // TopLeft->TopRight->BottomRight->BottomLeft
        public Vector3[] WorldSpaceCorners() {
            Vector3 topLeft     = transform.TransformPoint(new Vector3(-0.5f, 0.5f));
            Vector3 topRight    = transform.TransformPoint(new Vector3(0.5f, 0.5f));
            Vector3 bottomRight = transform.TransformPoint(new Vector3(0.5f, -0.5f));
            Vector3 bottomLeft  = transform.TransformPoint(new Vector3(-0.5f, -0.5f));
            return new Vector3[] {
                topLeft,
                topRight,
                bottomRight,
                bottomLeft
            };
        }

        public Vector3[] GetCorners() {
            //Bounds bounds = _meshFilter.sharedMesh.bounds;

            //Vector3 lowerLeft = transform.TransformPoint(new Vector3(bounds.extents.x, -bounds.extents.y, 0));
            //Vector3 lowerRight = transform.TransformPoint(new Vector3(-bounds.extents.x, -bounds.extents.y, 0));
            //Vector3 upperLeft = transform.TransformPoint(new Vector3(bounds.extents.x, bounds.extents.y, 0));
            //Vector3 upperRight = transform.TransformPoint(new Vector3(-bounds.extents.x, bounds.extents.y, 0));

            //return new Vector3[] {
            //    lowerLeft,
            //    upperLeft,
            //    upperRight,
            //    lowerRight,
            //};
            throw new System.NotImplementedException();
        }

        public float PortalScaleAverage() {
            return Helpers.VectorInternalAverage(this.PortalScale());
        }
        
        public Vector3 PortalScale() {
            return new Vector3(
                ExitPortal.transform.lossyScale.x / this.transform.lossyScale.x,
                ExitPortal.transform.lossyScale.y / this.transform.lossyScale.y,
                ExitPortal.transform.lossyScale.z / this.transform.lossyScale.z);
        }

        public Quaternion PortalRotation() {
            // Transforms a quaternion or vector into the second portal's space.
            // We have to flip the camera in between so that we face the outside direction of the portal
            return ExitPortal.transform.rotation * Quaternion.Euler(180, 0, 180) * Quaternion.Inverse(this.transform.rotation);
        }

        public Matrix4x4 PortalMatrix() {
            Quaternion flip = Quaternion.Euler(0, 180, 0);

            Matrix4x4 TRSEnter = Matrix4x4.TRS(
                this.transform.position,
                this.transform.rotation,
                this.transform.lossyScale);
            Matrix4x4 TRSExit = Matrix4x4.TRS(
                ExitPortal.transform.position,
                ExitPortal.transform.rotation * flip, // Flip around Y axis
                ExitPortal.transform.lossyScale);

            return TRSExit * TRSEnter.inverse; // Place origin at portal, then apply Exit portal's transform
        }

        public void ApplyWorldToPortalTransform(Transform target, Vector3 referencePosition, Quaternion referenceRotation, Vector3 referenceScale, bool ignoreScale = false) {
            Vector3 inversePosition = transform.InverseTransformPoint(referencePosition);

            Quaternion flip = Quaternion.Euler(0, 180, 0);

            target.position = ExitPortal.transform.TransformPoint(flip * inversePosition);
            target.rotation = PortalRotation() * referenceRotation;
            if (!ignoreScale) {
                Vector3 scale = PortalScale();
                target.localScale = new Vector3(
                    referenceScale.x * scale.x,
                    referenceScale.y * scale.y,
                    referenceScale.z * scale.z);
            }
        }

        public void ApplyWorldToPortalTransform(Transform target, Transform reference, bool ignoreScale = false) {
            ApplyWorldToPortalTransform(target, reference.position, reference.rotation, reference.lossyScale, ignoreScale);
        }
        
        public void RegisterCamera(Camera camera) {
            _camerasInside.Add(camera);
        }

        public void UnregisterCamera(Camera camera) {
            _camerasInside.Remove(camera);
        }

        void OnDrawGizmos() {
            if (ExitPortal) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(this.transform.position, ExitPortal.transform.position);
            }
        }
    }
}
