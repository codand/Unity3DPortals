using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class RenderWithMaterial : MonoBehaviour {
    [SerializeField] private Material _material;

    private RenderTexture _tmp;

    void Start() {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst) {
        _tmp = RenderTexture.GetTemporary(src.width, src.height, 24);

        Graphics.Blit(src, _tmp);

        _material.SetFloat("_StencilRef", 0);
        _material.SetColor("_Color", Color.black);
        DrawQuad(_tmp.colorBuffer, src.depthBuffer, _material);

        _material.SetFloat("_StencilRef", 1);
        _material.SetColor("_Color", Color.red);
        DrawQuad(_tmp.colorBuffer, src.depthBuffer, _material);

        _material.SetFloat("_StencilRef", 2);
        _material.SetColor("_Color", Color.green);
        DrawQuad(_tmp.colorBuffer, src.depthBuffer, _material);

        _material.SetFloat("_StencilRef", 3);
        _material.SetColor("_Color", Color.blue);
        DrawQuad(_tmp.colorBuffer, src.depthBuffer, _material);

        Graphics.Blit(_tmp, dst);
    }

    void DrawQuad(RenderBuffer colorBuffer, RenderBuffer depthBuffer, Material mat, int pass = 0) {
        Graphics.SetRenderTarget(colorBuffer, depthBuffer);

        mat.SetPass(pass);
        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);

        GL.Vertex(new Vector3(0, 0, 0));
        GL.Vertex(new Vector3(0, 1, 0));
        GL.Vertex(new Vector3(1, 1, 0));
        GL.Vertex(new Vector3(1, 0, 0));

        GL.End();
        GL.PopMatrix();
    }

    void OnPostRender() {
        RenderTexture.ReleaseTemporary(_tmp);
    }
}
