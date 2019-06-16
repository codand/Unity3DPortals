using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portals {
    public class PortalShadowCaster : MonoBehaviour {
        static List<Collider> allShadowCasterParents = new List<Collider>();

        Collider _parent;
        Portal _portal;
        MeshFilter _meshFilter;
        Renderer _renderer;

        public static PortalShadowCaster SpawnShadowCaster(Collider parent, Portal portal) {
            if (allShadowCasterParents.Contains(parent)) {
                return null;
            }
            allShadowCasterParents.Add(parent);

            GameObject go = new GameObject("~" + parent.name + "->ShadowCaster");
            PortalShadowCaster shadowCaster = go.AddComponent<PortalShadowCaster>();

            shadowCaster._parent = parent;
            shadowCaster._portal = portal;
            return shadowCaster;
        }


        void Update() {
            if (!_parent || !_portal) {
                return;
            }

            if (!_meshFilter) {
                MeshFilter parentMeshFilter = _parent.GetComponent<MeshFilter>();
                if (!parentMeshFilter) {
                    return;
                }
                _meshFilter = this.gameObject.AddComponent<MeshFilter>();
                _meshFilter.sharedMesh = parentMeshFilter.sharedMesh;
            }

            if (!_renderer) {
                Renderer parentRenderer = _parent.GetComponent<Renderer>();
                if (!parentRenderer) {
                    return;
                }
                _renderer = this.gameObject.AddComponent<MeshRenderer>();
                _renderer.receiveShadows = false;
                _renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                _renderer.sharedMaterial = parentRenderer.sharedMaterial;
            }
        }

        void LateUpdate() {
            if (!_parent || !_portal) {
                return;
            }

            _portal.ApplyWorldToPortalTransform(this.transform, _parent.transform);
        }

        void OnDestroy() {
            allShadowCasterParents.Remove(_parent);
        }
    }
}