// -----------------------------------------------------------------------------------------------------------
// <summary>
// Seamless portal system to teleport objects between locations. Objects must have a Teleportable script to pass through.
// </summary>
// -----------------------------------------------------------------------------------------------------------
namespace Portals {
    using System.Collections.Generic;
    using UnityEngine;

    [SelectionBase]
    public class Portal : MonoBehaviour {
        [Tooltip("Exit destination for this portal.")]
        [SerializeField] private Portal m_ExitPortal;
        [Tooltip("Default texture that will be used when this portal cannot render the exit side.")]
        [SerializeField] private Texture m_DefaultTexture;
        [Tooltip("Grayscale transparency mask. Black is visible, white is transparent.")]
        [SerializeField] private Texture m_TransparencyMask;
        [Tooltip("Maximum number of times this portal can recurse.")]
        [SerializeField] private int m_MaxRecursion = 2;
        [Tooltip("If enabled, uses the previous frame to draw the last recursion. Max Recursion must be 2 or greater.")]
        [SerializeField] private bool m_FakeInfiniteRecursion = true;
        [Tooltip("Colliders to ignore when entering this portal. Set this to the objects behind the portal.")]
        [SerializeField] private List<Collider> m_IgnoredColliders;
        [SerializeField] private AdvancedSettings m_AdvanceSettings = new AdvancedSettings() {
            useDepthMask = true,
            useCullingMatrix = true,
            useObliqueProjectionMatrix = true,
            clippingOffset = 0.01f,
            copyGlobalIllumination = false,
        };

        private PortalRenderer m_PortalRenderer;

        public delegate void PortalIgnoredCollidersChangedEvent(Portal portal, Collider[] oldColliders, Collider[] newColliders);
        public event PortalIgnoredCollidersChangedEvent OnIgnoredCollidersChanged;

        public delegate void PortalExitChangeEvent(Portal portal, Portal oldExitPortal, Portal newExitPortal);
        public event PortalExitChangeEvent OnExitPortalChanged;

        public delegate void PortalTextureChangeEvent(Portal portal, Texture oldTexture, Texture newTexture);
        public event PortalTextureChangeEvent OnDefaultTextureChanged;
        public event PortalTextureChangeEvent OnTransparencyMaskChanged;

        public PortalRenderer PortalRenderer {
            get {
                if (!m_PortalRenderer) {
                    m_PortalRenderer = GetComponentInChildren<PortalRenderer>();
                }
                return m_PortalRenderer;
            }
        }

        public bool IsOpen {
            get {
                return this.isActiveAndEnabled && ExitPortal && ExitPortal.isActiveAndEnabled; 
            }
        }

        public Portal ExitPortal {
            get {
                return m_ExitPortal;
            }

            set {
                Portal oldExitPortal = m_ExitPortal;

                m_ExitPortal = value;

                if (OnExitPortalChanged != null) {
                    OnExitPortalChanged(this, oldExitPortal, m_ExitPortal);
                }
            }
        }

        public Texture DefaultTexture {
            get {
                return m_DefaultTexture;
            }

            set {
                Texture oldTexture = m_DefaultTexture;

                m_DefaultTexture = value;

                if (OnDefaultTextureChanged != null) {
                    OnDefaultTextureChanged(this, oldTexture, m_DefaultTexture);
                }
            }
        }

        public Texture TransparencyMask {
            get {
                return m_TransparencyMask;
            }

            set {
                Texture oldTexture = m_TransparencyMask;

                m_TransparencyMask = value;

                if (OnTransparencyMaskChanged != null) {
                    OnTransparencyMaskChanged(this, oldTexture, m_TransparencyMask);
                }
            }
        }

        public int MaxRecursion {
            get { return m_MaxRecursion; }
            set { m_MaxRecursion = value; }
        }

        public bool FakeInfiniteRecursion {
            get { return m_FakeInfiniteRecursion; }
            set { m_FakeInfiniteRecursion = value; }
        }

        public bool UseDepthMask {
            get { return m_AdvanceSettings.useDepthMask; }
            set { m_AdvanceSettings.useDepthMask = value; }
        }

        public bool UseCullingMatrix {
            get { return m_AdvanceSettings.useCullingMatrix; }
            set { m_AdvanceSettings.useCullingMatrix = value; }
        }

        public bool UseObliqueProjectionMatrix {
            get { return m_AdvanceSettings.useObliqueProjectionMatrix; }
            set { m_AdvanceSettings.useObliqueProjectionMatrix = value; }
        }

        public bool UseScissorRect {
            get { return m_AdvanceSettings.useScissorRect; }
            set { m_AdvanceSettings.useScissorRect = value; }
        }

        public float ClippingOffset {
            get { return m_AdvanceSettings.clippingOffset; }
            set { m_AdvanceSettings.clippingOffset = value; }
        }

        public bool CopyGlobalIllumination {
            get { return m_AdvanceSettings.copyGlobalIllumination; }
            set { m_AdvanceSettings.copyGlobalIllumination = value; }
        }

        public Collider[] IgnoredColliders {
            get {
                return m_IgnoredColliders.ToArray();
            }

            set {
                Collider[] oldColliders = m_IgnoredColliders.ToArray();
                m_IgnoredColliders = new List<Collider>(value);

                if (OnIgnoredCollidersChanged != null) {
                    OnIgnoredCollidersChanged(this, oldColliders, IgnoredColliders);
                }
            }
        }

        public Plane Plane {
            get {
                return new Plane(transform.forward, transform.position);
            }
        }

        public Plane PlaneInverse {
            get {
                return new Plane(-transform.forward, transform.position);
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
            return MathUtil.VectorInternalAverage(this.PortalScale());
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

        public Vector3 TeleportPoint(Vector3 point) {
            return PortalMatrix().MultiplyPoint3x4(point);
        }

        public Vector3 TeleportVector(Vector3 vector) {
            return PortalMatrix().MultiplyVector(vector);
        }

        public Quaternion TeleportRotation(Quaternion rotation) {
            return PortalRotation() * rotation;
        }

        public Vector3 InverseTeleportPoint(Vector3 point) {
            return PortalMatrix().inverse.MultiplyPoint3x4(point);
        }

        public Vector3 InverseTeleportVector(Vector3 vector) {
            return PortalMatrix().inverse.MultiplyVector(vector);
        }

        public Quaternion InverseTeleportRotation(Quaternion rotation) {
            return Quaternion.Inverse(PortalRotation()) * rotation;
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
        
        ////private void OnDrawGizmos() {
        ////    if (ExitPortal) {
        ////        Gizmos.color = Color.magenta;
        ////        Gizmos.DrawLine(this.transform.position, ExitPortal.transform.position);
        ////    }
        ////}

        private void OnValidate() {
            // Calls OnX methods
            ExitPortal = m_ExitPortal;
            DefaultTexture = m_DefaultTexture;
            TransparencyMask = m_TransparencyMask;
        }

        [System.Serializable]
        private struct AdvancedSettings {
            public bool useDepthMask;
            [Tooltip("If enabled, uses a custom culling matrix to reduce number of objects drawn through a portal.")]
            public bool useCullingMatrix;
            [Tooltip("If enabled, uses a custom projection matrix to prevent objects behind the portal from being drawn.")]
            public bool useObliqueProjectionMatrix;
            [Tooltip("Enabling reduces overdraw by not rendering any pixels outside of a portal frame.")]
            public bool useScissorRect;
            [Tooltip("Offset at which the custom projection matrix will be disabled.Increase this value if you experience Z-Fighting issues. Decrease this if you can see objects behind the portal.")]
            public float clippingOffset;
            [Tooltip("If enabled, global illumination settings will be copied if the exit portal is in a different scene.")]
            public bool copyGlobalIllumination;
        }
    }
}
