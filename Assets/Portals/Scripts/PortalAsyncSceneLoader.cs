using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Portals {
    [RequireComponent(typeof(Collider))]
    public class PortalAsyncSceneLoader : MonoBehaviour {
        [SerializeField] SceneField _exitScene;
        [SerializeField] string _exitPortalName;

        Portal _portal;
        PortalAsyncSceneLoader _exitPortalSceneLoader;
        AsyncOperation _asyncOperation;
        bool _inTrigger;

        GameObject FindObjectInList(List<GameObject> gameObjects, string name) {
            // Non-recursive implementation using a breadth-first search.
            Queue<GameObject> queue = new Queue<GameObject>(gameObjects);
            while (queue.Count > 0) {
                GameObject obj = queue.Dequeue();
                if (obj.name == name) {
                    return obj;
                }

                foreach (Transform childTransform in obj.transform) {
                    queue.Enqueue(childTransform.gameObject);
                }
            }
            return null;
        }

        /// <summary>
        /// Find an object by name in a specific scene
        /// </summary>
        /// <param name="scene">Scene to search</param>
        /// <param name="name">Name to match</param>
        /// <returns>The first GameObject found with a matching name. Null if no matches found.</returns>
        GameObject FindObjectInScene(Scene scene, string name) {
            if (!scene.isLoaded) {
                Debug.LogErrorFormat("The scene \"{0}\" must be loaded before calling FindObjectInScene", scene.name);
                return null;
            }

            List<GameObject> rootObjects = new List<GameObject>();
            scene.GetRootGameObjects(rootObjects);
            return FindObjectInList(rootObjects, name);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            // TODO: Fix compare
            if (string.Compare(scene.name, _exitScene.name, true) != 0) {
                return;
            }

            // TODO: Seems like there is a bug in unity causing scene.isLoaded to be false during OnSceneLoaded.
            // Will need to work around that.
            //GameObject exitPortalObj = FindObjectInScene(scene, _exitPortalName);
            GameObject exitPortalObj = GameObject.Find(_exitPortalName);
            if (!exitPortalObj) {
                Debug.LogErrorFormat("Could not find portal GameObject \"{0}\" in scene \"{1}\"", _exitPortalName, _exitScene.name);
                return;
            }

            Portal exitPortal = exitPortalObj.GetComponent<Portal>();
            if (!exitPortal) {
                Debug.LogErrorFormat("Found object \"{0}\" in scene \"{1}\", but it does not have a {2} script attached", _exitPortalName, _exitScene.name, typeof(Portal).Name);
                return;
            }

            PortalAsyncSceneLoader exitPortalSceneLoader = exitPortalObj.GetComponentInChildren<PortalAsyncSceneLoader>();
            if (!exitPortalSceneLoader) {
                Debug.LogErrorFormat("Found object \"{0}\" in scene \"{1}\", but it does not have a {2} script attached", _exitPortalName, _exitScene.name, typeof(PortalAsyncSceneLoader).Name);
                return;
            }

            _portal.ExitPortal = exitPortal;
            exitPortal.ExitPortal = _portal;
            _exitPortalSceneLoader = exitPortalSceneLoader;
            exitPortalSceneLoader._exitPortalSceneLoader = this;

            // Workaround for a bug where RenderSettings.reflectionIntensity gets set to 0 on scene load.
            // Potentially related: https://issuetracker.unity3d.com/issues/changing-reflection-intensity-at-runtime-via-script-doesnt-work
            float savedReflectionIntensity = RenderSettings.reflectionIntensity;
            RenderSettings.reflectionIntensity = -1.0f;
            RenderSettings.reflectionIntensity = savedReflectionIntensity;
        }

        void OnSceneUnloaded(Scene scene) {
            // TODO: Fix compare
            if (string.Compare(scene.name, _exitScene.name, true) != 0) {
                return;
            }

            _portal.ExitPortal = null;
        }

        void OnPortalTeleport(Portal portal, GameObject obj) {
            if (_portal == portal) {
                SceneManager.SetActiveScene(portal.ExitPortal.gameObject.scene);
                _exitPortalSceneLoader._inTrigger = true;
            }
        }

        void Awake() {
            _inTrigger = false;
            _exitPortalSceneLoader = null;

            _portal = GetComponentInParent<Portal>();
            if (!_portal) {
                Debug.LogErrorFormat("Must have {0} script attached to parent GameObject", typeof(Portal).Name);
                return;
            }
        }

        void OnEnable() {
            //Portal.onPortalTeleportGlobal += OnPortalTeleport;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDisable() {
            //Portal.onPortalTeleportGlobal -= OnPortalTeleport;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnTriggerEnter(Collider collider) {
            // Only load if we haven't already
            if (!_inTrigger && !_exitPortalSceneLoader) {
                Debug.Log("Loading scene: " + _exitScene);
                SceneManager.LoadSceneAsync(_exitScene, LoadSceneMode.Additive);
            }
            _inTrigger = true;
        }

        void OnTriggerExit(Collider collider) {
            if (!_exitPortalSceneLoader || !_exitPortalSceneLoader._inTrigger) {
                Debug.Log("Unloading scene: " + _exitScene);
                SceneManager.UnloadSceneAsync(_exitScene);
                _exitPortalSceneLoader = null;
            }
            _inTrigger = false;
        }
    }
}
