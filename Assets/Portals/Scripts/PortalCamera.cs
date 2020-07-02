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
        private const float ObliqueClippingOffset = 0.001f;
            
        private Camera _parent;
        private Camera _camera;
        private Portal _portal;
        private int _renderDepth;
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

        public int renderDepth {
            get {
                return _renderDepth;
            }
            set {
                _renderDepth = value;
            }
        }

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
                    _camera.ResetProjectionMatrix();
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
                    // Need to restore original projection matrix for rendering fake recursion
                    _camera.ResetProjectionMatrix();
                    _previousFrameData.leftEyeProjectionMatrix = _camera.projectionMatrix;
                    _previousFrameData.leftEyeWorldToCameraMatrix = _camera.worldToCameraMatrix;
                    _previousFrameData.leftEyeTexture = _camera.targetTexture;
                    break;
            }
        }
        
        private RenderTexture GetTemporaryRT() {
            int w = Camera.current.pixelWidth / _portal.Downscaling;
            int h = Camera.current.pixelHeight / _portal.Downscaling;
            //int w = Screen.width;
            //int h = Screen.height;
            int depth = (int)_portal.DepthBufferQuality;
            var format = _camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            var writeMode = RenderTextureReadWrite.Default;
            int msaaSamples = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
            var memoryless = RenderTextureMemoryless.None;
            var vrUsage = VRTextureUsage.None;
            bool useDynamicScale = false;

            // TODO: figure out correct settings for VRUsage, memoryless, and dynamic scale
            RenderTexture rt = RenderTexture.GetTemporary(w, h, depth, format, writeMode, msaaSamples, memoryless, vrUsage, useDynamicScale);
            //rt.name = this.gameObject.name;
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;
            //rt.filterMode = FilterMode.Bilinear;

            return rt;
        }

        public RenderTexture RenderToTexture(Camera.MonoOrStereoscopicEye eye, Rect viewportRect, bool renderBackface) {
            _framesSinceLastUse = 0;

            // Copy parent camera's settings
            CopyCameraSettings(_parent, _camera);
            //_camera.farClipPlane = _parent.farClipPlane * _portal.PortalScaleAverage();

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
            _camera.transform.position = _portal.TeleportPoint(_parent.transform.position);
            _camera.transform.rotation = _portal.TeleportRotation(_parent.transform.rotation);
            _camera.projectionMatrix = projectionMatrix;
            //_camera.worldToCameraMatrix = worldToCameraMatrix * _portal.PortalMatrix().inverse;
            _camera.ResetWorldToCameraMatrix();

            Matrix4x4 defaultProjection = _camera.projectionMatrix;

            if (_portal.UseObliqueProjectionMatrix) {
                _camera.ResetProjectionMatrix();
                _camera.projectionMatrix = CalculateObliqueProjectionMatrix(projectionMatrix);
            } else {
                _camera.ResetProjectionMatrix();
            }

            if (_portal.UseScissorRect) {
                _camera.rect = viewportRect;
                _camera.projectionMatrix = MathUtil.ScissorsMatrix(_camera.projectionMatrix, viewportRect);
            } else {
                _camera.rect = new Rect(0, 0, 1, 1);
            }

            if (_portal.UseOcclusionMatrix) {
                _camera.cullingMatrix = CalculateCullingMatrix();
            } else {
                _camera.cullingMatrix = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            }

            if (_portal.DebuggingEnabled) {
                //Util.DrawDebugFrustum3(_camera.projectionMatrix * _camera.worldToCameraMatrix, Color.white);

                if (_portal.UseOcclusionMatrix) {
                    Util.DrawDebugFrustum3(_camera.cullingMatrix, Color.blue);
                }
            }


            if (_portal.UseRaycastOcclusion) {
                _camera.useOcclusionCulling = false;
            }

            RenderTexture texture = GetTemporaryRT();

            if (_portal.FakeInfiniteRecursion) {
                // RenderTexture must be cleared when using fake infinite recursion because
                // we might sometimes sample uninitialized garbage pixels otherwise, which can
                // cause significant visual artifacts.
                ClearRenderTexture(texture);
            }

            _camera.targetTexture = texture;
            _camera.Render();

            SaveFrameData(eye);

            return texture;
        }

        private void ClearRenderTexture(RenderTexture rt) {
            // TODO: This is probably a fairly expensive operation. We can make it cheaper by using
            // CommandBuffers to avoid swapping the active texture, but we have to clear the whole screen
            // instead of just the portion that is rendering
            var oldRT = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = oldRT;
        }

        public static void CopyCameraSettings(Camera src, Camera dst) {
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
            dst.useOcclusionCulling = src.useOcclusionCulling;
            //dst.useOcclusionCulling = false;

            dst.eventMask = 0; // Ignore OnMouseXXX events
            dst.cameraType = src.cameraType;

            if (!dst.stereoEnabled) {
                // Can't set FoV while in VR
                dst.fieldOfView = src.fieldOfView;
            }
        }

        Matrix4x4 CalculateObliqueProjectionMatrix(Matrix4x4 projectionMatrix) {
            // Calculate plane made from the exit portal's transform
            Plane exitPortalPlane = _portal.ExitPortal.Plane;

            Vector3 position = _camera.cameraToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            float distanceToPlane = exitPortalPlane.GetDistanceToPoint(position);
            if (distanceToPlane > _portal.ClippingOffset) {
                Vector4 exitPlaneWorldSpace = _portal.ExitPortal.VectorPlane;
                Vector4 exitPlaneCameraSpace = _camera.worldToCameraMatrix.inverse.transpose * exitPlaneWorldSpace;
                // Offset the clipping plane itself so that a character walking through a portal has no seams
                exitPlaneCameraSpace.w -= ObliqueClippingOffset;
                exitPlaneCameraSpace *= -1;
                MathUtil.MakeProjectionMatrixOblique(ref projectionMatrix, exitPlaneCameraSpace);
                //projectionMatrix = _camera.CalculateObliqueMatrix(exitPlaneCameraSpace);
            }
            return projectionMatrix;
        }

        private Matrix4x4 CalculateCullingMatrix() {
            _camera.ResetCullingMatrix();
            Vector3[] corners = _portal.ExitPortal.WorldSpaceCorners;

            Vector3 pa = corners[3]; // Lower left
            Vector3 pb = corners[2]; // Lower right
            Vector3 pc = corners[0]; // Upper left
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
                float near = _camera.nearClipPlane;
                float far = _camera.farClipPlane;
                return MathUtil.OffAxisProjectionMatrix(near, far, pa, pb, pc, pe);
            }
        }


        [System.Serializable]
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
