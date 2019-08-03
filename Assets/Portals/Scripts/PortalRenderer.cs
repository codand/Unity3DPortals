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
        private static int _staticRenderDepth;

        // Instanced materials
        private Material _portalMaterial;
        private Material _backfaceMaterial;

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


        private Vector3 ClampedWorldToViewportPoint(Camera cam, Vector3 worldPoint) {
            Vector3 viewportPoint = cam.WorldToViewportPoint(worldPoint);
            //if (viewportPoint.z < 0) {
            //    if (viewportPoint.x < 0.5f) {
            //        viewportPoint.x = 1;
            //    } else {
            //        viewportPoint.x = 0;
            //    }
            //    if (viewportPoint.y < 0.5f) {
            //        viewportPoint.y = 1;
            //    } else {
            //        viewportPoint.y = 0;
            //    }

            //    //if(Vector3.Dot(cam.transform.forward, transform.forward) > 0) {
            //    //    viewportPoint.x = viewportPoint.x - 1;
            //    //    viewportPoint.y = viewportPoint.y - 1;
            //    //}
            //    Debug.Log(Vector3.Dot(cam.transform.forward, transform.forward));
            //}
            return viewportPoint;
        }

        private Vector3 Min(params Vector3[] vecs) {
            Vector3 min = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
            foreach (Vector3 vec in vecs) {
                if (vec.z > 0) {
                    min = Vector3.Min(min, vec);
                }
            }
            return min;
        }

        private Vector3 Max(params Vector3[] vecs) {
            Vector3 max = new Vector3(Mathf.NegativeInfinity, Mathf.NegativeInfinity, Mathf.NegativeInfinity);
            foreach (Vector3 vec in vecs) {
                if (vec.z > 0) {
                    max = Vector3.Max(max, vec);
                }
            }
            return max;
        }

        private Rect CalculatePortalViewportRect(Camera cam) {
            // TODO: using the exit portal corners might cause issues with the backface

            // TODO: do this shit better. cache worldspacecorners maybe

            Vector3 tl, tr, br, bl;

            if (useOldRenderer) {
                var corners = _portal.WorldSpaceCorners();
                tl = ClampedWorldToViewportPoint(Camera.current, corners[0]);
                tr = ClampedWorldToViewportPoint(Camera.current, corners[1]);
                br = ClampedWorldToViewportPoint(Camera.current, corners[2]);
                bl = ClampedWorldToViewportPoint(Camera.current, corners[3]);
            } else {
                var corners = _portal.ExitPortal.WorldSpaceCorners();
                tl = cam.WorldToViewportPoint(corners[0]);
                tr = cam.WorldToViewportPoint(corners[1]);
                br = cam.WorldToViewportPoint(corners[2]);
                bl = cam.WorldToViewportPoint(corners[3]);
            }


            //Vector3 ftl = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(-0.5f, 0.5f, 1.0f)));
            //Vector3 ftr = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(0.5f, 0.5f, 1.0f)));
            //Vector3 fbr = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(0.5f, -0.5f, 1.0f)));
            //Vector3 fbl = ClampedWorldToViewportPoint(Camera.current, _portal.transform.TransformPoint(new Vector3(-0.5f, -0.5f, 1.0f)));

            //var min = Min(tl, tr, br, bl, ftl, ftr, fbr, fbl);
            //var max = Max(tl, tr, br, bl, ftl, ftr, fbr, fbl);
            var min = Min(tl, tr, br, bl);
            var max = Max(tl, tr, br, bl);

            min -= Vector3.one * buffer;
            max += Vector3.one * buffer;

            min = Vector3.Max(Vector3.zero, min);
            max = Vector3.Min(Vector3.one, max);

            Rect viewportRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            return viewportRect;
        }
        public float buffer = 0f;
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
            return _staticRenderDepth == 0;
        }


        public bool useOldRenderer = true;
        public void Update() {
            if (Input.GetKeyDown(KeyCode.LeftControl)) {
                useOldRenderer = !useOldRenderer;
                Debug.Log($"Using {(useOldRenderer ? "old" : "new")} renderer");
            }
            Shader.SetGlobalFloat("useOldRenderer", useOldRenderer ? 1 : 0);
        }


        protected override void PreRender() {
            if (useOldRenderer) {
                PreRenderOld();
            } else {
                PreRender3();
            }
        }
        protected override void PostRender() {
            if (useOldRenderer) {
                PostRenderOld();
            } else {
                PostRender3();
            }
        }

        protected void PreRender3() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (_propertyBlockObjectPool == null) {
                Awake();
            }
#endif

            if (!enabled || !_renderer || !_renderer.enabled) {
                return;
            }

            if (Camera.current.cameraType != CameraType.Game) {
                return;
            }


            if (_staticRenderDepth == 0) {
                var gpuProjectionMatrix = GL.GetGPUProjectionMatrix(Camera.current.projectionMatrix, true);
                Shader.SetGlobalMatrix("_PortalProjectionMatrix", gpuProjectionMatrix);
            }

            SaveMaterialProperties();
            MaterialPropertyBlock block = _propertyBlockObjectPool.Take();


            if (!_portal.ExitPortal || !_portal.ExitPortal.isActiveAndEnabled) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                return;
            }

            // Don't ever render our own exit portal
            if (_staticRenderDepth > 0 && _currentlyRenderingPortal == _portal.ExitPortal) {
                // Disable renderer until the end of the frame. This prevents an extra draw call
                _renderer.enabled = false;
                return;
            }

            if (_staticRenderDepth == 0) {
                if (_renderTexture != null) {
                    RenderTexture.ReleaseTemporary(_renderTexture);
                }
                int w = Camera.current.pixelWidth * _portal.Downscaling;
                int h = Camera.current.pixelHeight * _portal.Downscaling;
                int depth = (int)_portal.DepthBufferQuality;
                var format = RenderTextureFormat.Default;
                var writeMode = RenderTextureReadWrite.Default;
                int msaaSamples = 1;
                var memoryless = RenderTextureMemoryless.None;
                var vrUsage = VRTextureUsage.None;
                bool useDynamicScale = false;

                // TODO: figure out correct settings for VRUsage, memoryless, and dynamic scale
                _renderTexture = RenderTexture.GetTemporary(w, h, depth, format, writeMode, msaaSamples, memoryless, vrUsage, useDynamicScale);
                _renderTexture.filterMode = FilterMode.Point;
            }


            // The stencil buffer gets used by Unity in deferred rendering and must clear itself, otherwise
            // it will be full of junk. https://docs.unity3d.com/Manual/SL-Stencil.html
            if (_staticRenderDepth == 0 && IsCameraUsingDeferredShading(Camera.current)) {
                Camera.current.clearStencilAfterLightingPass = true;
            }

            // Stop recursion when we reach maximum depth
            if (_staticRenderDepth >= _portal.MaxRecursion) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                return;
            }

            Camera parentCam = Camera.current;
            Camera childCam = CreateTemporaryCamera(parentCam);

            //RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height, 32, RenderTextureFormat.Default);

            RenderToTexture(parentCam, childCam, Camera.MonoOrStereoscopicEye.Mono, _renderTexture);
            //Graphics.Blit(temp, _renderTexture);

            //RenderTexture.ReleaseTemporary(temp);
            ReleaseTemporaryCamera(childCam);

            block.SetTexture("_LeftEyeTexture", _renderTexture);
            _portalMaterial.DisableKeyword("SAMPLE_DEFAULT_TEXTURE");


            block.SetFloat("_BackfaceAlpha", 0f);
            block.SetTexture("_LeftEyeTexture", _renderTexture);
            _backfaceMaterial.DisableKeyword("SAMPLE_DEFAULT_TEXTURE");

            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);
        }

        protected void PostRender3() {
            RestoreMaterialProperties();
            //ReleaseRenderTexture();
            _renderer.enabled = true;
        }


        private static RenderTexture _renderTexture;
        public FilterMode filterMode = FilterMode.Point;
        protected void PreRenderNew() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (_propertyBlockObjectPool == null) {
                Awake();
            }
#endif

            if (!enabled || !_renderer || !_renderer.enabled) {
                return;
            }

            if (Camera.current.cameraType != CameraType.Game) {
                return;
            }


            if (_staticRenderDepth == 0) {
                var gpuProjectionMatrix = GL.GetGPUProjectionMatrix(Camera.current.projectionMatrix, true);
                Shader.SetGlobalMatrix("_PortalProjectionMatrix", gpuProjectionMatrix);
            }

            SaveMaterialProperties();
            MaterialPropertyBlock block = _propertyBlockObjectPool.Take();


            if (!_portal.ExitPortal || !_portal.ExitPortal.isActiveAndEnabled) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                return;
            }

            // Don't ever render our own exit portal
            if (_staticRenderDepth > 0 && _currentlyRenderingPortal == _portal.ExitPortal) {
                // Disable renderer until the end of the frame. This prevents an extra draw call
                _renderer.enabled = false;
                return;
            }

            if (_staticRenderDepth == 0) {
                if (_renderTexture != null) {
                    RenderTexture.ReleaseTemporary(_renderTexture);
                }
                int w = Camera.current.pixelWidth * _portal.Downscaling;
                int h = Camera.current.pixelHeight * _portal.Downscaling;
                int depth = (int)_portal.DepthBufferQuality;
                var format = RenderTextureFormat.Default;
                var writeMode = RenderTextureReadWrite.Default;
                int msaaSamples = 1;
                var memoryless = RenderTextureMemoryless.None;
                var vrUsage = VRTextureUsage.None;
                bool useDynamicScale = false;

                // TODO: figure out correct settings for VRUsage, memoryless, and dynamic scale
                _renderTexture = RenderTexture.GetTemporary(w, h, depth, format, writeMode, msaaSamples, memoryless, vrUsage, useDynamicScale);
                _renderTexture.filterMode = FilterMode.Point;
            }


            // The stencil buffer gets used by Unity in deferred rendering and must clear itself, otherwise
            // it will be full of junk. https://docs.unity3d.com/Manual/SL-Stencil.html
            if (_staticRenderDepth == 0 && IsCameraUsingDeferredShading(Camera.current)) {
                Camera.current.clearStencilAfterLightingPass = true;
            }

            // Stop recursion when we reach maximum depth
            if (_staticRenderDepth >= _portal.MaxRecursion) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                return;
            }

            Camera parentCam = Camera.current;
            Camera childCam = CreateTemporaryCamera(parentCam);

            //RenderTexture temp = RenderTexture.GetTemporary(Screen.width, Screen.height, 32, RenderTextureFormat.Default);

            RenderToTexture(parentCam, childCam, Camera.MonoOrStereoscopicEye.Mono, _renderTexture);
            //Graphics.Blit(temp, _renderTexture);

            //RenderTexture.ReleaseTemporary(temp);
            ReleaseTemporaryCamera(childCam);

            block.SetTexture("_LeftEyeTexture", _renderTexture);
            _portalMaterial.DisableKeyword("SAMPLE_DEFAULT_TEXTURE");


            block.SetFloat("_BackfaceAlpha", 0f);
            block.SetTexture("_LeftEyeTexture", _renderTexture);
            _backfaceMaterial.DisableKeyword("SAMPLE_DEFAULT_TEXTURE");

            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);
        }

        protected void PostRenderNew() {
            RestoreMaterialProperties();
            //ReleaseRenderTexture();
            _renderer.enabled = true;
        }

        public bool sw = false;
        void RenderToTexture(Camera parent, Camera cam, Camera.MonoOrStereoscopicEye eye, RenderTexture target) {
            PortalCamera.CopyCameraSettings(parent, cam);

            Matrix4x4 parentProjectionMatrix;
            Matrix4x4 parentWorldToCameraMatrix;


            switch (eye) {
                case Camera.MonoOrStereoscopicEye.Left:
                    parentProjectionMatrix = parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    parentWorldToCameraMatrix = parent.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                    break;
                case Camera.MonoOrStereoscopicEye.Right:
                    parentProjectionMatrix = parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    parentWorldToCameraMatrix = parent.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                    break;
                case Camera.MonoOrStereoscopicEye.Mono:
                default:
                    parentProjectionMatrix = parent.projectionMatrix;
                    parentWorldToCameraMatrix = parent.worldToCameraMatrix;
                    break;
            }

            Matrix4x4 newProjectionMatrix = parentProjectionMatrix;
            Matrix4x4 newWorldToCameraMatrix = parentWorldToCameraMatrix * _portal.PortalMatrix().inverse;
            // cam.transform.position = _portal.TeleportPoint(parent.transform.position);
            //cam.transform.rotation = _portal.TeleportRotation(parent.transform.rotation);


            cam.ResetProjectionMatrix();
            cam.rect = new Rect(0, 0, 1, 1);

            //cam.projectionMatrix = newProjectionMatrix;
            cam.worldToCameraMatrix = newWorldToCameraMatrix;

            cam.targetTexture = target;


            // Increase depth
            var savedCurrentlyRenderingPortal = _currentlyRenderingPortal;
            _currentlyRenderingPortal = _portal;
            _staticRenderDepth++;

            //var f = RenderTexture.active;
            //RenderTexture.active = target;
            //GL.Clear(true, true, _staticRenderDepth % 2 == 0? Color.red : Color.green);
            //RenderTexture.active = f;

            if (_portal.UseScissorRect) {
                // Calculate where in screen space the portal lies.
                // We use this to only render as much of the screen as necessary, avoiding overdraw.
                cam.ResetProjectionMatrix();
                cam.rect = new Rect(0, 0, 1, 1);
                Rect viewportRect = CalculatePortalViewportRect(cam);

                cam.projectionMatrix = MathUtil.ScissorsMatrix(cam.projectionMatrix, viewportRect);
                cam.rect = viewportRect;

            } else {
                cam.rect = new Rect(0, 0, 1, 1);
            }

            cam.Render();
            _staticRenderDepth--;
            _currentlyRenderingPortal = savedCurrentlyRenderingPortal;
        }



        protected void PreRenderOld() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (_propertyBlockObjectPool == null) {
                Awake();
            }
#endif
            SaveMaterialProperties();

            if (!enabled || !_renderer || !_renderer.enabled) {
                return;
            }

            if (!_portal.ExitPortal || !_portal.ExitPortal.isActiveAndEnabled) {
                _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                return;
            }

            PortalCamera currentPortalCamera = PortalCamera.current;

            // Don't ever render our own exit portal
            if (_staticRenderDepth > 0 && currentPortalCamera != null && _portal == currentPortalCamera.portal.ExitPortal) {
                _renderer.enabled = false;
                return;
            }

            // The stencil buffer gets used by Unity in deferred rendering and must clear itself, otherwise
            // it will be full of junk. https://docs.unity3d.com/Manual/SL-Stencil.html
            if (_staticRenderDepth == 0) {
                if (Camera.current.actualRenderingPath == RenderingPath.DeferredLighting || Camera.current.actualRenderingPath == RenderingPath.DeferredShading) {
                    Camera.current.clearStencilAfterLightingPass = true;
                }
            }

            // Stop recursion when we reach maximum depth
            if (_staticRenderDepth >= _portal.MaxRecursion) {
                if (_portal.FakeInfiniteRecursion && _portal.MaxRecursion >= 2) {
                    // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
                    // This creates an illusion of infinite recursion, but only works with at least two real recursions
                    // because the effect is unconvincing using the Main Camera's previous frame.
                    Camera parentCam = currentPortalCamera.parent;
                    PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

                    if (currentPortalCamera.portal == _portal && parentPortalCam.portal == _portal) {
                        // This portal is currently viewing itself.
                        // Render the bottom of the stack with the parent camera's view/projection.
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
                } else {
                    _portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    _portalMaterial.EnableKeyword("SAMPLE_DEFAULT_TEXTURE");
                }

                // Exit. We don't need to process any further depths.
                return;
            }

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

            _staticRenderDepth++;
            //RenderTexture tex = portalCamera.RenderToTexture2();
            //block.SetTexture("_PortalTexture", tex);
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
                //switch (Camera.current.stereoTargetEye) {
                //    case StereoTargetEyeMask.Right:
                //        block.SetTexture("_RightEyeTexture", tex);
                //        break;
                //    case StereoTargetEyeMask.None:
                //    case StereoTargetEyeMask.Left:
                //    case StereoTargetEyeMask.Both:
                //    default:
                //        block.SetTexture("_LeftEyeTexture", tex);
                //        break;
                //}
            }
            _staticRenderDepth--;
            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);
        }

        protected void PostRenderOld() {
            _renderer.enabled = true;
            RestoreMaterialProperties();
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

        private void SaveMaterialProperties2() {
            MaterialPropertyBlock block = _propertyBlockObjectPool.Take();
            _renderer.GetPropertyBlock(block);
            _propertyBlockStack.Push(block);
        }

        private void RestoreMaterialProperties2() {
            MaterialPropertyBlock block = _propertyBlockStack.Pop();
            _renderer.SetPropertyBlock(block);
            _propertyBlockObjectPool.Give(block);
        }

        private bool ShouldRenderBackface(Camera camera) {
            // Decrease the size of the box in which we will render the backface when rendering stereo.
            // This will prevent the backface from appearing in one eye when near the edge of the portal.
            float scaleMultiplier = camera.stereoEnabled ? 0.9f : 1.0f;
            if (_staticRenderDepth == 0 && LocalXYPlaneContainsPoint(Camera.current.transform.position, scaleMultiplier)) {
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
            Camera camera;
            if (!_cameraMap.TryGetValue(currentCamera, out camera)) {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + _portal.name, typeof(Camera));
                //go.hideFlags = HideFlags.HideAndDontSave;
                go.hideFlags = HideFlags.DontSave;

                camera = go.GetComponent<Camera>();
                camera.enabled = false;
                camera.gameObject.AddComponent<FlareLayer>();

                //camera.CopyFrom(currentCamera);
                _cameraMap[currentCamera] = camera;
            }

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
                camera.gameObject.AddComponent<FlareLayer>();

                // TODO: Awake doesn't get called when using ExecuteInEditMode
                portalCamera = go.AddComponent<PortalCamera>();
                portalCamera.Awake();
                portalCamera.enterScene = this.gameObject.scene;
                portalCamera.exitScene = _portal.ExitPortal ? _portal.ExitPortal.gameObject.scene : this.gameObject.scene;
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
