using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    [ExecuteInEditMode]
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

        public List<Collider> ignoredColliders = new List<Collider>();

        public delegate void PortalEvent(Portal portal, GameObject obj);
        //public static event PortalEvent onPortalEnter;
        public static event PortalEvent onPortalExit;

        // Maps cameras to their children
        private Dictionary<Camera, Camera> _camToPortalCam = new Dictionary<Camera, Camera>();

        // Used to track current recursion depth.
        // This is static because recursion can occur on more than one portal
        private static int s_depth;

        //TODO: figure out what the hell this is for
        // Used to prevent camera recusion in other portals
        // This is static because recursion can occur on more than one portal
        private static Portal _currentlyRenderingPortal;

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

        //private Dictionary<Camera, bool> _wasSeenByCamera = new Dictionary<Camera, bool>();
        //bool hasPopped = false;

        //void SaveMaterialProperties() {
        //    stack.Push(Camera.current.targetTexture);
        //}
        //void RestoreMaterialProperties() {
        //    if (stack.Count == 0) {
        //        //Debug.Log("Empty");
        //        return;
        //    }
        //    if (!hasPopped) {
        //        stack.Pop();
        //        hasPopped = true;
        //    }
        //    if (stack.Count == 0) {
        //        //Debug.Log("Popped and empty");
        //        return;
        //    }
        //    Texture tex = stack.Pop();

        //    Debug.Log("Restoring texture: " + tex.name + " to material: " + _portalMaterial.name + " after camera render: " + Camera.current);
        //    if (!tex) {
        //        //Debug.Log("!tex");
        //        return;
        //    }
        //    //Debug.Log("Popped: " + tex.name);
        //    _portalMaterial.mainTexture = tex;
        //    //_portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
        //}

        void Update() {
            _wasRenderedByCamera.Clear();
        }

        //void OnRenderObject() {
        //    bool wasSeen;
        //    _wasSeenByCamera.TryGetValue(Camera.current, out wasSeen);
        //    if (wasSeen) {
        //        OnPostRenderObject();
        //    }
        //}

        //// Unity doesn't have an analogous magic method to OnWillRenderObject, so we have to make our own.
        //void OnPostRenderObject() {
        //    //Debug.Log(Camera.current.name + " finished rendering: " + gameObject.name);
        //    RestoreMaterialProperties();
        //}

        bool IsCameraRenderingBothEyes (Camera camera) {
            return camera.stereoTargetEye == StereoTargetEyeMask.Both && camera.targetTexture == null;
        }

        private Dictionary<Camera, bool> _wasRenderedByCamera = new Dictionary<Camera, bool>();
        private Stack<Texture> _savedTextures = new Stack<Texture>();

        void PushCurrentTexture() {
            Texture tex = _portalMaterial.GetTexture("_RightEyeTexture");
            _savedTextures.Push(tex);
            _wasRenderedByCamera[Camera.current] = true;

            //Debug.Log("PushCurrentTexture: " + new string('*', s_depth) + gameObject.name + " " + (tex ? tex.name : "null"));
        }

        void PopCurrentTexture() {
            Texture tex = _savedTextures.Pop();
            _portalMaterial.SetTexture("_RightEyeTexture", tex);
            _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            _portalMaterial.DisableKeyword("DONT_SAMPLE");
            _wasRenderedByCamera[Camera.current] = false;

            //Debug.Log("PopCurrentTexture : " + new string('*', s_depth) + gameObject.name + " " + (tex ? tex.name : "null"));
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

            Camera currentCam = Camera.current;
            if (currentCam == null) {
                return;
            }

            if (currentCam.name == "SceneCamera" || currentCam.name == "Reflection Probes Camera" || currentCam.name == "Preview Camera")
                return;

            if (s_depth > 0 && _currentlyRenderingPortal != null && this == _currentlyRenderingPortal.ExitPortal) {
                return;
            }

            // Set this 
            _portalMaterial.SetTexture("_TransparencyMask", _transparencyMask);
            _portalMaterial.SetTexture("_DefaultTexture", _defaultTexture);

            //_wasSeenByCamera[Camera.current] = true;
            //SaveMaterialProperties();

            //Debug.Log("OnWillRenderObject:       " + gameObject.name + " " + Camera.current.name);

            //Debug.Log("OnWillRenderObject ENTER: " + new string('*', s_depth) + gameObject.name + " " + _portalMaterial.GetTexture("_RightEyeTexture"));

            PushCurrentTexture();

            // Stop recursion when we reach maximum depth
            if (s_depth >= _maxRecursiveDepth) {

                if (_fakeInfiniteRecursion) {
                    //if (!_recursionCamera)
                    //    return;
                    if (_maxRecursiveDepth >= 2) {
                        // Render the bottom portal using _recursionCamera's view/projection.
                        PortalCamera pc = currentCam.GetComponent<PortalCamera>();
                        Camera parentCam = pc.parent;
                        PortalCamera parentPC = parentCam.GetComponent<PortalCamera>();
                        //Debug.Log("Drawing final " + gameObject.name + " with " + parentPC.lastFrameRenderTexture);

                        if (pc.portal == this) {
                            GetComponent<Renderer>().sharedMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
                            GetComponent<Renderer>().sharedMaterial.SetMatrix("PORTAL_MATRIX_VP", parentPC.lastFrameProjectionMatrix * parentPC.lastFrameWorldToCameraMatrix);
                            GetComponent<Renderer>().sharedMaterial.SetTexture("_RightEyeTexture", parentPC.lastFrameRenderTexture);
                        } else {
                            GetComponent<Renderer>().sharedMaterial.EnableKeyword("DONT_SAMPLE");
                        }


                        //pc.lastFrameWorldToCameraMatrix = parentCam.worldToCameraMatrix;
                        //parentPC.lastFrameProjectionMatrix = parentCam.projectionMatrix;
                        //parentPC.lastFrameRenderTexture = parentCam.targetTexture;
                    } else {
                        GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                        GetComponent<Renderer>().sharedMaterial.EnableKeyword("DONT_SAMPLE");
                    }
                } else {
                    GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    GetComponent<Renderer>().sharedMaterial.EnableKeyword("DONT_SAMPLE");
                }
                return;
            }

            // Initialize or get anything needed for this depth level
            Camera portalCam = null;
            CreatePortalObjects(currentCam, out portalCam);
            portalCam.GetComponent<PortalCamera>().renderDepth = s_depth + 1;

            // Reset camera values to match the parent cam
            UpdateCameraModes(currentCam, portalCam);

            Portal parentPortal = _currentlyRenderingPortal;
            _currentlyRenderingPortal = this;

            //Debug.Log("Rendering " + portalCam);

            s_depth++;
            if (VRDevice.isPresent) {
                if (currentCam.stereoTargetEye == StereoTargetEyeMask.Both || currentCam.stereoTargetEye == StereoTargetEyeMask.Left) {
                    RenderTexture leftEyeTexture = portalCam.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Left);
                    _portalMaterial.SetTexture("_LeftEyeTexture", leftEyeTexture);
                }
                if (currentCam.stereoTargetEye == StereoTargetEyeMask.Both || currentCam.stereoTargetEye == StereoTargetEyeMask.Right) {
                    RenderTexture rightEyeTexture = portalCam.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Right);
                    _portalMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
                }
            } else {
                RenderTexture rightEyeTexture = portalCam.GetComponent<PortalCamera>().RenderToTexture(Camera.MonoOrStereoscopicEye.Mono);
                _portalMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
                _backFaceMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
            }
            s_depth--;

            //Debug.Log("Done rendering " + portalCam);

            //Debug.Log("OnWillRenderObject EXIT : " + new string('*', s_depth) + gameObject.name + " " + _portalMaterial.GetTexture("_RightEyeTexture"));
            _currentlyRenderingPortal = parentPortal;

            if (s_depth < _maxRecursiveDepth) {
                GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                GetComponent<Renderer>().sharedMaterial.DisableKeyword("DONT_SAMPLE");
            }
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
        
        public float GetScaleMultiplier() {
            float enterScale = Helpers.VectorInternalAverage(this.transform.lossyScale);
            float exitScale = Helpers.VectorInternalAverage(ExitPortal.transform.lossyScale);

            return exitScale / enterScale;
        }

        public Quaternion WorldToPortalQuaternion() {
            // Transforms a quaternion or vector into the second portal's space.
            // We have to flip the camera in between so that we face the outside direction of the portal
            return ExitPortal.transform.rotation * Quaternion.Euler(180, 0, 180) * Quaternion.Inverse(this.transform.rotation);
        }

        //public Matrix4x4 WorldToPortalMatrix() {
        //    Vector3 translation = this.transform.position - ExitPortal.transform.position;
        //    //Vector3 translation = Vector3.zero;
        //    //Quaternion rotation = this.transform.rotation;
        //    //Quaternion rotation = ExitPortal.transform.rotation * Quaternion.Inverse(this.transform.rotation);
        //    //Quaternion rotation = WorldToPortalQuaternion();
        //    //Quaternion rotation = Quaternion.identity;
        //    Quaternion rotation = Quaternion.Euler(EULER_ANGLES);
        //    //Debug.Log(translation);
        //    Vector3 scale = new Vector3(1f, 1f, 1f); // the last negative scale makes it point in the right direction
        //    return Matrix4x4.TRS(translation, rotation, scale);
        //}

        public Vector3 MultiplyPoint(Vector3 point) {
            Vector3 positionDelta = point - this.transform.position;
            Vector3 scaledPositionDelta = positionDelta * GetScaleMultiplier();
            Vector3 transformedDelta = WorldToPortalQuaternion() * scaledPositionDelta;

            return ExitPortal.transform.position + transformedDelta;
        }

        public void ApplyWorldToPortalTransform(Transform target, Transform reference) {
            Quaternion worldToPortal = WorldToPortalQuaternion();

            // Scale
            float scale = GetScaleMultiplier();

            // Translate
            Vector3 positionDelta = reference.position - this.transform.position;
            Vector3 scaledPositionDelta = positionDelta * scale;
            Vector3 transformedDelta = worldToPortal * scaledPositionDelta;

            target.position = ExitPortal.transform.position + transformedDelta;
            target.rotation = worldToPortal * reference.rotation;
            target.localScale = reference.localScale * scale;
        }

        public void ApplyWorldToPortalTransform(Transform target, Vector3 referencePosition, Quaternion referenceRotation, Vector3 referenceScale) {
            Quaternion worldToPortal = WorldToPortalQuaternion();

            // Scale
            float scale = GetScaleMultiplier();

            // Translate
            Vector3 positionDelta = referencePosition - this.transform.position;
            Vector3 scaledPositionDelta = positionDelta * scale;
            Vector3 transformedDelta = worldToPortal * scaledPositionDelta;

            target.position = ExitPortal.transform.position + transformedDelta;
            target.rotation = worldToPortal * referenceRotation;
            target.localScale = referenceScale * scale;
        }

        public Camera GetChildCamera(Camera camera) {
            Camera cam;
            _camToPortalCam.TryGetValue(camera, out cam);
            return cam;
        }

        void UpdateCameraModes(Camera src, Camera dest) {
            if (dest == null) {
                return;
            }
            dest.clearFlags = src.clearFlags;
            dest.backgroundColor = src.backgroundColor;
            // update other values to match current camera.
            // even if we are supplying custom camera&projection matrices,
            // some of values are used elsewhere (e.g. skybox uses far plane)
            dest.farClipPlane = src.farClipPlane;
            dest.nearClipPlane = src.nearClipPlane;
            dest.orthographic = src.orthographic;
            dest.fieldOfView = src.fieldOfView;
            dest.aspect = src.aspect;
            dest.orthographicSize = src.orthographicSize;
            dest.renderingPath = src.renderingPath;
            dest.allowHDR = src.allowHDR;
            dest.allowMSAA = src.allowMSAA;

            // TODO: Fix occlusion culling
            dest.useOcclusionCulling = src.useOcclusionCulling;
        }

        // On-demand create any objects we need for water
        void CreatePortalObjects(Camera currentCamera, out Camera portalCamera) {
            portalCamera = null;

            // Camera for reflection
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


        void OnTriggerEnter(Collider collider) {
            if (!ExitPortal) {
                return;
            }

            foreach (Collider other in ignoredColliders) {
                Physics.IgnoreCollision(collider, other, true);
            }
            foreach (Collider other in ExitPortal.ignoredColliders) {
                Physics.IgnoreCollision(collider, other, true);
            }
            //SpawnClone(collider.gameObject);
        }

        void OnTriggerStay(Collider collider) {
            if (!ExitPortal) {
                return;
            }

            Vector3 normal = transform.forward;
            float d = -1 * Vector3.Dot(normal, transform.position);
            bool throughPortal = new Plane(normal, d).GetSide(collider.transform.position);
            if (throughPortal) {
                OnPortalExit(collider);
            }
        }

        void OnTriggerExit(Collider collider) {
            if (!ExitPortal) {
                return;
            }

            // Restore collisions with the back of the portal doorway
            foreach (Collider other in ignoredColliders) {
                Physics.IgnoreCollision(collider, other, false);
            }
            foreach (Collider other in ExitPortal.ignoredColliders) {
                Physics.IgnoreCollision(collider, other, false);
            }
            //DestroyClone(collider.gameObject);
        }

        void OnPortalExit(Collider collider) {
            PortalLight light = collider.GetComponent<PortalLight>();
            if(light) {
                return;
            }

            CharacterController controller = collider.GetComponent<CharacterController>();
            Rigidbody rigidbody = collider.GetComponent<Rigidbody>();
            if (controller == null && rigidbody == null) {
                Debug.LogError("Object must have a Rigidbody or CharacterController to pass through the portal");
                return;
            }

            ApplyWorldToPortalTransform(collider.gameObject.transform, collider.gameObject.transform);

            if (rigidbody != null) {
                float scaleDelta = this.GetScaleMultiplier();
                rigidbody.velocity = WorldToPortalQuaternion() * rigidbody.velocity * scaleDelta;
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
            mesh.name = "Portal Backface";
            mesh.subMeshCount = 2;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(frontFaceTriangles, 0);
           mesh.SetTriangles(backFaceTriangles, 1);

            return mesh;
        }
    }
}
