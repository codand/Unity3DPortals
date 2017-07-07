using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class Util {
        public static float SignedPlanarAngle(Vector3 a, Vector3 b, Vector3 planeNormal) {
            // Project both vectors onto the normal plane
            Vector3 pa = Vector3.ProjectOnPlane(a, planeNormal);
            Vector3 pb = Vector3.ProjectOnPlane(b, planeNormal);

            float angle = Vector3.Angle(pa, pb);

            // Use the cross product to determine the sign of the angle
            Vector3 cross = Vector3.Cross(pa, pb);
            if (Vector3.Dot(planeNormal, cross) > 0) {
                angle *= -1;
            }
            return angle;
        }

        public static float NormalizeAngle(float angle, float start, float end) {
            float width = end - start;
            float offsetValue = angle - start;

            return (offsetValue - (Mathf.Floor(offsetValue / width) * width)) + start;
        }

        public static void SafeDestroy(Object obj) {
            if (Application.isPlaying) {
                GameObject.Destroy(obj);
            } else {
                GameObject.DestroyImmediate(obj);
            }
        }
    }
}
