using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class Util {
        public static void SafeDestroy(Object obj) {
            if (Application.isPlaying) {
                GameObject.Destroy(obj);
            } else {
                GameObject.DestroyImmediate(obj);
            }
        }

        public static void DrawDebugFrustum(Matrix4x4 matrix, Color color) {
            Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(matrix); //get planes from matrix
            Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            for (int i = 0; i < 4; i++) {
                nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix
            }
            for (int i = 0; i < 4; i++) {
                Debug.DrawLine(nearCorners[i], nearCorners[(i + 1) % 4], color); //near corners on the created projection matrix
                Debug.DrawLine(farCorners[i], farCorners[(i + 1) % 4], color); //far corners on the created projection matrix
                Debug.DrawLine(nearCorners[i], farCorners[i], color); //sides of the created projection matrix
            }
        }

        private static Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3) { //get the intersection point of 3 planes
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }

        public static void DrawDebugFrustum2(Matrix4x4 vp, Color color) {
            float w = 50f;
            float step = 2f;
            for (float x = -w / 2; x < w / 2; x += step) {
                for (float y = -w / 2; y < w / 2; y += step) {
                    for (float z = -w / 2; z < w / 2; z += step) {
                        Vector4 wPt = new Vector4(x, y, z, 1);
                        Vector4 cPt = vp * wPt;
                        if (cPt.w > 0) {
                            cPt /= cPt.w;
                            if (cPt.x > -1f && cPt.x < 1f && cPt.y > -1f && cPt.y < 1f && cPt.z > -1f && cPt.z < 1f) {
                                Debug.DrawLine(wPt, (Vector3)wPt + Vector3.up * step / 2);
                            }
                        }
                    }
                }
            }
        }

        public static void DrawDebugFrustum3(Matrix4x4 vp, Color color) {
            Vector4 c_nbl = new Vector4(-1, -1, -1,  1);
            Vector4 c_nbr = new Vector4( 1, -1, -1,  1);
            Vector4 c_ntl = new Vector4(-1,  1, -1,  1);
            Vector4 c_ntr = new Vector4( 1,  1, -1,  1);

            Vector4 c_fbl = new Vector4(-1, -1,  1,  1);
            Vector4 c_fbr = new Vector4( 1, -1,  1,  1);
            Vector4 c_ftl = new Vector4(-1,  1,  1,  1);
            Vector4 c_ftr = new Vector4( 1,  1,  1,  1);

            Matrix4x4 ivp = vp.inverse;

            Vector4 nbl = ivp * c_nbl;
            Vector4 nbr = ivp * c_nbr;
            Vector4 ntl = ivp * c_ntl;
            Vector4 ntr = ivp * c_ntr;

            Vector4 fbl = ivp * c_fbl;
            Vector4 fbr = ivp * c_fbr;
            Vector4 ftl = ivp * c_ftl;
            Vector4 ftr = ivp * c_ftr;

            nbl /= nbl.w;
            nbr /= nbr.w;
            ntl /= ntl.w;
            ntr /= ntr.w;
            fbl /= fbl.w;
            fbr /= fbr.w;
            ftl /= ftl.w;
            ftr /= ftr.w;

            // Near plane
            Debug.DrawLine(nbl, ntl, Color.green);
            Debug.DrawLine(ntl, ntr, Color.green);
            Debug.DrawLine(ntr, nbr, Color.green);
            Debug.DrawLine(nbr, nbl, Color.green);

            // Far plane
            Debug.DrawLine(fbl, ftl, Color.red);
            Debug.DrawLine(ftl, ftr, Color.red);
            Debug.DrawLine(ftr, fbr, Color.red);
            Debug.DrawLine(fbr, fbl, Color.red);

            // Sides
            Debug.DrawLine(nbl, fbl, color);
            Debug.DrawLine(ntl, ftl, color);
            Debug.DrawLine(ntr, ftr, color);
            Debug.DrawLine(nbr, fbr, color);
        }
    }
}
