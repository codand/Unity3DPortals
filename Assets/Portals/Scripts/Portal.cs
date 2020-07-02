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
        [SerializeField] private Portal _exitPortal;
        [Tooltip("Default texture that will be used when this portal cannot render the exit side.")]
        [SerializeField] private Texture _defaultTexture;
        [Tooltip("Grayscale transparency mask. Black is visible, white is transparent.")]
        [SerializeField] private Texture _transparencyMask;
        [Tooltip("Maximum number of times this portal can recurse.")]
        [SerializeField] private int _maxRecursion = 2;
        [Tooltip("If enabled, uses the previous frame to draw the last recursion. Max Recursion must be 2 or greater.")]
        [SerializeField] private bool _fakeInfiniteRecursion = true;
        [Tooltip("If enabled, gravity will be realligned when travelling through a portal if the GravityManipulator script is attached.")]
        [SerializeField] private bool _modifyGravity = false;
        [Tooltip("Colliders to ignore when entering this portal. Set this to the objects behind the portal.")]
        [SerializeField] private List<Collider> _ignoredColliders;
        [SerializeField] private PortalQualitySettings _qualitySettings = new PortalQualitySettings() {
            downscaling = 1,
            depthBufferQuality = PortalDepthBufferQuality._32
        };
        [SerializeField] private PortalAdvancedSettings _advancedSettings = new PortalAdvancedSettings() {
            useObliqueProjectionMatrix = true,
            useOcclusionMatrix = true,
            useScissorRect = true,
            useRaycastOcclusion = true,
            raycastOccluders = ~0,
            clippingOffset = 0.01f,
            copyGlobalIllumination = false,
            supportedCameraTypes = CameraType.Game | CameraType.SceneView,
            debugEnabled = false
        };

        private PortalRenderer _portalRenderer;
        private Vector3[] _cornerBuffer;

        public delegate void PortalIgnoredCollidersChangedEvent(Portal portal, Collider[] oldColliders, Collider[] newColliders);
        public delegate void PortalExitChangeEvent(Portal portal, Portal oldExitPortal, Portal newExitPortal);
        public delegate void PortalTextureChangeEvent(Portal portal, Texture oldTexture, Texture newTexture);
        public delegate void PortalSpawnEvent(Portal portal);

        public event PortalExitChangeEvent OnExitPortalChanged;
        public event PortalIgnoredCollidersChangedEvent OnIgnoredCollidersChanged;
        public event PortalTextureChangeEvent OnDefaultTextureChanged;
        public event PortalTextureChangeEvent OnTransparencyMaskChanged;

        public static event PortalSpawnEvent OnPortalSpawn;
        public static event PortalSpawnEvent OnPortalDespawn;

        public static List<Portal> AllPortals { get; private set; } = new List<Portal>();

        /// <summary>
        /// Returns the PortalRenderer component responbile for doing the actual rendering
        /// </summary>
        public PortalRenderer PortalRenderer {
            get {
                if (!_portalRenderer) {
                    _portalRenderer = GetComponentInChildren<PortalRenderer>();
                }
                return _portalRenderer;
            }
        }

        /// <summary>
        /// Returns true if this portal has an exit portal and both are enabled
        /// </summary>
        public bool IsOpen {
            get {
                return this.isActiveAndEnabled && ExitPortal && ExitPortal.isActiveAndEnabled; 
            }
        }

        /// <summary>
        /// Get or set the exit portal. Setting the exit portal will invoke OnExitPortalChanged.
        /// </summary>
        public Portal ExitPortal {
            get {
                return _exitPortal;
            }
            set {
                Portal oldExitPortal = _exitPortal;
                _exitPortal = value;
                OnExitPortalChanged?.Invoke(this, oldExitPortal, _exitPortal);
            }
        }

        /// <summary>
        /// Get or set the default texture. Setting the default texture will invoke OnDefaultTextureChanged.
        /// </summary>
        public Texture DefaultTexture {
            get {
                return _defaultTexture;
            }
            set {
                Texture oldTexture = _defaultTexture;
                _defaultTexture = value;
                OnDefaultTextureChanged?.Invoke(this, oldTexture, _defaultTexture);
            }
        }

        /// <summary>
        /// Get or set the transparency mask. Setting the transparency mask invoke OnTransparencyMaskChanged.
        /// </summary>
        public Texture TransparencyMask {
            get {
                return _transparencyMask;
            }
            set {
                Texture oldTexture = _transparencyMask;
                _transparencyMask = value;
                OnTransparencyMaskChanged?.Invoke(this, oldTexture, _transparencyMask);
            }
        }

        /// <summary>
        /// Get or set the maximum number of times a portal can render another portal.
        /// </summary>
        public int MaxRecursion {
            get { return _maxRecursion; }
            set { _maxRecursion = value; }
        }

        /// <summary>
        /// Enable or disable infinite recursion faking. <see cref="MaxRecursion"/> must be greater than 1 for this to work.
        /// </summary>
        public bool FakeInfiniteRecursion {
            get { return _fakeInfiniteRecursion; }
            set { _fakeInfiniteRecursion = value; }
        }

        /// <summary>
        /// Enable or disable gravity modification for teleportable objects with a GravityManipulator script attached.
        /// </summary>
        public bool ModifyGravity {
            get { return _modifyGravity; }
            set { _modifyGravity = value; }
        }

        /// <summary>
        /// Enable or disable oblique projection. Enabling this will prevent portals from rendering things behind them,
        /// but will lose some depth buffer accuracy. This should be enabled unless you are using materials that support
        /// planar clipping.
        /// </summary>
        public bool UseObliqueProjectionMatrix {
            get { return _advancedSettings.useObliqueProjectionMatrix; }
            set { _advancedSettings.useObliqueProjectionMatrix = value; }
        }

        /// <summary>
        /// TODO
        /// </summary>
        public bool UseOcclusionMatrix {
            get { return _advancedSettings.useOcclusionMatrix; }
            set { _advancedSettings.useOcclusionMatrix = value; }
        }

        /// <summary>
        /// Enable or disable rectangular portal clipping. This will make portals render only the rectangular area in
        /// which they can be seen on screen, reducing the total number of objects rendering and total number of pixels
        /// drawn. This should be enabled unless it is causing rendering issues.
        /// </summary>
        public bool UseScissorRect {
            get { return _advancedSettings.useScissorRect; }
            set { _advancedSettings.useScissorRect = value; }
        }

        /// <summary>
        /// Enable or disable using raycasts to check whether or not this portal should be rendered.
        /// </summary>
        public bool UseRaycastOcclusion {
            get { return _advancedSettings.useRaycastOcclusion; }
            set { _advancedSettings.useRaycastOcclusion = value; }
        }
        
        /// <summary>
        /// Layer mask determining which objects can occlude this portal
        /// </summary>
        public LayerMask RaycastOccluders {
            get { return _advancedSettings.raycastOccluders; }
            set { _advancedSettings.raycastOccluders = value; }
        }

        /// <summary>
        /// Get or set the clipping offset when using oblique projection.
        /// </summary>
        public float ClippingOffset {
            get { return _advancedSettings.clippingOffset; }
            set { _advancedSettings.clippingOffset = value; }
        }

        /// <summary>
        /// Get or set portal texture downscaling. Increase value to improve performance.
        /// </summary>
        public int Downscaling {
            get { return _qualitySettings.downscaling; }
            set { _qualitySettings.downscaling = value; }
        }

        /// <summary>
        /// Get or set portal texture depth buffer quality.
        /// </summary>
        public PortalDepthBufferQuality DepthBufferQuality {
            get => _qualitySettings.depthBufferQuality;
            set => _qualitySettings.depthBufferQuality = value;
        }

        /// <summary>
        /// Get or set camera types that can render portals.
        /// </summary>
        public CameraType SupportedCameraTypes {
            get { return _advancedSettings.supportedCameraTypes; }
            set { _advancedSettings.supportedCameraTypes = value; }
        }

        /// <summary>
        /// Enable or disable debugging in the editor
        /// </summary>
        public bool DebuggingEnabled {
            get { return _advancedSettings.debugEnabled; }
            set { _advancedSettings.debugEnabled = value; }
        }

        /// <summary>
        /// Get or set list of colliders that will be disabled when entering this portal.
        /// </summary>
        public Collider[] IgnoredColliders {
            get {
                return _ignoredColliders.ToArray();
            }
            set {
                Collider[] oldColliders = _ignoredColliders.ToArray();
                _ignoredColliders = new List<Collider>(value);
                OnIgnoredCollidersChanged?.Invoke(this, oldColliders, IgnoredColliders);
            }
        }

        /// <summary>
        /// Returns the portal's facing plane.
        /// </summary>
        public Plane Plane {
            get {
                return new Plane(transform.forward, transform.position);
            }
        }

        /// <summary>
        /// Returns the inverse of the portal's facing plane.
        /// </summary>
        public Plane PlaneInverse {
            get {
                return new Plane(-transform.forward, transform.position);
            }
        }

        /// <summary>
        /// Returns the portal's facing plane as a Vector4.
        /// </summary>
        public Vector4 VectorPlane {
            get {
                Plane plane = this.Plane;
                Vector3 normal = plane.normal;
                return new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }
        }

        /// <summary>
        /// Returns a list of this portals corner's in world space.
        /// TopLeft = corners[0]
        /// TopRight = corners[1]
        /// BottomRight = corners[2]
        /// BottomLeft = corners[3]
        /// </summary>
        public Vector3[] WorldSpaceCorners {
            get {
                if (transform.hasChanged) {
                    Vector3 topLeft = transform.TransformPoint(new Vector3(-0.5f, 0.5f));
                    Vector3 topRight = transform.TransformPoint(new Vector3(0.5f, 0.5f));
                    Vector3 bottomRight = transform.TransformPoint(new Vector3(0.5f, -0.5f));
                    Vector3 bottomLeft = transform.TransformPoint(new Vector3(-0.5f, -0.5f));
                    if (_cornerBuffer == null) {
                        _cornerBuffer = new Vector3[4];
                    }
                    _cornerBuffer[0] = topLeft;
                    _cornerBuffer[1] = topRight;
                    _cornerBuffer[2] = bottomRight;
                    _cornerBuffer[3] = bottomLeft;

                    transform.hasChanged = false;
                }

                return _cornerBuffer;
            }
        }

        /// <summary>
        /// Returns an estimate of the change in scale between the entrance portal and the exit portal.
        /// </summary>
        public float PortalScaleAverage {
            get {
                return MathUtil.VectorInternalAverage(this.PortalScale);
            }
        }
        
        /// <summary>
        /// Returns a vector representing the size difference between the entrance portal and the exit portal
        /// </summary>
        public Vector3 PortalScale {
            get {
                return new Vector3(
                    ExitPortal.transform.lossyScale.x / this.transform.lossyScale.x,
                    ExitPortal.transform.lossyScale.y / this.transform.lossyScale.y,
                    ExitPortal.transform.lossyScale.z / this.transform.lossyScale.z);
            }
        }

        /// <summary>
        /// Matrix which transforms world space coordinates to the other side of the portal
        /// </summary>
        public Matrix4x4 PortalMatrix {
            get {
                // Convert to portal's local space, rotate 180 degrees, then convert to world space from the Exit portal
                Matrix4x4 rotate = Matrix4x4.Rotate(Quaternion.Euler(0, 180, 0));
                Matrix4x4 worldToPortal = transform.worldToLocalMatrix;
                Matrix4x4 portalToWorld = ExitPortal.transform.localToWorldMatrix * rotate;

                return portalToWorld * worldToPortal;
            }
        }

        /// <summary>
        /// Teleports a point through the portal
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector3 TeleportPoint(Vector3 point) {
            return PortalMatrix.MultiplyPoint3x4(point);
        }

        /// <summary>
        /// Teleports a directional vector through the portal
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public Vector3 TeleportVector(Vector3 vector) {
            return PortalMatrix.MultiplyVector(vector);
        }

        /// <summary>
        /// Teleports a rotation through the portal
        /// </summary>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public Quaternion TeleportRotation(Quaternion rotation) {
            Quaternion portalRotation = ExitPortal.transform.rotation * Quaternion.Euler(180, 0, 180) * Quaternion.Inverse(this.transform.rotation);
            return portalRotation * rotation;
        }

        /// <summary>
        /// Returns a Vector3 that has been scaled by the difference in scale of the entrance portal and the exit portal
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public Vector3 TeleportScale(Vector3 scale) {
            Vector3 pScale = PortalScale;
            return new Vector3(scale.x * pScale.x, scale.y * pScale.y, scale.z * pScale.z);
        }

        /// <summary>
        /// Teleports a transform based on the reference transform's position, rotation, and scale
        /// </summary>
        /// <param name="target"></param>
        /// <param name="reference"></param>
        public void TeleportTransform(Transform target, Transform reference) {
            target.position = TeleportPoint(reference.position);
            target.rotation = TeleportRotation(reference.rotation);
            target.localScale = TeleportScale(reference.localScale);
        }

        /// <summary>
        /// Teleports a transform
        /// </summary>
        /// <param name="target"></param>
        /// <param name="reference"></param>
        public void TeleportTransform(Transform target) {
            target.position = TeleportPoint(target.position);
            target.rotation = TeleportRotation(target.rotation);
            target.localScale = TeleportScale(target.localScale);
        }

        //private void OnDrawGizmos() {
        //    if (ExitPortal) {
        //        Gizmos.color = Color.magenta;
        //        Gizmos.DrawLine(this.transform.position, ExitPortal.transform.position);
        //    }
        //}

        private void OnValidate() {
            // Calls OnX methods
            ExitPortal = _exitPortal;
            DefaultTexture = _defaultTexture;
            TransparencyMask = _transparencyMask;
        }

        private void OnEnable() {
            AllPortals.Add(this);
            OnPortalSpawn?.Invoke(this);
        }

        private void OnDisable() {
            AllPortals.Remove(this);
            OnPortalDespawn?.Invoke(this);
        }

        public enum PortalDepthBufferQuality {
            _0 = 0,
            _16 = 16,
            _24 = 24,
            _32 = 32
        }

        [System.Serializable]
        private struct PortalQualitySettings {
            [Tooltip("Reduce portal rendering resolution. Use 1 for the best quality, use higher numbers for better performance.")]
            [Range(1, 32)]
            public int downscaling;

            // TODO: Tooltip
            [Tooltip("blank")]
            public PortalDepthBufferQuality depthBufferQuality;
        }

        [System.Serializable]
        private struct PortalAdvancedSettings {
            [Tooltip("If enabled, uses a custom projection matrix to prevent objects behind the portal from being drawn.")]
            public bool useObliqueProjectionMatrix;
            [Tooltip("If enabled, uses a custom occlusion culling matrix")]
            public bool useOcclusionMatrix;
            [Tooltip("Enabling reduces overdraw by not rendering any pixels outside of a portal frame.")]
            public bool useScissorRect;
            [Tooltip("Offset at which the custom projection matrix will be disabled.Increase this value if you experience Z-Fighting issues. Decrease this if you can see objects behind the portal.")]
            public float clippingOffset;
            [Tooltip("If enabled, global illumination settings will be copied if the exit portal is in a different scene.")]
            public bool copyGlobalIllumination;
            [Tooltip("If enabled, will use raycasting to determine whether or not to render this portal.")]
            public bool useRaycastOcclusion;
            public LayerMask raycastOccluders;
            [Tooltip("Should portals be rendered in Scene view or in reflections?")]
            public CameraType supportedCameraTypes;

            public bool debugEnabled;
        }
    }
}
