using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class PortalPhysics {
        public static int PortalLayer = 8;
        public static int PortalLayerMask = 1 << PortalLayer;
        
        private const float Epsilon = 0.001f;

        private static bool RecursiveRaycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, int recursiveDepth, Portal fromPortal=null) {
            layerMask &= ~PortalLayerMask; // Don't allow raycasting for portals

            // Need to raycast twice because portals and objects can share the same location which would cause fighting
            RaycastHit portalHitInfo, objectHitInfo;
            bool portalHit = Physics.Raycast(origin, direction, out portalHitInfo, maxDistance, PortalLayerMask, QueryTriggerInteraction.Collide);
            bool objectHit = Physics.Raycast(origin, direction, out objectHitInfo, maxDistance, layerMask, queryTriggerInteraction);

            // Recurse we hit a portal and we did not hit an object OR if we hit both, but the portal is closer
            bool recurse = (portalHit && !objectHit) || (portalHit && objectHit && portalHitInfo.distance <= objectHitInfo.distance + Epsilon);
            if (recurse) {
                // We hit a portal
                Portal portal = portalHitInfo.collider.GetComponent<Portal>();
                if (!portal) {
                    string msg = string.Format("{0} is on Portal layer, but is not a portal", portalHitInfo.collider.gameObject);
                    throw new System.Exception(msg);
                }

                // Bail if we recurse too many times
                if (recursiveDepth >= portal.MaxRecursion) {
                    hitInfo = default(RaycastHit);
                    return false;
                }

                Matrix4x4 portalMatrix = portal.PortalMatrix;
                Vector3 newDirection = portalMatrix.MultiplyVector(direction);
                // Offset by Epsilon so we can't hit the exit portal on our way out
                Vector3 newOrigin = portalMatrix.MultiplyPoint3x4(portalHitInfo.point) + newDirection * Epsilon;
                float newDistance = maxDistance - portalHitInfo.distance - Epsilon;

                return RecursiveRaycast(newOrigin, newDirection, out hitInfo, newDistance, layerMask, queryTriggerInteraction, recursiveDepth+1, portal);
            } else {
                // We hit an object
                hitInfo = objectHitInfo;
                return objectHit;
            }
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
            return RecursiveRaycast(origin, direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal, 0);
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask) {
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray) {
            RaycastHit hitInfo;
            return Raycast(ray.origin, ray.direction, out hitInfo, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction) {
            RaycastHit hitInfo;
            return Raycast(origin, direction, out hitInfo, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray, float maxDistance) {
            RaycastHit hitInfo;
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo) {
            return Raycast(ray.origin, ray.direction, out hitInfo, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) {
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) {
            RaycastHit hitInfo;
            return Raycast(origin, direction, out hitInfo, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo) {
            return Raycast(origin, direction, out hitInfo, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray, float maxDistance, int layerMask) {
            RaycastHit hitInfo;
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Ray ray, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
            RaycastHit hitInfo;
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask) {
            RaycastHit hitInfo;
            return Raycast(origin, direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance) {
            return Raycast(origin, direction, out hitInfo, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask) {
            return Raycast(origin, direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
            RaycastHit hitInfo;
            return Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
            return Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);
        }
    }
}
