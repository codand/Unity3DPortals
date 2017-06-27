using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    //[ExecuteInEditMode]
    public class PortalRenderer : RenderedBehaviour {
        [SerializeField] Portal _portal;

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
        private MeshFilter _meshFilter;

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

        private Transform _transform;

        void Awake() {
            _renderer = GetComponent<Renderer>();
            _meshFilter = GetComponent<MeshFilter>();

            _transform = this.transform;
            _blockStack = new Stack<MaterialPropertyBlock>();
            _keywordStack = new Stack<ShaderKeyword>();
            _blockPool = new ObjectPool<MaterialPropertyBlock>(1, () => new MaterialPropertyBlock());

            // TODO
            //this.gameObject.layer = PortalPhysics.PortalLayer;

            if (!_mesh) {
                _mesh = MakeMesh();
            }

            _meshFilter.sharedMesh = _mesh;
            if (!_portalMaterial || !_backFaceMaterial) {
                Material portalMaterial = new Material(Shader.Find("Portal/Portal"));
                Material backFaceMaterial = new Material(Shader.Find("Portal/PortalBackface"));

                portalMaterial.name = "Portal FrontFace (Instanced)";
                backFaceMaterial.name = "Portal BackFace (Instanced)";

                _portalMaterial = portalMaterial;
                _backFaceMaterial = backFaceMaterial;

                _portalMaterial.SetTexture("_TransparencyMask", _portal.TransparencyMask);
                _portalMaterial.SetTexture("_DefaultTexture", _portal.DefaultTexture);

                _renderer.sharedMaterials = new Material[] {
                    _portalMaterial,
                    _backFaceMaterial,
                };
            }
        }

        void OnValidate() {

        }

        void Update() {
            _transform.localScale = Vector3.one;
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
            if (_depth > 0 && currentPortalCamera != null && _portal == currentPortalCamera.portal.ExitPortal) {
                return;
            }

            // Stop recursion when we reach maximum depth
            if (_depth >= _portal.MaxRecursiveDepth) {
                if (_portal.FakeInfiniteRecursion && _portal.MaxRecursiveDepth >= 2) {
                    // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
                    // This creates an illusion of infinite recursion, but only works with at least two real recursions
                    // because the effect is unconvincing using the Main Camera's previous frame.
                    Camera parentCam = currentPortalCamera.parent;
                    PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

                    if (currentPortalCamera.portal == _portal && parentPortalCam.portal == _portal) {
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

            if (!_portal.ExitPortal || !_portal.ExitPortal.isActiveAndEnabled) {
                _portalMaterial.EnableKeyword("DONT_SAMPLE");
            } else {
                _portalMaterial.DisableKeyword("DONT_SAMPLE");
            }

            MaterialPropertyBlock block = _blockPool.Take();
            _renderer.GetPropertyBlock(block);

            if (LocalXYPlaneContainsPoint(Camera.current.transform.position)) {
                float penetration = CalculateNearPlanePenetration(Camera.current);
                if (penetration > 0) {
                    penetration += 0.001f; // Add a small offset to avoid clip-fighting
                    Vector3 currentScale = _transform.localScale;
                    Vector3 newScale = new Vector3(
                        _transform.localScale.x,
                        _transform.localScale.y,
                        penetration / 2); // Divide by two because scale expands in both directions
                    _transform.localScale = newScale;
                    block.SetFloat("_BackfaceAlpha", 1.0f);
                } else {
                    block.SetFloat("_BackfaceAlpha", 0.0f);
                }
            } else {
                block.SetFloat("_BackfaceAlpha", 0.0f);
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

        private bool LocalXYPlaneContainsPoint(Vector3 point) {
            Vector3 localPoint = _transform.InverseTransformPoint(point);
            if (localPoint.x < -0.5f) return false;
            if (localPoint.x > 0.5f) return false;
            if (localPoint.y > 0.5f) return false;
            if (localPoint.y < -0.5f) return false;
            return true;
        }

        private float CalculateNearPlanePenetration(Camera camera) {
            Vector3[] corners = CalculateNearPlaneCorners(camera);
            Plane plane = _portal.Plane;
            float maxPenetration = Mathf.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++) {
                Vector3 corner = corners[i];
                float penetration = plane.GetDistanceToPoint(corner);
                maxPenetration = Mathf.Max(maxPenetration, penetration);
            }
            return maxPenetration;
        }

        public static Vector3[] CalculateNearPlaneCorners(Camera camera) {
            // Source: https://gamedev.stackexchange.com/questions/19774/determine-corners-of-a-specific-plane-in-the-frustum
            Transform t = camera.transform;
            Vector3 p = t.position;
            Vector3 v = t.forward;
            Vector3 up = t.up;
            Vector3 right = t.right;
            float nDis = camera.nearClipPlane;
            float fDis = camera.farClipPlane;
            float fov = camera.fieldOfView * Mathf.Deg2Rad;
            float ar = camera.aspect;

            float hNear = 2 * Mathf.Tan(fov / 2) * nDis;
            float wNear = hNear * ar;

            Vector3 cNear = p + v * nDis;

            Vector3 hHalf = up * hNear / 2;
            Vector3 wHalf = right * wNear / 2;

            Vector3 topLeft = cNear + hHalf - wHalf;
            Vector3 topRight = cNear + hHalf + wHalf;
            Vector3 bottomRight = cNear - hHalf + wHalf;
            Vector3 bottomLeft = cNear - hHalf - wHalf;

            return new Vector3[] {
            topLeft,
            topRight,
            bottomRight,
            bottomLeft
        };
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
                portalCamera.exitScene = _portal.ExitPortal.gameObject.scene;
                portalCamera.parent = currentCamera;
                portalCamera.portal = _portal;

                if (this.gameObject.scene != _portal.ExitPortal.gameObject.scene) {
                    //PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    //thing.scene = exitPortal.gameObject.scene;
                }

                _camToPortalCam[currentCamera] = portalCamera;
            }
            return portalCamera;
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
