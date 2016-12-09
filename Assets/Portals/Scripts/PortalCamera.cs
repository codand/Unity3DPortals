using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Portals {
    [RequireComponent(typeof(Camera))]
    public class PortalCamera : MonoBehaviour {
        bool _copyGI;
        Camera _parent;
        Camera _camera;
        Portal _portal;
        Scene _enterScene;
        Scene _exitScene;
        int _renderDepth;
        bool _useProjectionMatrix;
        bool _useCullingMatrix;
        public Matrix4x4 lastFrameWorldToCameraMatrix;

        public bool copyGI {
            get {
                return _copyGI;
            }
            set {
                _copyGI = value;
            }
        }

        public Camera parent {
            get {
                return _parent;
            }
            set {
                _parent = value;
            }
        }


        public Portal portal {
            get {
                return _portal;
            }
            set {
                _portal = value;
            }
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

        public bool useProjectionMatrix {
            get {
                return _useProjectionMatrix;
            }
            set {
                _useProjectionMatrix = value;
            }
        }

        public bool useCullingMatrix {
            get {
                return _useCullingMatrix;
            }
            set {
                _useCullingMatrix = value;
            }
        }

        RenderSettingsStruct _savedRenderSettings = new RenderSettingsStruct();
        RenderSettingsStruct _sceneRenderSettings = new RenderSettingsStruct();

        void Awake() {
            _camera = GetComponent<Camera>();
        }

        //public CameraEvent cameraEvent;
        //void OnValidate() {
        //    _camera.RemoveAllCommandBuffers();
        //    CommandBuffer buf = new CommandBuffer();
        //    buf.SetGlobalMatrix("UNITY_MATRIX_VP", Matrix4x4.identity);
        //    buf.SetGlobalMatrix("UNITY_MATRIX_MVP", Matrix4x4.identity);
        //    _camera.AddCommandBuffer(cameraEvent, buf);
            
        //}
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

        void OnDrawGizmosSelected() {
            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
            DrawFrustumGizmo(_camera.cullingMatrix);

            Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 1.0f); // Green 50%
            DrawFrustumGizmo(_camera.projectionMatrix * _camera.worldToCameraMatrix);
        }

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
    }
}