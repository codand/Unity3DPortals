using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR;

namespace Portals {

    internal class PortalRootCamera {
        public readonly Camera parent;
        public readonly Camera left;
        public readonly Camera right;


    }

    //[ExecuteInEditMode]
    public class Portal : MonoBehaviour {
        [SerializeField] private Portal _exitPortal;
        [SerializeField] private int _maxRecursiveDepth = 2;
        [SerializeField] private bool _fakeInfiniteRecursion = true;
        //[SerializeField] private float _clippingDistance = 0.0f;
        [SerializeField] private bool _useCullingMatrix = true;
        [SerializeField] private bool _useProjectionMatrix = true;

        public bool copyGI = false;

        public Portal exitPortal {
            get {
                return _exitPortal;
            }
            set {
                if (value) {
                    GetComponent<Renderer>().enabled = true;
                } else {
                    GetComponent<Renderer>().enabled = false;
                }
                _exitPortal = value;
            }
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

        // Used to prevent camera recusion in other portals
        // This is static because recursion can occur on more than one portal
        private static Portal _currentlyRenderingPortal;

        // Cached material
        private Material _portalMaterial;

        // Dictionary mapping objects to their clones on the other side of a portal
        private Dictionary<GameObject, GameObject> _objectToClone = new Dictionary<GameObject, GameObject>();

        void OnEnable() {
            if (!_portalMaterial) {
                _portalMaterial = new Material(Shader.Find("Portal/PortalRenderTexture"));
                _portalMaterial.name = "Runtime Material for " + this.gameObject.name;
                GetComponent<MeshRenderer>().sharedMaterial = _portalMaterial;
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
        //    //stack.Push(Camera.current.targetTexture);
        //}
        //void RestoreMaterialProperties() {
        //    //Debug.Log("Restoring texture: " + tex.name + " to material: " + _portalMaterial.name + " after camera render: " + Camera.current);
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

        //    if (!tex) {
        //        //Debug.Log("!tex");
        //        return;
        //    }
        //    //Debug.Log("Popped: " + tex.name);
        //    _portalMaterial.mainTexture = tex;
        //    //_portalMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
        //}

        //void Update() {
        //    _wasSeenByCamera.Clear();
        //    hasPopped = false;
        //}

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

        Stack<Texture> stack = new Stack<Texture>();
        void OnWillRenderObject() {
            //_wasSeenByCamera[Camera.current] = true;
            //SaveMaterialProperties();

            if (!enabled ||
                !GetComponent<Renderer>() ||
                !GetComponent<Renderer>().sharedMaterial ||
                !GetComponent<Renderer>().enabled) {
                return;
            }

            if (!exitPortal) {
                GetComponent<Renderer>().enabled = false;
                return;
            }

            Camera currentCam = Camera.current;
            if (currentCam == null) {
                return;
            }

            if (currentCam.name == "SceneCamera" || currentCam.name == "Reflection Probes Camera" || currentCam.name == "Preview Camera")
                return;

            if (s_depth > 0 && _currentlyRenderingPortal != null && this == _currentlyRenderingPortal.exitPortal) {
                return;
            }

            // Stop recursion when we reach maximum depth
            if (s_depth >= _maxRecursiveDepth) {

                if (_fakeInfiniteRecursion) {
                    //if (!_recursionCamera)
                    //    return;
                    if (_maxRecursiveDepth >= 2) {
                        // Render the bottom portal using _recursionCamera's view/projection.
                        PortalCamera pc = currentCam.GetComponent<PortalCamera>();
                        Camera parentCam = pc.parent;
                        GetComponent<Renderer>().sharedMaterial.EnableKeyword("SAMPLE_PREVIOUS_FRAME");

                        // TODO: Fix this up a bit nicer.
                        GetComponent<Renderer>().sharedMaterial.SetMatrix("PORTAL_MATRIX_VP", parentCam.projectionMatrix * pc.lastFrameWorldToCameraMatrix);
                        GetComponent<Renderer>().sharedMaterial.SetTexture("_MainTex", parentCam.targetTexture);
                        pc.lastFrameWorldToCameraMatrix = parentCam.worldToCameraMatrix;
                    } else {
                        GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
                    }
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

            Debug.Log("Rendering1: " + new string('*', s_depth) + gameObject.name + _portalMaterial.GetTexture("_RightEyeTexture").name);

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
            }

            s_depth--;

            Debug.Log("Rendering2: " + new string('*', s_depth) + gameObject.name + _portalMaterial.GetTexture("_RightEyeTexture").name);
            _currentlyRenderingPortal = parentPortal;

            if (s_depth < _maxRecursiveDepth) {
                GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            }
            //Debug.Log("Pushing: " + portalCam.targetTexture);
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
            float exitScale = Helpers.VectorInternalAverage(exitPortal.transform.lossyScale);

            return exitScale / enterScale;
        }

        public Quaternion WorldToPortalQuaternion() {
            // Transforms a quaternion or vector into the second portal's space.
            // We have to flip the camera in between so that we face the outside direction of the portal
            return exitPortal.transform.rotation * Quaternion.Euler(180, 0, 180) * Quaternion.Inverse(this.transform.rotation);
        }

        public Matrix4x4 WorldToPortalMatrix() {
            Vector3 translation = exitPortal.transform.position - this.transform.position;
            //Vector3 translation = Vector3.zero;
            Quaternion rotation = exitPortal.transform.rotation * Quaternion.Inverse(this.transform.rotation);
            //Quaternion rotation = WorldToPortalQuaternion();
            //Quaternion rotation = Quaternion.identity;
            //Debug.Log(translation);
            Vector3 scale = new Vector3(1f, 1f, -1f); // the last negative scale makes it point in the right direction

            return Matrix4x4.TRS(translation, rotation, scale).inverse;
        }

        public Vector3 MultiplyPoint(Vector3 point) {
            Vector3 positionDelta = point - this.transform.position;
            Vector3 scaledPositionDelta = positionDelta * GetScaleMultiplier();
            Vector3 transformedDelta = WorldToPortalQuaternion() * scaledPositionDelta;

            return exitPortal.transform.position + transformedDelta;
        }

        public void ApplyWorldToPortalTransform(Transform target, Transform reference) {
            Quaternion worldToPortal = WorldToPortalQuaternion();

            // Scale
            float scale = GetScaleMultiplier();

            // Translate
            Vector3 positionDelta = reference.position - this.transform.position;
            Vector3 scaledPositionDelta = positionDelta * scale;
            Vector3 transformedDelta = worldToPortal * scaledPositionDelta;

            target.position = exitPortal.transform.position + transformedDelta;
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

            target.position = exitPortal.transform.position + transformedDelta;
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
            dest.hdr = src.hdr;

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
                pc.enterScene = this.gameObject.scene;
                pc.exitScene = exitPortal.gameObject.scene;
                pc.copyGI = true;
                pc.parent = currentCamera;
                pc.portal = this;

                if (this.gameObject.scene != exitPortal.gameObject.scene) {
                    //PortalCameraRenderSettings thing = go.AddComponent<PortalCameraRenderSettings>();
                    //thing.scene = exitPortal.gameObject.scene;
                }

                _camToPortalCam[currentCamera] = portalCamera;

                //CommandBuffer buf = new CommandBuffer();
                //buf.name = "Copy GBuffer";

                //int depthbuf = Shader.PropertyToID("_Depth");
                //int gbuf0 = Shader.PropertyToID("_GBuf0");
                //int gbuf1 = Shader.PropertyToID("_GBuf1");
                //int gbuf2 = Shader.PropertyToID("_GBuf2");
                //int gbuf3 = Shader.PropertyToID("_GBuf3");

                //buf.ReleaseTemporaryRT(depthbuf);
                //buf.ReleaseTemporaryRT(gbuf0);
                //buf.ReleaseTemporaryRT(gbuf1);
                //buf.ReleaseTemporaryRT(gbuf2);
                //buf.ReleaseTemporaryRT(gbuf3);

                //buf.GetTemporaryRT(depthbuf, portalCamera.pixelWidth, portalCamera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Default);
                //buf.GetTemporaryRT(gbuf0, portalCamera.pixelWidth, portalCamera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                //buf.GetTemporaryRT(gbuf1, portalCamera.pixelWidth, portalCamera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                //buf.GetTemporaryRT(gbuf2, portalCamera.pixelWidth, portalCamera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Default);
                //buf.GetTemporaryRT(gbuf3, portalCamera.pixelWidth, portalCamera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Default);

                //buf.Blit(BuiltinRenderTextureType.ResolvedDepth, depthbuf);
                //buf.Blit(BuiltinRenderTextureType.GBuffer0, gbuf0);
                //buf.Blit(BuiltinRenderTextureType.GBuffer1, gbuf1);
                //buf.Blit(BuiltinRenderTextureType.GBuffer2, gbuf2);
                //buf.Blit(BuiltinRenderTextureType.GBuffer3, gbuf3);

                //buf.SetGlobalTexture("_Depth", depthbuf);
                //buf.SetGlobalTexture("_GBuf0", gbuf0);
                //buf.SetGlobalTexture("_GBuf1", gbuf1);
                //buf.SetGlobalTexture("_GBuf2", gbuf2);
                //buf.SetGlobalTexture("_GBuf3", gbuf3);

                //portalCamera.AddCommandBuffer(CameraEvent.AfterEverything, buf);
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
            if (!exitPortal) {
                return;
            }

            foreach (Collider other in ignoredColliders) {
                Physics.IgnoreCollision(collider, other, true);
            }
            foreach (Collider other in exitPortal.ignoredColliders) {
                Physics.IgnoreCollision(collider, other, true);
            }
            //SpawnClone(collider.gameObject);
        }

        void OnTriggerStay(Collider collider) {
            if (!exitPortal) {
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
            if (!exitPortal) {
                return;
            }

            // Restore collisions with the back of the portal doorway
            foreach (Collider other in ignoredColliders) {
                Physics.IgnoreCollision(collider, other, false);
            }
            foreach (Collider other in exitPortal.ignoredColliders) {
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
            if (!Camera.main) {
                return;
            }
            Camera cam;
            _camToPortalCam.TryGetValue(Camera.main, out cam);
            if (cam) {
                if (_useCullingMatrix) {
                    Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                    DrawFrustumGizmo(cam.cullingMatrix);
                }
                if (_useProjectionMatrix) {
                    // TODO: ... I don't know, this is ugly
                    //Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 1.0f); // Green 50%
                    //DrawFrustumGizmo(cam.projectionMatrix * cam.worldToCameraMatrix);
                }
            }
        }

        void OnDrawGizmos() {
            if (exitPortal) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(this.transform.position, exitPortal.transform.position);
            }
        }
    }
}
