//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using UnityEngine.VR;
using System;
using System.Collections.Generic;

namespace HTC.UnityPlugin.StereoRendering
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class StereoRenderManager : MonoBehaviour
    {
        // singleton
        private static StereoRenderManager instance = null;

        // flags
        private static bool isApplicationQuitting = false;

        // device types
        public HmdType hmdType;

        // factory for device-specific things
        public IDeviceParamFactory paramFactory;

        // the camera that represents HMD
        private static GameObject mainCameraParent;
        private static Camera mainCamera;

        // all current stereo renderers
        public List<StereoRenderer> stereoRendererList = new List<StereoRenderer>();

        // for callbacks
        private Action preRenderListeners;
        private Action postRenderListeners;

        /////////////////////////////////////////////////////////////////////////////////
        // initialization

        // whehter we have initialized the singleton
        public static bool Active { get { return instance != null; } }

        // singleton interface
        public static StereoRenderManager Instance
        {
            get
            {
                Initialize();
                return instance;
            }
        }

        private static void Initialize()
        {
            if (Active || isApplicationQuitting) { return; }

            // try to get existing manager
            var instances = FindObjectsOfType<StereoRenderManager>();
            if (instances.Length > 0)
            {
                instance = instances[0];
                if (instances.Length > 1) { Debug.LogError("Multiple StereoRenderManager is not supported."); }
            }

            // pop warning if no VR device detected
            #if UNITY_5_4_OR_NEWER
                if (!VRSettings.enabled) { Debug.LogError("VR is not enabled for this application."); }
            #endif

            // try to get HMD camera
            Camera mainCam = GetHmdCamera();
            if (mainCam == null) { return; }
            if (mainCam.transform.parent == null)
            {
                Debug.LogError("HMD Camera is not in proper hierarchy. You need a \"rig\" object as its parent.");
                return;
            }

            // if no exsiting instance, attach a new one to HMD camera
            if (!Active)
            {
                instance = mainCam.gameObject.AddComponent<StereoRenderManager>();
            }

            // record camera components
            if (Active)
            {
                mainCamera = mainCam;
                mainCameraParent = mainCam.transform.parent.gameObject;
            }
        }

        private static Camera GetHmdCamera()
        {
            Camera target = null;

            // if user has attached the script to an gameobject, try to get camera component attached to it
            if (Active)
            {
                target = instance.gameObject.GetComponent<Camera>();
                if (target == null) { Debug.LogError("You attached StereoRenderManager to an object without camera component!"); }
            }
            // otherwise try to get main camera
            else
            {
                var steamCam = FindObjectOfType<SteamVR_Camera>();

                // if SteamVR Unity plugin is used, attach manager to its camera
                if (steamCam != null)
                {
                    target = steamCam.gameObject.GetComponent<Camera>();
                }
#if UNITY_5_3
                else
                {
                    Debug.LogError("Need SteamVR_Camera for Unity 5.3.");
                    return null;
                }
#else
                // otherwise attch manager to main camera which is controlled by unity to present to HMD
                else
                {
                    target = Camera.main;
                    if (target == null) { Debug.LogError("No Camera tagged as \"MainCamera\" found."); }
                }
#endif
            }

            return target;
        }

        public void InitParamFactory()
        {
            // if not yet initialized
            if (paramFactory == null)
            {
                // get device type
                hmdType = StereoRenderDevice.GetHmdType();

                // create parameter factory
                paramFactory = StereoRenderDevice.InitParamFactory(hmdType);
                if (paramFactory == null)
                {
                    Debug.LogError("Current VR device is unsupported.");
                }
            }
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        /////////////////////////////////////////////////////////////////////////////////
        // render related

        void OnPreRender()
        {
            // invoke global pre-StereoRender events
            if (preRenderListeners != null)
                preRenderListeners.Invoke();

            // render registored stereo cameras
            for (int renderIter = 0; renderIter < stereoRendererList.Count; renderIter++)
            {
                StereoRenderer stereoRenderer = stereoRendererList[renderIter];

                if (stereoRenderer.shouldRender)
                {
                    stereoRenderer.Render();
                }
            }

            // invoke global post-StereoRender events
            if (postRenderListeners != null)
                postRenderListeners.Invoke();
        }

        /////////////////////////////////////////////////////////////////////////////////
        // callbacks

        public void AddToManager(StereoRenderer stereoRenderer)
        {
            stereoRenderer.InitMainCamera(mainCameraParent, mainCamera);
            stereoRendererList.Add(stereoRenderer);
        }

        public void RemoveFromManager(StereoRenderer stereoRenderer)
        {
            stereoRendererList.Remove(stereoRenderer);
        }

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
    }
}