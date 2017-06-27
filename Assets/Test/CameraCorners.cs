using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCorners : MonoBehaviour {

    void LateUpdate() {

        Camera camera = GetComponent<Camera>();

        //Vector3[] points = GetNearPlanePoints(camera);
        //Debug.Log("TopLeft: " + points[0]);
        //Debug.Log("TopRight: " + points[1]);
        //Debug.Log("BottomRight: " + points[2]);
        //Debug.Log("BottomLeft: " + points[3]);
    }

    public static Vector3[] GetNearPlanePoints(Camera camera) {
        // Source: https://gamedev.stackexchange.com/questions/19774/determine-corners-of-a-specific-plane-in-the-frustum
        Transform t = camera.transform;
        Vector3 p = t.position;
        Vector3 v = t.forward;
        Vector3 up = t.up;
        Vector3 right = t.right;
        float nDis = camera.nearClipPlane;
        float fDis = camera.farClipPlane;
        float fov = camera.fieldOfView * Mathf.Deg2Rad;
        float ar = camera.aspect;

        float hNear = 2 * Mathf.Tan(fov / 2) * nDis;
        float wNear = hNear * ar;

        Vector3 cNear = p + v * nDis;

        Vector3 hHalf = up * hNear / 2;
        Vector3 wHalf = right * wNear / 2;

        Vector3 topLeft = cNear + hHalf - wHalf;
        Vector3 topRight = cNear + hHalf + wHalf;
        Vector3 bottomRight = cNear - hHalf + wHalf;
        Vector3 bottomLeft = cNear - hHalf - wHalf;

        return new Vector3[] {
            topLeft,
            topRight,
            bottomRight,
            bottomLeft
        };
    }
}
