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
        [SerializeField] private Collider _attachedCollider;

        public Portal ExitPortal {
            get { return _exitPortal; }
            set {
                if (value) {
                    _portalMaterial.EnableKeyword("DONT_SAMPLE");
                } else {
                    _portalMaterial.DisableKeyword("DONT_SAMPLE");
                }
                _exitPortal = value;
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

        public Collider AttachedCollider {
            get { return _attachedCollider; }
            set {
                _attachedCollider = value;
            }
        }

        //public List<Collider> ignoredColliders = new List<Collider>();

        public delegate void PortalEvent(Portal portal, GameObject obj);
        //public static event PortalEvent onPortalEnter;
        public static event PortalEvent onPortalExit;

        // Maps cameras to their children
        private Dictionary<Camera, Camera> _camToPortalCam = new Dictionary<Camera, Camera>();

        // Used to track current recursion depth.
        // This is static because recursion can occur on more than one portal
        private static int s_depth;

        // Mesh spawned when walking through a portal so that you can't clip through the portal
        public static Mesh _mesh;
        
        // Per-portal instance of the backface object
        private GameObject _backFace;

        // Instanced material
        private Material _portalMaterial;

        // Instanced backface material
        private Material _backFaceMaterial;

        // Dictionary mapping objects to their clones on the other side of a portal
        private Dictionary<GameObject, GameObject> _objectToClone = new Dictionary<GameObject, GameObject>();

        private Dictionary<Camera, bool> _wasRenderedByCamera = new Dictionary<Camera, bool>();
        private Stack<Texture> _savedTextures = new Stack<Texture>();

        void Awake() {
            if (!_mesh) {
                _mesh = MakeMesh();
            }

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            if (!_portalMaterial || !_backFaceMaterial) {
                Material portalMaterial = new Material(Shader.Find("Portal/Portal"));
                Material backFaceMaterial = new Material(Shader.Find("Portal/PortalBackface"));

                portalMaterial.name = "Portal FrontFace (Instanced)";
                backFaceMaterial.name = "Portal BackFace (Instanced)";

                _portalMaterial = portalMaterial;
                _backFaceMaterial = backFaceMaterial;

                GetComponent<MeshRenderer>().sharedMaterials = new Material[] {
                    _portalMaterial,
                    _backFaceMaterial,
                };
            }
        }

        void OnDisable() {
            // Clean up cameras in scene. This is important when using ExecuteInEditMode because
            // script recompilation will disable then enable this script causing creation of duplicate
            // cameras.
            foreach (KeyValuePair<Camera, Camera> kvp in _camToPortalCam) {
                Camera child = kvp.Value;
                if (child && child.gameObject) {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        void Update() {
            _wasRenderedByCamera.Clear();
        }

        void PushCurrentTexture() {
            Texture tex = _portalMaterial.GetTexture("_RightEyeTexture");
            _savedTextures.Push(tex);
            _wasRenderedByCamera[Camera.current] = true;
        }

        void PopCurrentTexture() {
            Texture tex = _savedTextures.Pop();
            _portalMaterial.SetTexture("_RightEyeTexture", tex);
            _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            _portalMaterial.DisableKeyword("DONT_SAMPLE");
            _wasRenderedByCamera[Camera.current] = false;
        }

        void OnRenderObject() {
            bool wasRendered;
            _wasRenderedByCamera.TryGetValue(Camera.current, out wasRendered);
            if (wasRendered) {
                PopCurrentTexture();
            }
        }

        //Stack<Texture> stack = new Stack<Texture>();
        void OnWillRenderObject() {
            if (!enabled ||
                !GetComponent<Renderer>() ||
                !GetComponent<Renderer>().sharedMaterial ||
                !GetComponent<Renderer>().enabled) {
                return;
            }

            if (!ExitPortal) {
                GetComponent<Renderer>().enabled = false;
                return;
            }

            if (Camera.current == null) {
                return;
            }

            if (Camera.current.name == "SceneCamera" || Camera.current.name == "Reflection Probes Camera" || Camera.current.name == "Preview Camera")
                return;

            PortalCamera currentPortalCam = Camera.current.GetComponent<PortalCamera>();

            // Don't ever render our own exit portal
            if (s_depth > 0 && currentPortalCam != null && this == currentPortalCam.portal.ExitPortal) {
                return;
            }

            // TODO: set these only once
            _portalMaterial.SetTexture("_TransparencyMask", _transparencyMask);
            _portalMaterial.SetTexture("_DefaultTexture", _defaultTexture);

            PushCurrentTexture();

            // Stop recursion when we reach maximum depth
            if (s_depth >= _maxRecursiveDepth) {

                if (_fakeInfiniteRecursion && _maxRecursiveDepth >= 2) {
                    // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
                    // This creates an illusion of infinite recursion, but only works with at least two real recursions
                    // because the effect is unconvincing using the Main Camera's previous frame.
                    Camera parentCam = currentPortalCam.parent;
                    PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

                    if (currentPortalCam.portal == this) {
                        // This portal is currently viewing itself.
                        // Render the bottom of the stack with the parent camera's view/projection.
                        GetComponent<Renderer>().sharedMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
                        GetComponent<Renderer>().sharedMaterial.SetMatrix("PORTAL_MATRIX_VP", parentPortalCam.lastFrameProjectionMatrix * parentPortalCam.lastFrameWorldToCameraMatrix);
                        GetComponent<Renderer>().sharedMaterial.SetTexture("_RightEyeTexture", parentPortalCam.lastFrameRenderTexture);
                    } else {
                        // We are viewing another portal.
                        // Render the bottom of the stack with a base texture
                        GetComponent<Renderer>().sharedMaterial.EnableKeyword("DONT_SAMPLE");
                    }
                } else {
                    GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    GetComponent<Renderer>().sharedMaterial.EnableKeyword("DONT_SAMPLE");
                }

                // Exit. We don't need to process any further depths.
                return;
            }

            // Initialize or get anything needed for this depth level
            Camera camToRender = null;
            CreatePortalObjects(Camera.current, out camToRender);
            camToRender.GetComponent<PortalCamera>().renderDepth = s_depth + 1;

            s_depth++;
            if (VRDevice.isPresent) {
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Left) {
                    RenderTexture leftEyeTexture = camToRender.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Left);
                    _portalMaterial.SetTexture("_LeftEyeTexture", leftEyeTexture);
                }
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Right) {
                    RenderTexture rightEyeTexture = camToRender.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Right);
                    _portalMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
                }
            } else {
                RenderTexture rightEyeTexture = camToRender.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Mono);
                _portalMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
                _backFaceMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
            }
            s_depth--;
        }
        
        public Vector3[] GetCorners() {
            Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;

            Vector3 lowerLeft = transform.TransformPoint(new Vector3(bounds.extents.x, -bounds.extents.y, 0));
            Vector3 lowerRight = transform.TransformPoint(new Vector3(-bounds.extents.x, -bounds.extents.y, 0));
            Vector3 upperLeft = transform.TransformPoint(new Vector3(bounds.extents.x, bounds.extents.y, 0));
            Vector3 upperRight = transform.TransformPoint(new Vector3(-bounds.extents.x, bounds.extents.y, 0));

            return new Vector3[] {
                lowerLeft,
                upperLeft,
                upperRight,
                lowerRight,
            };
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

        void CreatePortalObjects(Camera currentCamera, out Camera portalCamera) {
            portalCamera = null;

            _camToPortalCam.TryGetValue(currentCamera, out portalCamera);
            if (!portalCamera) // catch both not-in-dictionary and in-dictionary-but-deleted-GO
            {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + gameObject.name, typeof(Camera));
                portalCamera = go.GetComponent<Camera>();
                portalCamera.enabled = false;
                portalCamera.transform.position = transform.position;
                portalCamera.transform.rotation = transform.rotation;
                //portalCamera.depthTextureMode = DepthTextureMode.DepthNormals;
                portalCamera.gameObject.AddComponent<FlareLayer>();
                //go.hideFlags = HideFlags.HideAndDontSave;
                go.hideFlags = HideFlags.DontSave;

                PortalCamera pc = go.AddComponent<PortalCamera>();
                // TODO: Awake doesn't get called when using ExecuteInEditMode
                pc.Awake();
                pc.enterScene = this.gameObject.scene;
                pc.exitScene = ExitPortal.gameObject.scene;
                pc.parent = currentCamera;
                pc.portal = this;

                if (this.gameObject.scene != ExitPortal.gameObject.scene) {
                    //PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    //thing.scene = exitPortal.gameObject.scene;
                }

                _camToPortalCam[currentCamera] = portalCamera;
            }
        }

        void SpawnClone(GameObject obj) {
            // Create a clone on the other side
            if (obj.tag == "Player")
                return;
            GameObject clone = Instantiate(obj);
            PortalClone cloneScript = clone.AddComponent<PortalClone>();
            cloneScript.target = obj.transform;
            cloneScript.portal = this;

            _objectToClone[obj] = clone;
        }

        void DestroyClone(GameObject obj) {
            // Destroy clone if exists
            GameObject clone;
            _objectToClone.TryGetValue(obj, out clone);
            if (clone) {
                Destroy(clone);
            }
        }

        void IgnoreCollisions(Collider collider, bool ignore) {
            if (AttachedCollider) {
                Physics.IgnoreCollision(collider, AttachedCollider, ignore);
            }
            //if (ExitPortal && ExitPortal.AttachedCollider) {
            //    Physics.IgnoreCollision(collider, ExitPortal.AttachedCollider, ignore);
            //}
        }

        void OnTriggerEnter(Collider collider) {
            if (!ExitPortal) {
                return;
            }

            if (AttachedCollider) {
                Physics.IgnoreCollision(collider, AttachedCollider, true);
            }

            collider.gameObject.SendMessage("OnPortalTriggered", this, SendMessageOptions.DontRequireReceiver);
        }

        void OnTriggerExit(Collider collider) {
            if (!ExitPortal) {
                return;
            }

            if (AttachedCollider) {
                Physics.IgnoreCollision(collider, AttachedCollider, false);
            }
        }

        void OnTriggerStay(Collider collider) {
            if (!ExitPortal) {
                return;
            }
            //Debug.Log(collider.name + " stayed " + this.name);
            Vector3 normal = transform.forward;
            float d = -1 * Vector3.Dot(normal, transform.position);
            bool throughPortal = new Plane(normal, d).GetSide(collider.transform.position);
            if (throughPortal) {
                OnPortalExit(collider);
            }
        }

        public void OnPortalExit(Collider collider) {
            PortalLight light = collider.GetComponent<PortalLight>();
            if(light) {
                return;
            }

            CharacterController characterController = collider.GetComponent<CharacterController>();
            Rigidbody rigidbody = collider.GetComponent<Rigidbody>();
            if (characterController == null && rigidbody == null) {
                Debug.LogError("Object must have a Rigidbody or CharacterController to pass through the portal");
                return;
            }

            if (ExitPortal.AttachedCollider) {
                Physics.IgnoreCollision(collider, ExitPortal.AttachedCollider, true);
            }

            ApplyWorldToPortalTransform(collider.gameObject.transform, collider.gameObject.transform);

            if (rigidbody != null) {
                float scaleDelta = this.PortalScaleAverage();
                rigidbody.velocity = PortalRotation() * rigidbody.velocity * scaleDelta;
                rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;
            }

            collider.gameObject.SendMessage("OnPortalExit", this, SendMessageOptions.DontRequireReceiver);
            if (onPortalExit != null)
                onPortalExit(this, collider.gameObject);

            //UnityEditor.EditorApplication.isPaused = true;
        }

        void OnDrawGizmos() {
            if (ExitPortal) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(this.transform.position, ExitPortal.transform.position);
            }
        }

        private Mesh MakeMesh() {
            // Front:
            //  1  2
            //  0  3

            // Back
            //  5  6
            //  4  7

            Vector3[] vertices = new Vector3[] {
                // Front
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                
                // Back
                new Vector3(-0.5f, -0.5f, 1.0f),
                new Vector3(-0.5f,  0.5f, 1.0f),
                new Vector3( 0.5f,  0.5f, 1.0f),
                new Vector3( 0.5f, -0.5f, 1.0f),
            };

            Vector2[] uvs = new Vector2[] {
                // Front
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),

                // Back
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            };

            int[] frontFaceTriangles = new int[] {
                //Front
                0, 1, 2,
                2, 3, 0
            };

            int[] backFaceTriangles = new int[] {
                // Left
                0, 1, 5,
                5, 4, 0,

                // Back
                4, 5, 6,
                6, 7, 4,

                // Right
                7, 6, 2,
                2, 3, 7,

                // Top
                6, 5, 1,
                1, 2, 6,

                // Bottom
                0, 4, 7,
                7, 3, 0
            };

            Mesh mesh = new Mesh();
            mesh.name = "Portal Mesh";
            mesh.subMeshCount = 2;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(frontFaceTriangles, 0);
           mesh.SetTriangles(backFaceTriangles, 1);

            return mesh;
        }
    }
}
