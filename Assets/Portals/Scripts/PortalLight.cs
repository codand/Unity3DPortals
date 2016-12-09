using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using PortalExtensions;

namespace Portals {
    //[ExecuteInEditMode]
    public class PortalLight : MonoBehaviour {
        Portal _portal;
        //[SerializeField] Material _clearDepthMaterial;
        
        Light _light;
        PortalLight _parentLight;
        CommandBuffer _commandBuffer;
        List<Portal> _allPortals;
        Dictionary<Portal, PortalLight> _portalToChild;

        static List<Renderer> _shadowRenderers;

        PortalLight SpawnLight(Portal portal) {
            GameObject obj = new GameObject();
            //obj.hideFlags = HideFlags.HideAndDontSave;
            obj.name = "~" + this.name + "->" + portal.name;
            Light light = obj.AddComponent<Light>();
            PortalLight portalLight = obj.AddComponent<PortalLight>();
            portalLight._parentLight = this;
            portalLight._portal = portal;

            portalLight.CopyLight(this._light);
            portalLight.SpawnShadowCasters();
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

            foreach (KeyValuePair<Portal, PortalLight> kvp in _portalToChild) {
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

                    // Only check if a hemisphere is in front. Can be strengthened further by using
                    // Cone-quad intersection, but I'm not yet sure how expensive that check will be.
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

        Vector4 MakePlane(Vector3 p1, Vector3 p2, Vector3 p3) {
            Vector3 p3p1 = p3 - p1;
            Vector3 p2p1 = p2 - p1;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(p3p1, p2p1));
            float w = Vector3.Dot(normal, p1);
            return new Vector4(normal.x, normal.y, normal.z, w);
        }

        Vector4[] GetShadowPlanes() {
            Vector3 position = transform.position;
            Vector3[] corners = _portal.exitPortal.GetCorners();
            Vector4[] shadowPlanes = new Vector4[] {
                MakePlane(corners[0], corners[1], corners[2]),
                MakePlane(position, corners[0], corners[1]),
                MakePlane(position, corners[1], corners[2]),
                MakePlane(position, corners[2], corners[3]),
                MakePlane(position, corners[3], corners[0]),
            };

            return shadowPlanes;
        }

        Matrix4x4 GetMVPMatrix() {
            // http://answers.unity3d.com/questions/12713/how-do-i-reproduce-the-mvp-matrix.html
            Matrix4x4 m = _portal.exitPortal.transform.localToWorldMatrix;
            Matrix4x4 v = transform.worldToLocalMatrix;
            // TODO: Find out what the actual shadow near plane should be.
            Matrix4x4 p = Matrix4x4.Perspective(_light.spotAngle, 1.0f, 0.1f, QualitySettings.shadowDistance);

            // Invert Z of view matrix because transform.worldToLocalMatrix is opposite camera.worldToCameraMatrix
            for (int i = 0; i < 4; i++) {
                v[2, i] = -v[2, i];
            }

            bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
            if (d3d) {
                // Invert Y for rendering to a render texture
                for (int i = 0; i < 4; i++) {
                    p[1, i] = -p[1, i];
                }
                // Scale and bias from OpenGL -> D3D depth range
                for (int i = 0; i < 4; i++) {
                    p[2, i] = p[2, i] * 0.5f + p[3, i] * 0.5f;
                }
            }
            Matrix4x4 mvp = p * v * m;

            return mvp;
        }

        void UpdateCommandBuffer() {
            if (_commandBuffer == null) {
                _commandBuffer = new CommandBuffer();
                _commandBuffer.name = "Set Shadow Planes";
                _light.AddCommandBuffer(LightEvent.AfterShadowMap, _commandBuffer);
            }
            _commandBuffer.Clear();

            if (_portal && _portal.exitPortal) {
                _commandBuffer.SetGlobalFloat("_UseShadowPlanes", 1);
                Vector3[] corners3 = _portal.exitPortal.GetCorners();
                Vector4[] corners4 = new Vector4[corners3.Length];
                for (int i = 0; i < corners3.Length; i++) {
                    Vector3 c3 = corners3[i];
                    corners4[i] = new Vector4(c3.x, c3.y, c3.z, 1);
                }
                _commandBuffer.SetGlobalVectorArray("_PortalCorners", corners4);
                //_commandBuffer.SetGlobalTexture("_PortalCookie", _portal.transform.Find("Overlay").GetComponent<Renderer>().sharedMaterial.mainTexture);
                _commandBuffer.SetGlobalVectorArray("_ShadowPlanes", GetShadowPlanes());
            } else {
                _commandBuffer.SetGlobalFloat("_UseShadowPlanes", 0);
            }

            //_commandBuffer.ClearRenderTarget(true, false, Color.white, 0.0f);


            //_commandBuffer.SetGlobalMatrix("_CustomShadowMVP", GetMVPMatrix());
            //Mesh portalMesh = _portal.exitPortal.GetComponent<MeshFilter>().sharedMesh;
            //Transform portalTransform = _portal.exitPortal.transform;
            // _commandBuffer.DrawMesh(portalMesh, trs, _clearDepthMaterial, 0, 0);
        }


        void Awake() {
            _light = GetComponent<Light>();
            _portalToChild = new Dictionary<Portal, PortalLight>();
            _shadowRenderers = new List<Renderer>();
        }

        public void SpawnShadowCasters() {
            if (!_parentLight || !_portal) {
                return;
            }

            Collider[] allColliders = Physics.OverlapSphere(_parentLight.transform.position, _parentLight._light.range, -1, QueryTriggerInteraction.Ignore);
            foreach (Collider collider in allColliders) {
                if (collider.GetComponent<Renderer>()) {
                    PortalShadowCaster.SpawnShadowCaster(collider, _portal);
                }
            }
        }
        void Start() {
            _allPortals = new List<Portal>(FindObjectsOfType<Portal>());
        }

        void OnDisable() {
            if (_commandBuffer != null) {
                _light.RemoveCommandBuffer(LightEvent.BeforeShadowMap, _commandBuffer);
            }

            // Destroy children - this will cause recursion so the children destroy their children too
            foreach (KeyValuePair<Portal, PortalLight> kvp in _portalToChild.ToArray()) {
                _portalToChild.Remove(kvp.Key);
                if (kvp.Value && kvp.Value.gameObject) {
                    Destroy(kvp.Value.gameObject);
                }
            }

            // If we don't set this here, it is possible to leave this value non-zero in the main scene
            // which will cause normal lights to use the portal shadows when they shouldn't.
            Shader.SetGlobalFloat("_UseShadowPlanes", 0);
        }

        void FixedUpdate() {
            foreach (Portal portal in _allPortals) {
                PortalLight child;
                _portalToChild.TryGetValue(portal, out child);

                // Check every portal in the scene if we're in range
                if (portal.exitPortal && IsInRange(portal.transform.position)) {
                    // Don't recurse on our own portal
                    if (!_portal || _portal.exitPortal != portal) {
                        // If there isn't a child, let's spawn a new one.
                        if (!child) {
                            _portalToChild[portal] = SpawnLight(portal);
                        }
                    }
                } else {
                    // If there is a child, but we're no longer in range, remove and destroy the child
                    if (child) {
                        _portalToChild.Remove(portal);
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        Vector3 RayPlaneIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector4 plane) {
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float denom = Vector3.Dot(normal, rayDirection);
            Vector3 planePoint = normal * plane.w;

            float t = Vector3.Dot((planePoint - rayOrigin), normal) / denom;
            Vector3 intersection = t * rayDirection + rayOrigin;
            return intersection;
        }

        void Update() {
            UpdateCommandBuffer();
            
            //if (!_portal) return;
            //Vector3 intersection = RayPlaneIntersection(transform.position, _portal.exitPortal.transform.position - transform.position, GetShadowPlanes()[0]);
            //Debug.Log(intersection);
        }

        void LateUpdate() {
            if (_parentLight) {
                CopyParentLightRecursive();
                if (_portal && _portal.exitPortal) {
                    _portal.ApplyWorldToPortalTransform(this.transform, _parentLight.transform);
                }
            }
        }
    }
}
