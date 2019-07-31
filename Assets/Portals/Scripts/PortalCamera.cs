using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class PortalCamera : MonoBehaviour {
        Camera _parent;
        Camera _camera;
        Portal _portal;
        Scene _enterScene;
        Scene _exitScene;
        int _renderDepth;
        private int _framesSinceLastUse = 0;
        private Material _depthPunchMaterial;

        [SerializeField]
        private FrameData _previousFrameData;

        public static Dictionary<Camera, PortalCamera> cameraMap = new Dictionary<Camera, PortalCamera>();

        public static PortalCamera current {
            get {
                PortalCamera c = null;
                cameraMap.TryGetValue(Camera.current, out c);
                return c;
            }
        }

        public FrameData PreviousFrameData {
            get { return _previousFrameData; }
        }

        public Camera parent {
            get { return _parent; }
            set { _parent = value; }
        }

        public new Camera camera {
            get { return _camera; }
        }


        public Portal portal {
            get { return _portal; }
            set { _portal = value; }
        }

        public Scene enterScene {
            get { return _enterScene; }
            set { _enterScene = value; }
        }

        public Scene exitScene {
            get {
                return _exitScene;
            }
            set {
                _exitScene = value;

                Scene activeScene = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(_exitScene);
                _sceneRenderSettings.CopyFromGlobalRenderSettings();
                SceneManager.SetActiveScene(activeScene);
            }
        }

        public int renderDepth {
            get {
                return _renderDepth;
            }
            set {
                _renderDepth = value;
            }
        }

        // RenderSettingsStruct _savedRenderSettings = new RenderSettingsStruct();
        RenderSettingsStruct _sceneRenderSettings = new RenderSettingsStruct();

        public void Awake() {
            _camera = GetComponent<Camera>();
            cameraMap[_camera] = this;

            _previousFrameData = new FrameData();
        }

        private void OnDestroy() {
            if (cameraMap != null && cameraMap.ContainsKey(_camera)) {
                cameraMap.Remove(_camera);
            }

            if (_camera && _camera.targetTexture && _camera.targetTexture != _previousFrameData.leftEyeTexture && _camera.targetTexture != _previousFrameData.rightEyeTexture) {
                RenderTexture.ReleaseTemporary(_camera.targetTexture);
            }
            if (_previousFrameData.leftEyeTexture) {
                RenderTexture.ReleaseTemporary(_previousFrameData.leftEyeTexture);
            }
            if (_previousFrameData.rightEyeTexture) {
                RenderTexture.ReleaseTemporary(_previousFrameData.rightEyeTexture);
            }
        }

        void Update() {
            if (_framesSinceLastUse > 0) {
                Util.SafeDestroy(this.gameObject);
            }
            _framesSinceLastUse++;
        }

        Vector3 GetStereoPosition(Camera camera, UnityEngine.XR.XRNode node) {
            if (camera.stereoEnabled) {
                // If our parent is rendering stereo, we need to handle eye offsets and root transformations
                Vector3 localPosition = UnityEngine.XR.InputTracking.GetLocalPosition(node);

                // GetLocalPosition returns the local position of the camera, but we need the global position
                // so we have to manually grab the parent.
                Transform parent = camera.transform.parent;
                if (parent) {
                    return parent.TransformPoint(localPosition);
                } else {
                    return localPosition;
                }
            } else {
                // Otherwise, we can just return the camera's position
                return camera.transform.position;
            }
        }

        Quaternion GetStereoRotation(Camera camera, UnityEngine.XR.XRNode node) {
            if (camera.stereoEnabled) {
                Quaternion localRotation = UnityEngine.XR.InputTracking.GetLocalRotation(node);
                Transform parent = camera.transform.parent;
                if (parent) {
                    return parent.rotation * localRotation;
                } else {
                    return localRotation;
                }
            } else {
                // Otherwise, we can just return the camera's position
                return camera.transform.rotation;
            }
        }

        private void SaveFrameData(Camera.MonoOrStereoscopicEye eye) {
            switch (eye) {
                case Camera.MonoOrStereoscopicEye.Right:
                    if (_previousFrameData.rightEyeTexture) {
                        RenderTexture.ReleaseTemporary(_previousFrameData.rightEyeTexture);
                    }
                    _previousFrameData.rightEyeProjectionMatrix = _camera.projectionMatrix;
                    _previousFrameData.rightEyeWorldToCameraMatrix = _camera.worldToCameraMatrix;
                    _previousFrameData.rightEyeTexture = _camera.targetTexture;
                    break;
                case Camera.MonoOrStereoscopicEye.Left:
                case Camera.MonoOrStereoscopicEye.Mono:
                default:
                    if (_previousFrameData.leftEyeTexture) {
                        RenderTexture.ReleaseTemporary(_previousFrameData.leftEyeTexture);
                    }
                    _previousFrameData.leftEyeProjectionMatrix = _camera.projectionMatrix;
                    _previousFrameData.leftEyeWorldToCameraMatrix = _camera.worldToCameraMatrix;
                    _previousFrameData.leftEyeTexture = _camera.targetTexture;
                    break;
            }
        }

        public RenderTexture RenderToTexture(Camera.MonoOrStereoscopicEye eye, Rect viewportRect, bool renderBackface) {
            _framesSinceLastUse = 0;

            // Copy parent camera's settings
            CopyCameraSettings(_parent, _camera);

            Matrix4x4 projectionMatrix;
            Matrix4x4 worldToCameraMatrix;
            switch (eye) {
                case Camera.MonoOrStereoscopicEye.Left:
                    projectionMatrix = _parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                    worldToCameraMatrix = _parent.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                    break;
                case Camera.MonoOrStereoscopicEye.Right:
                    projectionMatrix = _parent.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                    worldToCameraMatrix = _parent.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                    break;
                case Camera.MonoOrStereoscopicEye.Mono:
                default:
                    projectionMatrix = _parent.projectionMatrix;
                    worldToCameraMatrix = _parent.worldToCameraMatrix;
                    break;
            }

            //_camera.worldToCameraMatrix = worldToCameraMatrix * _portal.PortalMatrix().inverse;
            //_camera.projectionMatrix = projectionMatrix;
            //_camera.transform.position = _portal.TeleportPoint(_parent.transform.position);
            //_camera.transform.rotation = _portal.TeleportRotation(_parent.transform.rotation);
            
            _camera.projectionMatrix = projectionMatrix;
            _camera.worldToCameraMatrix = worldToCameraMatrix * _portal.PortalMatrix().inverse;
            _camera.transform.position = _camera.cameraToWorldMatrix.GetPosition();
            _camera.transform.rotation = _portal.TeleportRotation(_parent.transform.rotation);
            

            if (_portal.UseObliqueProjectionMatrix) {
                _camera.projectionMatrix = CalculateObliqueProjectionMatrix(projectionMatrix);
            }
            


            if (_portal.UseCullingMatrix) {
                _camera.cullingMatrix = CalculateCullingMatrix();
            } else {
                _camera.ResetCullingMatrix();
            }

            if (_portal.UseDepthMask) {
                DrawDepthPunchMesh(renderBackface);
            } else {
                _camera.RemoveAllCommandBuffers();
            }

            RenderTexture texture = RenderTexture.GetTemporary(_parent.pixelWidth, _parent.pixelHeight, 24, RenderTextureFormat.Default);
            texture.name = System.Enum.GetName(typeof(Camera.MonoOrStereoscopicEye), eye) + " " + _camera.stereoTargetEye + " " + Time.renderedFrameCount;
            
            _camera.targetTexture = texture;
            if (_portal.UseScissorRect) {
                _camera.rect = viewportRect;
                _camera.projectionMatrix = MathUtil.ScissorsMatrix(_camera.projectionMatrix, viewportRect);
            } else {
                _camera.rect = new Rect(0, 0, 1, 1);
            }
            _camera.Render();

            SaveFrameData(eye);

            return texture;
        }

        private void DrawDepthPunchMesh(bool renderBackface) {
            if (!_depthPunchMaterial) {
                Shader shader = Shader.Find("Portals/DepthPunch");
                _depthPunchMaterial = new Material(shader);
            }

            // Use a command buffer that clears depth to 0 (infinitely close)
            if (_camera.commandBufferCount == 0) {
                CommandBuffer buf = new CommandBuffer();
                buf.name = "Set Depth to 0";
                buf.ClearRenderTarget(true, true, Color.red, 0.0f);
                _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, buf);
            }

            // Need to render from the parent's point of view
            PortalRenderer renderer = _portal.PortalRenderer;
            _camera.ResetWorldToCameraMatrix();
            Matrix4x4 matrix = _camera.cameraToWorldMatrix * _parent.worldToCameraMatrix * renderer.transform.localToWorldMatrix;

            // Punch a hole in the depth buffer, so that everything can be drawn behind it
            Graphics.DrawMesh(PortalRenderer.Mesh, matrix, _depthPunchMaterial, renderer.gameObject.layer, _camera, 0);
            if (renderBackface) {
                Graphics.DrawMesh(PortalRenderer.Mesh, matrix, _depthPunchMaterial, renderer.gameObject.layer, _camera, 1);
            }
        }

        void CopyCameraSettings(Camera src, Camera dst) {
            dst.clearFlags = src.clearFlags;
            dst.backgroundColor = src.backgroundColor;
            dst.farClipPlane = src.farClipPlane;
            dst.nearClipPlane = src.nearClipPlane;
            dst.orthographic = src.orthographic;
            dst.aspect = src.aspect;
            dst.orthographicSize = src.orthographicSize;
            dst.renderingPath = src.renderingPath;
            dst.allowHDR = src.allowHDR;
            dst.allowMSAA = src.allowMSAA;
            dst.cullingMask = src.cullingMask;
            //dst.depthTextureMode = src.depthTextureMode;
            //dst.transparencySortAxis = src.transparencySortAxis;
            //dst.transparencySortMode = src.transparencySortMode;
            // TODO: Fix occlusion culling
            //dst.useOcclusionCulling = src.useOcclusionCulling;
            dst.useOcclusionCulling = false;

            dst.eventMask = 0; // Ignore OnMouseXXX events
            dst.cameraType = src.cameraType;

            if (!dst.stereoEnabled) {
                // Can't set FoV while in VR
                dst.fieldOfView = src.fieldOfView;
            }
        }

        void DecomposeMatrix4x4(Matrix4x4 matrix) {
            float near = matrix.m23 / (matrix.m22 - 1);
            float far = matrix.m23 / (matrix.m22 + 1);
            float bottom = near * (matrix.m12 - 1) / matrix.m11;
            float top = near * (matrix.m12 + 1) / matrix.m11;
            float left = near * (matrix.m02 - 1) / matrix.m00;
            float right = near * (matrix.m02 + 1) / matrix.m00;

            Debug.Log("near: " + near);
            Debug.Log("far: " + far);
            Debug.Log("bottom: " + bottom);
            Debug.Log("top: " + top);
            Debug.Log("left: " + left);
            Debug.Log("right: " + right);
        }

        Matrix4x4 CalculateObliqueProjectionMatrix(Matrix4x4 projectionMatrix) {
            // Calculate plane made from the exit portal's transform
            Plane exitPortalPlane = _portal.ExitPortal.Plane;

            // Determine whether or not we've crossed the plane already. If we have, we don't need to apply
            // oblique frustum clipping. Offset the value by our portal's ClippingOffset to reduce the effects
            // so that it swaps over slightly early. This helps reduce artifacts caused by loss of depth accuracy.
            bool onCloseSide = new Plane(exitPortalPlane.normal, exitPortalPlane.distance - _portal.ClippingOffset).GetSide(transform.position);
            if (onCloseSide) {
                // Offset the clipping plane itself so that a character walking through a portal has no seams
                exitPortalPlane.distance -= _portal.ClippingOffset / 2;

                // Project our world space clipping plane to the camera's local coordinates
                // e.g. normal (0, 0, 1) becomes (1, 0, 0) if we're looking left parallel to the plane
                Vector4 cameraSpaceNormal = _camera.transform.InverseTransformDirection(exitPortalPlane.normal);
                Vector4 cameraSpacePoint = _camera.transform.InverseTransformPoint(exitPortalPlane.normal * -exitPortalPlane.distance);

                // Calculate the d value for our plane by projecting our transformed point
                // onto our transformed normal vector.
                float distanceFromPlane = Vector4.Dot(cameraSpaceNormal, cameraSpacePoint);
                Vector4 cameraSpacePlane = new Vector4(-cameraSpaceNormal.x, -cameraSpaceNormal.y, cameraSpaceNormal.z, distanceFromPlane);

                MakeProjectionMatrixOblique(ref projectionMatrix, cameraSpacePlane);
            }
            return projectionMatrix;
        }

        void MakeProjectionMatrixOblique(ref Matrix4x4 projection, Vector4 clipPlane) {
            // Source: http://aras-p.info/texts/obliqueortho.html
            Vector4 q = projection.inverse * new Vector4(
                Mathf.Sign(clipPlane.x),
                Mathf.Sign(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
        }

        //Matrix4x4 HMDMatrix4x4ToMatrix4x4(Valve.VR.HmdMatrix44_t input) {
        //    var m = Matrix4x4.identity;

        //    m[0, 0] = input.m0;
        //    m[0, 1] = input.m1;
        //    m[0, 2] = input.m2;
        //    m[0, 3] = input.m3;

        //    m[1, 0] = input.m4;
        //    m[1, 1] = input.m5;
        //    m[1, 2] = input.m6;
        //    m[1, 3] = input.m7;

        //    m[2, 0] = input.m8;
        //    m[2, 1] = input.m9;
        //    m[2, 2] = input.m10;
        //    m[2, 3] = input.m11;

        //    m[3, 0] = input.m12;
        //    m[3, 1] = input.m13;
        //    m[3, 2] = input.m14;
        //    m[3, 3] = input.m15;

        //    return m;
        //}

        private Matrix4x4 CalculateCullingMatrix() {
            _camera.ResetCullingMatrix();

            // Lower left of the backside of our plane in world coordinates
            Vector3 pa = _portal.ExitPortal.transform.TransformPoint(new Vector3(0.5f, -0.5f, 0));

            // Lower right
            Vector3 pb = _portal.ExitPortal.transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0));

            // Upper left
            Vector3 pc = _portal.ExitPortal.transform.TransformPoint(new Vector3(0.5f, 0.5f, 0));

            Vector3 pe = _camera.transform.position;

            // Calculate what our horizontal field of view would be with off-axis projection.
            // If this fov is greater than our camera's fov, we should just use the camera's default projection
            // matrix instead. Otherwise, the frustum's fov will approach 180 degrees (way too large).
            Vector3 camToLowerLeft = pa - _camera.transform.position;
            camToLowerLeft.y = 0;
            Vector3 camToLowerRight = pb - _camera.transform.position;
            camToLowerRight.y = 0;
            float fieldOfView = Vector3.Angle(camToLowerLeft, camToLowerRight);
            if (fieldOfView > _camera.fieldOfView) {
                return _camera.cullingMatrix;
            } else {
                return MathUtil.OffAxisProjectionMatrix(_camera.nearClipPlane, _camera.farClipPlane, pa, pb, pc, pe);
            }
        }

        //void OnPreRender() {
        //    //if (!copyGI ||
        //    //    !enterScene.isLoaded || !enterScene.IsValid() ||
        //    //    !exitScene.isLoaded || !exitScene.IsValid() ||
        //    //    enterScene == exitScene) {
        //    //    return;
        //    //}

        //    //_savedRenderSettings.CopyFromGlobalRenderSettings();
        //    //_sceneRenderSettings.CopyToGlobalRenderSettings();
        //    //if (renderDepth == 1) {
        //    //    RenderSettings.ambientSkyColor = Color.red;
        //    //} else if (renderDepth == 2) {
        //    //    RenderSettings.ambientSkyColor = Color.green;
        //    //} else if (renderDepth == 3) {
        //    //    RenderSettings.ambientSkyColor = Color.blue;
        //    //}
        //}

        //void OnPostRender() {
        //    //if (!copyGI ||
        //    //    !enterScene.isLoaded || !enterScene.IsValid() ||
        //    //    !exitScene.isLoaded || !exitScene.IsValid() ||
        //    //    enterScene == exitScene) {
        //    //    return;
        //    //}
        //    //_savedRenderSettings.CopyToGlobalRenderSettings();

        //    //RenderSettings.ambientSkyColor = Color.white;
        //}

        Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3) { //get the intersection point of 3 planes
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }

        void DrawFrustumGizmo(Matrix4x4 matrix) {
            Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(matrix); //get planes from matrix
            Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            for (int i = 0; i < 4; i++) {
                nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
            }
            for (int i = 0; i < 4; i++) {
                Gizmos.DrawLine(nearCorners[i], nearCorners[(i + 1) % 4]); //near corners on the created projection matrix
                Gizmos.DrawLine(farCorners[i], farCorners[(i + 1) % 4]); //far corners on the created projection matrix
                Gizmos.DrawLine(nearCorners[i], farCorners[i]); //sides of the created projection matrix
            }
        }

        //void OnDrawGizmosSelected() {
        //    Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
        //    DrawFrustumGizmo(_camera.cullingMatrix);

        //    Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
        //    DrawFrustumGizmo(_camera.projectionMatrix * _camera.worldToCameraMatrix);
        //}

        struct RenderSettingsStruct {
            public Color ambientEquatorColor;
            public Color ambientGroundColor;
            public float ambientIntensity;
            public Color ambientLight;
            public AmbientMode ambientMode;
            public SphericalHarmonicsL2 ambientProbe;
            public Color ambientSkyColor;
            public Cubemap customReflection;
            public DefaultReflectionMode defaultReflectionMode;
            public int defaultReflectionResolution;
            public float flareFadeSpeed;
            public float flareStrength;
            public bool fog;
            public Color fogColor;
            public float fogDensity;
            public float fogEndDistance;
            public FogMode fogMode;
            public float fogStartDistance;
            public float haloStrength;
            public int reflectionBounces;
            public float reflectionIntensity;
            public Material skybox;
            public Light sun;

            public void CopyFromGlobalRenderSettings() {
                ambientEquatorColor = RenderSettings.ambientEquatorColor;
                ambientGroundColor = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                ambientLight = RenderSettings.ambientLight;
                ambientMode = RenderSettings.ambientMode;
                ambientProbe = RenderSettings.ambientProbe;
                ambientSkyColor = RenderSettings.ambientSkyColor;
                customReflection = RenderSettings.customReflection;
                defaultReflectionMode = RenderSettings.defaultReflectionMode;
                defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
                flareFadeSpeed = RenderSettings.flareFadeSpeed;
                flareStrength = RenderSettings.flareStrength;
                fog = RenderSettings.fog;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
                fogEndDistance = RenderSettings.fogEndDistance;
                fogMode = RenderSettings.fogMode;
                fogStartDistance = RenderSettings.fogStartDistance;
                haloStrength = RenderSettings.haloStrength;
                reflectionBounces = RenderSettings.reflectionBounces;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                skybox = RenderSettings.skybox;
                sun = RenderSettings.sun;
            }

            public void CopyToGlobalRenderSettings() {
                // Bug workaround where reflections were being sampled from the wrong skybox
                //RenderSettings.skybox = null;
                RenderSettings.skybox = skybox;

                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientProbe = ambientProbe;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.customReflection = customReflection;
                RenderSettings.defaultReflectionMode = defaultReflectionMode;
                RenderSettings.defaultReflectionResolution = defaultReflectionResolution;
                RenderSettings.flareFadeSpeed = flareFadeSpeed;
                RenderSettings.flareStrength = flareStrength;
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogEndDistance = fogEndDistance;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.haloStrength = haloStrength;
                RenderSettings.reflectionBounces = reflectionBounces;
                RenderSettings.reflectionIntensity = reflectionIntensity;

                RenderSettings.sun = sun;
                //DynamicGI.UpdateEnvironment();
            }
        }

        [System.Serializable] // TODO: shouldn't be serializable in the future
        public class FrameData {
            public RenderTexture leftEyeTexture;
            public RenderTexture rightEyeTexture;

            public Matrix4x4 leftEyeProjectionMatrix;
            public Matrix4x4 rightEyeProjectionMatrix;

            public Matrix4x4 leftEyeWorldToCameraMatrix;
            public Matrix4x4 rightEyeWorldToCameraMatrix;

            public FrameData() { }
        }
    }
}
