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
            _camera.projectionMatrix = projectionMatrix;
            _camera.worldToCameraMatrix = worldToCameraMatrix * _portal.PortalMatrix().inverse;

            if (_portal.UseObliqueProjectionMatrix) {
                _camera.projectionMatrix = CalculateObliqueProjectionMatrix(projectionMatrix);
            } else {
                CommandBuffer enableClippingCmdBuffer = new CommandBuffer();
                enableClippingCmdBuffer.EnableShaderKeyword("PLANAR_CLIPPING_ENABLED");
                enableClippingCmdBuffer.SetGlobalVector("_ClippingPlane", _portal.ExitPortal.VectorPlane);
                CommandBuffer disableClippingCmdBuffer = new CommandBuffer();
                disableClippingCmdBuffer.DisableShaderKeyword("PLANAR_CLIPPING_ENABLED");

                _camera.RemoveAllCommandBuffers();
                _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, enableClippingCmdBuffer);
                _camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, disableClippingCmdBuffer);
                _camera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, enableClippingCmdBuffer);
                _camera.AddCommandBuffer(CameraEvent.AfterDepthTexture, disableClippingCmdBuffer);
            }

            if (_portal.UseScissorRect) {
                _camera.rect = viewportRect;
                _camera.projectionMatrix = MathUtil.ScissorsMatrix(_camera.projectionMatrix, viewportRect);
            } else {
                _camera.rect = new Rect(0, 0, 1, 1);
            }

            RenderTexture texture = GetTemporaryRT();
            _camera.targetTexture = texture;
            _camera.Render();

            SaveFrameData(eye);

            return texture;
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
                //exitPlaneCameraSpace.w -= _portal.ClippingOffset;
                exitPlaneCameraSpace.w -= ObliqueClippingOffset;
                exitPlaneCameraSpace *= -1;
                MathUtil.MakeProjectionMatrixOblique(ref projectionMatrix, exitPlaneCameraSpace);
            }
            return projectionMatrix;
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
