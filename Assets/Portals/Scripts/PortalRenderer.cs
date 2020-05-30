// -----------------------------------------------------------------------------------------------------------
// <summary>
// Renders a portal using multiple cameras.
// </summary>
// -----------------------------------------------------------------------------------------------------------
namespace Portals {
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.VR;
    using UnityEngine.Rendering;

    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PortalRenderer : RenderedBehaviour {
        #region Members
        // Mesh spawned when walking through a portal so that you can't clip through the portal
        private static Mesh _mesh;

        // Counts the number of active PortalRenderers in the scene.
        private static int _activePortalRendererCount = 0;

        private Portal _portal;

        // Maps cameras to their children
        private Dictionary<Camera, PortalCamera> _portalByPortalCam = new Dictionary<Camera, PortalCamera>();

        // Used to track current recursion depth
        private static int _currentRenderDepth;

        // Instanced materials
        private Material _portalMaterial;
        private Material _backfaceMaterial;
        private static Material _stencilMaskMaterial;
        private static Material _depthMaskMaterial;

        // Members used to save and restore material properties between rendering in the same frame
        private Stack<MaterialPropertyBlock> _propertyBlockStack;
        private Stack<ShaderKeyword> _shaderKeywordStack;
        private ObjectPool<MaterialPropertyBlock> _propertyBlockObjectPool;

        // Cached components
        private Renderer _renderer;
        private MeshFilter _meshFilter;
        private Transform _transform;
        #endregion

        #region Properties
        public static Mesh Mesh {
            get {
                if (!_mesh) {
                    _mesh = MakePortalMesh();
                }
                return _mesh;
            }
        }
        #endregion

        #region Enums
        [Flags]
        private enum ShaderKeyword {
            None = 0,
            SamplePreviousFrame = 1,
            SampleDefaultTexture = 2,
        }
        #endregion

        #region Rendering

        private struct CameraData {

        }

        private Vector4 ClampedWorldToViewportPoint(Camera cam, Vector3 worldPoint) {
            //Vector3 viewportPoint = cam.WorldToViewportPoint(worldPoint);
            Matrix4x4 p = cam.projectionMatrix;
            Matrix4x4 v = cam.worldToCameraMatrix;
            Matrix4x4 vp = p * v;
            Vector4 point = new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1);
            Vector4 hPoint = vp * point;
            Vector3 ndcPoint;
            if (hPoint.w > 0) {
                // In front of the camera
                ndcPoint = ((Vector3)hPoint) / hPoint.w;
            } else {
                // Behind the camera
                ndcPoint = new Vector3(Mathf.Sign(hPoint.x), Mathf.Sign(hPoint.y), 0);
            }

            Vector3 viewPoint = new Vector3(ndcPoint.x / 2 + 0.5f, ndcPoint.y / 2 + 0.5f, hPoint.w);

            //Debug.Log(hPoint.ToString("F2") + " " + ndcPoint.ToString("F2") + " " + viewPoint.ToString("F2"));
            return viewPoint;
        }

        private Vector3 Min(params Vector3[] vecs) {
            Vector3 min = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
            foreach (Vector3 vec in vecs) {
                min = Vector3.Min(min, vec);
            }
            return min;
        }

        private Vector3 Max(params Vector3[] vecs) {
            Vector3 max = new Vector3(Mathf.NegativeInfinity, Mathf.NegativeInfinity, Mathf.NegativeInfinity);
            foreach (Vector3 vec in vecs) {
                max = Vector3.Max(max, vec);
            }
            return max;
        }

        private Rect CalculatePortalViewportRect(Camera cam) {
            // TODO: using the exit portal corners might cause issues with the backface

            // TODO: do this shit better. cache worldspacecorners maybe
            Vector4 tl, tr, br, bl;
            var corners = _portal.WorldSpaceCorners();
            tl = ClampedWorldToViewportPoint(Camera.current, corners[0]);
            tr = ClampedWorldToViewportPoint(Camera.current, corners[1]);
            br = ClampedWorldToViewportPoint(Camera.current, corners[2]);
            bl = ClampedWorldToViewportPoint(Camera.current, corners[3]);
            //Debug.Log($"TL: {tl}, TR: {tr}, BR: {br}, BL: {bl}");
            
            Vector3 min = Min(tl, tr, br, bl);
            Vector3 max = Max(tl, tr, br, bl);

            if (tl.z <= 0 || tr.z <= 0 || br.z <= 0 || bl.z <= 0) {
                Vector3 ftl = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(-0.5f, 0.5f, 1.0f)));
                Vector3 ftr = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(0.5f, 0.5f, 1.0f)));
                Vector3 fbr = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(0.5f, -0.5f, 1.0f)));
                Vector3 fbl = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(-0.5f, -0.5f, 1.0f)));

                min = Min(min, ftl, ftr, fbr, fbl);
                max = Max(max, ftl, ftr, fbr, fbl);
            }

            //Debug.Log("Min: " + min + " Max: " + max);

            min = Vector3.Max(Vector3.zero, min);
            max = Vector3.Min(Vector3.one, max);


            Rect viewportRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            return viewportRect;
        }

        private bool IsCameraUsingDeferredShading(Camera cam) {
            return Camera.current.actualRenderingPath == RenderingPath.DeferredLighting || Camera.current.actualRenderingPath == RenderingPath.DeferredShading;
        }

        private static Portal _currentlyRenderingPortal;

        //private struct CameraContext {
        //    public Matrix4x4 ProjectionMatrix { get; set; }
        //    public Matrix4x4 WorldToCameraMatrix { get; set; }
        //}

        //private struct StereoCameraContext {
        //    public CameraContext MonoEye { get => LeftEye; set => LeftEye = value; }
        //    public CameraContext LeftEye { get; set; }
        //    public CameraContext RightEye { get; set; }
        //}

        //private struct PortalRenderContext {
        //    public StereoCameraContext CurrentFrame { get; set; }
        //    public StereoCameraContext PreviousFrame { get; set; }

        //    public void FinalizeFrame() {
        //        PreviousFrame = CurrentFrame;
        //    }
        //}

        //private PortalRenderContext _renderContext = new PortalRenderContext();
        //private PortalRenderContext GetRenderContext() {
        //    return _renderContext;
        //}

        //private void ReleaseRenderContext(PortalRenderContext renderContext) {
        //    renderContext.FinalizeFrame();
        //}

        //private Dictionary<Camera, RenderTexture> _renderTexturesByCamera = new Dictionary<Camera, RenderTexture>();

        private bool IsToplevelCamera() {
            return _currentRenderDepth == 0;
        }

        public void OnDestroy() {
            foreach (var kvp in _cameraMap) {
                if (kvp.Value) {
                    Util.SafeDestroy(kvp.Value.gameObject);
                }
            }
        }

        private void Initialize() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (_propertyBlockObjectPool == null) {
                Awake();
            }
#endif
        }

        private bool ShouldRenderPortal(Camera camera) {
            // Don't render if renderer disabled. Not sure if this is possible anyway, but better to be safe.
            bool isRendererEnabled = enabled && _renderer && _renderer.enabled;

            // Don't render non-supported camera types (preview cameras can cause issues)
            bool isCameraSupported = _portal.SupportedCameraTypes.HasFlag(camera.cameraType);

            // Only render if an exit portal is set
            bool isExitPortalSet = _portal.ExitPortal != null;

            // Don't ever render an exit portal
            // TODO: Disable portal until end of frame
            bool isRenderingExitPortal = _currentRenderDepth > 0 && _currentlyRenderingPortal == _portal.ExitPortal;

            // Don't render too deep
            bool isAtMaxDepth = _currentRenderDepth >= _portal.MaxRecursion;

            return isRendererEnabled && isCameraSupported && isExitPortalSet && !isRenderingExitPortal && !isAtMaxDepth;
        }

        private bool ShouldRenderPreviousFrame(Camera camera) {
            // Stop recursion when we reach maximum depth
            return _portal.FakeInfiniteRecursion && _portal.MaxRecursion >= 2 && _currentRenderDepth >= _portal.MaxRecursion;
        }

        private void InitializeIfNeeded() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (_propertyBlockObjectPool == null) {
                Awake();
            }
#endif
        }

        protected override void PreRender() {
            InitializeIfNeeded();
            SaveMaterialProperties();

            Camera currentCam = Camera.current;
            if (ShouldRenderPortal(currentCam)) {
                RenderPortal(currentCam);
            } else if (ShouldRenderPreviousFrame(currentCam)) { 
                RenderPreviousFrame(currentCam);
            } else {
                RenderDefaultTexture();
            }
        }

        protected override void PostRender() {
            _renderer.enabled = true;
            RestoreMaterialProperties();
        }

        private void RenderPortal(Camera currentCam) {
            MaterialPropertyBlock block = _propertyBlockObjectPool.Take();
            _renderer.GetPropertyBlock(block);

            // Handle the player clipping through the portal's frontface
            bool renderBackface = ShouldRenderBackface(Camera.current);
            block.SetFloat("_BackfaceAlpha", renderBackface ? 1.0f : 0.0f);

            // Get camera for next depth level
            PortalCamera portalCamera = GetOrCreatePortalCamera(Camera.current);

            // Calculate where in screen space the portal lies.
            // We use this to only render as much of the screen as necessary, avoiding overdraw.
            Rect viewportRect = CalculatePortalViewportRect(Camera.current);

            // Viewport must be at least one pixel wide
            float pixelWidth = viewportRect.width * currentCam.pixelWidth;
            float pixelHeight = viewportRect.height * currentCam.pixelHeight;
            if (pixelWidth < 1 || pixelHeight < 1) {
                return;
            }

            // Save which portal is rendering 
            var parentPortal = _currentlyRenderingPortal;
            _currentlyRenderingPortal = _portal;
            _currentRenderDepth++;
            if (Camera.current.stereoEnabled) {
                // Stereo rendering. Render both eyes.
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Left) {
                    RenderTexture tex = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Left, viewportRect, renderBackface);
                    block.SetTexture("_LeftEyeTexture", tex);
                }
                if (Camera.current.stereoTargetEye == StereoTargetEyeMask.Both || Camera.current.stereoTargetEye == StereoTargetEyeMask.Right) {
                    RenderTexture tex = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Right, viewportRect, renderBackface);
                    block.SetTexture("_RightEyeTexture", tex);
                }
            } else {
                // Mono rendering. Render only one eye, but set which texture to use based on the camera's target eye.
                RenderTexture tex = portalCamera.RenderToTexture(Camera.MonoOrStereoscopicEye.Mono, viewportRect, renderBackface);
                block.SetTexture("_LeftEyeTexture", tex);
            }
            _currentRenderDepth--;
            _currentlyRenderingPortal = parentPortal;

            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);
        }


        private void RenderPreviousFrame(Camera currentCam) {
            PortalCamera portalCam = PortalCamera.current;

            // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
            // This creates an illusion of infinite recursion, but only works with at least two real recursions
            // because the effect is unconvincing using the Main Camera's previous frame.
            Camera parentCam = portalCam.parent;
            PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

            // Check if the currently rendering portal is viewing itself.
            // If it is, render the bottom of the stack with the parent camera's view/projection.
            if (portalCam.portal == _portal && parentPortalCam.portal == _portal) {
                PortalCamera.FrameData frameData = parentPortalCam.PreviousFrameData;
                Matrix4x4 projectionMatrix;
                Matrix4x4 worldToCameraMatrix;
                Texture texture;
                string sampler;
                switch (parentCam.stereoTargetEye) {
                    case StereoTargetEyeMask.Right:
                        projectionMatrix = frameData.rightEyeProjectionMatrix;
                        worldToCameraMatrix = frameData.rightEyeWorldToCameraMatrix;
                        texture = frameData.rightEyeTexture;
                        sampler = "_RightEyeTexture";
                        break;
                    case StereoTargetEyeMask.Left:
                    case StereoTargetEyeMask.Both:
                    case StereoTargetEyeMask.None:
                    default:
                        projectionMatrix = frameData.leftEyeProjectionMatrix;
                        worldToCameraMatrix = frameData.leftEyeWorldToCameraMatrix;
                        texture = frameData.leftEyeTexture;
                        sampler = "_LeftEyeTexture";
                        break;
                }

                _portalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
                _portalMaterial.SetMatrix("PORTAL_MATRIX_VP", projectionMatrix * worldToCameraMatrix);
                _portalMaterial.SetTexture(sampler, texture);
            } else {
                // We are viewing another portal.
                // Render the bottom of the stack with a base texture
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
            }
        }

        private void RenderDefaultTexture() {
            _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
        }
        #endregion

        #region Initialization
        private void Awake() {
            _portal = GetComponentInParent<Portal>();
            _renderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();

            _transform = this.transform;
            _propertyBlockStack = new Stack<MaterialPropertyBlock>();
            _shaderKeywordStack = new Stack<ShaderKeyword>();
            _propertyBlockObjectPool = new ObjectPool<MaterialPropertyBlock>(1, () => new MaterialPropertyBlock());

            //// TODO
            //// this.gameObject.layer = PortalPhysics.PortalLayer;

            _meshFilter.sharedMesh = PortalRenderer.Mesh;
            if (!_portalMaterial || !_backfaceMaterial) {
                Material portalMaterial = new Material(Shader.Find("Portal/Portal"));
                Material backFaceMaterial = new Material(Shader.Find("Portal/PortalBackface"));

                _portalMaterial = portalMaterial;
                _backfaceMaterial = backFaceMaterial;

                _portalMaterial.SetTexture("_TransparencyMask", _portal.TransparencyMask);
                _portalMaterial.SetTexture("_DefaultTexture", _portal.DefaultTexture);

                _renderer.sharedMaterials = new Material[] {
                    _portalMaterial,
                    _backfaceMaterial,
                };
            }

            if (!_stencilMaskMaterial) {
                _stencilMaskMaterial = new Material(Shader.Find("Portals/StencilMask"));
            }
            if (!_depthMaskMaterial) {
                _depthMaskMaterial = new Material(Shader.Find("Portals/DepthMask"));
            }
        }

        private void OnEnable() {
            _portal.OnDefaultTextureChanged += OnDefaultTextureChanged;
            _portal.OnTransparencyMaskChanged += OnTransparencyMaskChanged;

            if (_activePortalRendererCount == 0) {
                Camera.onPreRender += SetCurrentEyeGlobal;
                Camera.onPostRender += RestoreCurrentEyeGlobal;
            }
            _activePortalRendererCount += 1;
        }

        private void OnDisable() {
            _portal.OnDefaultTextureChanged -= OnDefaultTextureChanged;
            _portal.OnTransparencyMaskChanged -= OnTransparencyMaskChanged;

            // Clean up cameras in scene. This is important when using ExecuteInEditMode because
            // script recompilation will disable then enable this script causing creation of duplicate
            // cameras.
            foreach (KeyValuePair<Camera, PortalCamera> kvp in _portalByPortalCam) {
                PortalCamera child = kvp.Value;
                if (child && child.gameObject) {
                    Util.SafeDestroy(child.gameObject);
                }
            }

            _activePortalRendererCount -= 1;
            if (_activePortalRendererCount == 0) {
                Camera.onPreRender -= SetCurrentEyeGlobal;
                Camera.onPostRender -= RestoreCurrentEyeGlobal;
            }
        }
        #endregion

        ////private void Update() {
        ////    m_Transform.localScale = Vector3.one;
        ////}
        
        #region Callbacks
        private void OnDefaultTextureChanged(Portal portal, Texture oldTexture, Texture newTexture) {
            _portalMaterial.SetTexture("_DefaultTexture", newTexture);
        }

        private void OnTransparencyMaskChanged(Portal portal, Texture oldTexture, Texture newTexture) {
            _portalMaterial.SetTexture("_TransparencyMask", newTexture);
        }


        private static Stack<float> eyeStack = new Stack<float>();
        private static void SetCurrentEyeGlobal(Camera cam) {
            eyeStack.Push(Shader.GetGlobalFloat("_PortalMultiPassCurrentEye"));
            // Globally set the current eye for Multi-Pass stereo rendering.
            // We also run this code in Single-Pass rendering because Unity doesn't have a runtime
            // check for single/multi-pass stereo, but the value gets ignored.
            Shader.SetGlobalFloat("_PortalMultiPassCurrentEye", (int)cam.stereoActiveEye);
        }

        private static void RestoreCurrentEyeGlobal(Camera cam) {
            float eye = eyeStack.Pop();
            Shader.SetGlobalFloat("_PortalMultiPassCurrentEye", eye);
        }
        #endregion

        #region Private Methods
        private void SaveMaterialProperties() {
            MaterialPropertyBlock block = _propertyBlockObjectPool.Take();
            _renderer.GetPropertyBlock(block);
            _propertyBlockStack.Push(block);

            ShaderKeyword keywords = ShaderKeyword.None;
            if (_portalMaterial.IsKeywordEnabled("SAMPLE_PREVIOUS_FRAME")) {
                keywords |= ShaderKeyword.SamplePreviousFrame;
            }
            if (_portalMaterial.IsKeywordEnabled("SAMPLE_DEFAULT_TEXTURE")) {
                keywords |= ShaderKeyword.SampleDefaultTexture;
            }

            _shaderKeywordStack.Push(keywords);
        }

        private void RestoreMaterialProperties() {
            MaterialPropertyBlock block = _propertyBlockStack.Pop();
            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);

            ShaderKeyword keywords = _shaderKeywordStack.Pop();
            if ((keywords & ShaderKeyword.SamplePreviousFrame) == ShaderKeyword.SamplePreviousFrame) {
                _portalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
            } else {
                _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            }

            if ((keywords & ShaderKeyword.SampleDefaultTexture) == ShaderKeyword.SampleDefaultTexture) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
            } else {
                _portalMaterial.DisableKeyword("SAMPLE_DEFAULT_TEXTURE");
            }
        }

        private bool ShouldRenderBackface(Camera camera) {
            // Decrease the size of the box in which we will render the backface when rendering stereo.
            // This will prevent the backface from appearing in one eye when near the edge of the portal.
            float scaleMultiplier = camera.stereoEnabled ? 0.9f : 1.0f;
            if (_currentRenderDepth == 0 && LocalXYPlaneContainsPoint(Camera.current.transform.position, scaleMultiplier)) {
                // Camera is within the border of the camera
                if (!_portal.Plane.GetSide(Camera.current.transform.position)) {
                    return true;
                }
            }
            return false;
        }
        
        private bool LocalXYPlaneContainsPoint(Vector3 point, float scaleMultiplier) {
            float extents = 0.5f * scaleMultiplier;
            Vector3 localPoint = _transform.InverseTransformPoint(point);
            if (localPoint.x < -extents) return false;
            if (localPoint.x > extents) return false;
            if (localPoint.y > extents) return false;
            if (localPoint.y < -extents) return false;
            return true;
        }

        static Dictionary<Camera, Camera> _cameraMap = new Dictionary<Camera, Camera>();
        private Camera CreateTemporaryCamera(Camera currentCamera) {
            _cameraMap.TryGetValue(currentCamera, out Camera camera);
            if (!camera) {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + _portal.name, typeof(Camera));
                //go.hideFlags = HideFlags.HideAndDontSave;
                go.hideFlags = HideFlags.DontSave;

                camera = go.GetComponent<Camera>();
                //camera.gameObject.AddComponent<FlareLayer>();

                //camera.CopyFrom(currentCamera);
                _cameraMap[currentCamera] = camera;
            }

            camera.enabled = false;
            return camera;
        }

        private void ReleaseTemporaryCamera(Camera cam) {
            //Util.SafeDestroy(cam.gameObject);
        }

        private PortalCamera GetOrCreatePortalCamera(Camera currentCamera) {
            PortalCamera portalCamera = null;
            _portalByPortalCam.TryGetValue(currentCamera, out portalCamera);
            if (!portalCamera) {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + _portal.name, typeof(Camera));
                //go.hideFlags = HideFlags.HideAndDontSave;
                go.hideFlags = HideFlags.DontSave;

                Camera camera = go.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = transform.position;
                camera.transform.rotation = transform.rotation;
                //camera.gameObject.AddComponent<FlareLayer>();

                // TODO: Awake doesn't get called when using ExecuteInEditMode
                portalCamera = go.AddComponent<PortalCamera>();
                portalCamera.Awake();
                portalCamera.parent = currentCamera;
                portalCamera.portal = _portal;

                if (_portal.ExitPortal && this.gameObject.scene != _portal.ExitPortal.gameObject.scene) {
                    ////PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    ////thing.scene = exitPortal.gameObject.scene;
                }

                _portalByPortalCam[currentCamera] = portalCamera;
            }
            return portalCamera;
        }

        private static Mesh MakePortalMesh() {
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
                new Vector3(0.5f,  0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                
                // Back
                new Vector3(-0.5f, -0.5f, 1.0f),
                new Vector3(-0.5f,  0.5f, 1.0f),
                new Vector3(0.5f,  0.5f, 1.0f),
                new Vector3(0.5f, -0.5f, 1.0f),
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
                // Front
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
        #endregion
    }
}
