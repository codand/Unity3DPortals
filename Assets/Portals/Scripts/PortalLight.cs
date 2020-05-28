using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using PortalExtensions;

namespace Portals {
    [ExecuteInEditMode]
    public class PortalLight : MonoBehaviour {
        [SerializeField] private LightShadows _shadowType = LightShadows.Soft;
        [SerializeField] private int _maxRecursion = 1;

        private Portal _portal;
        private Light _light;
        private PortalLight _parentLight;
        private CommandBuffer _commandBuffer;
        private List<Portal> _allPortals;
        private Dictionary<Portal, PortalLight> _children;
        private static Material _depthPunchMaterial;
        private static ObjectPool<MaterialPropertyBlock> _propertyBlockPool;

        public int PortalDepth { get; private set; } = 0;
        public LightShadows ShadowType { get => _shadowType; set => _shadowType = value; }

        PortalLight SpawnChild(Portal portal) {
            GameObject obj = new GameObject();
            obj.hideFlags = HideFlags.DontSave;
            obj.name = "~" + this.name + "->" + portal.name;
            Light light = obj.AddComponent<Light>();
            PortalLight portalLight = obj.AddComponent<PortalLight>();
            portalLight._parentLight = this;
            portalLight._portal = portal;
            portalLight.PortalDepth = this.PortalDepth + 1;
            portalLight.CopyLight(this._light);
            portal.ApplyWorldToPortalTransform(portalLight.transform, this.transform);
            return portalLight;
        }

        void CopyLight(Light light) {
            _light.bounceIntensity = light.bounceIntensity;
            _light.color = light.color;
            _light.cookie = light.cookie;
            _light.cookieSize = light.cookieSize;
            _light.cullingMask = light.cullingMask;
            _light.flare = light.flare;
            _light.intensity = light.intensity;
            _light.range = light.range;
            _light.shadowBias = light.shadowBias;
            _light.shadowNearPlane = light.shadowNearPlane;
            _light.shadowNormalBias = light.shadowNormalBias;
            _light.shadows = light.shadows;
            _light.shadowStrength = light.shadowStrength;
            _light.spotAngle = light.spotAngle;
            _light.type = light.type;
        }

        void CopyParentLightRecursive() {
            if (_parentLight) {
                CopyLight(_parentLight._light);
            }

            foreach (KeyValuePair<Portal, PortalLight> kvp in _children) {
                kvp.Value.CopyParentLightRecursive();
            }
        }

        public bool IsInRange(Vector3 point) {
            switch (_light.type) {
                case LightType.Spot:
                    bool withinRange = Vector3.Distance(transform.position, point) < _light.range;
                    if (!withinRange) {
                        return false;
                    }
                    
                    // TODO: Cone-intersection
                    bool inFront = Vector3.Dot(transform.forward, point - transform.position) > 0;
                    return inFront;
                case LightType.Point:
                    return Vector3.Distance(transform.position, point) < _light.range;
                case LightType.Directional:
                    // Only allow one recursion
                    return _portal == null;
                case LightType.Area:
                    return false;
                default:
                    return false;
            }
        }

        void Awake() {
            _light = GetComponent<Light>();
            _children = new Dictionary<Portal, PortalLight>();
            _depthPunchMaterial = new Material(Shader.Find("Portal/DepthPunch"));
            if (_propertyBlockPool == null) {
                _propertyBlockPool = new ObjectPool<MaterialPropertyBlock>(5, () => new MaterialPropertyBlock());
            }
        }


        void OnDisable() {
            if (_commandBuffer != null) {
                _light.RemoveCommandBuffer(LightEvent.AfterShadowMapPass, _commandBuffer);
            }

            // Destroy children - this will cause recursion so the children destroy their children too
            if (_children != null) {
                foreach (KeyValuePair<Portal, PortalLight> kvp in _children.ToArray()) {
                    _children.Remove(kvp.Key);
                    if (kvp.Value && kvp.Value.gameObject) {
                        Util.SafeDestroy(kvp.Value.gameObject);
                    }
                }
            }

            // If we don't set this here, it is possible to leave this value non-zero in the main scene
            // which will cause normal lights to use the portal shadows when they shouldn't.
            Shader.SetGlobalFloat("_UseShadowPlanes", 0);
        }

        private void UpdateCommandBuffers() {
            if (!_parentLight) {
                return;
            }
            if (_commandBuffer == null) {
                _commandBuffer = new CommandBuffer();
                _commandBuffer.name = "Shadow Stuff";
                _light.AddCommandBuffer(LightEvent.AfterShadowMapPass, _commandBuffer);
            }
            _commandBuffer.Clear();

            _commandBuffer.ClearRenderTarget(true, false, Color.black, 0.0f);
            MaterialPropertyBlock block = _propertyBlockPool.Take();
            if (_portal.ExitPortal.TransparencyMask) {
                block.SetTexture("_TransparencyMask", _portal.ExitPortal.TransparencyMask);
            }
            _commandBuffer.DrawMesh(PortalRenderer.Mesh, _portal.ExitPortal.PortalRenderer.transform.localToWorldMatrix, _depthPunchMaterial, 0, 0, block);
            _propertyBlockPool.Give(block);
        }

        private bool ShouldHaveChild(Portal p) {
            bool areBothPortalsActive = p && p.isActiveAndEnabled && p.ExitPortal && p.ExitPortal.isActiveAndEnabled;
            bool isLightInRange = IsInRange(p.transform.position);
            bool isAtMaxRecursion = PortalDepth >= _maxRecursion;
            return areBothPortalsActive && isLightInRange && !isAtMaxRecursion;
        }

        private void LateUpdate() {
            if (_children == null) {
                Awake();
            }

            foreach (Portal portal in Portal.AllPortals) {
                _children.TryGetValue(portal, out PortalLight child);

                // Check every portal in the scene if we're in range
                if (ShouldHaveChild(portal)) {
                    // If there isn't a child, let's spawn a new one.
                    if (!child) {
                        _children[portal] = SpawnChild(portal);
                    }
                } else {
                    // If there is a child, but we're no longer in range, remove and destroy the child
                    if (child) {
                        _children.Remove(portal);
                        Util.SafeDestroy(child.gameObject);
                    }
                }
            }


            if (_parentLight) {
                CopyParentLightRecursive();
                if (_portal && _portal.ExitPortal) {
                    _portal.ApplyWorldToPortalTransform(this.transform, _parentLight.transform);
                }
                //_light.shadows = LightShadows.None;
            }

            UpdateCommandBuffers();

            //if (_shadowmap != null) {
            //    RenderTexture.ReleaseTemporary(_shadowmap);
            //}
            //_shadowmap = RenderSpotLightShadowmap(_light.spotAngle, _light.shadowNearPlane, _light.range);

            //Shader.SetGlobalTexture("_ShadowMapTexture", _shadowmap);
        }
    }
}
