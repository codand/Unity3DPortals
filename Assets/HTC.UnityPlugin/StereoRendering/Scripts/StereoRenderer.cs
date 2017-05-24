//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace HTC.UnityPlugin.StereoRendering
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class StereoRenderer : MonoBehaviour
    {
        #region variables

        //--------------------------------------------------------------------------------
        // getting/setting stereo anchor pose

        public Transform canvasOrigin;
        [SerializeField]
        private Vector3 m_canvasOriginWorldPosition = new Vector3(0.0f, 0.0f, 0.0f);
        [SerializeField]
        private Vector3 m_canvasOriginWorldRotation = new Vector3(0.0f, 0.0f, 0.0f);

        public Vector3 canvasOriginPos
        {
            get
            {
                if (canvasOrigin == null) { return m_canvasOriginWorldPosition; }
                return canvasOrigin.position;
            }
            set
            {
                m_canvasOriginWorldPosition = value;
            }
        }

        public Vector3 canvasOriginEuler
        {
            get
            {
                if (canvasOrigin == null) { return m_canvasOriginWorldRotation; }
                return canvasOrigin.eulerAngles;
            }
            set
            {
                m_canvasOriginWorldRotation = value;
            }
        }

        public Quaternion canvasOriginRot
        {
            get { return Quaternion.Euler(canvasOriginEuler); }
            set { canvasOriginEuler = value.eulerAngles; }
        }

        public Vector3 canvasOriginForward
        {
            get { return canvasOriginRot * Vector3.forward; }
        }

        public Vector3 canvasOriginUp
        {
            get { return canvasOriginRot * Vector3.up; }
        }

        public Vector3 canvasOriginRight
        {
            get { return canvasOriginRot * Vector3.right; }
        }

        public Vector3 localCanvasOriginPos
        {
            get { return transform.InverseTransformPoint(canvasOriginPos); }
            set { canvasOriginPos = transform.InverseTransformPoint(value); }
        }

        public Vector3 localCanvasOriginEuler
        {
            get { return (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(canvasOriginEuler)).eulerAngles; }
            set { canvasOriginEuler = (transform.rotation * Quaternion.Euler(value)).eulerAngles; }
        }

        public Quaternion localCanvasOriginRot
        {
            get { return Quaternion.Inverse(transform.rotation) * canvasOriginRot; }
            set { canvasOriginRot = transform.rotation * value; }
        }

        //--------------------------------------------------------------------------------
        // getting/setting stereo anchor pose

        public Transform anchorTransform;
        [SerializeField]
        private Vector3 m_anchorWorldPosition = new Vector3(0.0f, 0.0f, 0.0f);
        [SerializeField]
        private Vector3 m_anchorWorldRotation = new Vector3(0.0f, 0.0f, 0.0f);

        public Vector3 anchorPos
        {
            get
            {
                if (anchorTransform == null) { return m_anchorWorldPosition; }
                return anchorTransform.position;
            }
            set
            {
                m_anchorWorldPosition = value;
            }
        }

        public Vector3 anchorEuler
        {
            get
            {
                if (anchorTransform == null) { return m_anchorWorldRotation; }
                return anchorTransform.eulerAngles;
            }
            set
            {
                m_anchorWorldRotation = value;
            }
        }

        public Quaternion anchorRot
        {
            get { return Quaternion.Euler(anchorEuler); }
            set { anchorEuler = value.eulerAngles; }
        }

        //--------------------------------------------------------------------------------
        // other variables

        // flags
        private bool canvasVisible = false;
        public bool shouldRender = true;

        // the camera rig that represents HMD
        private GameObject mainCameraParent;
        private Camera mainCameraEye;

        // camera rig for stereo rendering, which is on the object this component attached to
        public GameObject stereoCameraHead = null;
        public Camera stereoCameraEye = null;

        // render texture for stereo rendering
        private RenderTexture leftEyeTexture = null;
        private RenderTexture rightEyeTexture = null;

        public float textureResolutionScale = 1.0f;

        // the materials for displaying render result
        private Material stereoMaterial;

        // list of objects that should be ignored when rendering
        [SerializeField]
        private List<GameObject> ignoreWhenRender = new List<GameObject>();

        // for mirror rendering
        public bool isMirror = false;
        private Matrix4x4 reflectionMat;

        // for callbacks
        private Action preRenderListeners;
        private Action postRenderListeners;

        #endregion

        /////////////////////////////////////////////////////////////////////////////////
        // initialization

        private void Start()
        {
            // don't initialize anything if is in editor and not playing
            if (IsEditing())
                return;

            // initialize parameter factory
            StereoRenderManager.Instance.InitParamFactory();

            // initialize stereo camera rig
            if (stereoCameraHead == null)
                CreateStereoCameraRig();

            // FIX Unity 5.4 shadow bug (issue trakcer ID 686520) ++
            #if UNITY_5_4
                GraphicsSettings.SetCustomShader(BuiltinShaderType.ScreenSpaceShadows, Shader.Find("ScreenSpaceShadows-ForCustomPerspectiveMat"));
            #endif
            // FIX Unity 5.4 shadow bug (issue trakcer ID 686520) --

            // swap correct stereo shader for different unity versions; also create stereo material if nothing is there
            SwapStereoShader();

            // create render textures as target of stereo rendering
            CreateRenderTextures(StereoRenderManager.Instance.paramFactory.GetRenderWidth(), StereoRenderManager.Instance.paramFactory.GetRenderHeight());

            // get main camera and registor to StereoRenderManager
            StereoRenderManager.Instance.AddToManager(this);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            // initialize canvas origin using mesh transform
            canvasOrigin = transform;
            canvasOriginPos = transform.position;
            canvasOriginEuler = transform.eulerAngles;

            if (stereoCameraHead == null)
            CreateStereoCameraRig();

            // if only one material, substitute it with stereo render material
            Renderer renderer = GetComponent<Renderer>();
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 1)
            {
                renderer.sharedMaterial = (Material)Resources.Load("StereoRenderMaterial", typeof(Material));
            }
        }
#endif

        private void OnDestroy()
        {
            if (IsEditing())
            {
                DestroyImmediate(stereoCameraHead);
            }
            else if (Application.isPlaying)
            {
                StereoRenderManager.Instance.RemoveFromManager(this);
            }
        }

        private void CreateStereoCameraRig()
        {
            stereoCameraHead = new GameObject("Stereo Camera Head [" + gameObject.name + "]");
            stereoCameraHead.transform.parent = transform;

            GameObject stereoCameraEyeObj = new GameObject("Stereo Camera Eye [" + gameObject.name + "]");
            stereoCameraEyeObj.transform.parent = stereoCameraHead.transform;
            stereoCameraEye = stereoCameraEyeObj.AddComponent<Camera>();
            stereoCameraEye.enabled = false;
        }

        private void SwapStereoShader()
        {
            // swap correct shader for different unity versions
            Renderer renderer = GetComponent<Renderer>();
            Material[] materialList = renderer.materials;

            int i = 0;
            for (i = 0; i < materialList.Length; i++)
            {
                Material mt = materialList[i];
                if (mt.shader.name == "Custom/StereoRenderShader" || mt.shader.name == "Custom/StereoRenderShader_5_3")
                {
                    stereoMaterial = mt;

                    #if UNITY_5_4_OR_NEWER
                        renderer.materials[i].shader = Shader.Find("Custom/StereoRenderShader");
                    #else
                        renderer.materials[i].shader = Shader.Find("Custom/StereoRenderShader_5_3");
                    #endif

                    break;
                }
            }

            // if couldn't find stereo material, replace first material
            if (i == materialList.Length)
            {
                renderer.sharedMaterial = (Material)Resources.Load("StereoRenderMaterial", typeof(Material));

                #if UNITY_5_4_OR_NEWER
                    renderer.materials[0].shader = Shader.Find("Custom/StereoRenderShader");
                #else
                    renderer.materials[0].shader = Shader.Find("Custom/StereoRenderShader_5_3");
                #endif

                stereoMaterial = renderer.materials[0];
            }
        }

        private void CreateRenderTextures(int sceneWidth, int sceneHeight, int aaLevel = 4)
        {
            int depth = 24;
            int w = (int)(textureResolutionScale * sceneWidth);
            int h = (int)(textureResolutionScale * sceneHeight);

            leftEyeTexture = new RenderTexture(w, h, depth);
            leftEyeTexture.antiAliasing = aaLevel;

            #if UNITY_5_4_OR_NEWER
                rightEyeTexture = new RenderTexture(w, h, depth);
                rightEyeTexture.antiAliasing = aaLevel;
            #endif
        }

        public void InitMainCamera(GameObject parent, Camera cam)
        {
            mainCameraParent = parent;
            mainCameraEye = cam;
        }

        /////////////////////////////////////////////////////////////////////////////////
        // support moving mirrors

        private void Update()
        {
            if (isMirror)
            {
                anchorPos = canvasOriginPos;
                anchorRot = canvasOriginRot;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////
        // visibility and rendering

        private void OnWillRenderObject()
        {
            if (Camera.current == mainCameraEye)
            {
                canvasVisible = true;
            }
        }

        public void Render()
        {
            // move stereo camera around based on HMD pose
            MoveStereoCameraBasedOnHmdPose();

            // invoke pre-render events
            if (preRenderListeners != null)
                preRenderListeners.Invoke();

            if (canvasVisible)
            {
                // disable specified objects
                for (int i = 0; i < ignoreWhenRender.Count; i++)
                    ignoreWhenRender[i].SetActive(false);

                // invert backface culling when rendering a mirror
                if (isMirror)
                    GL.invertCulling = true;

                // render the canvas
                #if UNITY_5_4_OR_NEWER
                    RenderWithTwoTextures();
                #else
                    RenderWithOneTexture();
                #endif

                // reset backface culling
                if (isMirror)
                    GL.invertCulling = false;

                // re-activate specified objects
                for (int i = 0; i < ignoreWhenRender.Count; i++)
                    ignoreWhenRender[i].SetActive(true);

                // finish this render pass, reset visibility
                canvasVisible = false;
            }

            // invoke post-render events
            if (postRenderListeners != null)
                postRenderListeners.Invoke();
        }

        public void MoveStereoCameraBasedOnHmdPose()
        {
            #if UNITY_5_4_OR_NEWER
                Vector3 mainCamPos = mainCameraEye.transform.position;
                Quaternion mainCamRot = mainCameraEye.transform.rotation;
            #else
                Vector3 mainCamPos = mainCameraParent.transform.position;
                Quaternion mainCamRot = mainCameraParent.transform.rotation;
            #endif

            if (isMirror)
            {
                // get reflection plane -- assume +y as normal
                float offset = 0.07f;
                float d = -Vector3.Dot(canvasOriginUp, canvasOriginPos) - offset;
                Vector4 reflectionPlane = new Vector4(canvasOriginUp.x, canvasOriginUp.y, canvasOriginUp.z, d);

                // get reflection matrix
                reflectionMat = Matrix4x4.zero;
                CalculateReflectionMatrix(ref reflectionMat, reflectionPlane);

                // set head position
                Vector3 reflectedPos = reflectionMat.MultiplyPoint(mainCamPos);
                stereoCameraHead.transform.position = reflectedPos;

                // set head orientation
                stereoCameraHead.transform.rotation = mainCamRot;
            }
            else
            {
                Vector3 posCanvasToMainCam = mainCamPos - canvasOriginPos;

                // compute the rotation between the portal entry and the portal exit
                Quaternion rotCanvasToAnchor = anchorRot * Quaternion.Inverse(canvasOriginRot);

                // move remote camera position
                Vector3 posAnchorToStereoCam = rotCanvasToAnchor * posCanvasToMainCam;
                stereoCameraHead.transform.position = anchorPos + posAnchorToStereoCam;

                // rotate remote camera
                stereoCameraHead.transform.rotation = rotCanvasToAnchor * mainCamRot;
            }
        }

        private void RenderWithTwoTextures()
        {
            float stereoSeparation = 2.0f * Mathf.Abs(StereoRenderManager.Instance.paramFactory.GetEyeLocalPosition(0).x);

            // left eye rendering -------------------------------------------------------------------
            int curEye = 0;

            // set eye pose
            stereoCameraEye.transform.localPosition = StereoRenderManager.Instance.paramFactory.GetEyeLocalPosition(curEye);
            stereoCameraEye.transform.localRotation = StereoRenderManager.Instance.paramFactory.GetEyeLocalRotation(curEye);

            // set view matrix for mirrors
            if (isMirror)
            {
                Matrix4x4 worldToCamera = mainCameraEye.worldToCameraMatrix;
                stereoCameraEye.worldToCameraMatrix = worldToCamera * reflectionMat;
            }

            // set projection matrix
            stereoCameraEye.projectionMatrix = StereoRenderManager.Instance.paramFactory.GetProjectionMatrix(curEye, stereoCameraEye.nearClipPlane, stereoCameraEye.farClipPlane);

            // render
            stereoCameraEye.targetTexture = leftEyeTexture;
            stereoCameraEye.Render();
            stereoMaterial.SetTexture("_LeftEyeTexture", leftEyeTexture);

            // right eye rendering -------------------------------------------------------------------
            curEye = 1;

            // set eye pose
            stereoCameraEye.transform.localPosition = StereoRenderManager.Instance.paramFactory.GetEyeLocalPosition(curEye);
            stereoCameraEye.transform.localRotation = StereoRenderManager.Instance.paramFactory.GetEyeLocalRotation(curEye);

            // set view matrix for mirrors
            if (isMirror)
            {
                Matrix4x4 worldToCamera = mainCameraEye.worldToCameraMatrix;
                worldToCamera.m03 -= stereoSeparation;
                stereoCameraEye.worldToCameraMatrix = worldToCamera * reflectionMat;
            }

            // set projection matrix
            stereoCameraEye.projectionMatrix = StereoRenderManager.Instance.paramFactory.GetProjectionMatrix(curEye, stereoCameraEye.nearClipPlane, stereoCameraEye.farClipPlane);

            // render
            stereoCameraEye.targetTexture = rightEyeTexture;
            stereoCameraEye.Render();
            stereoMaterial.SetTexture("_RightEyeTexture", rightEyeTexture);
        }

        private void RenderWithOneTexture()
        {
            // move remote camera eye to the corresponding position of the main camera eye
            // note that the main camera head pose has been syncronized with remote camera head
            Vector3 vectorHeadToEye = mainCameraEye.transform.position - mainCameraParent.transform.position;
            Quaternion rotCanvasToAnchor = anchorRot * Quaternion.Inverse(canvasOriginRot);

            // sync projection matrix from main camera
            stereoCameraEye.transform.position = stereoCameraHead.transform.position + rotCanvasToAnchor * vectorHeadToEye;
            stereoCameraEye.projectionMatrix = mainCameraEye.projectionMatrix;

            // render current eye
            stereoCameraEye.targetTexture = leftEyeTexture;
            stereoMaterial.SetTexture("_MainTexture", leftEyeTexture);
            stereoCameraEye.Render();
        }

        /////////////////////////////////////////////////////////////////////////////////
        // callbacks and utilities

        public void AddPreRenderListener(Action listener)
        {
            if (listener == null) { return; }
            preRenderListeners += listener;
        }

        public void AddPostRenderListener(Action listener)
        {
            if (listener == null) { return; }
            postRenderListeners += listener;
        }

        public void RemovePreRenderListener(Action listener)
        {
            if (listener == null) { return; }
            preRenderListeners -= listener;
        }

        public void RemovePostRenderListener(Action listener)
        {
            if (listener == null) { return; }
            postRenderListeners -= listener;
        }

        public bool IsEditing()
        {
            return Application.isEditor && !Application.isPlaying;
        }

        public void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 normal)
        {
            reflectionMat.m00 = (1.0f - 2.0f * normal[0] * normal[0]);
            reflectionMat.m01 = (-2.0f * normal[0] * normal[1]);
            reflectionMat.m02 = (-2.0f * normal[0] * normal[2]);
            reflectionMat.m03 = (-2.0f * normal[3] * normal[0]);

            reflectionMat.m10 = (-2.0f * normal[1] * normal[0]);
            reflectionMat.m11 = (1.0f - 2.0f * normal[1] * normal[1]);
            reflectionMat.m12 = (-2.0f * normal[1] * normal[2]);
            reflectionMat.m13 = (-2.0f * normal[3] * normal[1]);

            reflectionMat.m20 = (-2.0f * normal[2] * normal[0]);
            reflectionMat.m21 = (-2.0f * normal[2] * normal[1]);
            reflectionMat.m22 = (1.0f - 2.0f * normal[2] * normal[2]);
            reflectionMat.m23 = (-2.0f * normal[3] * normal[2]);

            reflectionMat.m30 = 0.0f;
            reflectionMat.m31 = 0.0f;
            reflectionMat.m32 = 0.0f;
            reflectionMat.m33 = 1.0f;
        }
    }
}