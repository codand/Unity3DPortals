using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawStereoViewMatrix : MonoBehaviour {
    void LateUpdate() {
        Camera camera = GetComponent<Camera>();

        camera.ResetStereoViewMatrices();
        Matrix4x4[] viewMatrices = new Matrix4x4[] {
            camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left),
            camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right),
        };

        foreach(Matrix4x4 viewMatrix in viewMatrices) {
            Vector3 position = viewMatrix.inverse.MultiplyPoint3x4(Vector3.zero);
            Debug.DrawLine(position, position + camera.transform.forward, Color.cyan, Time.deltaTime);
        }
    }
}
