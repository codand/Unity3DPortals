using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    //[ExecuteInEditMode]
    public class Portal : RenderedBehaviour {
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

        public Texture DefaultTexture {
            get {
                return _defaultTexture;
            }
            set {
                _defaultTexture = value;
                _portalMaterial.SetTexture("_DefaultTexture", _defaultTexture);
            }
        }

        public Texture TransparencyMask {
            get {
                return _transparencyMask;
            }
            set {
                _transparencyMask = value;
                _portalMaterial.SetTexture("_TransparencyMask", _transparencyMask);
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

        // Maps cameras to their children
        private Dictionary<Camera, PortalCamera> _camToPortalCam = new Dictionary<Camera, PortalCamera>();

        // Used to track current recursion depth.
        // This is static because recursion can occur on more than one portal
        private static int _depth;

        // Mesh spawned when walking through a portal so that you can't clip through the portal
        public static Mesh _mesh;
        
        // Per-portal instance of the backface object
        private GameObject _backFace;

        // Instanced material
        private Material _portalMaterial;

        // Instanced backface material
        private Material _backFaceMaterial;

        private Dictionary<Camera, bool> _wasRenderedByCamera = new Dictionary<Camera, bool>();
        private Stack<Texture> _savedTextures = new Stack<Texture>();

        private bool _inPortal = false;

        private Renderer _renderer;

        private HashSet<Camera> _camerasInside = new HashSet<Camera>();
        private HashSet<Teleportable> _teleportablesInside = new HashSet<Teleportable>();

        [Flags]
        private enum ShaderKeyword {
            None = 0,
            SamplePreviousFrame = 1,
            SampleDefaultTexture = 2,
        }

        private Stack<MaterialPropertyBlock> _blockStack;
        private Stack<ShaderKeyword> _keywordStack;
        private ObjectPool<MaterialPropertyBlock> _blockPool;

        void Awake() {
            _renderer = GetComponent<Renderer>();

            _blockStack = new Stack<MaterialPropertyBlock>();
            _keywordStack = new Stack<ShaderKeyword>();
            _blockPool = new ObjectPool<MaterialPropertyBlock>(1, () => new MaterialPropertyBlock());

            // TODO
            //this.gameObject.layer = PortalPhysics.PortalLayer;

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

                _portalMaterial.SetTexture("_TransparencyMask", _transparencyMask);
                _portalMaterial.SetTexture("_DefaultTexture", _defaultTexture);

                _renderer.sharedMaterials = new Material[] {
                    _portalMaterial,
                    _backFaceMaterial,
                };
            }
        }

        void OnValidate() {

        }

        void OnDisable() {
            // Clean up cameras in scene. This is important when using ExecuteInEditMode because
            // script recompilation will disable then enable this script causing creation of duplicate
            // cameras.
            foreach (KeyValuePair<Camera, PortalCamera> kvp in _camToPortalCam) {
                PortalCamera child = kvp.Value;
                if (child && child.gameObject) {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        void SaveMaterialProperties() {
            MaterialPropertyBlock block = _blockPool.Take();
            _renderer.GetPropertyBlock(block);
            _blockStack.Push(block);

            ShaderKeyword keywords = ShaderKeyword.None;
            if (_portalMaterial.IsKeywordEnabled("SAMPLE_PREVIOUS_FRAME")) {
                keywords |= ShaderKeyword.SamplePreviousFrame;
            }
            if (_portalMaterial.IsKeywordEnabled("DONT_SAMPLE")) {
                keywords |= ShaderKeyword.SampleDefaultTexture;
            }
            _keywordStack.Push(keywords);
        }

        void RestoreMaterialProperties() {
            MaterialPropertyBlock block = _blockStack.Pop();
            _renderer.SetPropertyBlock(block);
            _blockPool.Give(block);

            ShaderKeyword keywords = _keywordStack.Pop();
            if ((keywords & ShaderKeyword.SamplePreviousFrame) == ShaderKeyword.SamplePreviousFrame) {
                _portalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
            } else {
                _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            }

            if ((keywords & ShaderKeyword.SampleDefaultTexture) == ShaderKeyword.SampleDefaultTexture) {
                _portalMaterial.EnableKeyword("DONT_SAMPLE");
            } else {
                _portalMaterial.DisableKeyword("DONT_SAMPLE");
            }
        }

        protected override void PostRender() {
            RestoreMaterialProperties();
        }

        protected override void PreRender() {
            SaveMaterialProperties();

            if (!enabled || !_renderer || !_renderer.enabled) {
                return;
            }

            PortalCamera currentPortalCamera = PortalCamera.current;
            // Don't ever render our own exit portal
            if (_depth > 0 && currentPortalCamera != null && this == currentPortalCamera.portal.ExitPortal) {
                return;
            }

            // Stop recursion when we reach maximum depth
            if (_depth >= _maxRecursiveDepth) {
                if (_fakeInfiniteRecursion && _maxRecursiveDepth >= 2) {
                    // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
                    // This creates an illusion of infinite recursion, but only works with at least two real recursions
                    // because the effect is unconvincing using the Main Camera's previous frame.
                    Camera parentCam = currentPortalCamera.parent;
                    PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

                    if (currentPortalCamera.portal == this && parentPortalCam.portal == this) {
                        // This portal is currently viewing itself.
                        // Render the bottom of the stack with the parent camera's view/projection.

                        _portalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
                        _portalMaterial.SetMatrix("PORTAL_MATRIX_VP", parentPortalCam.lastFrameProjectionMatrix * parentPortalCam.lastFrameWorldToCameraMatrix);
                        _portalMaterial.SetTexture("_RightEyeTexture", parentPortalCam.lastFrameRenderTexture);
                    } else {
                        // We are viewing another portal.
                        // Render the bottom of the stack with a base texture
                        _portalMaterial.EnableKeyword("DONT_SAMPLE");
                    }
                } else {
                    _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    _portalMaterial.EnableKeyword("DONT_SAMPLE");
                }

                // Exit. We don't need to process any further depths.
                return;
            }

            if (!ExitPortal || !ExitPortal.isActiveAndEnabled) {
                _portalMaterial.EnableKeyword("DONT_SAMPLE");
            } else {
                _portalMaterial.DisableKeyword("DONT_SAMPLE");
            }

            MaterialPropertyBlock block = _blockPool.Take();
            _renderer.GetPropertyBlock(block);
            if (_depth == 0 && _camerasInside.Contains(Camera.current)) {
                block.SetFloat("_BackfaceAlpha", 1.0f);
            }


            // Initialize or get anything needed for this depth level
            PortalCamera portalCamera = GetOrCreatePortalCamera(Camera.current);
            //portalCamera.renderDepth = s_depth + 1;

            _depth++;
            if (VRDevice.isPresent) {
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Left) {
                    RenderTexture leftEyeTexture = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Left);
                    block.SetTexture("_LeftEyeTexture", leftEyeTexture);
                }
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Right) {
                    RenderTexture rightEyeTexture = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Right);
                    block.SetTexture("_RightEyeTexture", rightEyeTexture);
                }
            } else {
                RenderTexture rightEyeTexture = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Mono);
                block.SetTexture("_RightEyeTexture", rightEyeTexture);
            }
            _depth--;
            _renderer.SetPropertyBlock(block);
            _blockPool.Give(block);
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

        PortalCamera GetOrCreatePortalCamera(Camera currentCamera) {
            PortalCamera portalCamera = null;
            _camToPortalCam.TryGetValue(currentCamera, out portalCamera);
            if (!portalCamera) // catch both not-in-dictionary and in-dictionary-but-deleted-GO
            {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + gameObject.name, typeof(Camera));
                go.hideFlags = HideFlags.HideAndDontSave;

                Camera camera = go.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = transform.position;
                camera.transform.rotation = transform.rotation;
                camera.gameObject.AddComponent<FlareLayer>();
                //camera.depthTextureMode = DepthTextureMode.DepthNormals;

                // TODO: Awake doesn't get called when using ExecuteInEditMode
                portalCamera = go.AddComponent<PortalCamera>();
                portalCamera.Awake();
                portalCamera.enterScene = this.gameObject.scene;
                portalCamera.exitScene = ExitPortal.gameObject.scene;
                portalCamera.parent = currentCamera;
                portalCamera.portal = this;

                if (this.gameObject.scene != ExitPortal.gameObject.scene) {
                    //PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    //thing.scene = exitPortal.gameObject.scene;
                }

                _camToPortalCam[currentCamera] = portalCamera;
            }
            return portalCamera;
        }


        //void OnTriggerEnter(Collider collider) {
        //    if (!ExitPortal || !ExitPortal.isActiveAndEnabled) {
        //        return;
        //    }

        //    Teleportable teleportable = collider.GetComponent<Teleportable>();
        //    if (teleportable && !teleportable.IsClone) {
        //        teleportable._receivedOnTriggerEnterFrom.Add(this);
        //    }
        //    if (!teleportable || teleportable.IsClone || teleportable.IsInsidePortal(this)) {
        //        return;
        //    }
            
        //    if (teleportable.Camera) {
        //        _camerasInside.Add(teleportable.Camera);
        //    }

        //    collider.gameObject.SendMessage("OnPortalEnter", this, SendMessageOptions.DontRequireReceiver);
        //    if (onPortalEnterGlobal != null)
        //        onPortalEnterGlobal(this, collider.gameObject);
        //    if (onPortalEnter != null)
        //        onPortalEnter(this, collider.gameObject);
        //}

        //void OnTriggerExit(Collider collider) {
        //    if (!ExitPortal || !ExitPortal.isActiveAndEnabled) {
        //        return;
        //    }

        //    Teleportable teleportable = collider.GetComponent<Teleportable>();
        //    if (!teleportable || teleportable.IsClone || !teleportable.IsInsidePortal(this)) {
        //        return;
        //    }

        //    if (teleportable.Camera) {
        //        _camerasInside.Remove(teleportable.Camera);
        //    }


        //    collider.gameObject.SendMessage("OnPortalExit", this, SendMessageOptions.DontRequireReceiver);
        //    if (onPortalExitGlobal != null)
        //        onPortalExitGlobal(this, collider.gameObject);
        //    if (onPortalExit != null)
        //        onPortalExit(this, collider.gameObject);
        //}

        //void OnTriggerStay(Collider collider) {
            
        //    if (!ExitPortal) {
        //        return;
        //    }

        //    Teleportable teleportable = collider.GetComponent<Teleportable>();
        //    if (!teleportable || teleportable.IsClone || !teleportable.IsInsidePortal(this)) {
        //        return;
        //    }

        //    Vector3 position = teleportable.Camera ? teleportable.Camera.transform.position : collider.transform.position;
        //    bool throughPortal = Plane.GetSide(position);
        //    if (throughPortal) {
        //        TeleportObject(teleportable, collider);
        //    }
        //}

        //IEnumerator HighSpeedTeleportCheck(Teleportable teleportable, Collider collider) {
        //    // If we're going too fast, sometimes we can skip past the exit collider even with continuous collision detection.
        //    // In this case, we should wait to see if we get an OnTriggerEnter event from
        //    // the exit portal in exactly two frames. If we do not, then call OnTriggerExit manually.
        //    yield return new WaitForFixedUpdate();
        //    yield return new WaitForFixedUpdate();
        //    if (!teleportable._receivedOnTriggerEnterFrom.Contains(ExitPortal)) {
        //        //Debug.Log("TriggerEnter was NOT seen");
        //        ExitPortal.OnTriggerExit(collider);
        //    } else {
        //        //Debug.Log("TriggerEnter WAS seen");
        //    }
        //}

        //public void TeleportObject(Teleportable teleportable, Collider collider) {
        //    PortalLight light = collider.GetComponent<PortalLight>();
        //    if(light) {
        //        return;
        //    }

        //    if (teleportable.Camera) {
        //        _camerasInside.Remove(teleportable.Camera);
        //        ExitPortal._camerasInside.Add(teleportable.Camera);
        //    }
        //    StartCoroutine(HighSpeedTeleportCheck(teleportable, collider));

        //    CharacterController characterController = collider.GetComponent<CharacterController>();
        //    Rigidbody rigidbody = collider.GetComponent<Rigidbody>();
        //    //if (characterController == null && rigidbody == null) {
        //    //    Debug.LogError(collider.gameObject.name + " must have a Rigidbody or CharacterController to pass through the portal");
        //    //    return;
        //    //}



        //    //if (ExitPortal.AttachedCollider) {
        //    //    Physics.IgnoreCollision(collider, ExitPortal.AttachedCollider, true);
        //    //}

        //    ApplyWorldToPortalTransform(collider.gameObject.transform, collider.gameObject.transform);

        //    if (rigidbody != null) {
        //        // TODO: Evaluate whether or not using Rigidbody.position is important
        //        // Currently it messes up the _cameraInside stuff because it happens at the next physics step
        //        //Vector3 newPosition = PortalMatrix().MultiplyPoint3x4(rigidbody.position);
        //        //Quaternion newRotation = PortalRotation() * rigidbody.rotation;
        //        //rigidbody.position = newPosition;
        //        //rigidbody.transform.rotation = newRotation;

        //        float scaleDelta = this.PortalScaleAverage();
        //        rigidbody.velocity = PortalRotation() * rigidbody.velocity * scaleDelta;
        //        rigidbody.mass *= scaleDelta * scaleDelta * scaleDelta;
        //    }

        //    collider.gameObject.SendMessage("OnPortalTeleport", this, SendMessageOptions.DontRequireReceiver);
        //    if (onPortalTeleportGlobal != null)
        //        onPortalTeleportGlobal(this, collider.gameObject);
        //    if (onPortalTeleport != null)
        //        onPortalTeleport(this, collider.gameObject);

        //    //UnityEditor.EditorApplication.isPaused = true;
        //}

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
