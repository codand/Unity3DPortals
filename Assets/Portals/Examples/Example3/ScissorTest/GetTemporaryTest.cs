﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Portals;

public class GetTemporaryTest : MonoBehaviour
{
    public int width = 1920;
    public int height = 1080;

    public Texture2D textureToCopy;
    public Camera camera;
    public Rect viewportRect = new Rect(0, 0, 1, 1);
    private Rect defaultRect = new Rect(0, 0, 1, 1);

    public RenderTexture normal;
    public RenderTexture scissor;

    public Transform normalTransform;
    public Transform scissorTransform;
    public float scale = 1000;

    public RenderTexture normalTemp;
    public RenderTexture scissorTemp;

    public Material normalMat;
    public Material scissorMat;

    System.Diagnostics.Stopwatch watch1 = new System.Diagnostics.Stopwatch();
    System.Diagnostics.Stopwatch watch2 = new System.Diagnostics.Stopwatch();

    void ClearTexture(RenderTexture tex, Color c) {
        var active = RenderTexture.active;
        Graphics.SetRenderTarget(tex);
        GL.Clear(true, true, c);
        RenderTexture.active = active;
    }

    Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3) { //get the intersection point of 3 planes
        return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
            (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
    }

    void DrawProjectionFrustum(Matrix4x4 matrix, Color color) {
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

    void RenderNormally() {
        camera.ResetProjectionMatrix();
        camera.rect = viewportRect;
        camera.projectionMatrix = MathUtil.ScissorsMatrix(camera.projectionMatrix, viewportRect);

        //if (normalTemp) {
        //    RenderTexture.ReleaseTemporary(normalTemp);
        //}

        ClearTexture(normalTemp, Color.black);
        normalTransform.localScale = new Vector3(normalTemp.width / scale, normalTemp.height / scale, 1);
        camera.targetTexture = normalTemp;
        camera.Render();

        normalMat.SetTexture("_MainTex", normalTemp);

        //DrawProjectionFrustum(camera.projectionMatrix, Color.green);
    }

    public bool sciss;
    public bool cull;
    void RenderWithScissors() {
        camera.ResetProjectionMatrix();
        var proj = camera.projectionMatrix;
        if (sciss) {
            camera.projectionMatrix = MathUtil.ScissorsMatrix(proj, viewportRect);
            camera.rect = viewportRect;
        }
        if(cull) {

        }


        //camera.targetTexture = null;
        //camera.ResetProjectionMatrix();
        //camera.rect = defaultRect;
        //var proj = camera.projectionMatrix;

        //if (scissorTemp) {
        //    RenderTexture.ReleaseTemporary(scissorTemp);
        //}
        if (!scissorTemp) {
            scissorTemp = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.Default);
        }
        //scissorTemp = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default);
        ClearTexture(scissorTemp, Color.black);
        scissorTransform.localScale = new Vector3(scissorTemp.width / scale, scissorTemp.height / scale, 1);
        camera.targetTexture = scissorTemp;
        camera.Render();

        scissorMat.SetTexture("_MainTex", scissorTemp);

        DrawProjectionFrustum(camera.projectionMatrix * camera.worldToCameraMatrix, Color.red);
        //DrawProjectionFrustum(proj, Color.blue);
    }
    void Update() {
        RenderWithScissors();

        camera.targetTexture = null;
        camera.rect = defaultRect;
        camera.ResetProjectionMatrix();

        Vector3 bottomRight = new Vector3(0.5f, -0.5f, 0);
        Vector3 bottomLeft = new Vector3(5f, -0.5f, 0);
        Vector3 upperRight = new Vector3(0.5f, 0.5f, 0);
        Matrix4x4 offAxisProjectionMatrix = MathUtil.OffAxisProjectionMatrix(camera.nearClipPlane, camera.farClipPlane, bottomRight, bottomLeft, upperRight, camera.transform.position);
        DrawProjectionFrustum(offAxisProjectionMatrix, Color.blue);

        DrawProjectionFrustum(camera.projectionMatrix * camera.worldToCameraMatrix, Color.white);
    }
}