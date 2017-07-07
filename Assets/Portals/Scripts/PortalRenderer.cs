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

    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PortalRenderer : RenderedBehaviour {
        #region Members
        // Mesh spawned when walking through a portal so that you can't clip through the portal
        private static Mesh m_Mesh;

        private Portal m_Portal;

        // Maps cameras to their children
        private Dictionary<Camera, PortalCamera> m_PortalCamByCam = new Dictionary<Camera, PortalCamera>();

        // Used to track current recursion depth
        private static int s_Depth;

        // Instanced materials
        private Material m_PortalMaterial;
        private Material m_BackfaceMaterial;

        // Members used to save and restore material properties between rendering in the same frame
        private Stack<MaterialPropertyBlock> m_PropertyBlockStack;
        private Stack<ShaderKeyword> m_ShaderKeywordStack;
        private ObjectPool<MaterialPropertyBlock> m_PropertyBlockObjectPool;

        // Cached components
        private Renderer m_Renderer;
        private MeshFilter m_MeshFilter;
        private Transform m_Transform;
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
        protected override void PostRender() {
            RestoreMaterialProperties();
        }

        protected override void PreRender() {
#if UNITY_EDITOR
            // Workaround for Unity bug that causes Awake/Start to not be called when running in EditMode
            // https://issuetracker.unity3d.com/issues/awake-and-start-not-called-before-update-when-assembly-is-reloaded-for-executeineditmode-scripts
            if (m_PropertyBlockObjectPool == null) {
                Awake();
            }
#endif
            SaveMaterialProperties();

            if (!enabled || !m_Renderer || !m_Renderer.enabled) {
                return;
            }

            if (!m_Portal.ExitPortal || !m_Portal.ExitPortal.isActiveAndEnabled) {
                m_PortalMaterial.EnableKeyword("DONT_SAMPLE");
                return;
            }

            PortalCamera currentPortalCamera = PortalCamera.current;

            // Don't ever render our own exit portal
            if (s_Depth > 0 && currentPortalCamera != null && m_Portal == currentPortalCamera.portal.ExitPortal) {
                return;
            }

            // Stop recursion when we reach maximum depth
            if (s_Depth >= m_Portal.MaxRecursion) {
                if (m_Portal.FakeInfiniteRecursion && m_Portal.MaxRecursion >= 2) {
                    // Use the previous frame's RenderTexture from the parent camera to render the bottom layer.
                    // This creates an illusion of infinite recursion, but only works with at least two real recursions
                    // because the effect is unconvincing using the Main Camera's previous frame.
                    Camera parentCam = currentPortalCamera.parent;
                    PortalCamera parentPortalCam = parentCam.GetComponent<PortalCamera>();

                    if (currentPortalCamera.portal == m_Portal && parentPortalCam.portal == m_Portal) {
                        // This portal is currently viewing itself.
                        // Render the bottom of the stack with the parent camera's view/projection.
                        m_PortalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
                        m_PortalMaterial.SetMatrix("PORTAL_MATRIX_VP", parentPortalCam.lastFrameProjectionMatrix * parentPortalCam.lastFrameWorldToCameraMatrix);
                        m_PortalMaterial.SetTexture("_RightEyeTexture", parentPortalCam.lastFrameRenderTexture);
                    } else {
                        // We are viewing another portal.
                        // Render the bottom of the stack with a base texture
                        m_PortalMaterial.EnableKeyword("DONT_SAMPLE");
                    }
                } else {
                    m_PortalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    m_PortalMaterial.EnableKeyword("DONT_SAMPLE");
                }

                // Exit. We don't need to process any further depths.
                return;
            }

            MaterialPropertyBlock block = m_PropertyBlockObjectPool.Take();
            m_Renderer.GetPropertyBlock(block);

            // Handle the player clipping through the portal's frontface
            bool renderBackface = false;
            if (s_Depth == 0 && LocalXYPlaneContainsPoint(Camera.current.transform.position)) {
                // Camera is within the border of the camera
                if (!m_Portal.Plane.GetSide(Camera.current.transform.position)) {
                    // Camera is in front of the plane
                    float penetration = CalculateNearPlanePenetration(Camera.current);
                    if (penetration > 0) {
                        // Near clipping plane is behind the portal at some point
                        renderBackface = true;

                        ////// Z is divided by two because scale expands in both directions, and
                        ////// a small offset is added to avoid clip-fighting
                        ////Vector3 newScale = new Vector3(
                        ////    m_Transform.localScale.x,
                        ////    m_Transform.localScale.y,
                        ////    penetration / 2 + 0.001f);
                        ////m_Transform.localScale = newScale;
                    }
                }

                ////float distanceFromPlane = _portal.Plane.GetDistanceToPoint(Camera.current.transform.position);
                ////if (distanceFromPlane > _backfaceTriggerDistance && distanceFromPlane <= 0.0f) {
                ////    renderBackface = true;
                ////}
            }
            if (renderBackface) {
                block.SetFloat("_BackfaceAlpha", 1.0f);
            } else {
                block.SetFloat("_BackfaceAlpha", 0.0f);
            }

            // Initialize or get anything needed for this depth level
            PortalCamera portalCamera = GetOrCreatePortalCamera(Camera.current);
            //// portalCamera.renderDepth = s_depth + 1;

            s_Depth++;
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
            s_Depth--;
            m_Renderer.SetPropertyBlock(block);
            m_PropertyBlockObjectPool.Give(block);
        }
        #endregion

        #region Initialization
        private void Awake() {
            m_Portal = GetComponentInParent<Portal>();
            m_Renderer = GetComponent<MeshRenderer>();
            m_MeshFilter = GetComponent<MeshFilter>();

            m_Transform = this.transform;
            m_PropertyBlockStack = new Stack<MaterialPropertyBlock>();
            m_ShaderKeywordStack = new Stack<ShaderKeyword>();
            m_PropertyBlockObjectPool = new ObjectPool<MaterialPropertyBlock>(1, () => new MaterialPropertyBlock());

            //// TODO
            //// this.gameObject.layer = PortalPhysics.PortalLayer;

            if (!m_Mesh) {
                m_Mesh = MakePortalMesh();
            }

            m_MeshFilter.sharedMesh = m_Mesh;
            if (!m_PortalMaterial || !m_BackfaceMaterial) {
                Material portalMaterial = new Material(Shader.Find("Portal/Portal"));
                Material backFaceMaterial = new Material(Shader.Find("Portal/PortalBackface"));

                portalMaterial.name = "Portal FrontFace";
                backFaceMaterial.name = "Portal BackFace";

                m_PortalMaterial = portalMaterial;
                m_BackfaceMaterial = backFaceMaterial;

                m_PortalMaterial.SetTexture("_TransparencyMask", m_Portal.TransparencyMask);
                m_PortalMaterial.SetTexture("_DefaultTexture", m_Portal.DefaultTexture);

                m_Renderer.sharedMaterials = new Material[] {
                    m_PortalMaterial,
                    m_BackfaceMaterial,
                };
            }
        }

        private void OnEnable() {
            m_Portal.OnDefaultTextureChanged += OnDefaultTextureChanged;
            m_Portal.OnTransparencyMaskChanged += OnTransparencyMaskChanged;
        }

        private void OnDisable() {
            m_Portal.OnDefaultTextureChanged -= OnDefaultTextureChanged;
            m_Portal.OnTransparencyMaskChanged -= OnTransparencyMaskChanged;

            // Clean up cameras in scene. This is important when using ExecuteInEditMode because
            // script recompilation will disable then enable this script causing creation of duplicate
            // cameras.
            foreach (KeyValuePair<Camera, PortalCamera> kvp in m_PortalCamByCam) {
                PortalCamera child = kvp.Value;
                if (child && child.gameObject) {
                    Util.SafeDestroy(child.gameObject);
                }
            }
        }
        #endregion

        ////private void Update() {
        ////    m_Transform.localScale = Vector3.one;
        ////}
        
        #region Callbacks
        private void OnDefaultTextureChanged(Portal portal, Texture oldTexture, Texture newTexture) {
            m_PortalMaterial.SetTexture("_DefaultTexture", newTexture);
        }

        private void OnTransparencyMaskChanged(Portal portal, Texture oldTexture, Texture newTexture) {
            m_PortalMaterial.SetTexture("_TransparencyMask", newTexture);
        }
        #endregion

        #region Private Methods
        private void SaveMaterialProperties() {
            MaterialPropertyBlock block = m_PropertyBlockObjectPool.Take();
            m_Renderer.GetPropertyBlock(block);
            m_PropertyBlockStack.Push(block);

            ShaderKeyword keywords = ShaderKeyword.None;
            if (m_PortalMaterial.IsKeywordEnabled("SAMPLE_PREVIOUS_FRAME")) {
                keywords |= ShaderKeyword.SamplePreviousFrame;
            }
            if (m_PortalMaterial.IsKeywordEnabled("DONT_SAMPLE")) {
                keywords |= ShaderKeyword.SampleDefaultTexture;
            }
            m_ShaderKeywordStack.Push(keywords);
        }

        private void RestoreMaterialProperties() {
            MaterialPropertyBlock block = m_PropertyBlockStack.Pop();
            m_Renderer.SetPropertyBlock(block);
            m_PropertyBlockObjectPool.Give(block);

            ShaderKeyword keywords = m_ShaderKeywordStack.Pop();
            if ((keywords & ShaderKeyword.SamplePreviousFrame) == ShaderKeyword.SamplePreviousFrame) {
                m_PortalMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");
            } else {
                m_PortalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            }

            if ((keywords & ShaderKeyword.SampleDefaultTexture) == ShaderKeyword.SampleDefaultTexture) {
                m_PortalMaterial.EnableKeyword("DONT_SAMPLE");
            } else {
                m_PortalMaterial.DisableKeyword("DONT_SAMPLE");
            }
        }
        
        private bool LocalXYPlaneContainsPoint(Vector3 point) {
            Vector3 localPoint = m_Transform.InverseTransformPoint(point);
            if (localPoint.x < -0.5f) return false;
            if (localPoint.x > 0.5f) return false;
            if (localPoint.y > 0.5f) return false;
            if (localPoint.y < -0.5f) return false;
            return true;
        }

        private float CalculateNearPlanePenetration(Camera camera) {
            Vector3[] corners = new Vector3[4];
            CalculateNearPlaneCornersNoAlloc(camera, ref corners);
            Plane plane = m_Portal.Plane;
            float maxPenetration = Mathf.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++) {
                Vector3 corner = corners[i];
                float penetration = plane.GetDistanceToPoint(corner);
                maxPenetration = Mathf.Max(maxPenetration, penetration);
            }
            return maxPenetration;
        }

        private static void CalculateNearPlaneCornersNoAlloc(Camera camera, ref Vector3[] corners) {
            // Source: https://gamedev.stackexchange.com/questions/19774/determine-corners-of-a-specific-plane-in-the-frustum
            Transform t = camera.transform;
            Vector3 p = t.position;
            Vector3 v = t.forward;
            Vector3 up = t.up;
            Vector3 right = t.right;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float fov = camera.fieldOfView * Mathf.Deg2Rad;
            float ar = camera.aspect;

            float nearHeight = 2 * Mathf.Tan(fov / 2) * near;
            float nearWidth = nearHeight * ar;

            Vector3 nearCenter = p + (v * near);

            Vector3 halfHeight = up * nearHeight / 2;
            Vector3 halfWidth = right * nearWidth / 2;

            Vector3 topLeft = nearCenter + halfHeight - halfWidth;
            Vector3 topRight = nearCenter + halfHeight + halfWidth;
            Vector3 bottomRight = nearCenter - halfHeight + halfWidth;
            Vector3 bottomLeft = nearCenter - halfHeight - halfWidth;

            corners[0] = topLeft;
            corners[1] = topRight;
            corners[2] = bottomRight;
            corners[3] = bottomLeft;
        }

        private PortalCamera GetOrCreatePortalCamera(Camera currentCamera) {
            PortalCamera portalCamera = null;
            m_PortalCamByCam.TryGetValue(currentCamera, out portalCamera);
            if (!portalCamera) {
                GameObject go = new GameObject("~" + currentCamera.name + "->" + gameObject.name, typeof(Camera));
                go.hideFlags = HideFlags.HideAndDontSave;

                Camera camera = go.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = transform.position;
                camera.transform.rotation = transform.rotation;
                camera.gameObject.AddComponent<FlareLayer>();

                // TODO: Awake doesn't get called when using ExecuteInEditMode
                portalCamera = go.AddComponent<PortalCamera>();
                portalCamera.Awake();
                portalCamera.enterScene = this.gameObject.scene;
                portalCamera.exitScene = m_Portal.ExitPortal ? m_Portal.ExitPortal.gameObject.scene : this.gameObject.scene;
                portalCamera.parent = currentCamera;
                portalCamera.portal = m_Portal;

                if (m_Portal.ExitPortal && this.gameObject.scene != m_Portal.ExitPortal.gameObject.scene) {
                    ////PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    ////thing.scene = exitPortal.gameObject.scene;
                }

                m_PortalCamByCam[currentCamera] = portalCamera;
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
