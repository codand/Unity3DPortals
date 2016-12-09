using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portals {
    //[ExecuteInEditMode]
    public class Portal : MonoBehaviour {
        [SerializeField] private Portal _exitPortal;
        [SerializeField] private int _maxRecursiveDepth = 2;
        [SerializeField] private float _clippingDistance = 0.0f;
        [SerializeField] private Collider _backface;
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

        Stack<Texture> stack = new Stack<Texture>();
        void OnWillRenderObject() {
            //Debug.Log(Camera.current.name + " will render: " + gameObject.name);
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
                //stack.Push(null);
                return;
            }

            // Initialize or get anything needed for this depth level
            Camera portalCam = null;
            CreatePortalObjects(currentCam, out portalCam);
            portalCam.GetComponent<PortalCamera>().renderDepth = s_depth + 1;

            // Reset camera values to match the parent cam
            UpdateCameraModes(currentCam, portalCam);

            // Adjust camera transform
            ApplyWorldToPortalTransform(portalCam.transform, currentCam.transform);

            // Adjust camera projection matrix so that the clipping plane aligns with the exit portal
            if (_useProjectionMatrix) {
                Vector4 clippingPlane = GetTransformPlane(exitPortal.transform);
                ApplyCustomProjectionMatrix(portalCam, clippingPlane, Mathf.Pow(this.GetScaleMultiplier(), s_depth + 1));
            } else {
                portalCam.ResetProjectionMatrix();
            }
            if (_useCullingMatrix) {
                ApplyCustomClippingFrustum(portalCam);
            } else {
                portalCam.ResetCullingMatrix();
            }

            // Release the target texture from the previous frame
            if (portalCam.targetTexture) {
                RenderTexture.ReleaseTemporary(portalCam.targetTexture);
            }
            // Create a temporary RenderTexture for our current depth to render to.
            // This will stick around until the next frame.
            RenderTextureFormat fmt = portalCam.hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            RenderTexture temp = RenderTexture.GetTemporary(currentCam.pixelWidth, currentCam.pixelHeight, 24, fmt);
            temp.name = "tex-" + portalCam.name;
            portalCam.targetTexture = temp;
            

            //if (portalCam.targetTexture == null) {
            //    portalCam.targetTexture = new RenderTexture(currentCam.pixelWidth, currentCam.pixelHeight, 24, RenderTextureFormat.Default);
            //}

            // For simultaneous recurisve rendering of nearby portals
            //stack.Push(portalCam.targetTexture);



            Portal parentPortal = _currentlyRenderingPortal;
            _currentlyRenderingPortal = this;

            s_depth++;
            portalCam.Render();
            s_depth--;

                
            _currentlyRenderingPortal = parentPortal;



            _portalMaterial.SetTexture("_MainTex", portalCam.targetTexture);
            //_portalMaterial.mainTexture = null;
            if (s_depth < _maxRecursiveDepth) {
                GetComponent<Renderer>().sharedMaterial.DisableKeyword("SAMPLE_PREVIOUS_FRAME");
            }
            //Debug.Log("Pushing: " + portalCam.targetTexture);

            //Debug.Log("OnWillRenderObject END  : " + gameObject.name);

        }

        public static Vector4 GetTransformPlane(Transform trans) {
            Vector3 normal = trans.forward;
            float d = Vector3.Dot(normal, trans.position);
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, d);
            return plane;
        }

        void ApplyCustomProjectionMatrix(Camera camera, Vector4 plane, float scale) {
            // Restore original projection matrix
            camera.ResetProjectionMatrix();

            bool onFarSide = new Plane(-1 * plane, plane.w + _clippingDistance).GetSide(camera.transform.position);
            if (onFarSide) {
                return;
            }

            // Project our world space clipping plane to the camera's local coordinates
            // e.g. normal (0, 0, 1) becomes (1, 0, 0) if we're looking left parallel to the plane
            Vector4 transformedNormal = camera.transform.InverseTransformDirection(plane).normalized;
            Vector4 transformedPoint = camera.transform.InverseTransformPoint(plane.w * plane);

            // Calculate the d value for our plane by projecting our transformed point
            // onto our transformed normal vector.
            float projectedDistance = Vector4.Dot(transformedNormal, transformedPoint);
            projectedDistance = projectedDistance * scale + _clippingDistance;

            // Not sure why x and y have to be negative. 
            Vector4 transformedPlane = new Vector4(-transformedNormal.x, -transformedNormal.y, transformedNormal.z, projectedDistance);

            // Reassign to camera
            camera.projectionMatrix = camera.CalculateObliqueMatrix(transformedPlane);

            //Matrix4x4 projectionMatrix = camera.projectionMatrix;
            //CalculateObliqueMatrix(ref projectionMatrix, transformedPlane);
            //camera.projectionMatrix = projectionMatrix;
        }

        //private static void CalculateObliqueMatrix(ref Matrix4x4 projection, Vector4 clipPlane) {
        //    Vector4 q = projection.inverse * new Vector4(
        //        Mathf.Sign(clipPlane.x),
        //        Mathf.Sign(clipPlane.y),
        //        1.0f,
        //        1.0f
        //    );
        //    Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
        //    // third row = clip plane - fourth row
        //    projection[2] = c.x - projection[3];
        //    projection[6] = c.y - projection[7];
        //    projection[10] = c.z - projection[11];
        //    projection[14] = c.w - projection[15];
        //}

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

        void ApplyCustomClippingFrustum(Camera camera) {
            // Note to future self: This works as is, but it does not handle the clipping plane unfortunately.
            // For right now, I'm going to turn this off, but it should be enabled in the future.

            Bounds bounds = exitPortal.GetComponent<MeshFilter>().sharedMesh.bounds;

            // Lower left of the backside of our plane in world coordinates
            Vector3 pa = exitPortal.transform.TransformPoint(new Vector3(bounds.extents.x, -bounds.extents.y, 0));

            // Lower right
            Vector3 pb = exitPortal.transform.TransformPoint(new Vector3(-bounds.extents.x, -bounds.extents.y, 0));

            // Upper left
            Vector3 pc = exitPortal.transform.TransformPoint(new Vector3(bounds.extents.x, bounds.extents.y, 0));

            Vector3 pe = camera.transform.position;

            // Calculate what our horizontal field of view would be with off-axis projection.
            // If this fov is greater than our camera's fov, we should just use the camera's default projection
            // matrix instead. Otherwise, the frustum's fov will approach 180 degrees (way too large).
            Vector3 camToLowerLeft = pa - camera.transform.position;
            camToLowerLeft.y = 0;
            Vector3 camToLowerRight = pb - camera.transform.position;
            camToLowerRight.y = 0;
            float fieldOfView = Vector3.Angle(camToLowerLeft, camToLowerRight);
            if (fieldOfView > camera.fieldOfView) {
                camera.ResetCullingMatrix();
            } else {
                camera.cullingMatrix = CalculateOffAxisProjectionMatrix(camera, pa, pb, pc, pe);
            }
        }

        Matrix4x4 CalculateOffAxisProjectionMatrix(Camera camera, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 pe) {
            // eye position
            float n = camera.nearClipPlane;
            // distance of near clipping plane
            float f = camera.farClipPlane;
            // distance of far clipping plane

            Vector3 va; // from pe to pa
            Vector3 vb; // from pe to pb
            Vector3 vc; // from pe to pc
            Vector3 vr; // right axis of screen
            Vector3 vu; // up axis of screen
            Vector3 vn; // normal vector of screen

            float l; // distance to left screen edge
            float r; // distance to right screen edge
            float b; // distance to bottom screen edge
            float t; // distance to top screen edge
            float d; // distance from eye to screen 

            vr = pb - pa;
            vu = pc - pa;
            va = pa - pe;
            vb = pb - pe;
            vc = pc - pe;

            // are we looking at the backface of the plane object?
            if (Vector3.Dot(-Vector3.Cross(va, vc), vb) < 0.0) {
                // mirror points along the z axis (most users 
                // probably expect the x axis to stay fixed)
                vu = -vu;
                pa = pc;
                pb = pa + vr;
                pc = pa + vu;
                va = pa - pe;
                vb = pb - pe;
                vc = pc - pe;
            }

            vr.Normalize();
            vu.Normalize();
            vn = -Vector3.Cross(vr, vu);
            // we need the minus sign because Unity 
            // uses a left-handed coordinate system
            vn.Normalize();

            d = -Vector3.Dot(va, vn);

            // Set near clip plane
            n = d; // + _clippingDistance;
            //camera.nearClipPlane = n;

            l = Vector3.Dot(vr, va) * n / d;
            r = Vector3.Dot(vr, vb) * n / d;
            b = Vector3.Dot(vu, va) * n / d;
            t = Vector3.Dot(vu, vc) * n / d;

            Matrix4x4 p = new Matrix4x4(); // projection matrix 
            p[0, 0] = 2.0f * n / (r - l);
            p[0, 1] = 0.0f;
            p[0, 2] = (r + l) / (r - l);
            p[0, 3] = 0.0f;

            p[1, 0] = 0.0f;
            p[1, 1] = 2.0f * n / (t - b);
            p[1, 2] = (t + b) / (t - b);
            p[1, 3] = 0.0f;

            p[2, 0] = 0.0f;
            p[2, 1] = 0.0f;
            p[2, 2] = (f + n) / (n - f);
            p[2, 3] = 2.0f * f * n / (n - f);

            p[3, 0] = 0.0f;
            p[3, 1] = 0.0f;
            p[3, 2] = -1.0f;
            p[3, 3] = 0.0f;

            Matrix4x4 rm = new Matrix4x4(); // rotation matrix;
            rm[0, 0] = vr.x;
            rm[0, 1] = vr.y;
            rm[0, 2] = vr.z;
            rm[0, 3] = 0.0f;

            rm[1, 0] = vu.x;
            rm[1, 1] = vu.y;
            rm[1, 2] = vu.z;
            rm[1, 3] = 0.0f;

            rm[2, 0] = vn.x;
            rm[2, 1] = vn.y;
            rm[2, 2] = vn.z;
            rm[2, 3] = 0.0f;

            rm[3, 0] = 0.0f;
            rm[3, 1] = 0.0f;
            rm[3, 2] = 0.0f;
            rm[3, 3] = 1.0f;

            Matrix4x4 tm = new Matrix4x4(); // translation matrix;
            tm[0, 0] = 1.0f;
            tm[0, 1] = 0.0f;
            tm[0, 2] = 0.0f;
            tm[0, 3] = -pe.x;

            tm[1, 0] = 0.0f;
            tm[1, 1] = 1.0f;
            tm[1, 2] = 0.0f;
            tm[1, 3] = -pe.y;

            tm[2, 0] = 0.0f;
            tm[2, 1] = 0.0f;
            tm[2, 2] = 1.0f;
            tm[2, 3] = -pe.z;

            tm[3, 0] = 0.0f;
            tm[3, 1] = 0.0f;
            tm[3, 2] = 0.0f;
            tm[3, 3] = 1.0f;

            Matrix4x4 worldToCameraMatrix = rm * tm;
            return p * worldToCameraMatrix;
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