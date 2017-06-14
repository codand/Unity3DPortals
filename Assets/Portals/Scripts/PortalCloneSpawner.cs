using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Portals {
    public class PortalCloneSpawner : MonoBehaviour {
        [SerializeField]
        private Portal _portal;

        private static readonly Dictionary<Type, Action<Component, Component>> _componentCopyFuncs = new Dictionary<Type, Action<Component, Component>>() {
            { typeof(MeshFilter), CopyMeshFilter },
            { typeof(MeshRenderer), CopyMeshRenderer },
            { typeof(Rigidbody), null },
            { typeof(CapsuleCollider), null },
        };

        private void Awake() {
            _portal.onPortalTeleport += OnPortalTeleport;
        }

        private void OnTriggerEnter(Collider collider) {
            SpawnClone(collider.gameObject);
        }

        private void OnTriggerExit(Collider collider) {
            DespawnClone(collider.gameObject);
        }

        private void OnPortalTeleport(GameObject obj) {
            _portal.ExitPortal.GetComponentInChildren<PortalCloneSpawner>().SpawnClone(obj);
            DespawnClone(obj);
        }

        // Dictionary mapping objects to their clones on the other side of a portal
        private Dictionary<GameObject, GameObject> _objectToClone = new Dictionary<GameObject, GameObject>();

        private GameObject SpawnClone(GameObject obj) {
            GameObject clone;
            _objectToClone.TryGetValue(obj, out clone);
            if (clone) {
                clone.SetActive(true);
            } else {
                clone = RecursiveClone(null, obj.transform);
                PortalClone script = clone.AddComponent<PortalClone>();
                script.target = obj.transform;
                script.portal = _portal;

                //clone.hideFlags = HideFlags.HideAndDontSave;
                _objectToClone[obj] = clone;
            }
            return clone;
        }

        private void DespawnClone(GameObject obj, bool destroy = false) {
            GameObject clone;
            _objectToClone.TryGetValue(obj, out clone);
            if (clone) {
                if (destroy) {
                    GameObject.Destroy(clone);
                } else {
                    clone.SetActive(false);
                }
            }
        }

        private static GameObject RecursiveClone(Transform cloneParent, Transform toCopy) {
            Transform clone = new GameObject("Clone of " + toCopy.name).transform;
            clone.parent = cloneParent;
            clone.localPosition = toCopy.localPosition;
            clone.localRotation = toCopy.localRotation;
            clone.localScale = toCopy.localScale;

            CopyComponents(toCopy.gameObject, clone.gameObject);

            foreach (Transform child in toCopy.transform) {
                RecursiveClone(clone.transform, child);
            }

            return clone.gameObject;
        }

        private static void CopyComponents(GameObject from, GameObject to) {
            foreach (KeyValuePair<Type, Action<Component, Component>> kvp in _componentCopyFuncs) {
                Type type = kvp.Key;
                Action<Component, Component> copyFunc = kvp.Value;
                if (type == null || copyFunc == null) {
                    continue;
                }

                Component fromComp = from.GetComponent(type);
                if (fromComp) {
                    Component toComp = to.AddComponent(type);
                    copyFunc(fromComp, toComp);
                }
            }
        }


        private static void CopyMeshFilter(Component from, Component to) {
            MeshFilter f = from as MeshFilter;
            MeshFilter t = to as MeshFilter;

            t.sharedMesh = f.sharedMesh;
        }

        private static void CopyMeshRenderer(Component from, Component to) {
            MeshRenderer f = from as MeshRenderer;
            MeshRenderer t = to as MeshRenderer;

            t.enabled = f.enabled;

            t.additionalVertexStreams = f.additionalVertexStreams;
            t.lightProbeUsage = f.lightProbeUsage;
            t.lightProbeProxyVolumeOverride = f.lightProbeProxyVolumeOverride;
            t.reflectionProbeUsage = f.reflectionProbeUsage;
            t.probeAnchor = f.probeAnchor;
            t.shadowCastingMode = f.shadowCastingMode;
            t.receiveShadows = f.receiveShadows;
            t.motionVectorGenerationMode = f.motionVectorGenerationMode;

            t.sharedMaterials = f.sharedMaterials;
        }
    }
}
