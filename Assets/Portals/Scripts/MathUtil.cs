using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class MathUtil {
        public static float VectorInternalAverage(Vector3 vec) {
            return (vec.x + vec.y + vec.z) / 3;
        }

        public static float VectorInternalProduct(Vector3 vec) {
            return vec.x * vec.y * vec.z;
        }

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

        public struct ProjectionMatrixInfo {
            public float near;
            public float far;
            public float bottom;
            public float top;
            public float left;
            public float right;
        }

        public static ProjectionMatrixInfo DecomposeProjectionMatrix(Matrix4x4 matrix) {
            ProjectionMatrixInfo info = new ProjectionMatrixInfo();
            info.near = matrix.m23 / (matrix.m22 - 1);
            info.far = matrix.m23 / (matrix.m22 + 1);
            info.bottom = info.near * (matrix.m12 - 1) / matrix.m11;
            info.top = info.near * (matrix.m12 + 1) / matrix.m11;
            info.left = info.near * (matrix.m02 - 1) / matrix.m00;
            info.right = info.near * (matrix.m02 + 1) / matrix.m00;
            return info;
        }

        public static void MakeProjectionMatrixOblique(ref Matrix4x4 projection, Vector4 clipPlane) {
            // Source: http://aras-p.info/texts/obliqueortho.html
            Vector4 q = projection.inverse * new Vector4(
                Mathf.Sign(clipPlane.x),
                Mathf.Sign(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
        }

        public static Matrix4x4 ScissorsMatrix(Matrix4x4 projectionMatrix, Rect r) {
            if (r.x < 0) {
                r.width += r.x;
                r.x = 0;
            }

            if (r.y < 0) {
                r.height += r.y;
                r.y = 0;
            }

            r.width = Mathf.Min(1 - r.x, r.width);
            r.height = Mathf.Min(1 - r.y, r.height);



            Matrix4x4 m1 = Matrix4x4.TRS(new Vector3((1 / r.width - 1), (1 / r.height - 1), 0), Quaternion.identity, new Vector3(1 / r.width, 1 / r.height, 1));
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3(-r.x * 2 / r.width, -r.y * 2 / r.height, 0), Quaternion.identity, Vector3.one);
            return m2 * m1 * projectionMatrix;
        }

        public static Matrix4x4 OffAxisProjectionMatrix(float near, float far, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 pe) {
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
            near = d; // + _clippingDistance;
                      //camera.nearClipPlane = n;

            l = Vector3.Dot(vr, va) * near / d;
            r = Vector3.Dot(vr, vb) * near / d;
            b = Vector3.Dot(vu, va) * near / d;
            t = Vector3.Dot(vu, vc) * near / d;

            Matrix4x4 p = new Matrix4x4(); // projection matrix 
            p[0, 0] = 2.0f * near / (r - l);
            p[0, 1] = 0.0f;
            p[0, 2] = (r + l) / (r - l);
            p[0, 3] = 0.0f;

            p[1, 0] = 0.0f;
            p[1, 1] = 2.0f * near / (t - b);
            p[1, 2] = (t + b) / (t - b);
            p[1, 3] = 0.0f;

            p[2, 0] = 0.0f;
            p[2, 1] = 0.0f;
            p[2, 2] = (far + near) / (near - far);
            p[2, 3] = 2.0f * far * near / (near - far);

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

        private static void CalculateNearPlaneCornersNoAlloc(Camera camera, Vector3[] corners) {
            // Source: https://gamedev.stackexchange.com/questions/19774/determine-corners-of-a-specific-plane-in-the-frustum
            Transform t = camera.transform;
            Vector3 p = t.position;
            Vector3 v = t.forward;
            Vector3 up = t.up;
            Vector3 right = t.right;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float fov = camera.fieldOfView * Mathf.Deg2Rad;
            float ar = camera.aspect;

            float nearHeight = 2 * Mathf.Tan(fov / 2) * near;
            float nearWidth = nearHeight * ar;

            Vector3 nearCenter = p + (v * near);

            Vector3 halfHeight = up * nearHeight / 2;
            Vector3 halfWidth = right * nearWidth / 2;

            Vector3 topLeft = nearCenter + halfHeight - halfWidth;
            Vector3 topRight = nearCenter + halfHeight + halfWidth;
            Vector3 bottomRight = nearCenter - halfHeight + halfWidth;
            Vector3 bottomLeft = nearCenter - halfHeight - halfWidth;

            corners[0] = topLeft;
            corners[1] = topRight;
            corners[2] = bottomRight;
            corners[3] = bottomLeft;
        }        
        
        // No longer used, but not deleting them because they might be useful
        public static float CalculateNearPlanePenetration(Camera camera, Plane plane) {
            Vector3[] corners = new Vector3[4];
            CalculateNearPlaneCornersNoAlloc(camera, corners);
            float maxPenetration = Mathf.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++) {
                Vector3 corner = corners[i];
                float penetration = plane.GetDistanceToPoint(corner);
                maxPenetration = Mathf.Max(maxPenetration, penetration);
            }
            return maxPenetration;
        }

    }

    // TODO: Not sure what this is for, but I don't want to delete it yet.
    //Matrix4x4 HMDMatrix4x4ToMatrix4x4(Valve.VR.HmdMatrix44_t input) {
    //    var m = Matrix4x4.identity;

    //    m[0, 0] = input.m0;
    //    m[0, 1] = input.m1;
    //    m[0, 2] = input.m2;
    //    m[0, 3] = input.m3;

    //    m[1, 0] = input.m4;
    //    m[1, 1] = input.m5;
    //    m[1, 2] = input.m6;
    //    m[1, 3] = input.m7;

    //    m[2, 0] = input.m8;
    //    m[2, 1] = input.m9;
    //    m[2, 2] = input.m10;
    //    m[2, 3] = input.m11;

    //    m[3, 0] = input.m12;
    //    m[3, 1] = input.m13;
    //    m[3, 2] = input.m14;
    //    m[3, 3] = input.m15;

    //    return m;
    //}
}